using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventarioController : ControllerBase
{
    private readonly Marten.IDocumentSession _session;
    private readonly AppDbContext _context;
    private readonly ILogger<InventarioController> _logger;
    private readonly IActivityLogService _activityLogService;

    public InventarioController(
        Marten.IDocumentSession session,
        AppDbContext context,
        ILogger<InventarioController> logger,
        IActivityLogService activityLogService)
    {
        _session = session;
        _context = context;
        _logger = logger;
        _activityLogService = activityLogService;
    }

    /// <summary>
    /// Registrar entrada de mercancia (compra a proveedor)
    /// </summary>
    [HttpPost("entrada")]
    [Authorize(Policy = "Supervisor")]
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
            return BadRequest(new { errors });
        }

        // Verificar que producto existe
        var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == dto.ProductoId);
        if (producto == null)
            return BadRequest(new { error = "Producto no encontrado." });

        // Verificar sucursal
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return BadRequest(new { error = "Sucursal no encontrada." });

        // Verificar proveedor si se proporciona
        string? nombreTercero = null;
        if (dto.TerceroId.HasValue)
        {
            var tercero = await _context.Terceros.FindAsync(dto.TerceroId.Value);
            if (tercero == null)
                return BadRequest(new { error = "Tercero (proveedor) no encontrado." });
            nombreTercero = tercero.Nombre;
        }

        // Stream ID deterministic por producto+sucursal
        var streamId = InventarioAggregate.GenerarStreamId(dto.ProductoId, dto.SucursalId);

        // Intentar cargar aggregate existente o crear nuevo
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        // Calcular monto del impuesto
        var costoTotal = dto.Cantidad * dto.CostoUnitario;
        var montoImpuesto = costoTotal * (dto.PorcentajeImpuesto / 100m);

        object evento;
        if (aggregate == null)
        {
            var (newAggregate, newEvento) = InventarioAggregate.RegistrarEntrada(
                streamId, dto.ProductoId, dto.SucursalId,
                dto.Cantidad, dto.CostoUnitario,
                dto.PorcentajeImpuesto, montoImpuesto,
                dto.TerceroId, nombreTercero,
                dto.Referencia, dto.Observaciones,
                usuarioId: 1, sucursalUsuarioId: dto.SucursalId);
            aggregate = newAggregate;
            evento = newEvento;
            _session.Events.StartStream<InventarioAggregate>(streamId, evento);
        }
        else
        {
            evento = aggregate.AgregarEntrada(
                dto.Cantidad, dto.CostoUnitario,
                dto.TerceroId, nombreTercero,
                dto.Referencia, dto.Observaciones,
                usuarioId: 1);
            _session.Events.Append(streamId, evento);
        }

        await _session.SaveChangesAsync();

        _logger.LogInformation(
            "Entrada registrada via Event Sourcing. Producto: {ProductoId}, Sucursal: {SucursalId}, Cantidad: {Cantidad}",
            dto.ProductoId, dto.SucursalId, dto.Cantidad);

        // Leer stock actualizado (la projection ya lo actualizo)
        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId && s.SucursalId == dto.SucursalId);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "EntradaInventario",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Entrada de mercancía: {producto.Nombre} x {dto.Cantidad}. Costo unitario: ${dto.CostoUnitario:N2}. Proveedor: {nombreTercero ?? "N/A"}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Inventario",
            EntidadId: $"{dto.ProductoId}_{dto.SucursalId}",
            EntidadNombre: producto.Nombre,
            DatosNuevos: new
            {
                ProductoId = dto.ProductoId,
                NombreProducto = producto.Nombre,
                Cantidad = dto.Cantidad,
                CostoUnitario = dto.CostoUnitario,
                CostoTotal = costoTotal,
                PorcentajeImpuesto = dto.PorcentajeImpuesto,
                MontoImpuesto = montoImpuesto,
                TerceroId = dto.TerceroId,
                NombreTercero = nombreTercero,
                Referencia = dto.Referencia,
                Observaciones = dto.Observaciones,
                StockActual = stock?.Cantidad ?? dto.Cantidad,
                CostoPromedio = stock?.CostoPromedio ?? dto.CostoUnitario
            }
        ));

        return Ok(new
        {
            mensaje = "Entrada de mercancia registrada.",
            metodoCosteo = sucursal.MetodoCosteo.ToString(),
            stockActual = stock?.Cantidad ?? dto.Cantidad,
            costoPromedio = stock?.CostoPromedio ?? dto.CostoUnitario,
            eventosTotales = aggregate.Lotes.Count
        });
    }

    /// <summary>
    /// Registrar devolucion de mercancia a proveedor
    /// </summary>
    [HttpPost("devolucion-proveedor")]
    [Authorize(Policy = "Supervisor")]
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
            return BadRequest(new { errors });
        }

        var tercero = await _context.Terceros.FindAsync(dto.TerceroId);
        if (tercero == null)
            return BadRequest(new { error = "Proveedor no encontrado." });

        var streamId = InventarioAggregate.GenerarStreamId(dto.ProductoId, dto.SucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        if (aggregate == null)
            return BadRequest(new { error = "No existe inventario para este producto en esta sucursal." });

        try
        {
            var evento = aggregate.RegistrarDevolucion(
                dto.Cantidad, dto.TerceroId,
                tercero.Nombre, dto.Referencia,
                dto.Observaciones, usuarioId: 1);

            _session.Events.Append(streamId, evento);
            await _session.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId && s.SucursalId == dto.SucursalId);

        var producto = await _context.Productos.FindAsync(dto.ProductoId);

        _logger.LogInformation(
            "Devolucion registrada via Event Sourcing. Producto: {ProductoId}, Proveedor: {Proveedor}, Cantidad: {Cantidad}",
            dto.ProductoId, tercero.Nombre, dto.Cantidad);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DevolucionProveedor",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Devolución a proveedor: {producto?.Nombre ?? "Producto"} x {dto.Cantidad}. Proveedor: {tercero.Nombre}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Inventario",
            EntidadId: $"{dto.ProductoId}_{dto.SucursalId}",
            EntidadNombre: producto?.Nombre,
            DatosNuevos: new
            {
                ProductoId = dto.ProductoId,
                NombreProducto = producto?.Nombre,
                CantidadDevuelta = dto.Cantidad,
                TerceroId = dto.TerceroId,
                NombreTercero = tercero.Nombre,
                Referencia = dto.Referencia,
                Observaciones = dto.Observaciones,
                StockActual = stock?.Cantidad ?? 0,
                CostoPromedio = stock?.CostoPromedio ?? 0
            }
        ));

        return Ok(new
        {
            mensaje = "Devolucion a proveedor registrada.",
            cantidadDevuelta = dto.Cantidad,
            stockActual = stock?.Cantidad ?? 0,
            costoPromedio = stock?.CostoPromedio ?? 0
        });
    }

    /// <summary>
    /// Ajustar inventario manualmente (conteo fisico)
    /// </summary>
    [HttpPost("ajuste")]
    [Authorize(Policy = "Supervisor")]
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
            return BadRequest(new { errors });
        }

        var streamId = InventarioAggregate.GenerarStreamId(dto.ProductoId, dto.SucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        decimal cantidadAnterior;

        if (aggregate == null)
        {
            // Primera vez — crear stream con una entrada de ajuste
            cantidadAnterior = 0;
            var ajusteEvento = new POS.Domain.Events.Inventario.AjusteInventarioRegistrado
            {
                ProductoId = dto.ProductoId,
                SucursalId = dto.SucursalId,
                CantidadAnterior = 0,
                CantidadNueva = dto.CantidadNueva,
                Diferencia = dto.CantidadNueva,
                EsPositivo = true,
                CostoUnitario = 0,
                CostoTotal = 0,
                Observaciones = dto.Observaciones ?? $"Ajuste manual inicial: 0 → {dto.CantidadNueva}",
                UsuarioId = 1
            };
            _session.Events.StartStream<InventarioAggregate>(streamId, ajusteEvento);
        }
        else
        {
            cantidadAnterior = aggregate.Cantidad;
            try
            {
                var evento = aggregate.RegistrarAjuste(dto.CantidadNueva, dto.Observaciones, usuarioId: 1);
                _session.Events.Append(streamId, evento);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        await _session.SaveChangesAsync();

        var producto = await _context.Productos.FindAsync(dto.ProductoId);
        var diferencia = dto.CantidadNueva - cantidadAnterior;

        _logger.LogInformation(
            "Ajuste registrado via Event Sourcing. Producto: {ProductoId}, Anterior: {Anterior}, Nuevo: {Nuevo}",
            dto.ProductoId, cantidadAnterior, dto.CantidadNueva);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AjusteInventario",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Ajuste de inventario: {producto?.Nombre ?? "Producto"}. De {cantidadAnterior} a {dto.CantidadNueva} (Diferencia: {diferencia:+#;-#;0}). Motivo: {dto.Observaciones ?? "No especificado"}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Inventario",
            EntidadId: $"{dto.ProductoId}_{dto.SucursalId}",
            EntidadNombre: producto?.Nombre,
            DatosAnteriores: new
            {
                CantidadAnterior = cantidadAnterior
            },
            DatosNuevos: new
            {
                ProductoId = dto.ProductoId,
                NombreProducto = producto?.Nombre,
                CantidadNueva = dto.CantidadNueva,
                Diferencia = diferencia,
                Observaciones = dto.Observaciones
            }
        ));

        return Ok(new
        {
            mensaje = "Ajuste de inventario registrado.",
            cantidadAnterior,
            cantidadNueva = dto.CantidadNueva,
            diferencia
        });
    }

    /// <summary>
    /// Actualizar stock minimo de un producto en una sucursal
    /// </summary>
    [HttpPut("stock-minimo")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> ActualizarStockMinimo(
        [FromQuery] Guid productoId,
        [FromQuery] int sucursalId,
        [FromQuery] decimal stockMinimo)
    {
        if (stockMinimo < 0)
            return BadRequest(new { error = "El stock minimo no puede ser negativo." });

        var streamId = InventarioAggregate.GenerarStreamId(productoId, sucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        if (aggregate == null)
            return NotFound(new { error = "No existe inventario para este producto en esta sucursal." });

        try
        {
            var evento = aggregate.ActualizarStockMinimo(stockMinimo, usuarioId: 1);
            _session.Events.Append(streamId, evento);
            await _session.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        _logger.LogInformation("Stock minimo actualizado via Event Sourcing. Producto: {P}, Sucursal: {S}, Minimo: {M}",
            productoId, sucursalId, stockMinimo);

        return Ok(new { mensaje = "Stock minimo actualizado.", stockMinimo });
    }

    // ─── Endpoints de lectura (EF Core + Marten queries) ───

    /// <summary>
    /// Consultar stock actual (por sucursal o todos)
    /// </summary>
    [HttpGet]
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
    /// Productos con stock por debajo del minimo
    /// </summary>
    [HttpGet("alertas")]
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
    /// Historial de movimientos de inventario (desde Event Store de Marten)
    /// </summary>
    [HttpGet("movimientos")]
    public async Task<ActionResult<List<MovimientoInventarioDto>>> ObtenerMovimientos(
        [FromQuery] int? sucursalId = null,
        [FromQuery] Guid? productoId = null,
        [FromQuery] int limite = 50)
    {
        // Buscar streams de inventario relevantes desde la tabla stock
        var stockQuery = _context.Stock.AsQueryable();
        if (sucursalId.HasValue)
            stockQuery = stockQuery.Where(s => s.SucursalId == sucursalId.Value);
        if (productoId.HasValue)
            stockQuery = stockQuery.Where(s => s.ProductoId == productoId.Value);

        var stockRecords = await stockQuery.ToListAsync();

        var movimientos = new List<MovimientoInventarioDto>();

        foreach (var sr in stockRecords)
        {
            var streamId = InventarioAggregate.GenerarStreamId(sr.ProductoId, sr.SucursalId);
            var events = await _session.Events.FetchStreamAsync(streamId);

            var prod = await _context.Productos.FindAsync(sr.ProductoId);
            var suc = await _context.Sucursales.FindAsync(sr.SucursalId);

            foreach (var e in events.OrderByDescending(e => e.Timestamp))
            {
                MovimientoInventarioDto? mov = null;
                switch (e.Data)
                {
                    case POS.Domain.Events.Inventario.EntradaCompraRegistrada entrada:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, entrada.ProductoId, prod?.Nombre ?? "",
                            entrada.SucursalId, suc?.Nombre ?? "",
                            "EntradaCompra", entrada.Cantidad, entrada.CostoUnitario,
                            entrada.CostoTotal, entrada.PorcentajeImpuesto, entrada.MontoImpuesto,
                            entrada.Referencia, entrada.Observaciones,
                            entrada.TerceroId, entrada.NombreTercero,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.DevolucionProveedorRegistrada devolucion:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, devolucion.ProductoId, prod?.Nombre ?? "",
                            devolucion.SucursalId, suc?.Nombre ?? "",
                            "DevolucionProveedor", devolucion.Cantidad, devolucion.CostoUnitario,
                            devolucion.CostoTotal, 0, 0, // Devs usually reverse tax, simplifying to 0 for now
                            devolucion.Referencia, devolucion.Observaciones,
                            devolucion.TerceroId, devolucion.NombreTercero,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.AjusteInventarioRegistrado ajuste:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, ajuste.ProductoId, prod?.Nombre ?? "",
                            ajuste.SucursalId, suc?.Nombre ?? "",
                            ajuste.EsPositivo ? "AjustePositivo" : "AjusteNegativo",
                            Math.Abs(ajuste.Diferencia), ajuste.CostoUnitario,
                            ajuste.CostoTotal, 0, 0, 
                            null, ajuste.Observaciones,
                            null, null,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.SalidaVentaRegistrada salida:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, salida.ProductoId, prod?.Nombre ?? "",
                            salida.SucursalId, suc?.Nombre ?? "",
                            "SalidaVenta", salida.Cantidad, salida.CostoUnitario,
                            salida.CostoTotal, salida.PorcentajeImpuesto, salida.MontoImpuesto,
                            salida.ReferenciaVenta, null,
                            null, null,
                            e.Timestamp.UtcDateTime);
                        break;

                    case POS.Domain.Events.Inventario.StockMinimoActualizado minimo:
                        mov = new MovimientoInventarioDto(
                            (int)e.Version, minimo.ProductoId, prod?.Nombre ?? "",
                            minimo.SucursalId, suc?.Nombre ?? "",
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

        return Ok(movimientos
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(limite)
            .ToList());
    }

    /// <summary>
    /// [DEBUG] Consultar lotes de inventario
    /// </summary>
    [HttpGet("lotes")]
    public async Task<ActionResult> ObtenerLotes(
        [FromQuery] Guid? productoId = null,
        [FromQuery] int? sucursalId = null)
    {
        var query = _context.LotesInventario.AsQueryable();

        if (productoId.HasValue)
            query = query.Where(l => l.ProductoId == productoId.Value);

        if (sucursalId.HasValue)
            query = query.Where(l => l.SucursalId == sucursalId.Value);

        var lotes = await query
            .OrderBy(l => l.FechaEntrada)
            .Select(l => new
            {
                l.Id,
                l.ProductoId,
                l.SucursalId,
                l.CantidadInicial,
                l.CantidadDisponible,
                l.CostoUnitario,
                l.Referencia,
                l.FechaEntrada
            })
            .ToListAsync();

        return Ok(lotes);
    }
}
