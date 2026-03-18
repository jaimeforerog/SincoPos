using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public class RadarNegocioService : IRadarNegocioService
{
    private readonly global::Marten.IDocumentSession _session;
    private readonly AppDbContext _context;

    public RadarNegocioService(global::Marten.IDocumentSession session, AppDbContext context)
    {
        _session = session;
        _context = context;
    }

    public async Task<BusinessRadar?> ObtenerPatronAsync(int sucursalId)
        => await _session.LoadAsync<BusinessRadar>(sucursalId);

    public async Task<RadarNegocioDto?> ObtenerRadarAsync(int sucursalId)
    {
        // Verificar que la sucursal existe
        var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == sucursalId);
        if (!sucursalExiste) return null;

        var colombiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota");
        var ahoraEnColombia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombiaTimeZone);
        var hoy      = ahoraEnColombia.Date;
        var ayer     = hoy.AddDays(-1);
        var hoyUtc   = TimeZoneInfo.ConvertTimeToUtc(hoy, colombiaTimeZone);
        var mananaUtc = TimeZoneInfo.ConvertTimeToUtc(hoy.AddDays(1), colombiaTimeZone);
        var ayerUtc  = TimeZoneInfo.ConvertTimeToUtc(ayer, colombiaTimeZone);

        // ── Métricas del día ──────────────────────────────────────────────────
        var ventasHoyQuery = _context.Ventas
            .Where(v => v.SucursalId == sucursalId
                     && v.FechaVenta >= hoyUtc
                     && v.FechaVenta < mananaUtc);

        var ventasAyerQuery = _context.Ventas
            .Where(v => v.SucursalId == sucursalId
                     && v.FechaVenta >= ayerUtc
                     && v.FechaVenta < hoyUtc);

        var totalHoy  = await ventasHoyQuery.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var totalAyer = await ventasAyerQuery.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var porcentajeCambio = totalAyer > 0 ? (totalHoy - totalAyer) / totalAyer * 100m : 0m;

        var cantidadVentas    = await ventasHoyQuery.CountAsync();
        var productosVendidos = (int)(await ventasHoyQuery
            .SelectMany(v => v.Detalles)
            .SumAsync(d => (decimal?)d.Cantidad) ?? 0m);
        var clientesAtendidos = await ventasHoyQuery
            .Where(v => v.ClienteId.HasValue)
            .Select(v => v.ClienteId!.Value)
            .Distinct()
            .CountAsync();
        var ticketPromedio = cantidadVentas > 0 ? totalHoy / cantidadVentas : 0m;

        var costoHoy     = await ventasHoyQuery
            .SelectMany(v => v.Detalles)
            .SumAsync(d => (decimal?)(d.CostoUnitario * d.Cantidad)) ?? 0m;
        var utilidadHoy  = totalHoy - costoHoy;
        var margenPromedio = totalHoy > 0 ? utilidadHoy / totalHoy * 100m : 0m;

        var metricas = new MetricasDelDiaDto(
            totalHoy, totalAyer, porcentajeCambio,
            cantidadVentas, productosVendidos, clientesAtendidos,
            ticketPromedio, utilidadHoy, margenPromedio
        );

        // ── Ventas por hora (timezone Colombia) ───────────────────────────────
        var horaData = await ventasHoyQuery
            .Select(v => new { v.FechaVenta, v.Total })
            .ToListAsync();
        var ventasPorHora = horaData
            .GroupBy(v => TimeZoneInfo.ConvertTimeFromUtc(v.FechaVenta, colombiaTimeZone).Hour)
            .Select(g => new VentaPorHoraDto(g.Key, g.Sum(v => v.Total), g.Count()))
            .OrderBy(v => v.Hora)
            .ToList();

        // ── Riesgos de ruptura de stock ───────────────────────────────────────
        var riesgos = await _context.Stock
            .Include(s => s.Producto)
            .Include(s => s.Sucursal)
            .Where(s => s.SucursalId == sucursalId
                     && s.Producto.Activo
                     && s.StockMinimo > 0
                     && s.Cantidad <= s.StockMinimo)
            .Select(s => new AlertaStockDto(
                s.ProductoId,
                s.Producto.Nombre,
                s.Producto.CodigoBarras,
                s.SucursalId,
                s.Sucursal.Nombre,
                s.Cantidad,
                s.StockMinimo))
            .ToListAsync();

        return new RadarNegocioDto(metricas, ventasPorHora, riesgos);
    }
}
