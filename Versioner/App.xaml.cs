using System.Windows;
using Versioner.Common;
using Versioner.DependencyInjection;
using Versioner.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Versioner;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("Host 尚未初始化。");

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppPaths.EnsureCreated();

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
            })
            .UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(
                        path: AppPaths.LogFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 31,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            })
            .ConfigureServices((context, services) =>
            {
                services.AddVersionerServices(context.Configuration);
            })
            .Build();

        await _host.StartAsync();
        Log.Information("应用启动，日志目录：{LogDir}", AppPaths.LogsDirectory);

        _host.Services.GetRequiredService<MainWindow>().Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("应用退出。");
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
