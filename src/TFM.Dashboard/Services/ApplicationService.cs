using System.Text.Json;
using TFM.Dashboard.Interfaces;
using TFM.Dashboard.Models;

namespace TFM.Dashboard.Services;

/// <summary>
/// Implementación del servicio de gestión de aplicaciones.
/// Usa archivo JSON como persistencia.
/// 
/// SOLID:
/// - S: Solo gestiona aplicaciones
/// - D: Depende de IDockerService y ITraefikConfigService
/// </summary>
public class ApplicationService : IApplicationService
{
    private readonly string _dataPath;
    private readonly IDockerService _dockerService;
    private readonly ITraefikConfigService _traefikService;
    private readonly ILogger<ApplicationService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ApplicationService(
        IConfiguration configuration,
        IDockerService dockerService,
        ITraefikConfigService traefikService,
        ILogger<ApplicationService> logger)
    {
        _dockerService = dockerService;
        _traefikService = traefikService;
        _logger = logger;

        _dataPath = configuration["Data:AppsPath"]
            ?? throw new InvalidOperationException("Data:AppsPath not configured");

        // Crear directorio si no existe
        var directory = Path.GetDirectoryName(_dataPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<List<Application>> GetApplicationsAsync()
    {
        if (!File.Exists(_dataPath))
        {
            return new List<Application>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_dataPath);
            return JsonSerializer.Deserialize<List<Application>>(json)
                ?? new List<Application>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading applications from {Path}", _dataPath);
            return new List<Application>();
        }
    }

    public async Task<Application?> GetApplicationAsync(string id)
    {
        var apps = await GetApplicationsAsync();
        return apps.FirstOrDefault(a => a.Id == id);
    }

    public async Task<Application> CreateApplicationAsync(Application app)
    {
        await _lock.WaitAsync();
        try
        {
            var apps = await GetApplicationsAsync();

            app.Id = Guid.NewGuid().ToString();
            app.CreatedAt = DateTime.UtcNow;
            app.Status = ApplicationStatus.Stopped;

            apps.Add(app);
            await SaveApplicationsAsync(apps);

            _logger.LogInformation("✓ Application created: {Name} ({Id})", app.Name, app.Id);
            return app;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateApplicationAsync(Application app)
    {
        await _lock.WaitAsync();
        try
        {
            var apps = await GetApplicationsAsync();
            var index = apps.FindIndex(a => a.Id == app.Id);

            if (index >= 0)
            {
                apps[index] = app;
                await SaveApplicationsAsync(apps);
                _logger.LogInformation("✓ Application updated: {Name}", app.Name);
            }
            else
            {
                _logger.LogWarning("Application not found for update: {Id}", app.Id);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteApplicationAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var apps = await GetApplicationsAsync();
            var app = apps.FirstOrDefault(a => a.Id == id);

            if (app != null)
            {
                // Detener y eliminar contenedores
                foreach (var containerId in app.ContainerIds)
                {
                    try
                    {
                        await _dockerService.StopContainerAsync(containerId);
                        await _dockerService.RemoveContainerAsync(containerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove container {ContainerId}", containerId);
                    }
                }

                // Eliminar configuración de Traefik
                await _traefikService.RemoveDomainAsync(app.Domain);

                // Si tiene docker-compose, ejecutar down
                if (!string.IsNullOrEmpty(app.ComposeFilePath) && File.Exists(app.ComposeFilePath))
                {
                    try
                    {
                        await _dockerService.RemoveComposeAsync(
                            app.ComposeFilePath,
                            app.Name.ToLowerInvariant().Replace(" ", "-"));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove compose project");
                    }
                }

                apps.Remove(app);
                await SaveApplicationsAsync(apps);

                _logger.LogInformation("🗑️ Application deleted: {Name}", app.Name);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Application> DeployApplicationAsync(Application app)
    {
        _logger.LogInformation("🚀 Deploying application: {Name}", app.Name);

        app.Status = ApplicationStatus.Deploying;
        await UpdateApplicationAsync(app);

        try
        {
            // 1. Configurar dominio en Traefik
            await _traefikService.AddDomainAsync(new DomainConfiguration
            {
                Domain = app.Domain,
                ServiceName = app.Name.ToLowerInvariant().Replace(" ", "-"),
                Port = app.Port,
                EnableHttps = app.EnableHttps
            });

            // 2. Desplegar con Docker Compose si está disponible
            if (!string.IsNullOrEmpty(app.ComposeFilePath) && File.Exists(app.ComposeFilePath))
            {
                var projectName = app.Name.ToLowerInvariant().Replace(" ", "-");
                await _dockerService.DeployComposeAsync(app.ComposeFilePath, projectName);

                // Esperar un poco para que los contenedores inicien
                await Task.Delay(2000);

                // Sincronizar para obtener los container IDs
                await SyncApplicationWithDockerAsync(app);
            }

            app.Status = ApplicationStatus.Running;
            app.LastDeployedAt = DateTime.UtcNow;
            await UpdateApplicationAsync(app);

            _logger.LogInformation("✅ Application deployed successfully: {Name}", app.Name);
            return app;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Application deployment failed: {Name}", app.Name);
            app.Status = ApplicationStatus.Error;
            await UpdateApplicationAsync(app);
            throw;
        }
    }

    public async Task StopApplicationAsync(string id)
    {
        var app = await GetApplicationAsync(id);
        if (app == null)
        {
            throw new InvalidOperationException($"Application {id} not found");
        }

        _logger.LogInformation("⏹️ Stopping application: {Name}", app.Name);

        foreach (var containerId in app.ContainerIds)
        {
            try
            {
                await _dockerService.StopContainerAsync(containerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop container {ContainerId}", containerId);
            }
        }

        app.Status = ApplicationStatus.Stopped;
        await UpdateApplicationAsync(app);
    }

    public async Task StartApplicationAsync(string id)
    {
        var app = await GetApplicationAsync(id);
        if (app == null)
        {
            throw new InvalidOperationException($"Application {id} not found");
        }

        _logger.LogInformation("▶️ Starting application: {Name}", app.Name);

        foreach (var containerId in app.ContainerIds)
        {
            try
            {
                await _dockerService.StartContainerAsync(containerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start container {ContainerId}", containerId);
            }
        }

        app.Status = ApplicationStatus.Running;
        await UpdateApplicationAsync(app);
    }

    public async Task SyncWithDockerAsync()
    {
        _logger.LogInformation("🔄 Syncing applications with Docker...");

        var apps = await GetApplicationsAsync();
        var containers = await _dockerService.GetContainersAsync(all: false);

        foreach (var app in apps)
        {
            await SyncApplicationWithDockerAsync(app, containers);
        }

        await SaveApplicationsAsync(apps);
        _logger.LogInformation("✅ Sync completed: {Count} applications", apps.Count);
    }

    public async Task<List<string>> GetApplicationLogsAsync(string id, int tail = 100)
    {
        var app = await GetApplicationAsync(id);
        if (app == null || !app.ContainerIds.Any())
        {
            return new List<string>();
        }

        var allLogs = new List<string>();

        foreach (var containerId in app.ContainerIds)
        {
            try
            {
                var logs = await _dockerService.GetContainerLogsAsync(containerId, tail);
                allLogs.Add($"=== Logs from {containerId} ===");
                allLogs.AddRange(logs);
                allLogs.Add("");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get logs for container {ContainerId}", containerId);
            }
        }

        return allLogs;
    }

    private async Task SyncApplicationWithDockerAsync(
        Application app,
        List<DockerContainer>? containers = null)
    {
        containers ??= await _dockerService.GetContainersAsync(all: false);

        // Buscar contenedores por labels o nombre del proyecto
        var projectName = app.Name.ToLowerInvariant().Replace(" ", "-");

        var appContainers = containers.Where(c =>
            c.Labels.GetValueOrDefault("com.docker.compose.project") == projectName ||
            c.Labels.GetValueOrDefault("app.id") == app.Id ||
            c.Name.StartsWith(projectName, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (appContainers.Any())
        {
            app.ContainerIds = appContainers.Select(c => c.Id).ToList();
            app.Status = appContainers.All(c => c.State == "running")
                ? ApplicationStatus.Running
                : ApplicationStatus.Stopped;
        }
        else
        {
            app.Status = ApplicationStatus.Stopped;
            app.ContainerIds.Clear();
        }
    }

    private async Task SaveApplicationsAsync(List<Application> apps)
    {
        var json = JsonSerializer.Serialize(apps, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_dataPath, json);
    }
}