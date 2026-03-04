using TFM.Dashboard.Models;

namespace TFM.Dashboard.Interfaces;

/// <summary>
/// Gestiona la configuración dinámica de Traefik.
/// </summary>
public interface ITraefikConfigService
{
    /// <summary>
    /// Obtiene todas las rutas configuradas.
    /// </summary>
    Task<List<DomainConfiguration>> GetDomainsAsync();

    /// <summary>
    /// Agrega una nueva configuración de dominio.
    /// </summary>
    Task AddDomainAsync(DomainConfiguration config);

    /// <summary>
    /// Elimina la configuración de un dominio.
    /// </summary>
    Task RemoveDomainAsync(string domain);

    /// <summary>
    /// Actualiza la configuración de un dominio.
    /// </summary>
    Task UpdateDomainAsync(string domain, DomainConfiguration config);

    /// <summary>
    /// Recarga la configuración de Traefik.
    /// </summary>
    Task ReloadConfigAsync();

    /// <summary>
    /// Genera labels de Docker para Traefik.
    /// </summary>
    Task<Dictionary<string, string>> GenerateDockerLabelsAsync(DomainConfiguration config);
}