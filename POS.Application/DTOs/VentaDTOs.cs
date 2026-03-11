namespace POS.Application.DTOs;

// ─── Ventas ─────────────────────────────────────────

public record CrearVentaDto(
    int SucursalId,
    int CajaId,
    int? ClienteId,
    int MetodoPago,       // 0=Efectivo, 1=Tarjeta, 2=Transferencia, 3=Mixto
    decimal? MontoPagado,
    string? Observaciones,
    List<LineaVentaDto> Lineas
);

public record LineaVentaDto(
    Guid ProductoId,
    decimal Cantidad,
    decimal? PrecioUnitario,  // null = usa precio resuelto
    decimal Descuento = 0
);

public record VentaDto(
    int Id,
    string NumeroVenta,
    int SucursalId,
    string NombreSucursal,
    int CajaId,
    string NombreCaja,
    int? ClienteId,
    string? NombreCliente,
    decimal Subtotal,
    decimal Descuento,
    decimal Impuestos,
    decimal Total,
    string Estado,
    string MetodoPago,
    decimal? MontoPagado,
    decimal? Cambio,
    string? Observaciones,
    DateTime FechaVenta,
    List<DetalleVentaDto> Detalles,
    bool RequiereFacturaElectronica = false
);

public record DetalleVentaDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    string? NumeroLote,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal CostoUnitario,
    decimal Descuento,
    decimal PorcentajeImpuesto,
    decimal MontoImpuesto,
    decimal Subtotal,
    decimal MargenGanancia  // (Precio - Costo) / Precio * 100
);

public record ResumenVentaDto(
    int TotalVentas,
    decimal MontoTotal,
    decimal CostoTotal,
    decimal GananciaTotal,
    decimal MargenPromedio
);

// ─── Precios ─────────────────────────────────────────

public record PrecioSucursalDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    int SucursalId,
    string NombreSucursal,
    decimal PrecioVenta,
    decimal? PrecioMinimo,
    DateTime? FechaModificacion
);

public record CrearPrecioSucursalDto(
    Guid ProductoId,
    int SucursalId,
    decimal PrecioVenta,
    decimal? PrecioMinimo,
    string? OrigenDato = null  // "Manual", "Migrado", "Importado", etc.
);

public record PrecioResueltoDto(
    decimal PrecioVenta,
    decimal? PrecioMinimo,
    string Origen,  // "Sucursal", "Producto", "Margen"
    string? OrigenDato = null  // "Manual", "Migrado", etc. (si viene de Sucursal)
);

public record PrecioResueltoLoteItemDto(
    Guid ProductoId,
    decimal PrecioVenta,
    decimal? PrecioMinimo,
    string Origen
);

// ─── Devoluciones ─────────────────────────────────────────

public record CrearDevolucionParcialDto(
    string Motivo,
    List<LineaDevolucionDto> Lineas
);

public record LineaDevolucionDto(
    Guid ProductoId,
    decimal Cantidad
);

/// <summary>
/// DTO para el endpoint POST /Ventas/devoluciones. Usa detalleVentaId (int) en lugar de productoId.
/// </summary>
public record CrearDevolucionDto(
    int VentaId,
    string Motivo,
    List<LineaDevolucionPorDetalleDto> Lineas
);

public record LineaDevolucionPorDetalleDto(
    int DetalleVentaId,
    decimal CantidadDevuelta
);

public record DevolucionVentaDto(
    int Id,
    int VentaId,
    string NumeroVenta,
    string NumeroDevolucion,
    string Motivo,
    decimal TotalDevuelto,
    DateTime FechaDevolucion,
    string? AutorizadoPor,
    List<DetalleDevolucionDto> Detalles
);

public record DetalleDevolucionDto(
    int Id,
    Guid ProductoId,
    string NombreProducto,
    decimal CantidadDevuelta,
    decimal PrecioUnitario,
    decimal CostoUnitario,
    decimal SubtotalDevuelto
);
