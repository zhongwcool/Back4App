using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Versioner.Common;
using Versioner.DependencyInjection;
using Versioner.Services.Settings;
using Versioner.Views;

namespace Versioner;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("Host 尚未初始化。");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AppPaths.EnsureCreated();

        try
        {
            var options = UserSettingsStore.Load();
            if (!options.IsConfigured)
            {
                var dialog = new CredentialWindow(options);
                if (dialog.ShowDialog() == true)
                    UserSettingsStore.Save(options, dialog.Result);
            }

            _host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration((_, config) => config.Sources.Clear())
                .UseSerilog((_, _, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .MinimumLevel.Override("System", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .WriteTo.File(
                            path: AppPaths.LogFilePath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 31,
                            shared: true,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddVersionerServices(options);
                })
                .Build();

            await _host.StartAsync();
            Log.Information(
                "应用启动，日志目录：{LogDir}，Back4App {State}",
                AppPaths.LogsDirectory,
                options.IsConfigured ? "已配置" : "未配置");

            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            MessageBox.Show(ex.Message, "Versioner", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
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
