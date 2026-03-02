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
