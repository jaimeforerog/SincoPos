namespace POS.Application.Services;

/// <summary>
/// Helpers para resolver el rol principal y los permisos asociados a partir
/// de los claims del IdP. Centraliza lo que antes estaba duplicado en controllers.
/// </summary>
public static class RolPermisos
{
    public static string DeterminarRolPrincipal(IReadOnlyList<string> roles)
    {
        if (roles.Any(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase)))
            return "admin";
        if (roles.Any(r => r.Equals("supervisor", StringComparison.OrdinalIgnoreCase)))
            return "supervisor";
        if (roles.Any(r => r.Equals("cajero", StringComparison.OrdinalIgnoreCase)))
            return "cajero";
        return "vendedor";
    }

    public static IReadOnlyList<string> ObtenerPermisosPorRol(string rol) => rol.ToLower() switch
    {
        "admin" => new[]
        {
            "usuarios.listar",
            "usuarios.ver",
            "usuarios.activar",
            "usuarios.estadisticas",
            "sucursales.crear",
            "sucursales.modificar",
            "sucursales.eliminar",
            "impuestos.crear",
            "impuestos.modificar",
            "impuestos.eliminar",
            "categorias.crear",
            "categorias.modificar",
            "categorias.eliminar",
            "productos.crear",
            "productos.modificar",
            "productos.eliminar",
            "inventario.ajustar",
            "precios.modificar",
            "ventas.crear",
            "ventas.anular",
            "reportes.ver"
        },
        "supervisor" => new[]
        {
            "usuarios.listar",
            "categorias.crear",
            "categorias.modificar",
            "productos.crear",
            "productos.modificar",
            "inventario.ajustar",
            "precios.modificar",
            "ventas.crear",
            "ventas.anular",
            "reportes.ver"
        },
        "cajero" => new[]
        {
            "productos.ver",
            "ventas.crear",
            "cajas.abrir",
            "cajas.cerrar",
            "terceros.crear",
            "terceros.modificar"
        },
        "vendedor" => new[]
        {
            "productos.ver",
            "categorias.ver",
            "terceros.ver",
            "inventario.ver"
        },
        _ => Array.Empty<string>()
    };
}
