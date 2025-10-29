namespace TFM.Broker.Interfaces;

/// <summary>
/// Servicio de enrutamiento de dominios a agentes.
/// En single-tenant: Busca en un archivo de configuración JSON.
/// En multi-tenant: Buscaría en una base de datos PostgreSQL.
/// </summary>
public interface IRoutingService
{
    Task<RouteInfo?> GetRouteForDomainAsync(string domain);
    Task ReloadRoutesAsync();
}

public class RouteInfo
{
    public required string AgentId { get; set; }
    public int TargetPort { get; set; }
    public string? UserId { get; set; } // Preparado para multi-tenant
}