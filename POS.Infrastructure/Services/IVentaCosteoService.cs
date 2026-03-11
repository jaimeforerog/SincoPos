using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Encapsula la lógica de consumo de inventario durante una venta.
/// Extraído de VentaService para cumplir el principio de responsabilidad única.
/// </summary>
public interface IVentaCosteoService
{
    /// <summary>
    /// Consume stock de un producto según el método de costeo de la sucursal.
    /// Retorna costoUnitario y, si el producto maneja lotes, el loteId y numeroLote.
    /// </summary>
    Task<(decimal CostoUnitario, int? LoteId, string? NumeroLote)> ConsumirAsync(
        Guid productoId,
        int sucursalId,
        decimal cantidad,
        MetodoCosteo metodoCosteo,
        bool manejaLotes);
}
