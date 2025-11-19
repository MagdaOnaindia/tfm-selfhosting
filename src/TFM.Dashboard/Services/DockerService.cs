using Docker.DotNet;
using Docker.DotNet.Models;
using TFM.Dashboard.Interfaces;
using TFM.Dashboard.Models;
using CliWrap;
using CliWrap.Buffered;

namespace TFM.Dashboard.Services;

/// <summary>
/// Implementación del servicio de Docker.

/// </summary>
public class DockerService : IDockerService, IDisposable
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerService> _logger;

    public DockerService(IConfiguration configuration, ILogger<DockerService> logger)
    {
        _logger = logger;

        var dockerUrl = configuration["Docker:Url"] ?? DetectDockerUrl();

        _client = new DockerClientConfiguration(new Uri(dockerUrl))
            .CreateClient();

        _logger.LogInformation(" Docker client initialized: {Url}", dockerUrl);
    }

    private static string DetectDockerUrl()
    {
        if (OperatingSystem.IsWindows())
            return "npipe://./pipe/docker_engine";
        return "unix:///var/run/docker.sock";
    }

    public async Task<List<DockerContainer>> GetContainersAsync(bool all = false)
    {
        try
        {
            var containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters { All = all });

            return containers.Select(c => new DockerContainer
            {
                Id = c.ID[..Math.Min(12, c.ID.Length)],
                Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
                Image = c.Image,
                Status = c.Status,
                State = c.State,
                Created = c.Created,
                Labels = c.Labels?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, string>(),
                Ports = c.Ports.Select(p => new PortMapping
                {
                    PrivatePort = (int)p.PrivatePort,
                    PublicPort = (int)p.PublicPort,
                    Type = p.Type
                }).ToList()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting containers");
            throw;
        }
    }

    public async Task<DockerContainer?> GetContainerByIdAsync(string id)
    {
        var containers = await GetContainersAsync(all: true);
        return containers.FirstOrDefault(c =>
            c.Id == id ||
            c.Id.StartsWith(id) ||
            c.Name == id);
    }

    public async Task StartContainerAsync(string id)
    {
        _logger.LogInformation("Starting container: {Id}", id);
        await _client.Containers.StartContainerAsync(id, new ContainerStartParameters());
    }

    public async Task StopContainerAsync(string id)
    {
        _logger.LogInformation(" Stopping container: {Id}", id);
        await _client.Containers.StopContainerAsync(id, new ContainerStopParameters
        {
            WaitBeforeKillSeconds = 10
        });
    }

    public async Task RestartContainerAsync(string id)
    {
        _logger.LogInformation("Restarting container: {Id}", id);
        await _client.Containers.RestartContainerAsync(id, new ContainerRestartParameters());
    }

    public async Task RemoveContainerAsync(string id)
    {
        _logger.LogWarning("Removing container: {Id}", id);
        await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters
        {
            Force = true
        });
    }

    public async Task<ContainerStats?> GetContainerStatsAsync(string id)
    {
        try
        {
            // TODO: Prasear json completo de stats
            return new ContainerStats
            {
                CpuPercent = 0,
                MemoryUsageMB = 0,
                MemoryLimitMB = 0,
                MemoryPercent = 0,
                NetworkRxMB = 0,
                NetworkTxMB = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get stats for container {Id}", id);
            return null;
        }
    }

    public async Task<List<string>> GetContainerLogsAsync(string id, int tail = 100)
    {
        try
        {
            var logs = await _client.Containers.GetContainerLogsAsync(
                id,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Tail = tail.ToString()
                });

            using var reader = new StreamReader(logs);
            var lines = new List<string>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    // Limpiar caracteres de control de Docker
                    if (line.Length > 8)
                        line = line.Substring(8);

                    lines.Add(line);
                }
            }

            return lines;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs for container {Id}", id);
            return new List<string> { $"Error: {ex.Message}" };
        }
    }

    public async Task<string> DeployComposeAsync(string composeFilePath, string projectName)
    {
        _logger.LogInformation("Deploying compose project: {Project}", projectName);

        var result = await Cli.Wrap("docker")
            .WithArguments(new[]
            {
                "compose",
                "-f", composeFilePath,
                "-p", projectName,
                "up", "-d", "--remove-orphans"
            })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode != 0)
        {
            _logger.LogError("Compose deployment failed: {Error}", result.StandardError);
            throw new InvalidOperationException($"Deployment failed: {result.StandardError}");
        }

        _logger.LogInformation(" Compose project deployed: {Project}", projectName);
        return result.StandardOutput;
    }

    public async Task<string> StopComposeAsync(string composeFilePath, string projectName)
    {
        _logger.LogInformation("Stopping compose project: {Project}", projectName);

        var result = await Cli.Wrap("docker")
            .WithArguments(new[]
            {
                "compose",
                "-f", composeFilePath,
                "-p", projectName,
                "stop"
            })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        return result.StandardOutput;
    }

    public async Task<string> RemoveComposeAsync(string composeFilePath, string projectName)
    {
        _logger.LogInformation(" Removing compose project: {Project}", projectName);

        var result = await Cli.Wrap("docker")
            .WithArguments(new[]
            {
                "compose",
                "-f", composeFilePath,
                "-p", projectName,
                "down",
                "-v"  // También eliminar volúmenes
            })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        return result.StandardOutput;
    }

    public async Task<bool> IsDockerAvailableAsync()
    {
        try
        {
            await _client.System.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DockerSystemInfo> GetSystemInfoAsync()
    {
        var info = await _client.System.GetSystemInfoAsync();

        return new DockerSystemInfo
        {
            Containers = info.Containers,
            ContainersRunning = info.ContainersRunning,
            ContainersPaused = info.ContainersPaused,
            ContainersStopped = info.ContainersStopped,
            Images = info.Images,
            OperatingSystem = info.OperatingSystem,
            Architecture = info.Architecture,
            MemTotal = info.MemTotal
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}