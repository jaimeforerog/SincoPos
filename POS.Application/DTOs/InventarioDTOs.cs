namespace POS.Application.DTOs;

public record EntradaInventarioDto(
    Guid ProductoId,
    int SucursalId,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal PorcentajeImpuesto,
    int? TerceroId,
    string? Referencia,
    string? Observaciones
);

public record AjusteInventarioDto(
    Guid ProductoId,
    int SucursalId,
    decimal CantidadNueva,
    string? Observaciones
);

public record DevolucionProveedorDto(
    Guid ProductoId,
    int SucursalId,
    decimal Cantidad,
    int TerceroId,
    string? Referencia,
    string? Observaciones
);

public record StockDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    string? CodigoBarras,
    int SucursalId,
    string NombreSucursal,
    decimal Cantidad,
    decimal StockMinimo,
    decimal CostoPromedio,
    DateTime UltimaActualizacion
);

public record MovimientoInventarioDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    int SucursalId,
    string NombreSucursal,
    string TipoMovimiento,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal CostoTotal,
    decimal PorcentajeImpuesto,
    decimal MontoImpuesto,
    string? Referencia,
    string? Observaciones,
    int? TerceroId,
    string? NombreTercero,
    DateTime FechaMovimiento
);

public record AlertaStockDto(
    Guid ProductoId,
    string NombreProducto,
    string? CodigoBarras,
    int SucursalId,
    string NombreSucursal,
    decimal CantidadActual,
    decimal StockMinimo
);

// ─── Traslados ─────────────────────────────────────────

// REQUEST - Crear traslado
public record CrearTrasladoDto(
    int SucursalOrigenId,
    int SucursalDestinoId,
    string? Observaciones,
    List<LineaTrasladoDto> Lineas
);

public record LineaTrasladoDto(
    Guid ProductoId,
    decimal Cantidad,
    string? Observaciones
);

// REQUEST - Recibir traslado
public record RecibirTrasladoDto(
    List<LineaRecepcionDto> Lineas,
    string? Observaciones
);

public record LineaRecepcionDto(
    Guid ProductoId,
    decimal CantidadRecibida,
    string? Observaciones
);

// REQUEST - Rechazar traslado
public record RechazarTrasladoDto(
    string MotivoRechazo
);

// REQUEST - Cancelar traslado
public record CancelarTrasladoDto(
    string Motivo
);

// RESPONSE
public record TrasladoDto(
    int Id,
    string NumeroTraslado,
    int SucursalOrigenId,
    string NombreSucursalOrigen,
    int SucursalDestinoId,
    string NombreSucursalDestino,
    string Estado,
    DateTime FechaTraslado,
    DateTime? FechaEnvio,
    DateTime? FechaRecepcion,
    string? RecibidoPor,
    string? Observaciones,
    string? MotivoRechazo,
    List<DetalleTrasladoDto> Detalles
);

public record DetalleTrasladoDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    decimal CantidadSolicitada,
    decimal CantidadRecibida,
    decimal CostoUnitario,
    decimal CostoTotal,
    string? Observaciones
);

// ─── Órdenes de Compra ─────────────────────────────────────────

// REQUEST - Crear orden de compra
public record CrearOrdenCompraDto(
    int SucursalId,
    int ProveedorId,
    DateTime? FechaEntregaEsperada,
    string? Observaciones,
    List<LineaOrdenCompraDto> Lineas
);

public record LineaOrdenCompraDto(
    Guid ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario,
    int? ImpuestoId = null,                // null = auto-detectar del producto
    decimal? PorcentajeImpuesto = null     // alternativa a ImpuestoId: porcentaje directo (0-100), ej. 19 = 19%
);

// REQUEST - Recibir orden de compra
public record RecibirOrdenCompraDto(
    List<LineaRecepcionOrdenCompraDto> Lineas
);

public record LineaRecepcionOrdenCompraDto(
    Guid ProductoId,
    decimal CantidadRecibida,
    string? Observaciones
);

// REQUEST - Aprobar orden de compra
public record AprobarOrdenCompraDto(
    string? Observaciones
);

// REQUEST - Rechazar orden de compra
public record RechazarOrdenCompraDto(
    string MotivoRechazo
);

// REQUEST - Cancelar orden de compra
public record CancelarOrdenCompraDto(
    string Motivo
);

// RESPONSE
public record OrdenCompraDto(
    int Id,
    string NumeroOrden,
    int SucursalId,
    string NombreSucursal,
    int ProveedorId,
    string NombreProveedor,
    string Estado,
    DateTime FechaOrden,
    DateTime? FechaEntregaEsperada,
    DateTime? FechaAprobacion,
    DateTime? FechaRecepcion,
    string? AprobadoPor,
    string? RecibidoPor,
    string? Observaciones,
    string? MotivoRechazo,
    decimal Subtotal,
    decimal Impuestos,
    decimal Total,
    bool RequiereFacturaElectronica,
    List<DetalleOrdenCompraDto> Detalles
);

public record DetalleOrdenCompraDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    decimal CantidadSolicitada,
    decimal CantidadRecibida,
    decimal PrecioUnitario,
    decimal PorcentajeImpuesto,
    decimal MontoImpuesto,
    decimal Subtotal,
    string? NombreImpuesto,
    string? Observaciones
);
