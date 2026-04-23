namespace POS.Application.DTOs;

public record EntradaInventarioDto(
    Guid ProductoId,
    int SucursalId,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal PorcentajeImpuesto,
    int? TerceroId,
    string? Referencia,
    string? Observaciones,
    string? NumeroLote = null,
    DateOnly? FechaVencimiento = null,
    DateTime? FechaMovimiento = null  // null = usar DateTime.UtcNow en el servidor
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

// ─── Lotes ─────────────────────────────────────────────

public record LoteDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    string? CodigoBarras,
    int SucursalId,
    string NombreSucursal,
    string? NumeroLote,
    DateOnly? FechaVencimiento,
    int? OrdenCompraId,
    decimal CantidadInicial,
    decimal CantidadDisponible,
    decimal CostoUnitario,
    string? Referencia,
    DateTime FechaEntrada
);

public record AlertaLoteDto(
    int LoteId,
    Guid ProductoId,
    string NombreProducto,
    string? CodigoBarras,
    int SucursalId,
    string NombreSucursal,
    string? NumeroLote,
    DateOnly FechaVencimiento,
    int DiasParaVencer,
    decimal CantidadDisponible,
    DateTime FechaEntrada
);

public record ActualizarLoteDto(
    string? NumeroLote,
    DateOnly? FechaVencimiento
);

// ─── Trazabilidad de Lote ────────────────────────────────────────────────────

public record TrazabilidadLoteDto(
    LoteDto Lote,
    TrazabilidadEntradaDto? Entrada,
    List<TrazabilidadMovimientoDto> Movimientos
);

public record TrazabilidadEntradaDto(
    string Tipo,           // "OrdenCompra" | "EntradaManual" | "Traslado"
    string Referencia,
    DateTime Fecha,
    string? Proveedor,
    decimal CantidadInicial,
    decimal CostoUnitario
);

public record TrazabilidadMovimientoDto(
    string Tipo,           // "Venta" | "Devolucion" | "Traslado"
    string Referencia,
    DateTime Fecha,
    decimal Cantidad,
    string? Detalle,       // cliente, destino, motivo, etc.
    decimal Saldo          // saldo acumulado del lote después de este movimiento
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
    string? Observaciones,
    string? NumeroLote = null,
    string? FechaVencimiento = null
);

// ─── Órdenes de Compra ─────────────────────────────────────────

// REQUEST - Crear orden de compra
public record CrearOrdenCompraDto(
    int SucursalId,
    int ProveedorId,
    DateTime? FechaEntregaEsperada,
    string FormaPago = "Contado",
    int DiasPlazo = 0,
    string? Observaciones = null,
    List<LineaOrdenCompraDto> Lineas = null!,
    DateTime? FechaOrden = null  // null = usar DateTime.UtcNow en el servidor
);

public record LineaOrdenCompraDto(
    Guid ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario,
    int? ImpuestoId = null,                // null = auto-detectar del producto
    decimal? PorcentajeImpuesto = null     // alternativa a ImpuestoId: porcentaje directo (0-100), ej. 19 = 19%
);

// REQUEST - Actualizar orden de compra pendiente
public record ActualizarOrdenCompraDto(
    DateTime? FechaEntregaEsperada = null,
    string? Observaciones = null,
    string? FormaPago = null,
    int? DiasPlazo = null,
    List<LineaOrdenCompraDto>? Lineas = null   // null = no modificar líneas
);

// REQUEST - Recibir orden de compra
public record RecibirOrdenCompraDto(
    List<LineaRecepcionOrdenCompraDto> Lineas,
    DateTime? FechaRecepcion = null  // null = usar DateTime.UtcNow en el servidor
);

public record LineaRecepcionOrdenCompraDto(
    Guid ProductoId,
    decimal CantidadRecibida,
    string? Observaciones,
    string? NumeroLote = null,
    DateOnly? FechaVencimiento = null
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
    string FormaPago,
    int DiasPlazo,
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
    bool SincronizadoErp,
    DateTime? FechaSincronizacionErp,
    string? ErpReferencia,
    string? ErrorSincronizacion,
    List<DetalleOrdenCompraDto> Detalles
);

// ─── Devoluciones de Orden de Compra ──────────────────────────

public record CrearDevolucionCompraDto(
    string Motivo,
    List<LineaDevolucionCompraDto> Lineas
);

public record LineaDevolucionCompraDto(
    Guid ProductoId,
    decimal Cantidad
);

public record DevolucionCompraDto(
    int Id,
    int OrdenCompraId,
    string NumeroOrden,
    string NumeroDevolucion,
    string Motivo,
    decimal Total,
    DateTime FechaDevolucion,
    string? AutorizadoPor,
    List<DetalleDevolucionCompraDto> Detalles
);

public record DetalleDevolucionCompraDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    decimal CantidadDevuelta,
    decimal PrecioUnitario,
    decimal Subtotal
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
    string? Observaciones,
    bool ManejaLotes = false,
    int? DiasVidaUtil = null
);

// ─── Reporte de Lotes por Vencimiento ─────────────────────────────────────────

public record ReporteLotesQueryDto(
    int? SucursalId = null,
    Guid? ProductoId = null,
    bool SoloConStock = true,
    string? EstadoVencimiento = null,          // "Vencido" | "Critico" | "Proximo" | "Vigente" | "SinFecha"
    DateOnly? FechaVencimientoDesde = null,
    DateOnly? FechaVencimientoHasta = null,
    int Page = 1,
    int PageSize = 100
);

public record LoteReporteItemDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    string? CodigoBarras,
    int SucursalId,
    string NombreSucursal,
    string? NumeroLote,
    DateOnly? FechaVencimiento,
    int? DiasParaVencer,
    decimal CantidadDisponible,
    decimal CostoUnitario,
    decimal ValorTotal,
    string? Referencia,
    DateTime FechaEntrada,
    string EstadoVencimiento   // "Vencido" | "Critico" | "Proximo" | "Vigente" | "SinFecha"
);

public record ReporteLotesDto(
    int TotalLotes,           // total sin filtro de estado (para KPIs)
    decimal TotalUnidades,
    decimal ValorTotalInventario,
    int LotesVencidos,
    int LotesCriticos,
    int LotesProximos,
    int LotesVigentes,
    int LotesSinFecha,
    int TotalItems,           // total con filtro de estado aplicado (para paginación)
    int TotalPaginas,
    int PaginaActual,
    List<LoteReporteItemDto> Items
);
