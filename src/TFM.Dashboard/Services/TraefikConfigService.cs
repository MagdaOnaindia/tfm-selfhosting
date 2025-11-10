using System.Text;
using TFM.Dashboard.Interfaces;
using TFM.Dashboard.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TFM.Dashboard.Services;

/// <summary>
/// Gestiona la configuración dinámica de Traefik mediante archivos YAML.
/// 
/// SOLID:
/// - S: Solo gestión de configuración de Traefik
/// - D: Depende de IConfiguration y ILogger
/// </summary>
public class TraefikConfigService : ITraefikConfigService
{
    private readonly string _configPath;
    private readonly ILogger<TraefikConfigService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TraefikConfigService(
        IConfiguration configuration,
        ILogger<TraefikConfigService> logger)
    {
        _logger = logger;
        _configPath = configuration["Traefik:DynamicConfigPath"]
            ?? throw new InvalidOperationException("Traefik:DynamicConfigPath not configured");

        Directory.CreateDirectory(_configPath);
    }

    public async Task<List<DomainConfiguration>> GetDomainsAsync()
    {
        var configs = new List<DomainConfiguration>();

        if (!Directory.Exists(_configPath))
        {
            return configs;
        }

        foreach (var file in Directory.GetFiles(_configPath, "*.yml"))
        {
            try
            {
                var yaml = await File.ReadAllTextAsync(file);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                // Parsear YAML de Traefik (simplificado)
                // En producción, usar un modelo completo
                var fileName = Path.GetFileNameWithoutExtension(file);

                configs.Add(new DomainConfiguration
                {
                    Domain = fileName,
                    ServiceName = fileName,
                    Port = 80
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse config file: {File}", file);
            }
        }

        return configs;
    }

    public async Task AddDomainAsync(DomainConfiguration config)
    {
        await _lock.WaitAsync();
        try
        {
            var fileName = $"{config.ServiceName}.yml";
            var filePath = Path.Combine(_configPath, fileName);

            // Generar configuración YAML de Traefik
            var traefikConfig = new
            {
                http = new
                {
                    routers = new Dictionary<string, object>
                    {
                        [config.ServiceName] = new
                        {
                            rule = $"Host(`{config.Domain}`)",
                            service = config.ServiceName,
                            entryPoints = new[] { "web" },
                            middlewares = config.Middlewares ?? new List<string>()
                        }
                    },
                    services = new Dictionary<string, object>
                    {
                        [config.ServiceName] = new
                        {
                            loadBalancer = new
                            {
                                servers = new[]
                                {
                                    new { url = $"http://{config.ServiceName}:{config.Port}" }
                                }
                            }
                        }
                    }
                }
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(traefikConfig);
            await File.WriteAllTextAsync(filePath, yaml);

            _logger.LogInformation(
                "✅ Domain configuration added: {Domain} -> {Service}:{Port}",
                config.Domain, config.ServiceName, config.Port);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveDomainAsync(string domain)
    {
        await _lock.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_configPath, "*.yml");
            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                if (content.Contains(domain))
                {
                    File.Delete(file);
                    _logger.LogInformation("🗑️ Domain configuration removed: {Domain}", domain);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateDomainAsync(string domain, DomainConfiguration config)
    {
        await RemoveDomainAsync(domain);
        await AddDomainAsync(config);
    }

    public Task ReloadConfigAsync()
    {
        // Traefik recarga automáticamente los archivos
        _logger.LogInformation("🔄 Traefik will auto-reload configuration");
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, string>> GenerateDockerLabelsAsync(DomainConfiguration config)
    {
        var labels = new Dictionary<string, string>
        {
            ["traefik.enable"] = "true",
            [$"traefik.http.routers.{config.ServiceName}.rule"] = $"Host(`{config.Domain}`)",
            [$"traefik.http.routers.{config.ServiceName}.entrypoints"] = "web",
            [$"traefik.http.services.{config.ServiceName}.loadbalancer.server.port"] = config.Port.ToString()
        };

        if (config.EnableHttps)
        {
            labels[$"traefik.http.routers.{config.ServiceName}.tls"] = "true";
        }

        return Task.FromResult(labels);
    }
}