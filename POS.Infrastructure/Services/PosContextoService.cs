using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class PosContextoService : IPosContextoService
{
    private readonly AppDbContext _context;

    public PosContextoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TurnContextDto> ObtenerContextoAsync(int sucursalId)
    {
        // ── Clientes recientes ────────────────────────────────────────────────
        // Última venta por cliente en la sucursal → los 20 más recientes
        var clientesRecientes = await _context.Ventas
            .Where(v => v.SucursalId == sucursalId && v.ClienteId.HasValue)
            .GroupBy(v => v.ClienteId!.Value)
            .Select(g => new
            {
                ClienteId   = g.Key,
                UltimaVenta = g.Max(v => v.FechaVenta)
            })
            .OrderByDescending(x => x.UltimaVenta)
            .Take(20)
            .Join(
                _context.Terceros.Where(t => t.Activo),
                x => x.ClienteId,
                t => t.Id,
                (x, t) => new ClienteRecienteDto(
                    t.Id,
                    t.Nombre,
                    t.Identificacion,
                    x.UltimaVenta))
            .ToListAsync();

        // ── Órdenes pendientes de recibir ─────────────────────────────────────
        var estadosPendientes = new[]
        {
            EstadoOrdenCompra.Pendiente,
            EstadoOrdenCompra.Aprobada,
            EstadoOrdenCompra.RecibidaParcial
        };

        var ordenesPendientes = await _context.OrdenesCompra
            .Where(oc => oc.SucursalId == sucursalId && estadosPendientes.Contains(oc.Estado))
            .OrderByDescending(oc => oc.FechaOrden)
            .Take(10)
            .Select(oc => new OrdenPendienteResumenDto(
                oc.Id,
                oc.NumeroOrden,
                oc.Proveedor.Nombre,
                oc.FechaOrden,
                oc.FechaEntregaEsperada,
                oc.Total,
                oc.Detalles.Count))
            .ToListAsync();

        return new TurnContextDto(clientesRecientes, ordenesPendientes);
    }
}
