using JasperFx.Events;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Capa 14 — Radar de Negocio.
/// Proyección inline que acumula ingresos + velocidad de productos por sucursal
/// en el documento BusinessRadar. Alimenta el endpoint /api/v1/radar/sucursal/{id}.
/// </summary>
public class BusinessRiskProjection : IProjection
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

            var radar = await operations.LoadAsync<BusinessRadar>(ventaEvt.SucursalId, cancellation)
                ?? new BusinessRadar { Id = ventaEvt.SucursalId, SucursalId = ventaEvt.SucursalId };

            radar.Apply(ventaEvt, @event.Timestamp.UtcDateTime);
            operations.Store(radar);
        }
    }
}
