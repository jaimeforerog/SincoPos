namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Precio de venta especifico por producto y sucursal.
/// Override del precio base del producto.
/// </summary>
public class PrecioSucursal : EntidadAuditable
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal? PrecioMinimo { get; set; }  // Piso para descuentos en caja
    public string? OrigenDato { get; set; }  // "Manual", "Migrado", "Importado", etc.

    // Navegacion
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
}
