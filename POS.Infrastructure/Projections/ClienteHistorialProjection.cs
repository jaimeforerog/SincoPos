using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Projection inline: cada VentaCompletadaEvent con ClienteId actualiza el documento ClienteHistorial.
/// Capa 4 — Dependencias inteligentes: el historial del cliente se actualiza automáticamente
/// cuando se registra una venta, sin intervención manual del operador.
/// </summary>
public class ClienteHistorialProjection : IProjection
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
            if (!ventaEvt.ClienteId.HasValue) continue;

            var clienteId = ventaEvt.ClienteId.Value;
            var historial = await operations.LoadAsync<ClienteHistorial>(clienteId, cancellation)
                ?? new ClienteHistorial { Id = clienteId, ClienteId = clienteId };

            historial.Apply(ventaEvt, @event.Timestamp.UtcDateTime);
            operations.Store(historial);
        }
    }
}
