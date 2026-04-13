namespace POS.Infrastructure.Configuration;

/// <summary>
/// Configuración para WorkOS User Management API.
/// </summary>
public class WorkOsOptions
{
    public const string SectionName = "WorkOs";

    /// <summary>API Key de WorkOS (sk_live_... o sk_test_...).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Client ID de la aplicación WorkOS.</summary>
    public string ClientId { get; set; } = string.Empty;
}
