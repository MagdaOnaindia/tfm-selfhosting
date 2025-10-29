namespace TFM.Contracts.Models;

/// <summary>
/// Configuración del Broker (VPS).
/// En single-tenant: una configuración simple.
/// En multi-tenant: podría tener una lista de agentes.
/// </summary>
public class BrokerConfiguration
{
    public string BrokerName { get; set; } = "SelfHosting-Broker";
    public string Version { get; set; } = "1.0.0-single";

    // Para single-tenant: solo hay una configuración de agente.
    public AgentConfiguration? Agent { get; set; }

    // Preparado para multi-tenant: una lista de todos los agentes permitidos.
    public List<AgentConfiguration>? Agents { get; set; }
}

/// <summary>
/// Representa la configuración de un único agente en el Broker.
/// </summary>
public class AgentConfiguration
{
    public required string AgentId { get; set; }
    public required string CertificateThumbprint { get; set; }
    public DateTime CertificateExpiresAt { get; set; }
    public string? UserId { get; set; } // Campo para futuro multi-tenant
}