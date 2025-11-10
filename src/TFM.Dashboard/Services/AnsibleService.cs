using CliWrap;
using CliWrap.Buffered;
using TFM.Dashboard.Interfaces;
using TFM.Dashboard.Models;

namespace TFM.Dashboard.Services;

/// <summary>
/// Servicio para ejecutar playbooks de Ansible.
/// 
/// SOLID:
/// - S: Solo ejecución de Ansible
/// - D: Depende de IConfiguration y ILogger
/// </summary>
public class AnsibleService : IAnsibleService
{
    private readonly ILogger<AnsibleService> _logger;
    private readonly string _playbooksPath;

    public AnsibleService(IConfiguration configuration, ILogger<AnsibleService> logger)
    {
        _logger = logger;
        _playbooksPath = configuration["Ansible:PlaybooksPath"]
            ?? throw new InvalidOperationException("Ansible:PlaybooksPath not configured");

        Directory.CreateDirectory(_playbooksPath);
    }

    public async Task<bool> IsAnsibleInstalledAsync()
    {
        try
        {
            var result = await Cli.Wrap("ansible")
                .WithArguments("--version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetAnsibleVersionAsync()
    {
        try
        {
            var result = await Cli.Wrap("ansible")
                .WithArguments("--version")
                .ExecuteBufferedAsync();

            return result.StandardOutput.Split('\n')[0];
        }
        catch
        {
            return "Not installed";
        }
    }

    public async Task<AnsibleResult> RunPlaybookAsync(
        string playbookPath,
        Dictionary<string, string>? extraVars = null,
        Action<string>? onOutput = null)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("🎭 Running Ansible playbook: {Playbook}", playbookPath);

        var args = new List<string> { playbookPath };

        // Variables extra
        if (extraVars != null && extraVars.Any())
        {
            var varsJson = System.Text.Json.JsonSerializer.Serialize(extraVars);
            args.Add("--extra-vars");
            args.Add(varsJson);
        }

        // Inventario local
        args.Add("-i");
        args.Add("localhost,");
        args.Add("--connection=local");

        try
        {
            var stdOutBuffer = new List<string>();
            var stdErrBuffer = new List<string>();

            var result = await Cli.Wrap("ansible-playbook")
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                {
                    _logger.LogInformation("  {Line}", line);
                    stdOutBuffer.Add(line);
                    onOutput?.Invoke(line);
                }))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    _logger.LogWarning("  {Line}", line);
                    stdErrBuffer.Add(line);
                    onOutput?.Invoke($"[ERROR] {line}");
                }))
                .ExecuteAsync();

            var duration = DateTime.UtcNow - startTime;

            var ansibleResult = new AnsibleResult
            {
                Success = result.ExitCode == 0,
                Output = string.Join("\n", stdOutBuffer),
                Error = string.Join("\n", stdErrBuffer),
                ExitCode = result.ExitCode,
                Duration = duration
            };

            if (ansibleResult.Success)
            {
                _logger.LogInformation(
                    "✅ Playbook completed successfully in {Duration:F1}s",
                    duration.TotalSeconds);
            }
            else
            {
                _logger.LogError(
                    "❌ Playbook failed with exit code {ExitCode}",
                    result.ExitCode);
            }

            return ansibleResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running playbook");
            return new AnsibleResult
            {
                Success = false,
                Error = ex.Message,
                ExitCode = -1,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<string> GeneratePlaybookAsync(PlaybookTemplate template)
    {
        var playbookPath = Path.Combine(_playbooksPath, $"{template.Name}.yml");

        var content = template.Content;

        // Reemplazar variables
        foreach (var (key, value) in template.Variables)
        {
            content = content.Replace($"{{{{{key}}}}}", value);
        }

        await File.WriteAllTextAsync(playbookPath, content);

        _logger.LogInformation("✅ Playbook generated: {Path}", playbookPath);

        return playbookPath;
    }
}