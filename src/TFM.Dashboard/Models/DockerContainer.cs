namespace TFM.Dashboard.Models;

public class DockerContainer
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Image { get; set; }
    public required string Status { get; set; }
    public required string State { get; set; }
    public DateTime Created { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public List<PortMapping> Ports { get; set; } = new();
    public ContainerStats? Stats { get; set; }
}

public class PortMapping
{
    public int PrivatePort { get; set; }
    public int PublicPort { get; set; }
    public string Type { get; set; } = "tcp";
}

public class ContainerStats
{
    public double CpuPercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long MemoryLimitMB { get; set; }
    public double MemoryPercent { get; set; }
    public long NetworkRxMB { get; set; }
    public long NetworkTxMB { get; set; }
}

public class DockerSystemInfo
{
    public long Containers { get; set; }
    public long ContainersRunning { get; set; }
    public long ContainersPaused { get; set; }
    public long ContainersStopped { get; set; }
    public long Images { get; set; }
    public string OperatingSystem { get; set; } = "";
    public string Architecture { get; set; } = "";
    public long MemTotal { get; set; }
}