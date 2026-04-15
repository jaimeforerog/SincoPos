namespace POS.Infrastructure.Data.Entities;

public enum EstadoOrdenCompra
{
    Borrador = 0,
    Pendiente = 1,
    Aprobada = 2,
    RecibidaParcial = 3,
    RecibidaCompleta = 4,
    Rechazada = 5,
    Cancelada = 6
}

/// <summary>
/// Orden de compra a proveedores
/// </summary>
public class OrdenCompra : EntidadAuditable
{
    public string NumeroOrden { get; set; } = string.Empty;      // "OC-000001"
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int ProveedorId { get; set; }                         // FK a Terceros
    public EstadoOrdenCompra Estado { get; set; } = EstadoOrdenCompra.Pendiente;
    public DateTime FechaOrden { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEntregaEsperada { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public int? AprobadoPorUsuarioId { get; set; }
    public int? RecibidoPorUsuarioId { get; set; }
    public string? Observaciones { get; set; }
    public string? MotivoRechazo { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Impuestos { get; set; }
    public decimal Total { get; set; }
    public bool RequiereFacturaElectronica { get; set; } = false;
    
    // Forma de Pago y Tesorería
    public string FormaPago { get; set; } = "Contado"; // "Contado" o "Credito"
    public int DiasPlazo { get; set; } = 0;            // Ej. 15, 30, 45, 60...

    // Trazabilidad Integración ERP
    public bool SincronizadoErp { get; set; } = false;
    public DateTime? FechaSincronizacionErp { get; set; }
    public string? ErpReferencia { get; set; } 
    public string? ErrorSincronizacion { get; set; }

    // Navegación
    public Sucursal Sucursal { get; set; } = null!;
    public Tercero Proveedor { get; set; } = null!;
    public ICollection<DetalleOrdenCompra> Detalles { get; set; } = new List<DetalleOrdenCompra>();
}

/// <summary>
/// Detalle de productos en la orden de compra
/// </summary>
public class DetalleOrdenCompra
{
    public int Id { get; set; }
    public int OrdenCompraId { get; set; }
    public Guid ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;   // Snapshot
    public decimal CantidadSolicitada { get; set; }
    public decimal CantidadRecibida { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeImpuesto { get; set; }
    public decimal MontoImpuesto { get; set; }
    public decimal Subtotal { get; set; }
    public string? NombreImpuesto { get; set; }   // Snapshot: "IVA 19%", "INC 8%"
    public string? Observaciones { get; set; }

    // Navegación
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

/// <summary>
/// Devolución parcial o total de mercancía a proveedor, asociada a una orden de compra recibida
/// </summary>
public class DevolucionCompra
{
    public int Id { get; set; }
    public int OrdenCompraId { get; set; }
    public string NumeroDevolucion { get; set; } = string.Empty;  // "DC-000001"
    public string Motivo { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime FechaDevolucion { get; set; } = DateTime.UtcNow;
    public int? AutorizadoPorUsuarioId { get; set; }

    // Navegación
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public ICollection<DetalleDevolucionCompra> Detalles { get; set; } = new List<DetalleDevolucionCompra>();
}

/// <summary>
/// Línea de producto devuelto en una devolución de compra
/// </summary>
public class DetalleDevolucionCompra
{
    public int Id { get; set; }
    public int DevolucionCompraId { get; set; }
    public Guid ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal CantidadDevuelta { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    // Navegación
    public DevolucionCompra DevolucionCompra { get; set; } = null!;
}
