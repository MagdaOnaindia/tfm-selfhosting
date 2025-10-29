// src/TFM.Broker/Services/FileRoutingService.cs
using System.Text.Json;
using TFM.Broker.Interfaces;
using TFM.Contracts.Models; // Necesitarás crear este modelo

namespace TFM.Broker.Services;

public class FileRoutingService : IRoutingService
{
    private readonly ILogger<FileRoutingService> _logger;
    private readonly string _configPath;
    private DomainMappingConfiguration _config = new();

    public FileRoutingService(IConfiguration configuration, ILogger<FileRoutingService> logger)
    {
        _logger = logger;
        _configPath = configuration["Routing:ConfigPath"] ?? throw new InvalidOperationException("Ruta del archivo de configuración de routing no definida.");
        ReloadRoutesAsync().GetAwaiter().GetResult();
    }

    public Task<RouteInfo?> GetRouteForDomainAsync(string domain)
    {
        if (_config.Routes.TryGetValue(domain, out var route))
        {
            _logger.LogDebug("Ruta encontrada para {Domain} -> Agente {AgentId}", domain, route.AgentId);
            return Task.FromResult<RouteInfo?>(new RouteInfo { AgentId = route.AgentId, TargetPort = route.TargetPort });
        }
        // Aquí iría la lógica para wildcards, si la añades
        _logger.LogWarning("No se encontró ruta para el dominio {Domain}", domain);
        return Task.FromResult<RouteInfo?>(null);
    }

    public async Task ReloadRoutesAsync()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogWarning("Archivo de configuración de rutas no encontrado en: {Path}", _configPath);
            return;
        }
        var json = await File.ReadAllTextAsync(_configPath);
        var config = JsonSerializer.Deserialize<DomainMappingConfiguration>(json);
        if (config != null)
        {
            _config = config;
            _logger.LogInformation("{Count} rutas cargadas desde {Path}", _config.Routes.Count, _configPath);
        }
    }
}