using System.Text.Json;
using TFM.Broker.Interfaces;
using TFM.Contracts.Models;

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
        try
        {
            var json = await File.ReadAllTextAsync(_configPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var config = JsonSerializer.Deserialize<DomainMappingConfiguration>(json, options);

            if (config?.Routes == null)
            {
                _logger.LogError("SECURITY: Invalid configuration - Routes is null");
                return;
            }

            // Validar cada ruta
            var validRoutes = new Dictionary<string, DomainRoute>();
            foreach (var (domain, route) in config.Routes)
            {
                if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(route.AgentId))
                {
                    _logger.LogWarning("SECURITY: Ruta inválida ignorada - Domain: {Domain}", domain);
                    continue;
                }

                // Validar formato de dominio
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    domain,
                    @"^(\*\.)?[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    _logger.LogWarning("SECURITY: Formato de dominio inválido ignorado: {Domain}", domain);
                    continue;
                }

                validRoutes[domain] = route;
            }

            config.Routes = validRoutes;
            _config = config;
            _logger.LogInformation("{Count} rutas válidas cargadas desde {Path}", _config.Routes.Count, _configPath);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SECURITY: Error parseando JSON de configuración");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SECURITY: Error leyendo archivo de configuración");
        }
        
    }
}