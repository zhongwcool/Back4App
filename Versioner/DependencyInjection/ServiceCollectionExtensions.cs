using Versioner.Options;
using Versioner.Services.Email;
using Versioner.Services.Parse;
using Versioner.ViewModels;
using Versioner.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Versioner.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVersionerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddOptions<Back4AppOptions>()
            .Bind(configuration.GetSection(Back4AppOptions.SectionName));
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IAppReleaseClient, AppReleaseClient>();
        services.AddTransient<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
