using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación de IVentaCosteoService.
/// Encapsula ConsumirLotesFEFO y ConsumirStock, que antes vivían inline en VentaService.
/// </summary>
public class VentaCosteoService : IVentaCosteoService
{
    private readonly CosteoService _costeoService;

    public VentaCosteoService(CosteoService costeoService)
    {
        _costeoService = costeoService;
    }

    /// <inheritdoc/>
    public async Task<(decimal CostoUnitario, int? LoteId, string? NumeroLote, List<ConsumoLoteItem> Lotes)> ConsumirAsync(
        Guid productoId,
        int sucursalId,
        decimal cantidad,
        MetodoCosteo metodoCosteo,
        bool manejaLotes)
    {
        if (manejaLotes)
        {
            var (_, costoUnitario, lotes) =
                await _costeoService.ConsumirLotesFEFO(productoId, sucursalId, cantidad);
            var primero = lotes.Count > 0 ? lotes[0] : null;
            return (costoUnitario, primero?.LoteId, primero?.NumeroLote, lotes);
        }

        var (_, cu) = await _costeoService.ConsumirStock(productoId, sucursalId, cantidad, metodoCosteo);
        return (cu, null, null, new List<ConsumoLoteItem>());
    }
}
