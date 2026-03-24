using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.Infrastructure.Projections;

/// <summary>
/// Capa 13 — Inteligencia colectiva (nivel local).
/// Acumula pares de productos vendidos juntos por sucursal (cross-selling intelligence).
/// Se actualiza inline en cada venta con 2+ líneas de producto.
/// </summary>
public class ProductoComboProjection : IProjection
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
            if (ventaEvt.Items.Count < 2) continue;

            var combo = await operations.LoadAsync<ProductoCombo>(ventaEvt.SucursalId, cancellation)
                ?? new ProductoCombo { Id = ventaEvt.SucursalId, SucursalId = ventaEvt.SucursalId };

            combo.Apply(ventaEvt);
            operations.Store(combo);
        }
    }
}
