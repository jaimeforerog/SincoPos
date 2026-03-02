using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public ReportesController(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    /// <summary>
    /// Reporte de ventas por período
    /// </summary>
    [HttpGet("ventas")]
    public async Task<ActionResult<ReporteVentasDto>> ObtenerReporteVentas(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? metodoPago = null)
    {
        // Validar fechas
        if (fechaDesde > fechaHasta)
            return BadRequest("La fecha desde no puede ser mayor que la fecha hasta.");

        // Convertir a UTC para PostgreSQL (timestamp with time zone requiere UTC)
        var fechaDesdeUtc = DateTime.SpecifyKind(fechaDesde, DateTimeKind.Utc);
        var fechaHastaUtc = DateTime.SpecifyKind(fechaHasta, DateTimeKind.Utc);

        // Query base
        var ventasQuery = _context.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.FechaVenta >= fechaDesdeUtc && v.FechaVenta <= fechaHastaUtc);

        // Filtros opcionales
        if (sucursalId.HasValue)
            ventasQuery = ventasQuery.Where(v => v.SucursalId == sucursalId.Value);

        if (metodoPago.HasValue)
            ventasQuery = ventasQuery.Where(v => (int)v.MetodoPago == metodoPago.Value);

        var ventas = await ventasQuery.ToListAsync();

        // Calcular totales
        var totalVentas = ventas.Sum(v => v.Total);
        var cantidadVentas = ventas.Count;
        var costoTotal = ventas.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad));
        var utilidadTotal = totalVentas - costoTotal;
        var ticketPromedio = cantidadVentas > 0 ? totalVentas / cantidadVentas : 0;
        var margenPromedio = totalVentas > 0 ? (utilidadTotal / totalVentas) * 100 : 0;

        // Ventas por método de pago
        var ventasPorMetodo = ventas
            .GroupBy(v => v.MetodoPago.ToString())
            .Select(g => new VentaPorMetodoPagoDto(
                g.Key,
                g.Sum(v => v.Total),
                g.Count()
            ))
            .ToList();

        // Ventas por día
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
            totalVentas,
            cantidadVentas,
            ticketPromedio,
            costoTotal,
            utilidadTotal,
            margenPromedio,
            ventasPorMetodo,
            ventasPorDia
        );

        // Activity log
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

        return Ok(reporte);
    }

    /// <summary>
    /// Reporte de inventario valorizado
    /// </summary>
    [HttpGet("inventario-valorizado")]
    public async Task<ActionResult<ReporteInventarioValorizadoDto>> ObtenerInventarioValorizado(
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool soloConStock = false)
    {
        // Query base
        var stockQuery = _context.Stock
            .Include(s => s.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(s => s.Sucursal)
            .AsQueryable();

        // Filtros
        if (sucursalId.HasValue)
            stockQuery = stockQuery.Where(s => s.SucursalId == sucursalId.Value);

        if (categoriaId.HasValue)
            stockQuery = stockQuery.Where(s => s.Producto.CategoriaId == categoriaId.Value);

        if (soloConStock)
            stockQuery = stockQuery.Where(s => s.Cantidad > 0);

        var stock = await stockQuery.ToListAsync();

        // Mapear a DTOs
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

        // Totales
        var totalCosto = productos.Sum(p => p.CostoTotal);
        var totalVenta = productos.Sum(p => p.ValorVenta);
        var utilidadPotencial = productos.Sum(p => p.UtilidadPotencial);
        var totalProductos = productos.Count;
        var totalUnidades = productos.Sum(p => p.Cantidad);

        var reporte = new ReporteInventarioValorizadoDto(
            totalCosto,
            totalVenta,
            utilidadPotencial,
            totalProductos,
            totalUnidades,
            productos.OrderByDescending(p => p.CostoTotal).ToList()
        );

        // Activity log
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

        return Ok(reporte);
    }

    /// <summary>
    /// Reporte de movimientos de caja
    /// </summary>
    [HttpGet("caja/{cajaId}")]
    public async Task<ActionResult<ReporteCajaDto>> ObtenerReporteCaja(
        int cajaId,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        // Buscar caja
        var caja = await _context.Cajas
            .Include(c => c.Sucursal)
            .FirstOrDefaultAsync(c => c.Id == cajaId);

        if (caja == null)
            return NotFound($"Caja con ID {cajaId} no encontrada.");

        // Query de ventas
        var ventasQuery = _context.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Cliente)
            .Where(v => v.CajaId == cajaId);

        // Si la caja está abierta y no se especifican fechas, usar desde la apertura
        if (caja.Estado == EstadoCaja.Abierta)
        {
            fechaDesde ??= caja.FechaApertura;
            fechaHasta ??= DateTime.UtcNow;
        }

        // Convertir a UTC si se proporcionan fechas
        DateTime? fechaDesdeUtc = fechaDesde.HasValue
            ? DateTime.SpecifyKind(fechaDesde.Value, DateTimeKind.Utc)
            : null;
        DateTime? fechaHastaUtc = fechaHasta.HasValue
            ? DateTime.SpecifyKind(fechaHasta.Value, DateTimeKind.Utc)
            : null;

        // Filtrar por fechas si se proporcionan
        if (fechaDesdeUtc.HasValue)
            ventasQuery = ventasQuery.Where(v => v.FechaVenta >= fechaDesdeUtc.Value);

        if (fechaHastaUtc.HasValue)
            ventasQuery = ventasQuery.Where(v => v.FechaVenta <= fechaHastaUtc.Value);

        var ventas = await ventasQuery.OrderByDescending(v => v.FechaVenta).ToListAsync();

        // Calcular totales por método de pago
        var totalEfectivo = ventas
            .Where(v => v.MetodoPago == MetodoPago.Efectivo)
            .Sum(v => v.Total);

        var totalTarjeta = ventas
            .Where(v => v.MetodoPago == MetodoPago.Tarjeta)
            .Sum(v => v.Total);

        var totalTransferencia = ventas
            .Where(v => v.MetodoPago == MetodoPago.Transferencia)
            .Sum(v => v.Total);

        var totalVentas = ventas.Sum(v => v.Total);

        // Mapear ventas
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

        // Calcular diferencias (solo para cajas cerradas)
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
            caja.Id,
            caja.Nombre,
            caja.SucursalId,
            caja.Sucursal.Nombre,
            caja.FechaApertura ?? DateTime.MinValue,
            caja.FechaCierre,
            caja.MontoApertura,
            totalEfectivo,
            totalTarjeta,
            totalTransferencia,
            totalVentas,
            montoCierre,
            diferenciaEsperado,
            diferenciaReal,
            ventasDto
        );

        // Activity log
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

        return Ok(reporte);
    }
}
