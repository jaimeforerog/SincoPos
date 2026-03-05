namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Usuario del sistema POS sincronizado con Keycloak
/// </summary>
public class Usuario : EntidadAuditable
{
    /// <summary>
    /// Subject ID de Keycloak (UUID del usuario en Keycloak)
    /// </summary>
    public string KeycloakId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }

    /// <summary>
    /// Rol principal: admin, supervisor, cajero, vendedor
    /// </summary>
    public string Rol { get; set; } = string.Empty;

    /// <summary>
    /// Sucursal por defecto del usuario
    /// </summary>
    public int? SucursalDefaultId { get; set; }

    public DateTime? UltimoAcceso { get; set; }

    // Navegación
    public Sucursal? SucursalDefault { get; set; }
    public ICollection<UsuarioSucursal> Sucursales { get; set; } = new List<UsuarioSucursal>();
}

/// <summary>
/// Roles del sistema
/// </summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string Supervisor = "supervisor";
    public const string Cajero = "cajero";
    public const string Vendedor = "vendedor";

    public static readonly string[] TodosLosRoles =
    {
        Admin,
        Supervisor,
        Cajero,
        Vendedor
    };

    /// <summary>
    /// Verifica si un rol es válido
    /// </summary>
    public static bool EsValido(string rol) => TodosLosRoles.Contains(rol.ToLower());
}
