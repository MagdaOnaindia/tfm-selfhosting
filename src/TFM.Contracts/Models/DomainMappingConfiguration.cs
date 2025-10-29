// src/TFM.Contracts/Models/DomainMappingConfiguration.cs
using System.Collections.Generic;

namespace TFM.Contracts.Models;

/// <summary>
/// Define el mapeo de dominios a rutas de agentes.
/// </summary>
public class DomainMappingConfiguration
{
    // En single-tenant: un diccionario de dominios directos.
    // En multi-tenant: podría incluir patrones de subdominios.
    public Dictionary<string, DomainRoute> Routes { get; set; } = new();
}

public class DomainRoute
{
    public required string AgentId { get; set; }
    public int TargetPort { get; set; }
    public string? Description { get; set; }
}