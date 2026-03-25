namespace POS.Infrastructure.Configuration;

/// <summary>
/// Configuracion para el Admin REST API de Keycloak.
/// </summary>
public class KeycloakAdminOptions
{
    public const string SectionName = "KeycloakAdmin";

    /// <summary>URL base de Keycloak (ej: http://localhost:8180).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8180";

    /// <summary>Realm donde se gestionan los usuarios (ej: sincopos).</summary>
    public string Realm { get; set; } = "sincopos";

    /// <summary>Usuario administrador del realm master.</summary>
    public string AdminUsername { get; set; } = "admin";

    /// <summary>Contraseña del administrador del realm master.</summary>
    public string AdminPassword { get; set; } = "admin";
}
