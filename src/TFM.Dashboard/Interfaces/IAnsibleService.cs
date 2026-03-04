using TFM.Dashboard.Models;

namespace TFM.Dashboard.Interfaces;

/// <summary>
/// Ejecuta playbooks de Ansible.
/// </summary>
public interface IAnsibleService
{
    /// <summary>
    /// Ejecuta un playbook de Ansible.
    /// </summary>
    /// <param name="playbookPath">Ruta al playbook YAML</param>
    /// <param name="extraVars">Variables extra para pasar al playbook</param>
    /// <param name="onOutput">Callback para recibir output en tiempo real</param>
    Task<AnsibleResult> RunPlaybookAsync(
        string playbookPath,
        Dictionary<string, string>? extraVars = null,
        Action<string>? onOutput = null);

    /// <summary>
    /// Verifica si Ansible está instalado.
    /// </summary>
    Task<bool> IsAnsibleInstalledAsync();

    /// <summary>
    /// Obtiene la versión de Ansible instalada.
    /// </summary>
    Task<string> GetAnsibleVersionAsync();

    /// <summary>
    /// Genera un playbook dinámicamente.
    /// </summary>
    Task<string> GeneratePlaybookAsync(PlaybookTemplate template);
}