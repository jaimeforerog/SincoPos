using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class ReportesService : IReportesService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public ReportesService(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    public async Task<ReporteVentasDto> ObtenerReporteVentasAsync(
        DateTime fechaDesde, DateTime fechaHasta, int? sucursalId = null, int? metodoPago = null)
    {
        var (fechaDesdeUtc, fechaHastaUtc) = NormalizarRangoUtc(fechaDesde, fechaHasta);

        var ventasQuery = _context.Ventas
            .Where(v => v.FechaVenta >= fechaDesdeUtc && v.FechaVenta <= fechaHastaUtc);

        if (sucursalId.HasValue)
            ventasQuery = ventasQuery.Where(v => v.SucursalId == sucursalId.Value);

        if (metodoPago.HasValue)
            ventasQuery = ventasQuery.Where(v => (int)v.MetodoPago == metodoPago.Value);

        var totalVentas = await ventasQuery.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var cantidadVentas = await ventasQuery.CountAsync();
        var costoTotal = await ventasQuery
            .SelectMany(v => v.Detalles)
            .SumAsync(d => (decimal?)(d.CostoUnitario * d.Cantidad)) ?? 0m;
        var utilidadTotal = totalVentas - costoTotal;
        var ticketPromedio = cantidadVentas > 0 ? totalVentas / cantidadVentas : 0;
        var margenPromedio = totalVentas > 0 ? (utilidadTotal / totalVentas) * 100 : 0;

        var ventasPorMetodoRaw = await ventasQuery
            .GroupBy(v => v.MetodoPago)
            .Select(g => new { MetodoPago = g.Key, Total = g.Sum(v => v.Total), Cantidad = g.Count() })
            .ToListAsync();
        var ventasPorMetodo = ventasPorMetodoRaw
            .Select(r => new VentaPorMetodoPagoDto(r.MetodoPago.ToString(), r.Total, r.Cantidad))
            .ToList();

        var totalPorDiaRaw = await ventasQuery
            .GroupBy(v => v.FechaVenta.Date)
            .Select(g => new { Fecha = g.Key, Total = g.Sum(v => v.Total), Cantidad = g.Count() })
            .ToListAsync();
        var costoPorDiaDict = await ventasQuery
            .SelectMany(v => v.Detalles, (v, d) => new { FechaVenta = v.FechaVenta, Costo = d.CostoUnitario * d.Cantidad })
            .GroupBy(x => x.FechaVenta.Date)
            .Select(g => new { Fecha = g.Key, Costo = g.Sum(x => x.Costo) })
            .ToDictionaryAsync(g => g.Fecha, g => g.Costo);
        var ventasPorDia = totalPorDiaRaw
            .Select(r =>
            {
                var costo = costoPorDiaDict.TryGetValue(r.Fecha, out var c) ? c : 0m;
                return new VentaPorDiaDto(r.Fecha.ToString("yyyy-MM-dd"), r.Total, r.Cantidad, costo, r.Total - costo);
            })
            .OrderBy(v => v.Fecha)
            .ToList();

        var reporte = new ReporteVentasDto(
            totalVentas, cantidadVentas, ticketPromedio,
            costoTotal, utilidadTotal, margenPromedio,
            ventasPorMetodo, ventasPorDia
        );

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarReporteVentas",
            Tipo: TipoActividad.Sistema,
            Descripcion: $"Reporte de ventas: {fechaDesde:yyyy-MM-dd} a {fechaHasta:yyyy-MM-dd}. Total: ${totalVentas:N2}, Cantidad: {cantidadVentas}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: "ventas",
            EntidadNombre: "Reporte de Ventas",
            DatosNuevos: new { fechaDesde, fechaHasta, sucursalId, metodoPago, totalVentas, cantidadVentas }
        ));

        return reporte;
    }

    public async Task<ReporteInventarioValorizadoDto> ObtenerInventarioValorizadoAsync(
        int? sucursalId = null, int? categoriaId = null, bool soloConStock = false)
    {
        var stockQuery = _context.Stock
            .Include(s => s.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(s => s.Sucursal)
            .AsQueryable();

        if (sucursalId.HasValue)
            stockQuery = stockQuery.Where(s => s.SucursalId == sucursalId.Value);

        if (categoriaId.HasValue)
            stockQuery = stockQuery.Where(s => s.Producto.CategoriaId == categoriaId.Value);

        if (soloConStock)
            stockQuery = stockQuery.Where(s => s.Cantidad > 0);

        var stock = await stockQuery.ToListAsync();

        var productoIds = stock.Select(s => s.ProductoId).Distinct().ToList();
        var preciosDict = (await _context.PreciosSucursal
            .Where(ps => productoIds.Contains(ps.ProductoId))
            .ToListAsync())
            .GroupBy(ps => (ps.ProductoId, ps.SucursalId))
            .ToDictionary(g => g.Key, g => g.Max(ps => ps.PrecioVenta));

        var productos = stock.Select(s =>
        {
            decimal precioVenta;
            if (preciosDict.TryGetValue((s.ProductoId, s.SucursalId), out var psSucursal) && psSucursal > 0)
                precioVenta = psSucursal;
            else if (s.Producto.PrecioVenta > 0)
                precioVenta = s.Producto.PrecioVenta;
            else
            {
                var costo = s.CostoPromedio > 0 ? s.CostoPromedio : s.Producto.PrecioCosto;
                precioVenta = Math.Round(costo * (1 + (s.Producto.Categoria?.MargenGanancia ?? 0m)), 2);
            }

            var costoTotal = s.Cantidad * s.CostoPromedio;
            var valorVenta = s.Cantidad * precioVenta;
            var utilidadPotencial = valorVenta - costoTotal;
            var margen = valorVenta > 0 ? (utilidadPotencial / valorVenta) * 100 : 0;

            return new ProductoValorizadoDto(
                s.ProductoId, s.Producto.CodigoBarras, s.Producto.Nombre,
                s.Producto.Categoria?.Nombre, s.SucursalId, s.Sucursal.Nombre,
                s.Cantidad, s.CostoPromedio, costoTotal, precioVenta,
                valorVenta, utilidadPotencial, margen
            );
        }).ToList();

        var totalCosto = productos.Sum(p => p.CostoTotal);
        var totalVenta = productos.Sum(p => p.ValorVenta);
        var utilidadPotencialTotal = productos.Sum(p => p.UtilidadPotencial);

        var reporte = new ReporteInventarioValorizadoDto(
            totalCosto, totalVenta, utilidadPotencialTotal,
            productos.Count, productos.Sum(p => p.Cantidad),
            productos.OrderByDescending(p => p.CostoTotal).ToList()
        );

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarReporteInventario",
            Tipo: TipoActividad.Sistema,
            Descripcion: $"Reporte de inventario valorizado. Productos: {productos.Count}, Valor total: ${totalVenta:N2}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: "inventario",
            EntidadNombre: "Reporte de Inventario",
            DatosNuevos: new { sucursalId, categoriaId, soloConStock, totalProductos = productos.Count, totalCosto, totalVenta }
        ));

        return reporte;
    }

    public async Task<(ReporteCajaDto? reporte, string? error)> ObtenerReporteCajaAsync(
        int cajaId, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        var caja = await _context.Cajas
            .Include(c => c.Sucursal)
            .FirstOrDefaultAsync(c => c.Id == cajaId);

        if (caja == null)
            return (null, $"Caja con ID {cajaId} no encontrada.");

        var ventasQuery = _context.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Cliente)
            .Where(v => v.CajaId == cajaId);

        if (caja.Estado == EstadoCaja.Abierta)
        {
            fechaDesde ??= caja.FechaApertura;
            fechaHasta ??= DateTime.UtcNow;
        }

        if (fechaDesde.HasValue && fechaDesde.Value.TimeOfDay == TimeSpan.Zero)
            fechaDesde = fechaDesde.Value.Date;

        if (fechaHasta.HasValue && fechaHasta.Value.TimeOfDay == TimeSpan.Zero)
            fechaHasta = fechaHasta.Value.Date.AddDays(1).AddTicks(-1);

        DateTime? fechaDesdeUtc = fechaDesde.HasValue
            ? DateTime.SpecifyKind(fechaDesde.Value, DateTimeKind.Utc) : null;
        DateTime? fechaHastaUtc = fechaHasta.HasValue
            ? DateTime.SpecifyKind(fechaHasta.Value, DateTimeKind.Utc) : null;

        if (fechaDesdeUtc.HasValue)
            ventasQuery = ventasQuery.Where(v => v.FechaVenta >= fechaDesdeUtc.Value);

        if (fechaHastaUtc.HasValue)
            ventasQuery = ventasQuery.Where(v => v.FechaVenta <= fechaHastaUtc.Value);

        var ventas = await ventasQuery.OrderByDescending(v => v.FechaVenta).ToListAsync();

        var totalEfectivo = ventas.Where(v => v.MetodoPago == MetodoPago.Efectivo).Sum(v => v.Total);
        var totalTarjeta = ventas.Where(v => v.MetodoPago == MetodoPago.Tarjeta).Sum(v => v.Total);
        var totalTransferencia = ventas.Where(v => v.MetodoPago == MetodoPago.Transferencia).Sum(v => v.Total);
        var totalVentas = ventas.Sum(v => v.Total);

        var ventasDto = ventas.Select(v => new VentaCajaDto(
            v.Id, v.NumeroVenta, v.FechaVenta, v.MetodoPago.ToString(), v.Total,
            v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad),
            v.Total - v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad),
            v.Cliente?.Nombre
        )).ToList();

        decimal? diferenciaEsperado = null;
        decimal? diferenciaReal = null;
        decimal? montoCierre = null;

        if (caja.Estado == EstadoCaja.Cerrada)
        {
            montoCierre = caja.MontoActual;
            var montoEsperado = caja.MontoApertura + totalEfectivo;
            diferenciaEsperado = caja.MontoActual - montoEsperado;
            diferenciaReal = diferenciaEsperado;
        }

        var reporte = new ReporteCajaDto(
            caja.Id, caja.Nombre, caja.SucursalId, caja.Sucursal.Nombre,
            caja.FechaApertura ?? DateTime.MinValue, caja.FechaCierre,
            caja.MontoApertura,
            totalEfectivo, totalTarjeta, totalTransferencia, totalVentas,
            montoCierre, diferenciaEsperado, diferenciaReal,
            ventasDto
        );

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarReporteCaja",
            Tipo: TipoActividad.Sistema,
            Descripcion: $"Reporte de caja: {caja.Nombre}. Total ventas: ${totalVentas:N2}",
            SucursalId: caja.SucursalId,
            TipoEntidad: "Reporte",
            EntidadId: cajaId.ToString(),
            EntidadNombre: $"Reporte Caja {caja.Nombre}",
            DatosNuevos: new { cajaId, fechaDesde, fechaHasta, totalVentas, montoCierre }
        ));

        return (reporte, null);
    }

    public async Task<ReporteAuditoriaComprasDto> ObtenerAuditoriaComprasAsync(
        ReporteAuditoriaComprasQueryDto query)
    {
        var (fechaDesdeUtc, fechaHastaUtc) = NormalizarRangoUtc(query.FechaDesde, query.FechaHasta);

        // ── Logs paginados via IActivityLogService ─────────────────────────────
        var filter = new ActivityLogFilterDto(
            FechaDesde: fechaDesdeUtc,
            FechaHasta: fechaHastaUtc,
            UsuarioEmail: query.UsuarioEmail,
            Tipo: TipoActividad.Compra,
            Accion: query.Accion,
            SucursalId: query.SucursalId,
            Exitosa: query.SoloErrores.HasValue ? !query.SoloErrores.Value : null,
            PageNumber: query.PageNumber,
            PageSize: query.PageSize
        );
        var logsResult = await _activityLogService.GetActivitiesAsync(filter);

        // ── KPIs de conteo desde ActivityLog ──────────────────────────────────
        var kpiQuery = _context.ActivityLogs
            .Where(a => a.Tipo == TipoActividad.Compra
                     && a.FechaHora >= fechaDesdeUtc
                     && a.FechaHora <= fechaHastaUtc);

        if (query.SucursalId.HasValue)
            kpiQuery = kpiQuery.Where(a => a.SucursalId == query.SucursalId.Value);

        var kpiRaw = await kpiQuery
            .Select(a => new { a.Accion, a.Exitosa })
            .ToListAsync();

        var eventosPorAccion = kpiRaw
            .GroupBy(a => a.Accion)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalDevoluciones = kpiRaw.Count(a => a.Accion == "DevolucionCompra");

        // ── KPIs monetarios desde OrdenCompra ─────────────────────────────────
        var ordenesQuery = _context.OrdenesCompra
            .Where(o => o.FechaOrden >= fechaDesdeUtc && o.FechaOrden <= fechaHastaUtc);

        if (query.SucursalId.HasValue)
            ordenesQuery = ordenesQuery.Where(o => o.SucursalId == query.SucursalId.Value);

        if (query.ProveedorId.HasValue)
            ordenesQuery = ordenesQuery.Where(o => o.ProveedorId == query.ProveedorId.Value);

        var ordenesData = await ordenesQuery
            .Select(o => new { o.Total, o.Estado, o.ErrorSincronizacion })
            .ToListAsync();

        var valorTotalComprado = ordenesData
            .Where(o => o.Estado == EstadoOrdenCompra.RecibidaCompleta
                     || o.Estado == EstadoOrdenCompra.RecibidaParcial)
            .Sum(o => o.Total);

        var ordenesConErrorErp = ordenesData
            .Count(o => !string.IsNullOrEmpty(o.ErrorSincronizacion));

        var kpis = new KpisAuditoriaComprasDto(
            TotalEventos: kpiRaw.Count,
            EventosExitosos: kpiRaw.Count(a => a.Exitosa),
            EventosFallidos: kpiRaw.Count(a => !a.Exitosa),
            EventosPorAccion: eventosPorAccion,
            OrdenesConErrorErp: ordenesConErrorErp,
            TotalDevoluciones: totalDevoluciones,
            ValorTotalComprado: valorTotalComprado
        );

        return new ReporteAuditoriaComprasDto(kpis, logsResult);
    }

    private static (DateTime desde, DateTime hasta) NormalizarRangoUtc(DateTime fechaDesde, DateTime fechaHasta) =>
        (DateTime.SpecifyKind(fechaDesde.Date, DateTimeKind.Utc),
         DateTime.SpecifyKind(fechaHasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));
}
