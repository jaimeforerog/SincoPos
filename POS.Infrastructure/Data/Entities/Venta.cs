namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Venta realizada en una sucursal/caja.
/// </summary>
public class Venta : EntidadAuditable
{
    public string NumeroVenta { get; set; } = string.Empty; // "V-0001"
    public int SucursalId { get; set; }
    public int CajaId { get; set; }
    public int? ClienteId { get; set; }     // Tercero (opcional)
    public int? UsuarioId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Impuestos { get; set; }
    public decimal Total { get; set; }
    public EstadoVenta Estado { get; set; } = EstadoVenta.Completada;
    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;
    public decimal? MontoPagado { get; set; }
    public decimal? Cambio { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaVenta { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Flag activado por el TaxEngine cuando Total > 5 UVT.
    /// Indica que la transacción requiere factura electrónica según DIAN.
    /// </summary>
    public bool RequiereFacturaElectronica { get; set; } = false;

    // Navegacion
    public Sucursal Sucursal { get; set; } = null!;
    public Caja Caja { get; set; } = null!;
    public Tercero? Cliente { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
}

/// <summary>
/// Linea de detalle de una venta.
/// </summary>
public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public Guid ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty; // Snapshot al momento
    public int? LoteInventarioId { get; set; }     // Lote consumido (si aplica)
    public string? NumeroLote { get; set; }        // Snapshot del lote para trazabilidad
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }     // Precio al que se vendio
    public decimal CostoUnitario { get; set; }      // Costo al momento (para margen)
    public decimal Descuento { get; set; }          // Descuento en esta linea
    public decimal PorcentajeImpuesto { get; set; } // Porcentaje de IVA en el momento (ej: 0.19)
    public decimal MontoImpuesto { get; set; }      // Monto total de IVA para esta linea
    public decimal Subtotal { get; set; }           // (Precio * Cantidad) - Descuento

    // Navegacion
    public Venta Venta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

public enum EstadoVenta
{
    Pendiente = 0,
    Completada = 1,
    Anulada = 2
}

public enum MetodoPago
{
    Efectivo = 0,
    Tarjeta = 1,
    Transferencia = 2,
    Mixto = 3
}

/// <summary>
/// Registro de devolución parcial de productos de una venta.
/// </summary>
public class DevolucionVenta : EntidadAuditable
{
    public int VentaId { get; set; }
    public string NumeroDevolucion { get; set; } = string.Empty; // "DEV-000001"
    public string Motivo { get; set; } = string.Empty;
    public decimal TotalDevuelto { get; set; }
    public DateTime FechaDevolucion { get; set; } = DateTime.UtcNow;
    public int? AutorizadoPorUsuarioId { get; set; }

    // Navigation
    public Venta Venta { get; set; } = null!;
    public ICollection<DetalleDevolucion> Detalles { get; set; } = new List<DetalleDevolucion>();
}

/// <summary>
/// Detalle de productos devueltos en una devolución parcial.
/// </summary>
public class DetalleDevolucion
{
    public int Id { get; set; }
    public int DevolucionVentaId { get; set; }
    public Guid ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal CantidadDevuelta { get; set; }
    public decimal PrecioUnitario { get; set; }     // Del detalle original
    public decimal CostoUnitario { get; set; }      // Del detalle original
    public decimal SubtotalDevuelto { get; set; }

    // Navigation
    public DevolucionVenta DevolucionVenta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
