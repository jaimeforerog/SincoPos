namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Tabla pivot: usuarios asignados a múltiples sucursales
/// </summary>
public class UsuarioSucursal
{
    public int UsuarioId { get; set; }
    public int SucursalId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
}
