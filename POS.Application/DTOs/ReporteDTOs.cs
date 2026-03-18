namespace POS.Application.DTOs;

// ─── Reporte de Ventas ─────────────────────────────────────────

public record ReporteVentasDto(
    decimal TotalVentas,
    int CantidadVentas,
    decimal TicketPromedio,
    decimal CostoTotal,
    decimal UtilidadTotal,
    decimal MargenPromedio,
    List<VentaPorMetodoPagoDto> VentasPorMetodoPago,
    List<VentaPorDiaDto> VentasPorDia
);

public record VentaPorMetodoPagoDto(
    string Metodo,
    decimal Total,
    int Cantidad
);

public record VentaPorDiaDto(
    string Fecha,
    decimal Total,
    int Cantidad,
    decimal CostoTotal,
    decimal Utilidad
);

// ─── Reporte de Inventario Valorizado ─────────────────────────────────────────

public record ReporteInventarioValorizadoDto(
    decimal TotalCosto,
    decimal TotalVenta,
    decimal UtilidadPotencial,
    int TotalProductos,
    decimal TotalUnidades,
    List<ProductoValorizadoDto> Productos
);

public record ProductoValorizadoDto(
    Guid ProductoId,
    string CodigoBarras,
    string Nombre,
    string? Categoria,
    int SucursalId,
    string NombreSucursal,
    decimal Cantidad,
    decimal CostoPromedio,
    decimal CostoTotal,
    decimal PrecioVenta,
    decimal ValorVenta,
    decimal UtilidadPotencial,
    decimal MargenPorcentaje
);

// ─── Reporte de Caja ─────────────────────────────────────────

public record ReporteCajaDto(
    int CajaId,
    string NombreCaja,
    int SucursalId,
    string NombreSucursal,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    decimal MontoApertura,
    decimal TotalVentasEfectivo,
    decimal TotalVentasTarjeta,
    decimal TotalVentasTransferencia,
    decimal TotalVentas,
    decimal? MontoCierre,
    decimal? DiferenciaEsperado,
    decimal? DiferenciaReal,
    List<VentaCajaDto> Ventas
);

public record VentaCajaDto(
    int VentaId,
    string NumeroVenta,
    DateTime FechaVenta,
    string MetodoPago,
    decimal Total,
    decimal CostoTotal,
    decimal Utilidad,
    string? Cliente
);

// ─── Parámetros de consulta ─────────────────────────────────────────

public record ReporteVentasQueryDto(
    DateTime FechaDesde,
    DateTime FechaHasta,
    int? SucursalId = null,
    int? MetodoPago = null
);

public record ReporteInventarioQueryDto(
    int? SucursalId = null,
    int? CategoriaId = null,
    bool SoloConStock = false
);

public record ReporteCajaQueryDto(
    int CajaId,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null
);

// ─── Dashboard ─────────────────────────────────────────

public record DashboardDto(
    MetricasDelDiaDto MetricasDelDia,
    List<VentaPorHoraDto> VentasPorHora,
    List<TopProductoDto> TopProductos,
    List<AlertaStockDto> AlertasStock
);

public record MetricasDelDiaDto(
    decimal VentasTotales,
    decimal VentasAyer,
    decimal PorcentajeCambio,
    int CantidadVentas,
    int ProductosVendidos,
    int ClientesAtendidos,
    decimal TicketPromedio,
    decimal UtilidadDelDia,
    decimal MargenPromedio
);

public record VentaPorHoraDto(
    int Hora,
    decimal Total,
    int Cantidad
);

public record TopProductoDto(
    Guid ProductoId,
    string CodigoBarras,
    string Nombre,
    string? Categoria,
    int CantidadVendida,
    decimal TotalVentas,
    decimal Utilidad,
    decimal MargenPorcentaje
);

// ─── Capa 14 — Radar de Negocio ────────────────────────────────────────────

/// <summary>
/// Respuesta del endpoint GET /api/v1/radar/sucursal/{id}.
/// Combina métricas del día (EF Core) con velocidad de productos (Marten)
/// y riesgos de ruptura de stock para el Radar de Negocio.
/// </summary>
public record RadarNegocioDto(
    MetricasDelDiaDto   MetricasHoy,
    List<VentaPorHoraDto> VentasPorHora,
    List<AlertaStockDto>  RiesgosStock
);

// ─── Kardex de Inventario ─────────────────────────────────────────

public record ReporteKardexDto(
    Guid ProductoId,
    string CodigoBarras,
    string Nombre,
    int SucursalId,
    string NombreSucursal,
    DateTime FechaDesde,
    DateTime FechaHasta,
    decimal SaldoInicial,
    decimal SaldoFinal,
    decimal CostoPromedioVigente,
    List<KardexMovimientoDto> Movimientos
);

public record KardexMovimientoDto(
    DateTime Fecha,
    string TipoMovimiento,
    string Referencia,
    string? Observaciones,
    decimal Entrada,
    decimal Salida,
    decimal SaldoAcumulado,
    decimal CostoUnitario,
    decimal CostoTotalMovimiento
);

public record ReporteKardexQueryDto(
    Guid ProductoId,
    int SucursalId,
    DateTime FechaDesde,
    DateTime FechaHasta
);
