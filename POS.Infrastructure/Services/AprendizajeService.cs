using POS.Application.Services;
using POS.Domain.Aggregates;

namespace POS.Infrastructure.Services;

public class AprendizajeService : IAprendizajeService
{
    private readonly global::Marten.IDocumentSession _session;

    public AprendizajeService(global::Marten.IDocumentSession session)
    {
        _session = session;
    }

    public async Task<CashierPattern?> ObtenerPatronCajero(string externalUserId)
    {
        if (!Guid.TryParse(externalUserId, out var streamId))
            return null;

        return await _session.LoadAsync<CashierPattern>(streamId);
    }

    public async Task<StorePattern?> ObtenerPatronTienda(int sucursalId)
        => await _session.LoadAsync<StorePattern>(sucursalId);
}
