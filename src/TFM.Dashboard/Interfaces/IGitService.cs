namespace TFM.Dashboard.Interfaces;

/// <summary>
/// Operaciones con repositorios Git.
/// 
/// SOLID:
/// - S: Solo operaciones Git
/// - I: Interface específica para Git
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Clona un repositorio Git.
    /// </summary>
    Task<string> CloneRepositoryAsync(
        string repoUrl,
        string destinationPath,
        string? username = null,
        string? token = null,
        string? branch = null);

    /// <summary>
    /// Verifica si una URL de repositorio es válida.
    /// </summary>
    bool IsValidRepositoryUrl(string url);

    /// <summary>
    /// Extrae información de un repositorio (owner, name).
    /// </summary>
    (string owner, string repo) ParseGitHubUrl(string url);
}