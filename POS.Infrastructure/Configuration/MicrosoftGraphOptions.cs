namespace POS.Infrastructure.Configuration;

/// <summary>
/// Configuracion para Microsoft Graph API (Entra ID user management).
/// </summary>
public class MicrosoftGraphOptions
{
    public const string SectionName = "MicrosoftGraph";

    /// <summary>Tenant ID del directorio organizacional de Entra ID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Client ID de la App Registration con permisos Graph.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret de la App Registration.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Object ID del Service Principal (Enterprise Application) para asignar App Roles.</summary>
    public string ServicePrincipalId { get; set; } = string.Empty;

    /// <summary>Dominio del tenant (e.g., sincopos.onmicrosoft.com). Usado para generar UPN.</summary>
    public string TenantDomain { get; set; } = string.Empty;

    /// <summary>
    /// Mapeo de roles SincoPos → App Role IDs de Entra ID.
    /// Ej: { "admin": "guid-del-app-role", "supervisor": "guid", ... }
    /// </summary>
    public Dictionary<string, string> AppRoleIds { get; set; } = new();
}
