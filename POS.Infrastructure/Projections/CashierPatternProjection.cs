using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Capa 9 — Aprendizaje continuo (nivel individual).
/// Acumula el patrón de ventas por cajero: velocidad de productos + horas pico + días activos.
/// Complementa UserBehaviorProjection (Capa 5) con datos enriquecidos de comportamiento.
/// </summary>
public class CashierPatternProjection : IProjection
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
            if (!Guid.TryParse(ventaEvt.ExternalUserId, out var streamId)) continue;

            var pattern = await operations.LoadAsync<CashierPattern>(streamId, cancellation)
                ?? new CashierPattern { Id = streamId, ExternalUserId = ventaEvt.ExternalUserId };

            pattern.Apply(ventaEvt);
            operations.Store(pattern);
        }
    }
}
