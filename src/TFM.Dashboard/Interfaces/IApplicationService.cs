using TFM.Dashboard.Models;

namespace TFM.Dashboard.Interfaces;

/// <summary>
/// Gestiona el ciclo de vida completo de las aplicaciones desplegadas.
/// 
/// SOLID:
/// - S: Solo gestión de aplicaciones
/// - O: Extensible con diferentes fuentes de datos
/// - L: Implementaciones intercambiables
/// - I: Interface específica para aplicaciones
/// - D: Componentes dependen de esta abstracción
/// </summary>
public interface IApplicationService
{
    /// <summary>
    /// Obtiene todas las aplicaciones desplegadas.
    /// </summary>
    Task<List<Application>> GetApplicationsAsync();

    /// <summary>
    /// Obtiene una aplicación por ID.
    /// </summary>
    Task<Application?> GetApplicationAsync(string id);

    /// <summary>
    /// Crea una nueva aplicación (sin desplegar).
    /// </summary>
    Task<Application> CreateApplicationAsync(Application app);

    /// <summary>
    /// Actualiza los datos de una aplicación.
    /// </summary>
    Task UpdateApplicationAsync(Application app);

    /// <summary>
    /// Elimina una aplicación (detiene y elimina contenedores).
    /// </summary>
    Task DeleteApplicationAsync(string id);

    /// <summary>
    /// Despliega una aplicación (ejecuta docker compose).
    /// </summary>
    Task<Application> DeployApplicationAsync(Application app);

    /// <summary>
    /// Detiene una aplicación.
    /// </summary>
    Task StopApplicationAsync(string id);

    /// <summary>
    /// Inicia una aplicación detenida.
    /// </summary>
    Task StartApplicationAsync(string id);

    /// <summary>
    /// Sincroniza el estado de las aplicaciones con Docker.
    /// Actualiza estados y container IDs.
    /// </summary>
    Task SyncWithDockerAsync();

    /// <summary>
    /// Obtiene logs de una aplicación.
    /// </summary>
    Task<List<string>> GetApplicationLogsAsync(string id, int tail = 100);
}