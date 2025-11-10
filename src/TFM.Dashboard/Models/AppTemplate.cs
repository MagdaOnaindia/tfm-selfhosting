namespace TFM.Dashboard.Models;

/// <summary>
/// Template predefinido para una aplicación.
/// </summary>
public class AppTemplate
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? IconUrl { get; set; }
    public required string Category { get; set; }
    public string? DockerImage { get; set; }
    public string? ComposeTemplate { get; set; }
    public List<EnvironmentVariable> RequiredEnvVars { get; set; } = new();
    public List<VolumeMount> Volumes { get; set; } = new();
    public int DefaultPort { get; set; } = 80;
    public string? Documentation { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class EnvironmentVariable
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? DefaultValue { get; set; }
    public bool Required { get; set; } = true;
    public bool IsSecret { get; set; } = false;
    public string? ValidationRegex { get; set; }
}

public class VolumeMount
{
    public required string Name { get; set; }
    public required string ContainerPath { get; set; }
    public required string HostPath { get; set; }
}