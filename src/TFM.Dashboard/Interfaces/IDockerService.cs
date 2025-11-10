using TFM.Dashboard.Models;

namespace TFM.Dashboard.Interfaces;

/// <summary>
/// Abstracción para interactuar con Docker.
/// 
/// SOLID:
/// - S: Solo operaciones de Docker
/// - O: Puede tener implementaciones mock para testing
/// - I: Interface específica para Docker
/// </summary>
public interface IDockerService
{
    /// <summary>
    /// Lista todos los contenedores.
    /// </summary>
    /// <param name="all">Si es true, incluye contenedores detenidos</param>
    Task<List<DockerContainer>> GetContainersAsync(bool all = false);

    /// <summary>
    /// Obtiene un contenedor por ID o nombre.
    /// </summary>
    Task<DockerContainer?> GetContainerByIdAsync(string id);

    /// <summary>
    /// Inicia un contenedor.
    /// </summary>
    Task StartContainerAsync(string id);

    /// <summary>
    /// Detiene un contenedor.
    /// </summary>
    Task StopContainerAsync(string id);

    /// <summary>
    /// Reinicia un contenedor.
    /// </summary>
    Task RestartContainerAsync(string id);

    /// <summary>
    /// Elimina un contenedor.
    /// </summary>
    Task RemoveContainerAsync(string id);

    /// <summary>
    /// Obtiene estadísticas de un contenedor.
    /// </summary>
    Task<ContainerStats?> GetContainerStatsAsync(string id);

    /// <summary>
    /// Obtiene logs de un contenedor.
    /// </summary>
    Task<List<string>> GetContainerLogsAsync(string id, int tail = 100);

    /// <summary>
    /// Despliega un proyecto usando Docker Compose.
    /// </summary>
    /// <param name="composeFilePath">Ruta al docker-compose.yml</param>
    /// <param name="projectName">Nombre del proyecto</param>
    Task<string> DeployComposeAsync(string composeFilePath, string projectName);

    /// <summary>
    /// Detiene un proyecto de Docker Compose.
    /// </summary>
    Task<string> StopComposeAsync(string composeFilePath, string projectName);

    /// <summary>
    /// Elimina un proyecto de Docker Compose.
    /// </summary>
    Task<string> RemoveComposeAsync(string composeFilePath, string projectName);

    /// <summary>
    /// Verifica si Docker está disponible.
    /// </summary>
    Task<bool> IsDockerAvailableAsync();

    /// <summary>
    /// Obtiene información del sistema Docker.
    /// </summary>
    Task<DockerSystemInfo> GetSystemInfoAsync();
}