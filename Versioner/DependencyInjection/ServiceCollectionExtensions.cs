using Microsoft.Extensions.DependencyInjection;
using Versioner.Options;
using Versioner.Services.Email;
using Versioner.Services.Parse;
using Versioner.ViewModels;
using Versioner.Views;

namespace Versioner.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVersionerServices(
        this IServiceCollection services,
        Back4AppOptions back4App)
    {
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(back4App));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new EmailOptions()));
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IAppReleaseClient, AppReleaseClient>();
        services.AddTransient<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
