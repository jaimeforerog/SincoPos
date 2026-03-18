namespace POS.Application.DTOs;

/// <summary>
/// Capa 4 — Dependencias inteligentes.
/// Historial acumulado de compras de un cliente, alimentado automáticamente
/// por ClienteHistorialProjection cada vez que se registra una venta.
/// </summary>
public record ClienteHistorialDto(
    int     ClienteId,
    int     TotalCompras,
    decimal TotalGastado,
    decimal GastoPromedio,
    DateTime? PrimeraVisita,
    DateTime? UltimaVisita,
    List<ProductoFrecuenteDto> TopProductos,
    Dictionary<string, int>    VisitasPorDiaSemana,
    Dictionary<string, int>    VisitasPorHora
);

public record ProductoFrecuenteDto(
    Guid   ProductoId,
    string NombreProducto,
    int    CantidadTotal
);
