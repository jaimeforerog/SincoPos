namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Stock actual por producto y sucursal (tabla de lectura rapida).
/// Se actualiza con cada movimiento de inventario.
/// </summary>
public class Stock : EntidadAuditable
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal StockMinimo { get; set; }
    public decimal CostoPromedio { get; set; }
    public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;

    // Navegacion
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
}

/// <summary>
/// Registro de cada movimiento de inventario (trazabilidad completa).
/// </summary>
public class MovimientoInventario
{
    public int Id { get; set; }
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PorcentajeImpuesto { get; set; } // IVA asociado (ej: 0.19)
    public decimal MontoImpuesto { get; set; }      // IVA total pagado en este movimiento
    public string? Referencia { get; set; }        // # factura, # ajuste, etc.
    public string? Observaciones { get; set; }
    public int? TerceroId { get; set; }             // Proveedor (en compras)
    public int? SucursalDestinoId { get; set; }     // Para transferencias
    public int UsuarioId { get; set; }
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;

    // Navegacion
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Tercero? Tercero { get; set; }
    public Sucursal? SucursalDestino { get; set; }
}

public enum TipoMovimiento
{
    EntradaCompra = 0,
    SalidaVenta = 1,
    AjustePositivo = 2,
    AjusteNegativo = 3,
    TransferenciaSalida = 4,
    TransferenciaEntrada = 5,
    DevolucionProveedor = 6
}
