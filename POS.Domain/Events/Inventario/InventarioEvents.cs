namespace POS.Domain.Events.Inventario;

/// <summary>
/// Clase base para todos los eventos del dominio (trazabilidad).
/// </summary>
public abstract class BaseEvent
{
    public int? UsuarioId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
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
