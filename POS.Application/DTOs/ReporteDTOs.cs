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
