namespace TFM.Dashboard.Models;

/// <summary>
/// Representa una aplicación desplegada en el sistema.
/// </summary>
public class Application
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Domain { get; set; }
    public string? Description { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Stopped;

    // Docker
    public List<string> ContainerIds { get; set; } = new();
    public string? ComposeFilePath { get; set; }

    // Deployment
    public DeploymentSource? Source { get; set; }
    public Dictionary<string, string> Environment { get; set; } = new();

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeployedAt { get; set; }
    public string? Version { get; set; }
    public long? DiskUsageMB { get; set; }

    // Configuración
    public int Port { get; set; } = 80;
    public bool EnableHttps { get; set; } = true;
}

public enum ApplicationStatus
{
    Starting,
    Running,
    Stopped,
    Error,
    Deploying,
    Updating
}

public class DeploymentSource
{
    public DeploymentType Type { get; set; }
    public string? GitUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? DockerImage { get; set; }
    public string? PlaybookPath { get; set; }
}

public enum DeploymentType
{
    Template,
    GitHub,
    DockerCompose,
    AnsiblePlaybook,
    DockerImage
}