namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Usuario del sistema POS sincronizado con el proveedor de identidad externo (WorkOS)
/// </summary>
public class Usuario : EntidadAuditable
{
    /// <summary>
    /// ID externo del proveedor de identidad (WorkOS / Entra ID)
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

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

    /// <summary>
    /// Obtiene el nivel jerárquico de un rol (mayor = más privilegios).
    /// admin=4, supervisor=3, cajero=2, vendedor=1
    /// </summary>
    public static int GetNivelJerarquico(string rol) => rol.ToLower() switch
    {
        Admin => 4,
        Supervisor => 3,
        Cajero => 2,
        Vendedor => 1,
        _ => 0
    };

    /// <summary>
    /// Verifica si un rol creador puede asignar el rol destino.
    /// Un usuario no puede asignar un rol de nivel igual o superior al suyo (excepto admin).
    /// </summary>
    public static bool PuedeAsignarRol(string rolCreador, string rolDestino)
    {
        var nivelCreador = GetNivelJerarquico(rolCreador);
        var nivelDestino = GetNivelJerarquico(rolDestino);

        // Admin puede asignar cualquier rol
        if (rolCreador.ToLower() == Admin)
            return true;

        // Los demás solo pueden asignar roles de nivel inferior
        return nivelCreador > nivelDestino;
    }
}
