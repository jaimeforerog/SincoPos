using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Domain.Events.Inventario;
using POS.Domain.Aggregates;

namespace POS.Infrastructure.Services;

public class ReportesService : IReportesService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;
    private readonly global::Marten.IDocumentSession _session;

    public ReportesService(AppDbContext context, IActivityLogService activityLogService, global::Marten.IDocumentSession session)
    {
        _context = context;
        _activityLogService = activityLogService;
        _session = session;
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

        // Scalars: SUM/COUNT ejecutados en PostgreSQL
        var totalVentas = await ventasQuery.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var cantidadVentas = await ventasQuery.CountAsync();
        var costoTotal = await ventasQuery
            .SelectMany(v => v.Detalles)
            .SumAsync(d => (decimal?)(d.CostoUnitario * d.Cantidad)) ?? 0m;
        var utilidadTotal = totalVentas - costoTotal;
        var ticketPromedio = cantidadVentas > 0 ? totalVentas / cantidadVentas : 0;
        var margenPromedio = totalVentas > 0 ? (utilidadTotal / totalVentas) * 100 : 0;

        // ventasPorMetodo: SQL GROUP BY MetodoPago
        var ventasPorMetodoRaw = await ventasQuery
            .GroupBy(v => v.MetodoPago)
            .Select(g => new { MetodoPago = g.Key, Total = g.Sum(v => v.Total), Cantidad = g.Count() })
            .ToListAsync();
        var ventasPorMetodo = ventasPorMetodoRaw
            .Select(r => new VentaPorMetodoPagoDto(r.MetodoPago.ToString(), r.Total, r.Cantidad))
            .ToList();

        // ventasPorDia: dos consultas SQL GROUP BY DATE, unidas en C#
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

        // Cargar precios por sucursal en batch (GroupBy para tolerar duplicados en DB)
        var productoIds = stock.Select(s => s.ProductoId).Distinct().ToList();
        var preciosDict = (await _context.PreciosSucursal
            .Where(ps => productoIds.Contains(ps.ProductoId))
            .ToListAsync())
            .GroupBy(ps => (ps.ProductoId, ps.SucursalId))
            .ToDictionary(g => g.Key, g => g.Max(ps => ps.PrecioVenta));

        var productos = stock.Select(s =>
        {
            // Cascada: PrecioSucursal → Producto.PrecioVenta → PrecioCosto × (1 + Margen)
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
                s.ProductoId,
                s.Producto.CodigoBarras,
                s.Producto.Nombre,
                s.Producto.Categoria?.Nombre,
                s.SucursalId,
                s.Sucursal.Nombre,
                s.Cantidad,
                s.CostoPromedio,
                costoTotal,
                precioVenta,
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

        if (fechaDesde.HasValue && fechaDesde.Value.TimeOfDay == TimeSpan.Zero)
            fechaDesde = fechaDesde.Value.Date;
            
        if (fechaHasta.HasValue && fechaHasta.Value.TimeOfDay == TimeSpan.Zero)
            fechaHasta = fechaHasta.Value.Date.AddDays(1).AddTicks(-1);

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
            .Where(v => v.FechaVenta >= hoyUtc && v.FechaVenta < mañanaUtc);

        var ventasAyerQuery = _context.Ventas
            .Where(v => v.FechaVenta >= ayerUtc && v.FechaVenta < hoyUtc);

        if (sucursalId.HasValue)
        {
            ventasHoyQuery = ventasHoyQuery.Where(v => v.SucursalId == sucursalId.Value);
            ventasAyerQuery = ventasAyerQuery.Where(v => v.SucursalId == sucursalId.Value);
        }

        // Scalars: SUM/COUNT/DISTINCT ejecutados en PostgreSQL
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

        // Ventas por hora: solo {FechaVenta, Total} — timezone conversion en C#
        var horaData = await ventasHoyQuery
            .Select(v => new { v.FechaVenta, v.Total })
            .ToListAsync();
        var ventasPorHora = horaData
            .GroupBy(v => TimeZoneInfo.ConvertTimeFromUtc(v.FechaVenta, colombiaTimeZone).Hour)
            .Select(g => new VentaPorHoraDto(g.Key, g.Sum(v => v.Total), g.Count()))
            .OrderBy(v => v.Hora)
            .ToList();

        // Top 5 productos: columnas mínimas, agrupación en C#
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
        var (fechaDesdeUtc, fechaHastaUtc) = NormalizarRangoUtc(fechaDesde, fechaHasta);

        var ventasQuery = _context.Ventas
            .Where(v => v.FechaVenta >= fechaDesdeUtc && v.FechaVenta <= fechaHastaUtc);

        if (sucursalId.HasValue)
            ventasQuery = ventasQuery.Where(v => v.SucursalId == sucursalId.Value);

        // GROUP BY en PostgreSQL via JOIN ventas→detalles
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

    public async Task<ReporteKardexDto> ObtenerKardexAsync(
        Guid productoId, int sucursalId, DateTime fechaDesde, DateTime fechaHasta)
    {
        var (fechaDesdeUtc, fechaHastaUtc) = NormalizarRangoUtc(fechaDesde, fechaHasta);

        var productoInfo = await _context.Productos
            .Where(p => p.Id == productoId)
            .Select(p => new { p.CodigoBarras, p.Nombre })
            .FirstOrDefaultAsync();

        var sucursalInfo = await _context.Sucursales
            .Where(s => s.Id == sucursalId)
            .Select(s => new { s.Nombre })
            .FirstOrDefaultAsync();

        if (productoInfo == null || sucursalInfo == null)
            throw new InvalidOperationException("Producto o Sucursal no encontrados.");

        var streamId = InventarioAggregate.GenerarStreamId(productoId, sucursalId);
        
        // Obtenemos todos los eventos del stream
        var eventosMarten = await _session.Events.FetchStreamAsync(streamId);

        decimal saldoAcumulado = 0;
        decimal saldoInicial = 0;
        decimal costoPromedioVigente = 0;
        var movimientos = new List<KardexMovimientoDto>();

        foreach (var martenEvent in eventosMarten.OrderBy(e => e.Timestamp))
        {
            var evtData = martenEvent.Data as BaseEvent;
            if (evtData == null) continue;

            var evtTimestamp = evtData.Timestamp;

            if (evtTimestamp < fechaDesdeUtc)
            {
                // Solo reconstruimos saldo y costo promedio *antes* del rango seleccionado
                if (evtData is EntradaCompraRegistrada ec) { saldoInicial += ec.Cantidad; costoPromedioVigente = RecalcularPromedio(saldoInicial - ec.Cantidad, costoPromedioVigente, ec.Cantidad, ec.CostoUnitario); }
                else if (evtData is SalidaVentaRegistrada sv) { saldoInicial -= sv.Cantidad; }
                else if (evtData is DevolucionProveedorRegistrada dp) { saldoInicial -= dp.Cantidad; }
                else if (evtData is AjusteInventarioRegistrado ai) { saldoInicial = ai.CantidadNueva; costoPromedioVigente = ai.CostoUnitario; }
                else if (evtData is TrasladoSalidaRegistrado ts) { saldoInicial -= ts.Cantidad; }
                else if (evtData is TrasladoEntradaRegistrado te) { saldoInicial += te.CantidadRecibida; costoPromedioVigente = RecalcularPromedio(saldoInicial - te.CantidadRecibida, costoPromedioVigente, te.CantidadRecibida, te.CostoUnitario); }

                saldoAcumulado = saldoInicial;
            }
            else if (evtTimestamp <= fechaHastaUtc)
            {
                // Es un movimiento ocurrido dentro del periodo, guardarlo en el Kardex
                string tipoMovimiento = "Desconocido";
                string referencia = "";
                string observaciones = "";
                decimal entrada = 0;
                decimal salida = 0;
                decimal costoUnitario = 0;
                decimal costoTotalMov = 0;

                if (evtData is EntradaCompraRegistrada ec)
                {
                    tipoMovimiento = "EntradaCompra";
                    referencia = ec.Referencia ?? "";
                    observaciones = ec.Observaciones ?? "";
                    entrada = ec.Cantidad;
                    costoUnitario = ec.CostoUnitario;
                    costoTotalMov = ec.CostoTotal;

                    costoPromedioVigente = RecalcularPromedio(saldoAcumulado, costoPromedioVigente, ec.Cantidad, ec.CostoUnitario);
                    saldoAcumulado += ec.Cantidad;
                }
                else if (evtData is SalidaVentaRegistrada sv)
                {
                    tipoMovimiento = "SalidaVenta";
                    referencia = sv.ReferenciaVenta ?? "";
                    salida = sv.Cantidad;
                    costoUnitario = sv.CostoUnitario;
                    costoTotalMov = sv.CostoTotal;
                    saldoAcumulado -= sv.Cantidad;
                }
                else if (evtData is DevolucionProveedorRegistrada dp)
                {
                    tipoMovimiento = "DevolucionCompra";
                    referencia = dp.Referencia ?? "";
                    salida = dp.Cantidad;
                    costoUnitario = dp.CostoUnitario;
                    costoTotalMov = dp.CostoTotal;
                    saldoAcumulado -= dp.Cantidad;
                }
                else if (evtData is AjusteInventarioRegistrado ai)
                {
                    tipoMovimiento = "Ajuste";
                    referencia = "Ajuste de Inventario";
                    observaciones = ai.Observaciones ?? "";

                    if (ai.EsPositivo) entrada = ai.Diferencia;
                    else salida = Math.Abs(ai.Diferencia);

                    costoUnitario = ai.CostoUnitario;
                    costoTotalMov = ai.CostoTotal;
                    saldoAcumulado = ai.CantidadNueva;
                    costoPromedioVigente = ai.CostoUnitario;
                }
                else if (evtData is TrasladoSalidaRegistrado ts)
                {
                    tipoMovimiento = "TrasladoSalida";
                    referencia = ts.NumeroTraslado ?? "";
                    salida = ts.Cantidad;
                    costoUnitario = ts.CostoUnitario;
                    costoTotalMov = ts.CostoTotal;
                    saldoAcumulado -= ts.Cantidad;
                }
                else if (evtData is TrasladoEntradaRegistrado te)
                {
                    tipoMovimiento = "TrasladoEntrada";
                    referencia = te.NumeroTraslado ?? "";
                    entrada = te.CantidadRecibida;
                    costoUnitario = te.CostoUnitario;
                    costoTotalMov = te.CostoTotal;

                    costoPromedioVigente = RecalcularPromedio(saldoAcumulado, costoPromedioVigente, te.CantidadRecibida, te.CostoUnitario);
                    saldoAcumulado += te.CantidadRecibida;
                }

                if (tipoMovimiento != "Desconocido")
                {
                    movimientos.Add(new KardexMovimientoDto(
                        evtTimestamp,
                        tipoMovimiento,
                        referencia,
                        observaciones,
                        entrada,
                        salida,
                        saldoAcumulado,
                        costoUnitario,
                        costoTotalMov
                    ));
                }
            }
        }

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarKardex",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Kardex de {productoInfo.CodigoBarras} consultado. Saldo Final: {saldoAcumulado}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: productoId.ToString(),
            EntidadNombre: "Kardex Inventario",
            DatosNuevos: new { productoId, sucursalId, fechaDesde, fechaHasta, saldoInicial, saldoFinal = saldoAcumulado }
        ));

        return new ReporteKardexDto(
            productoId,
            productoInfo.CodigoBarras,
            productoInfo.Nombre,
            sucursalId,
            sucursalInfo.Nombre,
            fechaDesdeUtc,
            fechaHastaUtc,
            saldoInicial,
            saldoAcumulado,
            costoPromedioVigente,
            movimientos
        );
    }

    private decimal RecalcularPromedio(decimal cantAnterior, decimal costoAnterior, decimal cantNueva, decimal costoNuevo)
    {
        var totalCant = cantAnterior + cantNueva;
        if (totalCant <= 0) return costoNuevo;
        return ((cantAnterior * costoAnterior) + (cantNueva * costoNuevo)) / totalCant;
    }

    private static (DateTime desde, DateTime hasta) NormalizarRangoUtc(DateTime fechaDesde, DateTime fechaHasta) =>
        (DateTime.SpecifyKind(fechaDesde.Date, DateTimeKind.Utc),
         DateTime.SpecifyKind(fechaHasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));
}
