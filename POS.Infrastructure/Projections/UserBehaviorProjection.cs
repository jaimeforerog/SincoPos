using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Projection inline: cada VentaCompletadaEvent actualiza el documento UserBehavior.
/// Capa 5 — Anticipación funcional: acumula qué productos vende cada cajero.
/// </summary>
public class UserBehaviorProjection : IProjection
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

            var behavior = await operations.LoadAsync<UserBehavior>(streamId, cancellation)
                ?? new UserBehavior { Id = streamId, ExternalUserId = ventaEvt.ExternalUserId };

            behavior.Apply(ventaEvt);
            operations.Store(behavior);
        }
    }
}
