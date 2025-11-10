namespace TFM.Dashboard.Models;

public class AnsibleResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
}

public class PlaybookTemplate
{
    public required string Name { get; set; }
    public required string Content { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}