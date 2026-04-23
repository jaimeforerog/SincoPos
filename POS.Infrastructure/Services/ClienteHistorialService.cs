using global::Marten;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;

namespace POS.Infrastructure.Services;

/// <summary>
/// Capa 4 — Dependencias inteligentes.
/// Lee el documento ClienteHistorial (Marten) acumulado por ClienteHistorialProjection.
/// </summary>
public sealed class ClienteHistorialService : IClienteHistorialService
{
    private readonly IDocumentStore _store;

    public ClienteHistorialService(IDocumentStore store) => _store = store;

    public async Task<ClienteHistorialDto?> ObtenerHistorialAsync(int clienteId)
    {
        await using var session = _store.QuerySession();
        var historial = await session.LoadAsync<ClienteHistorial>(clienteId);
        if (historial is null) return null;

        var topProductos = historial.TopProductos
            .Select(id =>
            {
                historial.ProductoFrecuencia.TryGetValue(id, out var qty);
                historial.ProductoNombres.TryGetValue(id, out var nombre);
                return Guid.TryParse(id, out var guid)
                    ? new ProductoFrecuenteDto(guid, nombre ?? id, qty)
                    : null;
            })
            .Where(p => p is not null)
            .Cast<ProductoFrecuenteDto>()
            .ToList();

        // Convertir las claves int a string para JSON amigable (lunes=1, etc.)
        var visitasDia  = historial.VisitasPorDiaSemana.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
        var visitasHora = historial.VisitasPorHora.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);

        return new ClienteHistorialDto(
            ClienteId:          historial.ClienteId,
            TotalCompras:       historial.TotalCompras,
            TotalGastado:       historial.TotalGastado,
            GastoPromedio:      historial.GastoPromedio,
            PrimeraVisita:      historial.TotalCompras > 0 ? historial.PrimeraVisita : null,
            UltimaVisita:       historial.TotalCompras > 0 ? historial.UltimaVisita  : null,
            TopProductos:       topProductos,
            VisitasPorDiaSemana: visitasDia,
            VisitasPorHora:     visitasHora
        );
    }
}
