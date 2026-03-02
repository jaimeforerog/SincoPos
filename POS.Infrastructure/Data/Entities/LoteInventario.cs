namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Lote de inventario: cada compra/entrada crea un lote con su costo.
/// Necesario para FIFO y LIFO (se consume de lotes especificos).
/// </summary>
public class LoteInventario : EntidadAuditable
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal CantidadInicial { get; set; }
    public decimal CantidadDisponible { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal PorcentajeImpuesto { get; set; }       // Ej 0.19
    public decimal MontoImpuestoUnitario { get; set; }    // IVA pagado por unidad
    public string? Referencia { get; set; }       // # factura de compra
    public int? TerceroId { get; set; }            // Proveedor
    public DateTime FechaEntrada { get; set; } = DateTime.UtcNow;

    // Navegacion
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Tercero? Tercero { get; set; }
}
