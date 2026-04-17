namespace POS.Domain.Events.Inventario;

/// <summary>
/// Clase base para todos los eventos del dominio (trazabilidad).
/// </summary>
public abstract class BaseEvent
{
    public int? UsuarioId { get; set; }

    /// <summary>
    /// Fecha efectiva del movimiento en UTC.
    /// Puede ser retroactiva cuando el usuario registra un movimiento con fecha pasada.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha real del sistema (UTC) en que se creó y guardó el evento — solo para auditoría.
    /// A diferencia de Timestamp, este campo nunca se sobreescribe: siempre refleja
    /// el momento exacto en que el servidor procesó la operación.
    /// Null en eventos anteriores a este campo (compatibilidad hacia atrás).
    /// </summary>
    public DateTime? FechaRegistro { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Compra de mercancia a un proveedor
/// </summary>
public class EntradaCompraRegistrada : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PorcentajeImpuesto { get; set; }
    public decimal MontoImpuesto { get; set; }
    public int? TerceroId { get; set; }
    public string? NombreTercero { get; set; }
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Devolucion de mercancia a un proveedor
/// </summary>
public class DevolucionProveedorRegistrada : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public int TerceroId { get; set; }
    public string? NombreTercero { get; set; }
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Ajuste manual por conteo fisico
/// </summary>
public class AjusteInventarioRegistrado : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal CantidadAnterior { get; set; }
    public decimal CantidadNueva { get; set; }
    public decimal Diferencia { get; set; }
    public bool EsPositivo { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Salida de inventario por una venta
/// </summary>
public class SalidaVentaRegistrada : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal PorcentajeImpuesto { get; set; }
    public decimal MontoImpuesto { get; set; }
    public string? ReferenciaVenta { get; set; }
}

/// <summary>
/// Stock minimo actualizado para alertas
/// </summary>
public class StockMinimoActualizado : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal StockMinimoAnterior { get; set; }
    public decimal StockMinimoNuevo { get; set; }
}

/// <summary>
/// Registra la salida de productos de la sucursal origen por traslado
/// </summary>
public class TrasladoSalidaRegistrado : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalOrigenId { get; set; }
    public int SucursalDestinoId { get; set; }
    public string NumeroTraslado { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>
/// Registra la entrada de productos en la sucursal destino por traslado
/// </summary>
public class TrasladoEntradaRegistrado : BaseEvent
{
    public Guid ProductoId { get; set; }
    public int SucursalOrigenId { get; set; }
    public int SucursalDestinoId { get; set; }
    public string NumeroTraslado { get; set; } = string.Empty;
    public decimal CantidadRecibida { get; set; }
    public decimal CostoUnitario { get; set; }  // Del origen
    public decimal CostoTotal { get; set; }
    public string? Observaciones { get; set; }
}
