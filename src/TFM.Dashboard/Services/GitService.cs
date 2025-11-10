using System.Text.RegularExpressions;
using LibGit2Sharp;
using TFM.Dashboard.Interfaces;

namespace TFM.Dashboard.Services;

/// <summary>
/// Servicio para operaciones con repositorios Git.
/// 
/// SOLID:
/// - S: Solo operaciones Git
/// </summary>
public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CloneRepositoryAsync(
        string repoUrl,
        string destinationPath,
        string? username = null,
        string? token = null,
        string? branch = null)
    {
        _logger.LogInformation("📥 Cloning repository: {Url}", repoUrl);

        try
        {
            var cloneOptions = new CloneOptions();

            // Si hay credenciales, configurar
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token))
            {
                cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                    new UsernamePasswordCredentials
                    {
                        Username = username,
                        Password = token
                    };
            }

            // Rama específica
            if (!string.IsNullOrEmpty(branch))
            {
                cloneOptions.BranchName = branch;
            }

            await Task.Run(() => Repository.Clone(repoUrl, destinationPath, cloneOptions));

            _logger.LogInformation("✅ Repository cloned to: {Path}", destinationPath);
            return destinationPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to clone repository");
            throw;
        }
    }

    public bool IsValidRepositoryUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Patrones válidos de Git
        var patterns = new[]
        {
            @"^https?://github\.com/[\w-]+/[\w.-]+(?:\.git)?$",
            @"^git@github\.com:[\w-]+/[\w.-]+(?:\.git)?$",
            @"^https?://gitlab\.com/[\w-]+/[\w.-]+(?:\.git)?$",
            @"^https?://bitbucket\.org/[\w-]+/[\w.-]+(?:\.git)?$"
        };

        return patterns.Any(pattern => Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase));
    }

    public (string owner, string repo) ParseGitHubUrl(string url)
    {
        // Ejemplo: https://github.com/user/repo.git
        var match = Regex.Match(url, @"github\.com[:/]([\w-]+)/([\w.-]+?)(?:\.git)?$");

        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        throw new ArgumentException("Invalid GitHub URL format");
    }
}