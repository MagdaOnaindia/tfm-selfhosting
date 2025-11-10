using TFM.Dashboard.Models;

namespace TFM.Dashboard.Interfaces;

/// <summary>
/// Proporciona templates predefinidos para aplicaciones.
/// 
/// SOLID:
/// - S: Solo gestión de templates
/// - O: Extensible con nuevos templates
/// - I: Interface específica para templates
/// </summary>
public interface IAppTemplateService
{
    /// <summary>
    /// Obtiene todos los templates disponibles.
    /// </summary>
    List<AppTemplate> GetTemplates();

    /// <summary>
    /// Obtiene un template por ID.
    /// </summary>
    AppTemplate? GetTemplate(string id);

    /// <summary>
    /// Obtiene templates por categoría.
    /// </summary>
    List<AppTemplate> GetTemplatesByCategory(string category);

    /// <summary>
    /// Obtiene todas las categorías disponibles.
    /// </summary>
    List<string> GetCategories();

    /// <summary>
    /// Genera un docker-compose.yml a partir de un template.
    /// </summary>
    Task<string> GenerateDockerComposeAsync(
        AppTemplate template,
        Dictionary<string, string> variables);

    /// <summary>
    /// Valida que todas las variables requeridas estén presentes.
    /// </summary>
    bool ValidateVariables(AppTemplate template, Dictionary<string, string> variables);
}