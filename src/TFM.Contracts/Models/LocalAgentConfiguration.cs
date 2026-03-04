namespace TFM.Contracts.Models;

/// <summary>
/// Configuración del Agente que se ejecuta en la torre local.
/// </summary>
public class LocalAgentConfiguration
{
    public required string AgentId { get; set; }
    public required string BrokerUrl { get; set; }

    // Rutas a los certificados mTLS
    public required string CertificatePath { get; set; }
    public required string CertificatePassword { get; set; }
    public required string CaCertPath { get; set; }

    // URL del proxy local (Traefik)
    public string TraefikUrl { get; set; } = "http://localhost:80";

    // Campos preparados para multi-tenant
    public string? UserId { get; set; }
    public string? Subdomain { get; set; }
}