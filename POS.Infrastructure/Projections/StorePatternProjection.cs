using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Capa 9 — Aprendizaje continuo (nivel organizacional).
/// Acumula el patrón de ventas por tienda: velocidad de productos + horas pico + días activos.
/// Alimenta la Capa 14 (Radar de Negocio) para forecasting y detección de anomalías.
/// </summary>
public class StorePatternProjection : IProjection
{
    public void Apply(IDocumentOperations operations, IReadOnlyList<StreamAction> streams)
        => throw new NotSupportedException("Usar ApplyAsync");

    public async Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<IEvent> events,
        CancellationToken cancellation)
    {
        foreach (var @event in events)
        {
            if (@event.Data is not VentaCompletadaEvent ventaEvt) continue;

            var pattern = await operations.LoadAsync<StorePattern>(ventaEvt.SucursalId, cancellation)
                ?? new StorePattern { Id = ventaEvt.SucursalId, SucursalId = ventaEvt.SucursalId };

            pattern.Apply(ventaEvt);
            operations.Store(pattern);
        }
    }
}
