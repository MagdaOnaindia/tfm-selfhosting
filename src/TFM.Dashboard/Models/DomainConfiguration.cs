namespace TFM.Dashboard.Models;

/// <summary>
/// Configuración de enrutamiento de un dominio en Traefik.
/// </summary>
public class DomainConfiguration
{
    public required string Domain { get; set; }
    public required string ServiceName { get; set; }
    public int Port { get; set; } = 80;
    public bool EnableHttps { get; set; } = true;
    public List<string>? Middlewares { get; set; }
    public Dictionary<string, string>? CustomHeaders { get; set; }
    public string? PathPrefix { get; set; }
}