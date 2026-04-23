using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class InventarioController : ControllerBase
{
    private readonly IInventarioService _inventarioService;
    private readonly AppDbContext _context;
    private readonly Marten.IDocumentSession _session;
    private readonly ILogger<InventarioController> _logger;

    public InventarioController(
        IInventarioService inventarioService,
        AppDbContext context,
        Marten.IDocumentSession session,
        ILogger<InventarioController> logger)
    {
        _inventarioService = inventarioService;
        _context = context;
        _session = session;
        _logger = logger;
    }

    /// <summary>
    /// Registrar entrada de mercancía (compra a proveedor).
    /// Emite evento <c>EntradaCompraRegistrada</c> al Event Store y actualiza el stock
    /// usando el método de costeo configurado (FIFO/LIFO/Promedio).
    /// </summary>
    /// <response code="200">Entrada registrada. Retorna el estado actualizado del stock.</response>
    /// <response code="400">Validación fallida (proveedor inexistente, cantidad ≤ 0, etc.).</response>
    [HttpPost("entrada")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RegistrarEntrada(
        EntradaInventarioDto dto,
        [FromServices] IValidator<EntradaInventarioDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (resultado, error) = await _inventarioService.RegistrarEntradaAsync(dto, email);
        return error != null ? Problem(detail: error, statusCode: StatusCodes.Status400BadRequest) : Ok(resultado);
    }

    /// <summary>
    /// Registrar devolución de mercancía a proveedor. Descuenta stock del lote correspondiente.
    /// </summary>
    /// <response code="200">Devolución registrada.</response>
    /// <response code="400">Stock insuficiente u otro error de negocio.</response>
    [HttpPost("devolucion-proveedor")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DevolucionProveedor(
        DevolucionProveedorDto dto,
        [FromServices] IValidator<DevolucionProveedorDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (resultado, error) = await _inventarioService.DevolucionProveedorAsync(dto, email);
        return error != null ? Problem(detail: error, statusCode: StatusCodes.Status400BadRequest) : Ok(resultado);
    }

    /// <summary>
    /// Ajustar inventario manualmente (conteo físico). La diferencia puede ser positiva o negativa.
    /// </summary>
    /// <response code="200">Ajuste registrado.</response>
    /// <response code="400">Producto/sucursal inexistente u otro error.</response>
    [HttpPost("ajuste")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AjustarInventario(
        AjusteInventarioDto dto,
        [FromServices] IValidator<AjusteInventarioDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (resultado, error) = await _inventarioService.AjustarInventarioAsync(dto, email);
        return error != null ? Problem(detail: error, statusCode: StatusCodes.Status400BadRequest) : Ok(resultado);
    }

    /// <summary>
    /// Actualizar stock mínimo de un producto en una sucursal.
    /// El sistema alertará cuando el stock actual sea ≤ este valor.
    /// </summary>
    /// <response code="200">Stock mínimo actualizado.</response>
    /// <response code="404">No existe stock para este producto en esta sucursal.</response>
    [HttpPut("stock-minimo")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ActualizarStockMinimo(
        [FromQuery] Guid productoId,
        [FromQuery] int sucursalId,
        [FromQuery] decimal stockMinimo)
    {
        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (success, error) = await _inventarioService.ActualizarStockMinimoAsync(productoId, sucursalId, stockMinimo, email);
        if (!success) return error == "NOT_FOUND" ? Problem(detail: "No existe inventario para este producto en esta sucursal.", statusCode: StatusCodes.Status404NotFound) : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        return Ok(new { mensaje = "Stock minimo actualizado.", stockMinimo });
    }

    // ─── Endpoints de lectura ───

    /// <summary>
    /// Consultar stock actual (por sucursal, producto o ambos).
    /// </summary>
    /// <param name="sucursalId">Filtrar por sucursal. Null = todas.</param>
    /// <param name="productoId">Filtrar por producto específico.</param>
    /// <param name="soloConStock">Si true, excluye registros con cantidad = 0.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<StockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StockDto>>> ObtenerStock(
        [FromQuery] int? sucursalId = null,
        [FromQuery] Guid? productoId = null,
        [FromQuery] bool soloConStock = false)
    {
        var query = _context.Stock
            .Include(s => s.Producto)
            .Include(s => s.Sucursal)
            .AsQueryable();

        if (sucursalId.HasValue)
            query = query.Where(s => s.SucursalId == sucursalId.Value);
        if (productoId.HasValue)
            query = query.Where(s => s.ProductoId == productoId.Value);
        if (soloConStock)
            query = query.Where(s => s.Cantidad > 0);

        var stock = await query
            .OrderBy(s => s.Sucursal.Nombre)
            .ThenBy(s => s.Producto.Nombre)
            .Select(s => new StockDto(
                s.Id, s.ProductoId, s.Producto.Nombre, s.Producto.CodigoBarras,
                s.SucursalId, s.Sucursal.Nombre,
                s.Cantidad, s.StockMinimo, s.CostoPromedio, s.UltimaActualizacion))
            .ToListAsync();

        return Ok(stock);
    }

    /// <summary>
    /// Stock proyectado a una fecha de corte pasada.
    /// Calcula: stockActual + ventasPostFecha + reversaDeltaMovimientos.
    /// Usado por el POS cuando se registran ventas con fecha retroactiva.
    /// </summary>
    [HttpGet("historico")]
    [ProducesResponseType(typeof(List<StockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StockDto>>> ObtenerStockHistorico(
        [FromQuery] int sucursalId,
        [FromQuery] DateTime fecha)
    {
        var fechaUtc = DateTime.SpecifyKind(fecha, DateTimeKind.Utc);

        // 1. Stock actual de la sucursal
        var stockActual = await _context.Stock
            .Include(s => s.Producto)
            .Include(s => s.Sucursal)
            .Where(s => s.SucursalId == sucursalId)
            .ToListAsync();

        // 2. Ventas no anuladas con fecha efectiva DESPUÉS del corte → reversar (sumar de vuelta)
        var ventasDelta = await _context.DetalleVentas
            .Where(dv => dv.Venta.SucursalId == sucursalId
                      && dv.Venta.FechaVenta > fechaUtc
                      && dv.Venta.Estado != EstadoVenta.Anulada)
            .GroupBy(dv => dv.ProductoId)
            .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(dv => dv.Cantidad) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Cantidad);

        // 3. Movimientos (compras, traslados, ajustes, devoluciones proveedor) DESPUÉS del corte
        var movimientosDespues = await _context.MovimientosInventario
            .Where(m => m.SucursalId == sucursalId && m.FechaMovimiento > fechaUtc)
            .GroupBy(m => new { m.ProductoId, m.TipoMovimiento })
            .Select(g => new { g.Key.ProductoId, g.Key.TipoMovimiento, Cantidad = g.Sum(m => m.Cantidad) })
            .ToListAsync();

        var movDelta = new Dictionary<Guid, decimal>();
        foreach (var mov in movimientosDespues)
        {
            // Para cada movimiento ocurrido DESPUÉS del corte, lo reversamos:
            // entradas (compras, traslados+, ajustes+) → restar; salidas (devoluciones proveedor, traslados-, ajustes-) → sumar
            var delta = mov.TipoMovimiento switch
            {
                TipoMovimiento.EntradaCompra       => -mov.Cantidad,
                TipoMovimiento.AjustePositivo      => -mov.Cantidad,
                TipoMovimiento.TransferenciaEntrada => -mov.Cantidad,
                TipoMovimiento.DevolucionProveedor  =>  mov.Cantidad,
                TipoMovimiento.AjusteNegativo       =>  mov.Cantidad,
                TipoMovimiento.TransferenciaSalida  =>  mov.Cantidad,
                _ => 0m
            };
            movDelta[mov.ProductoId] = movDelta.GetValueOrDefault(mov.ProductoId, 0) + delta;
        }

        // 4. Construir respuesta con cantidades históricas (mínimo 0)
        var resultado = stockActual.Select(s =>
        {
            var ventasReversa = ventasDelta.GetValueOrDefault(s.ProductoId, 0);
            var movReversa    = movDelta.GetValueOrDefault(s.ProductoId, 0);
            var cantidadHistorica = Math.Max(0, s.Cantidad + ventasReversa + movReversa);

            return new StockDto(
                s.Id, s.ProductoId, s.Producto.Nombre, s.Producto.CodigoBarras,
                s.SucursalId, s.Sucursal.Nombre,
                cantidadHistorica, s.StockMinimo, s.CostoPromedio, s.UltimaActualizacion);
        }).ToList();

        return Ok(resultado);
    }

    /// <summary>
    /// Productos con stock por debajo del mínimo configurado. Usado por el dashboard de alertas.
    /// </summary>
    [HttpGet("alertas")]
    [ProducesResponseType(typeof(List<AlertaStockDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AlertaStockDto>>> ObtenerAlertas(
        [FromQuery] int? sucursalId = null)
    {
        var query = _context.Stock
            .Include(s => s.Producto)
            .Include(s => s.Sucursal)
            .Where(s => s.Cantidad <= s.StockMinimo && s.Producto.Activo)
            .AsQueryable();

        if (sucursalId.HasValue)
            query = query.Where(s => s.SucursalId == sucursalId.Value);

        var alertas = await query
            .OrderBy(s => s.Cantidad)
            .Select(s => new AlertaStockDto(
                s.ProductoId, s.Producto.Nombre, s.Producto.CodigoBarras,
                s.SucursalId, s.Sucursal.Nombre,
                s.Cantidad, s.StockMinimo))
            .ToListAsync();

        return Ok(alertas);
    }

    /// <summary>
    /// Historial de movimientos de inventario desde el Event Store de Marten.
    /// </summary>
    /// <remarks>
    /// Tipos de movimiento posibles: <c>EntradaCompra</c>, <c>DevolucionProveedor</c>,
    /// <c>AjustePositivo</c>, <c>AjusteNegativo</c>, <c>SalidaVenta</c>,
    /// <c>StockMinimoActualizado</c>, <c>TrasladoSalida</c>, <c>TrasladoEntrada</c>.<br/>
    /// Los movimientos provienen del Event Store (Marten), no de la tabla EF Core.
    /// </remarks>
    [HttpGet("movimientos")]
    [ProducesResponseType(typeof(PaginatedResult<MovimientoInventarioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<MovimientoInventarioDto>>> ObtenerMovimientos(
        [FromQuery] int? sucursalId = null,
        [FromQuery] Guid? productoId = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var desde = (fechaDesde ?? DateTime.UtcNow.AddDays(-30)).ToUniversalTime();
        var hasta = (fechaHasta ?? DateTime.UtcNow).ToUniversalTime().AddDays(1).AddTicks(-1);

        var stockQuery = _context.Stock.AsQueryable();
        if (sucursalId.HasValue)
            stockQuery = stockQuery.Where(s => s.SucursalId == sucursalId.Value);
        if (productoId.HasValue)
            stockQuery = stockQuery.Where(s => s.ProductoId == productoId.Value);

        var stockRecords = await stockQuery.ToListAsync();
        var movimientos = new List<MovimientoInventarioDto>();

        // Batch-load products and sucursales in 2 queries to avoid N+1
        var productoIds = stockRecords.Select(s => s.ProductoId).Distinct().ToList();
        var sucursalIds = stockRecords.Select(s => s.SucursalId).Distinct().ToList();

        var productosDict = await _context.Productos
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nombre);

        var sucursalesDict = await _context.Sucursales
            .Where(s => sucursalIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Nombre);

        foreach (var sr in stockRecords)
        {
            var streamId = InventarioAggregate.GenerarStreamId(sr.ProductoId, sr.SucursalId);
            var events = await _session.Events.FetchStreamAsync(streamId);

            var prodNombre = productosDict.GetValueOrDefault(sr.ProductoId, "");
            var sucNombre = sucursalesDict.GetValueOrDefault(sr.SucursalId, "");

            foreach (var e in events.Where(e => e.Timestamp >= desde && e.Timestamp <= hasta))
            {
                MovimientoInventarioDto? mov = null;
                switch (e.Data)
                {
                    case POS.Domain.Events.Inventario.EntradaCompraRegistrada entrada:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, entrada.ProductoId, prodNombre,
                            entrada.SucursalId, sucNombre,
                            "EntradaCompra", entrada.Cantidad, entrada.CostoUnitario,
                            entrada.CostoTotal, entrada.PorcentajeImpuesto, entrada.MontoImpuesto,
                            entrada.Referencia, entrada.Observaciones,
                            entrada.TerceroId, entrada.NombreTercero,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.EntradaManualRegistrada manual:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, manual.ProductoId, prodNombre,
                            manual.SucursalId, sucNombre,
                            "EntradaManual", manual.Cantidad, manual.CostoUnitario,
                            manual.CostoTotal, manual.PorcentajeImpuesto, manual.MontoImpuesto,
                            manual.Referencia, manual.Observaciones,
                            manual.TerceroId, manual.NombreTercero,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.DevolucionProveedorRegistrada devolucion:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, devolucion.ProductoId, prodNombre,
                            devolucion.SucursalId, sucNombre,
                            "DevolucionProveedor", devolucion.Cantidad, devolucion.CostoUnitario,
                            devolucion.CostoTotal, 0, 0,
                            devolucion.Referencia, devolucion.Observaciones,
                            devolucion.TerceroId, devolucion.NombreTercero,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.AjusteInventarioRegistrado ajuste:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, ajuste.ProductoId, prodNombre,
                            ajuste.SucursalId, sucNombre,
                            ajuste.EsPositivo ? "AjustePositivo" : "AjusteNegativo",
                            Math.Abs(ajuste.Diferencia), ajuste.CostoUnitario,
                            ajuste.CostoTotal, 0, 0,
                            null, ajuste.Observaciones,
                            null, null,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.SalidaVentaRegistrada salida:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, salida.ProductoId, prodNombre,
                            salida.SucursalId, sucNombre,
                            "SalidaVenta", salida.Cantidad, salida.CostoUnitario,
                            salida.CostoTotal, salida.PorcentajeImpuesto, salida.MontoImpuesto,
                            salida.ReferenciaVenta, null,
                            null, null,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.StockMinimoActualizado minimo:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, minimo.ProductoId, prodNombre,
                            minimo.SucursalId, sucNombre,
                            "StockMinimoActualizado", 0, 0, 0, 0, 0, null,
                            $"Stock minimo: {minimo.StockMinimoAnterior} → {minimo.StockMinimoNuevo}",
                            null, null,
                            e.Timestamp.UtcDateTime);
                        break;
                }

                if (mov != null)
                    movimientos.Add(mov);
            }
        }

        var sorted = movimientos.OrderByDescending(m => m.FechaMovimiento).ToList();
        var ps = Math.Clamp(pageSize, 1, 500);
        var pg = Math.Max(1, page);
        var totalCount = sorted.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / ps);
        var items = sorted.Skip((pg - 1) * ps).Take(ps).ToList();

        return Ok(new PaginatedResult<MovimientoInventarioDto>(items, totalCount, pg, ps, totalPages));
    }

}
