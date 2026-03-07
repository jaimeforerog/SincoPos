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
        var fechaDesdeUtc = DateTime.SpecifyKind(fechaDesde, DateTimeKind.Utc);
        var fechaHastaUtc = DateTime.SpecifyKind(fechaHasta, DateTimeKind.Utc);

        var ventasQuery = _context.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= fechaDesdeUtc && v.FechaVenta <= fechaHastaUtc);

        if (sucursalId.HasValue)
            ventasQuery = ventasQuery.Where(v => v.SucursalId == sucursalId.Value);

        if (metodoPago.HasValue)
            ventasQuery = ventasQuery.Where(v => (int)v.MetodoPago == metodoPago.Value);

        var ventas = await ventasQuery.ToListAsync();

        var totalVentas = ventas.Sum(v => v.Total);
        var cantidadVentas = ventas.Count;
        var costoTotal = ventas.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad));
        var utilidadTotal = totalVentas - costoTotal;
        var ticketPromedio = cantidadVentas > 0 ? totalVentas / cantidadVentas : 0;
        var margenPromedio = totalVentas > 0 ? (utilidadTotal / totalVentas) * 100 : 0;

        var ventasPorMetodo = ventas
            .GroupBy(v => v.MetodoPago.ToString())
            .Select(g => new VentaPorMetodoPagoDto(
                g.Key,
                g.Sum(v => v.Total),
                g.Count()
            ))
            .ToList();

        var ventasPorDia = ventas
            .GroupBy(v => v.FechaVenta.Date)
            .Select(g => new VentaPorDiaDto(
                g.Key.ToString("yyyy-MM-dd"),
                g.Sum(v => v.Total),
                g.Count(),
                g.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad)),
                g.Sum(v => v.Total) - g.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad))
            ))
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

        var productos = stock.Select(s =>
        {
            var costoTotal = s.Cantidad * s.CostoPromedio;
            var valorVenta = s.Cantidad * s.Producto.PrecioVenta;
            var utilidadPotencial = valorVenta - costoTotal;
            var margen = valorVenta > 0 ? (utilidadPotencial / valorVenta) * 100 : 0;

            return new ProductoValorizadoDto(
                s.ProductoId,
                s.Producto.CodigoBarras,
                s.Producto.Nombre,
                s.Producto.Categoria?.Nombre,
                s.SucursalId,
                s.Sucursal.Nombre,
                s.Cantidad,
                s.CostoPromedio,
                costoTotal,
                s.Producto.PrecioVenta,
                valorVenta,
                utilidadPotencial,
                margen
            );
        }).ToList();

        var totalCosto = productos.Sum(p => p.CostoTotal);
        var totalVenta = productos.Sum(p => p.ValorVenta);
        var utilidadPotencialTotal = productos.Sum(p => p.UtilidadPotencial);
        var totalProductos = productos.Count;
        var totalUnidades = productos.Sum(p => p.Cantidad);

        var reporte = new ReporteInventarioValorizadoDto(
            totalCosto, totalVenta, utilidadPotencialTotal,
            totalProductos, totalUnidades,
            productos.OrderByDescending(p => p.CostoTotal).ToList()
        );

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarReporteInventario",
            Tipo: TipoActividad.Sistema,
            Descripcion: $"Reporte de inventario valorizado. Productos: {totalProductos}, Valor total: ${totalVenta:N2}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: "inventario",
            EntidadNombre: "Reporte de Inventario",
            DatosNuevos: new { sucursalId, categoriaId, soloConStock, totalProductos, totalCosto, totalVenta }
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

        DateTime? fechaDesdeUtc = fechaDesde.HasValue
            ? DateTime.SpecifyKind(fechaDesde.Value, DateTimeKind.Utc)
            : null;
        DateTime? fechaHastaUtc = fechaHasta.HasValue
            ? DateTime.SpecifyKind(fechaHasta.Value, DateTimeKind.Utc)
            : null;

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
            v.Id,
            v.NumeroVenta,
            v.FechaVenta,
            v.MetodoPago.ToString(),
            v.Total,
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
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= hoyUtc && v.FechaVenta < mañanaUtc);

        var ventasAyerQuery = _context.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= ayerUtc && v.FechaVenta < hoyUtc);

        if (sucursalId.HasValue)
        {
            ventasHoyQuery = ventasHoyQuery.Where(v => v.SucursalId == sucursalId.Value);
            ventasAyerQuery = ventasAyerQuery.Where(v => v.SucursalId == sucursalId.Value);
        }

        var ventasHoy = await ventasHoyQuery.ToListAsync();
        var ventasAyer = await ventasAyerQuery.ToListAsync();

        var totalHoy = ventasHoy.Sum(v => v.Total);
        var totalAyer = ventasAyer.Sum(v => v.Total);
        var porcentajeCambio = totalAyer > 0 ? ((totalHoy - totalAyer) / totalAyer) * 100 : 0;

        var cantidadVentas = ventasHoy.Count;
        var productosVendidos = ventasHoy.Sum(v => v.Detalles.Sum(d => d.Cantidad));
        var clientesAtendidos = ventasHoy.Where(v => v.ClienteId.HasValue)
            .Select(v => v.ClienteId!.Value).Distinct().Count();
        var ticketPromedio = cantidadVentas > 0 ? totalHoy / cantidadVentas : 0;

        var costoHoy = ventasHoy.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad));
        var utilidadHoy = totalHoy - costoHoy;
        var margenPromedio = totalHoy > 0 ? (utilidadHoy / totalHoy) * 100 : 0;

        var metricas = new MetricasDelDiaDto(
            totalHoy, totalAyer, porcentajeCambio,
            cantidadVentas, (int)productosVendidos, clientesAtendidos,
            ticketPromedio, utilidadHoy, margenPromedio
        );

        // Ventas por hora
        var ventasPorHora = ventasHoy
            .GroupBy(v => TimeZoneInfo.ConvertTimeFromUtc(v.FechaVenta, colombiaTimeZone).Hour)
            .Select(g => new VentaPorHoraDto(g.Key, g.Sum(v => v.Total), g.Count()))
            .OrderBy(v => v.Hora)
            .ToList();

        // Top 5 productos
        var detallesHoy = ventasHoy.SelectMany(v => v.Detalles).ToList();
        var productoIdsTop = detallesHoy.Select(d => d.ProductoId).Distinct().ToList();
        var codigosBarras = await _context.Productos
            .Where(p => productoIdsTop.Contains(p.Id))
            .Select(p => new { p.Id, p.CodigoBarras, p.Categoria!.Nombre })
            .ToDictionaryAsync(p => p.Id);

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

        // Alertas de stock bajo
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
        var fechaDesdeUtc = DateTime.SpecifyKind(fechaDesde, DateTimeKind.Utc);
        var fechaHastaUtc = DateTime.SpecifyKind(fechaHasta, DateTimeKind.Utc);

        var ventasQuery = _context.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= fechaDesdeUtc && v.FechaVenta <= fechaHastaUtc);

        if (sucursalId.HasValue)
            ventasQuery = ventasQuery.Where(v => v.SucursalId == sucursalId.Value);

        var ventas = await ventasQuery.ToListAsync();

        var detalles = ventas.SelectMany(v => v.Detalles).ToList();
        var productoIds = detalles.Select(d => d.ProductoId).Distinct().ToList();
        var codigosBarras = await _context.Productos
            .Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CodigoBarras, CategoriaNombre = p.Categoria!.Nombre })
            .ToDictionaryAsync(p => p.Id);

        var topProductos = detalles
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
                    prod?.CategoriaNombre,
                    (int)g.Sum(d => d.Cantidad),
                    totalVtas, utilidad,
                    totalVtas > 0 ? (utilidad / totalVtas) * 100 : 0
                );
            })
            .OrderByDescending(p => p.CantidadVendida)
            .Take(limite)
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
}
