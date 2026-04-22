using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Un lote consumido al procesar una venta. Incluye cantidad y costo para trazabilidad exacta.
/// </summary>
public record ConsumoLoteItem(int LoteId, string? NumeroLote, decimal Cantidad, decimal CostoUnitario);

/// <summary>
/// Encapsula la lógica de consumo de inventario durante una venta.
/// Extraído de VentaService para cumplir el principio de responsabilidad única.
/// </summary>
public interface IVentaCosteoService
{
    /// <summary>
    /// Consume stock de un producto según el método de costeo de la sucursal.
    /// Retorna costoUnitario promedio ponderado, el primer lote (para el snapshot en DetalleVenta)
    /// y la lista completa de lotes consumidos para trazabilidad en DetalleVentaLote.
    /// </summary>
    Task<(decimal CostoUnitario, int? LoteId, string? NumeroLote, List<ConsumoLoteItem> Lotes)> ConsumirAsync(
        Guid productoId,
        int sucursalId,
        decimal cantidad,
        MetodoCosteo metodoCosteo,
        bool manejaLotes);
}
