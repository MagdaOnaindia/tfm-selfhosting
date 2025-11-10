using TFM.Dashboard.Interfaces;
using TFM.Dashboard.Services;

namespace TFM.Dashboard.Extensions;

/// <summary>
/// Extensiones para configurar servicios del Dashboard.
/// 
/// SOLID:
/// - S: Solo configuración de DI
/// - O: Fácil agregar nuevos servicios
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todos los servicios del Dashboard.
    /// </summary>
    public static IServiceCollection AddDashboardServices(this IServiceCollection services)
    {
        // Servicios principales (Singleton - estado compartido)
        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IApplicationService, ApplicationService>();
        services.AddSingleton<ITraefikConfigService, TraefikConfigService>();
        services.AddSingleton<IAnsibleService, AnsibleService>();
        services.AddSingleton<IAppTemplateService, AppTemplateService>();
        services.AddSingleton<IGitService, GitService>();

        return services;
    }
}