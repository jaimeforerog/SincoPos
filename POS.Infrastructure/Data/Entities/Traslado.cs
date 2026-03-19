namespace POS.Infrastructure.Data.Entities;

public enum EstadoTraslado
{
    Pendiente = 0,
    EnTransito = 1,
    Recibido = 2,
    Rechazado = 3,
    Cancelado = 4
}

/// <summary>
/// Traslado de inventario entre sucursales
/// </summary>
public class Traslado : EntidadAuditable
{
    public string NumeroTraslado { get; set; } = string.Empty;      // "TRAS-000001"
    public int? EmpresaId { get; set; }
    public int SucursalOrigenId { get; set; }
    public int SucursalDestinoId { get; set; }
    public EstadoTraslado Estado { get; set; } = EstadoTraslado.Pendiente;
    public DateTime FechaTraslado { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEnvio { get; set; }                       // Cuando cambia a EnTransito
    public DateTime? FechaRecepcion { get; set; }                   // Cuando cambia a Recibido
    public int? RecibidoPorUsuarioId { get; set; }
    public string? Observaciones { get; set; }
    public string? MotivoRechazo { get; set; }

    // Navegación
    public Sucursal SucursalOrigen { get; set; } = null!;
    public Sucursal SucursalDestino { get; set; } = null!;
    public ICollection<DetalleTraslado> Detalles { get; set; } = new List<DetalleTraslado>();
}

/// <summary>
/// Detalle de productos trasladados
/// </summary>
public class DetalleTraslado
{
    public int Id { get; set; }
    public int TrasladoId { get; set; }
    public Guid ProductoId { get; set; }
    public string NombreProducto { get; set; } = string.Empty;      // Snapshot
    public decimal CantidadSolicitada { get; set; }
    public decimal CantidadRecibida { get; set; }
    public decimal CostoUnitario { get; set; }                      // Del origen
    public decimal CostoTotal { get; set; }
    public string? Observaciones { get; set; }
    // Trazabilidad de lote (snapshot del lote consumido en la sucursal origen)
    public int? LoteInventarioId { get; set; }
    public string? NumeroLote { get; set; }
    public DateOnly? FechaVencimiento { get; set; }

    // Navegación
    public Traslado Traslado { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
