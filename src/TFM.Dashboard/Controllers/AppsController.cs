using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc;
namespace TFM.Dashboard.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AppsController : ControllerBase
{
    private readonly ILogger<AppsController> _logger;
    private readonly DockerClient _dockerClient;
    public AppsController(ILogger<AppsController> logger)
    {
        _logger = logger;
        var dockerUrl = Environment.OSVersion.Platform == PlatformID.Win32NT
        ? "npipe://./pipe/docker_engine"
        : "unix:///var/run/docker.sock";
        _dockerClient = new DockerClientConfiguration(new Uri(dockerUrl)).CreateClient();
    }
    [HttpGet]

    public async Task<IActionResult> GetApps()
    {
        try
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });
            var apps = containers
            .Where(c => c.Labels.ContainsKey("selfhosting.app.name"))
            .Select(c => new
            {
                Id = c.ID,
                Name = c.Labels.ContainsKey("selfhosting.app.name") ? c.Labels["selfhosting.app.name"] : "unknown",
                Type = c.Labels.ContainsKey("selfhosting.app.type") ? c.Labels["selfhosting.app.type"] : "unknown",
                Domain = c.Labels.ContainsKey("selfhosting.app.domain") ? c.Labels["selfhosting.app.domain"] : "",
                Status = c.State,
                Image = c.Image,
                Created = c.Created,
                Ports = c.Ports.Select(p => new
                {
                    p.PublicPort,
                    p.PrivatePort,
                    p.Type
                }).ToList()
            });
            return Ok(apps);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting apps");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartApp(string id)
    {
        try
        {
            await _dockerClient.Containers.StartContainerAsync(
            id,
            new ContainerStartParameters());
            return Ok(new { message = "Container started" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopApp(string id)
    {
        try
        {
            await _dockerClient.Containers.StopContainerAsync(
            id,
            new ContainerStopParameters { WaitBeforeKillSeconds = 10 });
            return Ok(new { message = "Container stopped" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/restart")]
    public async Task<IActionResult> RestartApp(string id)
    {
        try
        {
            await _dockerClient.Containers.RestartContainerAsync(
            id,
            new ContainerRestartParameters { WaitBeforeKillSeconds = 10 });
            return Ok(new { message = "Container restarted" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    [HttpGet("{id}/logs")]
    public async Task<IActionResult> GetLogs(string id, [FromQuery] int tail = 100)
    {
        try
        {
            var logsStream = await _dockerClient.Containers.GetContainerLogsAsync(
                id,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Tail = tail.ToString()
                });
            using var reader = new StreamReader(logsStream);
            var logs = await reader.ReadToEndAsync();
            return Ok(new { logs });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters { All = false });
            var totalContainers = containers.Count;
            var runningContainers = containers.Count(c => c.State == "running");
            return Ok(new
            {
                totalContainers,
                runningContainers,
                stoppedContainers = totalContainers - runningContainers
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}