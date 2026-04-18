using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public DashboardService(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    public async Task<DashboardDto> ObtenerDashboardAsync(int? sucursalId = null)
    {
        var colombiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota");
        var ahoraEnColombia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombiaTimeZone);
        var hoy = ahoraEnColombia.Date;
        var ayer = hoy.AddDays(-1);

        var hoyUtc = TimeZoneInfo.ConvertTimeToUtc(hoy, colombiaTimeZone);
        var mañanaUtc = TimeZoneInfo.ConvertTimeToUtc(hoy.AddDays(1), colombiaTimeZone);
        var ayerUtc = TimeZoneInfo.ConvertTimeToUtc(ayer, colombiaTimeZone);

        var ventasHoyQuery = _context.Ventas
            .Where(v => v.FechaVenta >= hoyUtc && v.FechaVenta < mañanaUtc);

        var ventasAyerQuery = _context.Ventas
            .Where(v => v.FechaVenta >= ayerUtc && v.FechaVenta < hoyUtc);

        if (sucursalId.HasValue)
        {
            ventasHoyQuery = ventasHoyQuery.Where(v => v.SucursalId == sucursalId.Value);
            ventasAyerQuery = ventasAyerQuery.Where(v => v.SucursalId == sucursalId.Value);
        }

        var totalHoy = await ventasHoyQuery.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var totalAyer = await ventasAyerQuery.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var porcentajeCambio = totalAyer > 0 ? ((totalHoy - totalAyer) / totalAyer) * 100 : 0;

        var cantidadVentas = await ventasHoyQuery.CountAsync();
        var productosVendidos = (int)(await ventasHoyQuery
            .SelectMany(v => v.Detalles)
            .SumAsync(d => (decimal?)d.Cantidad) ?? 0m);
        var clientesAtendidos = await ventasHoyQuery
            .Where(v => v.ClienteId.HasValue)
            .Select(v => v.ClienteId!.Value)
            .Distinct()
            .CountAsync();
        var ticketPromedio = cantidadVentas > 0 ? totalHoy / cantidadVentas : 0;

        var costoHoy = await ventasHoyQuery
            .SelectMany(v => v.Detalles)
            .SumAsync(d => (decimal?)(d.CostoUnitario * d.Cantidad)) ?? 0m;
        var utilidadHoy = totalHoy - costoHoy;
        var margenPromedio = totalHoy > 0 ? (utilidadHoy / totalHoy) * 100 : 0;

        var metricas = new MetricasDelDiaDto(
            totalHoy, totalAyer, porcentajeCambio,
            cantidadVentas, productosVendidos, clientesAtendidos,
            ticketPromedio, utilidadHoy, margenPromedio
        );

        var horaData = await ventasHoyQuery
            .Select(v => new { v.FechaVenta, v.Total })
            .ToListAsync();
        var ventasPorHora = horaData
            .GroupBy(v => TimeZoneInfo.ConvertTimeFromUtc(v.FechaVenta, colombiaTimeZone).Hour)
            .Select(g => new VentaPorHoraDto(g.Key, g.Sum(v => v.Total), g.Count()))
            .OrderBy(v => v.Hora)
            .ToList();

        var detallesHoy = await ventasHoyQuery
            .SelectMany(v => v.Detalles)
            .Select(d => new { d.ProductoId, d.NombreProducto, d.Cantidad, d.PrecioUnitario, d.CostoUnitario, d.Subtotal })
            .ToListAsync();
        var productoIdsTop = detallesHoy.Select(d => d.ProductoId).Distinct().ToList();
        var codigosBarras = productoIdsTop.Count > 0
            ? await _context.Productos
                .Where(p => productoIdsTop.Contains(p.Id))
                .Select(p => new { p.Id, p.CodigoBarras, p.Categoria!.Nombre })
                .ToDictionaryAsync(p => p.Id)
            : [];

        var topProductos = detallesHoy
            .GroupBy(d => new { d.ProductoId, d.NombreProducto })
            .Select(g =>
            {
                codigosBarras.TryGetValue(g.Key.ProductoId, out var prod);
                var totalVtas = g.Sum(d => d.Subtotal);
                var utilidad = g.Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad);
                return new TopProductoDto(
                    g.Key.ProductoId,
                    prod?.CodigoBarras ?? "",
                    g.Key.NombreProducto,
                    prod?.Nombre,
                    (int)g.Sum(d => d.Cantidad),
                    totalVtas, utilidad,
                    totalVtas > 0 ? (utilidad / totalVtas) * 100 : 0
                );
            })
            .OrderByDescending(p => p.CantidadVendida)
            .Take(5)
            .ToList();

        var stockQuery = _context.Stock
            .Include(s => s.Producto)
            .Include(s => s.Sucursal)
            .Where(s => s.Cantidad <= 10);

        if (sucursalId.HasValue)
            stockQuery = stockQuery.Where(s => s.SucursalId == sucursalId.Value);

        var alertasStock = await stockQuery
            .OrderBy(s => s.Cantidad)
            .Take(10)
            .Select(s => new AlertaStockDto(
                s.ProductoId, s.Producto.Nombre, s.Producto.CodigoBarras,
                s.SucursalId, s.Sucursal.Nombre,
                s.Cantidad, 5
            ))
            .ToListAsync();

        var dashboard = new DashboardDto(metricas, ventasPorHora, topProductos, alertasStock);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarDashboard",
            Tipo: TipoActividad.Sistema,
            Descripcion: $"Dashboard consultado. Ventas hoy: ${totalHoy:N2}, Productos vendidos: {productosVendidos}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: "dashboard",
            EntidadNombre: "Dashboard",
            DatosNuevos: new { sucursalId, totalHoy, cantidadVentas }
        ));

        return dashboard;
    }

    public async Task<List<TopProductoDto>> ObtenerTopProductosAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? sucursalId = null, int limite = 10)
    {
        var (fechaDesdeUtc, fechaHastaUtc) = NormalizarRangoUtc(fechaDesde, fechaHasta);

        var ventasQuery = _context.Ventas
            .Where(v => v.FechaVenta >= fechaDesdeUtc && v.FechaVenta <= fechaHastaUtc);

        if (sucursalId.HasValue)
            ventasQuery = ventasQuery.Where(v => v.SucursalId == sucursalId.Value);

        var topRaw = await ventasQuery
            .SelectMany(v => v.Detalles)
            .GroupBy(d => new { d.ProductoId, d.NombreProducto })
            .Select(g => new
            {
                ProductoId = g.Key.ProductoId,
                NombreProducto = g.Key.NombreProducto,
                Cantidad = g.Sum(d => d.Cantidad),
                TotalVtas = g.Sum(d => d.Subtotal),
                Utilidad = g.Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad)
            })
            .OrderByDescending(g => g.Cantidad)
            .Take(limite)
            .ToListAsync();

        if (topRaw.Count == 0) return [];

        var productoIds = topRaw.Select(t => t.ProductoId).ToList();
        var codigosBarras = await _context.Productos
            .Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CodigoBarras, CategoriaNombre = p.Categoria!.Nombre })
            .ToDictionaryAsync(p => p.Id);

        var topProductos = topRaw
            .Select(t =>
            {
                codigosBarras.TryGetValue(t.ProductoId, out var prod);
                return new TopProductoDto(
                    t.ProductoId, prod?.CodigoBarras ?? "", t.NombreProducto,
                    prod?.CategoriaNombre, (int)t.Cantidad, t.TotalVtas, t.Utilidad,
                    t.TotalVtas > 0 ? (t.Utilidad / t.TotalVtas) * 100 : 0
                );
            })
            .ToList();

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarTopProductos",
            Tipo: TipoActividad.Sistema,
            Descripcion: $"Top productos: {fechaDesde:yyyy-MM-dd} a {fechaHasta:yyyy-MM-dd}. Top 1: {topProductos.FirstOrDefault()?.Nombre}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: "top-productos",
            EntidadNombre: "Top Productos",
            DatosNuevos: new { fechaDesde, fechaHasta, sucursalId, limite }
        ));

        return topProductos;
    }

    private static (DateTime desde, DateTime hasta) NormalizarRangoUtc(DateTime fechaDesde, DateTime fechaHasta) =>
        (DateTime.SpecifyKind(fechaDesde.Date, DateTimeKind.Utc),
         DateTime.SpecifyKind(fechaHasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));
}
