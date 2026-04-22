using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Validators;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class VentasController : ControllerBase
{
    private readonly IVentaService _ventaService;
    private readonly AppDbContext _context;
    private readonly ILogger<VentasController> _logger;

    public VentasController(
        IVentaService ventaService,
        AppDbContext context,
        ILogger<VentasController> logger)
    {
        _ventaService = ventaService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crear una venta completa.
    /// Valida caja abierta, resuelve precios (cascada sucursal → producto → margen),
    /// aplica TaxEngine, consume stock via Event Sourcing y registra la venta.
    /// </summary>
    /// <remarks>
    /// El campo <c>precioUnitario</c> en cada línea es opcional. Si se omite (null),
    /// el sistema resuelve el precio configurado para la sucursal.<br/>
    /// <c>metodoPago</c>: 0 = Efectivo, 1 = Tarjeta, 2 = Transferencia.<br/>
    /// <c>montoPagado</c> debe ser &gt;= total de la venta.
    /// </remarks>
    /// <response code="200">Venta creada exitosamente con su VentaDto completo.</response>
    /// <response code="400">Caja cerrada, stock insuficiente, precio inválido u otro error de negocio.</response>
    [HttpPost]
    [Authorize(Policy = "Cajero")]
    [EnableRateLimiting("ventas")]
    [ProducesResponseType(typeof(VentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<VentaDto>> CrearVenta([FromBody] CrearVentaDto dto)
    {
        var validResult = await new CrearVentaValidator().ValidateAsync(dto);
        if (!validResult.IsValid)
        {
            var errors = validResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var (venta, error) = await _ventaService.CrearVentaAsync(dto);
        return error != null ? Problem(detail: error, statusCode: StatusCodes.Status400BadRequest) : Ok(venta);
    }

    /// <summary>
    /// Obtener detalle de una venta por ID.
    /// </summary>
    /// <response code="200">VentaDto con líneas de detalle.</response>
    /// <response code="404">Venta no encontrada.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VentaDto>> ObtenerVenta(int id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles).ThenInclude(d => d.Lotes)
            .Include(v => v.Sucursal)
            .Include(v => v.Caja)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return NotFound();

        return Ok(VentaService.MapToDto(venta, venta.Sucursal.Nombre, venta.Caja.Nombre, venta.Cliente?.Nombre));
    }

    /// <summary>
    /// Listar ventas con filtros opcionales y paginación.
    /// </summary>
    /// <param name="sucursalId">Filtrar por sucursal. Null = todas las sucursales.</param>
    /// <param name="cajaId">Filtrar por caja específica.</param>
    /// <param name="clienteId">Filtrar por cliente (tercero).</param>
    /// <param name="desde">Fecha de inicio del período (UTC).</param>
    /// <param name="hasta">Fecha fin del período (UTC).</param>
    /// <param name="estado">0 = Completada, 1 = Anulada, 2 = Devuelta. Null = todos.</param>
    /// <param name="page">Número de página (default 1).</param>
    /// <param name="pageSize">Tamaño de página (default 50, máx 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<VentaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<VentaDto>>> ListarVentas(
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? cajaId = null,
        [FromQuery] int? clienteId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] EstadoVenta? estado = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var query = _context.Ventas
            .Include(v => v.Detalles).ThenInclude(d => d.Lotes)
            .Include(v => v.Sucursal)
            .Include(v => v.Caja)
            .Include(v => v.Cliente)
            .AsQueryable();

        if (sucursalId.HasValue) query = query.Where(v => v.SucursalId == sucursalId.Value);
        if (cajaId.HasValue) query = query.Where(v => v.CajaId == cajaId.Value);
        if (clienteId.HasValue) query = query.Where(v => v.ClienteId == clienteId.Value);
        if (desde.HasValue) query = query.Where(v => v.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(v => v.FechaVenta <= hasta.Value);
        if (estado.HasValue) query = query.Where(v => v.Estado == estado.Value);

        var totalCount = await query.CountAsync();
        var ventas = await query
            .OrderByDescending(v => v.FechaVenta)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = ventas.Select(v =>
            VentaService.MapToDto(v, v.Sucursal.Nombre, v.Caja.Nombre, v.Cliente?.Nombre)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Ok(new PaginatedResult<VentaDto>(items, totalCount, page, pageSize, totalPages));
    }

    /// <summary>
    /// Anular una venta. Revierte el stock consumido via Event Sourcing y descuenta de la caja.
    /// </summary>
    /// <param name="id">ID de la venta a anular.</param>
    /// <param name="motivo">Motivo opcional de la anulación (queda en el log de auditoría).</param>
    /// <response code="200">Venta anulada. Stock revertido.</response>
    /// <response code="404">Venta no encontrada.</response>
    /// <response code="400">Venta ya anulada u otro error de negocio.</response>
    [HttpPost("{id:int}/anular")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AnularVenta(int id, [FromQuery] string? motivo = null)
    {
        var (success, error) = await _ventaService.AnularVentaAsync(id, motivo);
        if (!success)
        {
            if (error == "NOT_FOUND") return NotFound();
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(new { mensaje = "Venta anulada", stockRevertido = true });
    }

    /// <summary>
    /// Resumen agregado de ventas: totales, costo, ganancia y margen por período/sucursal.
    /// Solo incluye ventas en estado Completada.
    /// </summary>
    [HttpGet("resumen")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(ResumenVentaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumenVentaDto>> ObtenerResumen(
        [FromQuery] int? sucursalId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        var query = _context.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.Estado == EstadoVenta.Completada)
            .AsQueryable();

        if (sucursalId.HasValue) query = query.Where(v => v.SucursalId == sucursalId.Value);
        if (desde.HasValue) query = query.Where(v => v.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(v => v.FechaVenta <= hasta.Value);

        var ventas = await query.ToListAsync();

        var totalVentas = ventas.Count;
        var montoTotal = ventas.Sum(v => v.Total);
        var costoTotal = ventas.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad));
        var gananciaTotal = montoTotal - costoTotal;
        var margenPromedio = montoTotal > 0 ? Math.Round(gananciaTotal / montoTotal * 100, 2) : 0;

        return Ok(new ResumenVentaDto(totalVentas, montoTotal, costoTotal, gananciaTotal, margenPromedio));
    }

    /// <summary>
    /// Conteo de ventas pendientes de sincronización con el ERP.
    /// Retorna cuántas ventas (o anulaciones) tienen outbox en estado Pendiente o Error.
    /// </summary>
    [HttpGet("erp/pendientes-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> ObtenerErpPendientesCount([FromQuery] int? sucursalId = null)
    {
        var query = _context.ErpOutboxMessages
            .Where(m => (m.TipoDocumento == "VentaCompletada" || m.TipoDocumento == "AnulacionVenta")
                     && (m.Estado == EstadoOutbox.Pendiente || m.Estado == EstadoOutbox.Error));

        if (sucursalId.HasValue)
        {
            var ventaIds = _context.Ventas
                .Where(v => v.SucursalId == sucursalId.Value)
                .Select(v => v.Id);
            query = query.Where(m => ventaIds.Contains(m.EntidadId));
        }

        var count = await query.CountAsync();
        return Ok(new { pendientes = count });
    }

    /// <summary>
    /// Reintentar manualmente la sincronización ERP de una venta.
    /// Reactiva los mensajes Outbox en estado Error o Descartado para que el background service los reprocese.
    /// </summary>
    /// <response code="200">Mensajes reactivados. El background service los procesará en los próximos 15 segundos.</response>
    /// <response code="404">No se encontraron mensajes ERP pendientes para esta venta.</response>
    [HttpPost("{id:int}/erp/reintentar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ReinentarSincronizacionErp(int id)
    {
        var mensajes = await _context.ErpOutboxMessages
            .Where(m => m.EntidadId == id
                     && (m.TipoDocumento == "VentaCompletada" || m.TipoDocumento == "AnulacionVenta")
                     && m.Estado != EstadoOutbox.Procesado)
            .ToListAsync();

        if (!mensajes.Any())
            return Problem(detail: "No se encontraron mensajes ERP pendientes para esta venta.", statusCode: StatusCodes.Status404NotFound);

        foreach (var m in mensajes)
        {
            m.Estado = EstadoOutbox.Pendiente;
            m.Intentos = 0;
            m.UltimoError = null;
        }

        var venta = await _context.Ventas.FindAsync(id);
        if (venta != null)
            venta.ErrorSincronizacion = null;

        await _context.SaveChangesAsync();
        _logger.LogInformation("ERP reintento manual activado para Venta {VentaId} ({Count} mensajes)", id, mensajes.Count);
        return Ok(new { mensaje = $"{mensajes.Count} mensaje(s) ERP reactivados. Se procesarán en los próximos 15 segundos." });
    }

    /// <summary>
    /// Crear devolución parcial de productos de una venta.
    /// Reintegra el stock vía Event Sourcing y ajusta el monto de la caja.
    /// Se permiten múltiples devoluciones sobre la misma venta hasta agotar las cantidades originales.
    /// </summary>
    /// <response code="200">Devolución registrada con DevolucionVentaDto completo.</response>
    /// <response code="404">Venta no encontrada.</response>
    /// <response code="400">Cantidades superan lo vendido, línea ya devuelta u otro error.</response>
    [HttpPost("{ventaId:int}/devolucion-parcial")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(DevolucionVentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DevolucionVentaDto>> CrearDevolucionParcial(
        int ventaId,
        [FromBody] CrearDevolucionParcialDto dto)
    {
        var validResult = await new CrearDevolucionParcialValidator().ValidateAsync(dto);
        if (!validResult.IsValid)
        {
            var errors = validResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var emailUsuario = User.FindFirst("email")?.Value ?? User.Identity?.Name;

        var (devolucion, error) = await _ventaService.CrearDevolucionParcialAsync(ventaId, dto, emailUsuario);
        if (devolucion == null)
        {
            if (error == "NOT_FOUND") return Problem(detail: "Venta no encontrada.", statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(devolucion);
    }

    /// <summary>
    /// Crear devolución usando detalleVentaId (alternativa a devolucion-parcial).
    /// Acepta { ventaId, motivo, lineas:[{ detalleVentaId, cantidadDevuelta }] }.
    /// </summary>
    [HttpPost("devoluciones")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(DevolucionVentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DevolucionVentaDto>> CrearDevolucion([FromBody] CrearDevolucionDto dto)
    {
        // Cargar la venta con sus detalles para resolver ProductoId desde DetalleVentaId
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == dto.VentaId);

        if (venta == null)
            return Problem(detail: "Venta no encontrada.", statusCode: StatusCodes.Status404NotFound);

        // Mapear detalleVentaId → productoId + cantidad
        var lineasMapeadas = new List<LineaDevolucionDto>();
        foreach (var linea in dto.Lineas)
        {
            var detalle = venta.Detalles.FirstOrDefault(d => d.Id == linea.DetalleVentaId);
            if (detalle == null)
                return Problem(detail: $"Detalle {linea.DetalleVentaId} no pertenece a esta venta.", statusCode: StatusCodes.Status400BadRequest);

            lineasMapeadas.Add(new LineaDevolucionDto(detalle.ProductoId, linea.CantidadDevuelta));
        }

        var dtoMapeado = new CrearDevolucionParcialDto(dto.Motivo, lineasMapeadas);
        var emailUsuario = User.FindFirst("email")?.Value ?? User.Identity?.Name;

        var (devolucion, error) = await _ventaService.CrearDevolucionParcialAsync(dto.VentaId, dtoMapeado, emailUsuario);
        if (devolucion == null)
        {
            if (error == "NOT_FOUND") return Problem(detail: "Venta no encontrada.", statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(devolucion);
    }

    /// <summary>
    /// Clientes distintos que tienen al menos una venta Completada en la sucursal indicada.
    /// Usado exclusivamente por el flujo de devoluciones para restringir la búsqueda
    /// a clientes que realmente compraron en esa sucursal.
    /// </summary>
    [HttpGet("clientes-con-ventas")]
    [ProducesResponseType(typeof(List<TerceroDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TerceroDto>>> ClientesConVentas(
        [FromQuery] int? sucursalId = null,
        [FromQuery] string? q = null)
    {
        var terceroQuery = _context.Terceros
            .Include(t => t.Actividades)
            .Where(t => t.Activo)
            .Where(t => _context.Ventas.Any(v =>
                v.ClienteId == t.Id
                && v.Estado == EstadoVenta.Completada
                && (sucursalId == null || v.SucursalId == sucursalId)));

        if (!string.IsNullOrWhiteSpace(q))
            terceroQuery = terceroQuery.Where(t =>
                t.Nombre.Contains(q) || t.Identificacion.Contains(q));

        var clientes = await terceroQuery
            .OrderBy(t => t.Nombre)
            .Take(50)
            .ToListAsync();

        var dtos = clientes.Select(t => new TerceroDto(
            t.Id,
            t.TipoIdentificacion.ToString(),
            t.Identificacion,
            t.DigitoVerificacion,
            t.Nombre,
            t.TipoTercero.ToString(),
            t.Telefono,
            t.Email,
            t.Direccion,
            t.Ciudad,
            t.CodigoDepartamento,
            t.CodigoMunicipio,
            t.PerfilTributario,
            t.EsGranContribuyente,
            t.EsAutorretenedor,
            t.EsResponsableIVA,
            t.OrigenDatos.ToString(),
            t.ExternalId,
            t.Activo,
            t.Actividades
                .Select(a => new TerceroActividadDto(a.Id, a.CodigoCIIU, a.Descripcion, a.EsPrincipal))
                .ToList()
        )).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Obtener todas las devoluciones de una venta específica.
    /// </summary>
    /// <response code="200">Lista de devoluciones (puede ser vacía si no hay devoluciones).</response>
    /// <response code="404">Venta no encontrada.</response>
    [HttpGet("{ventaId:int}/devoluciones")]
    [ProducesResponseType(typeof(List<DevolucionVentaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<DevolucionVentaDto>>> ObtenerDevolucionesPorVenta(int ventaId)
    {
        var venta = await _context.Ventas.FindAsync(ventaId);
        if (venta == null)
            return Problem(detail: "Venta no encontrada.", statusCode: StatusCodes.Status404NotFound);

        var devoluciones = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Where(d => d.VentaId == ventaId)
            .OrderByDescending(d => d.FechaDevolucion)
            .ToListAsync();

        // Batch-load all referenced users in one query to avoid N+1
        var usuarioIds = devoluciones
            .Where(d => d.AutorizadoPorUsuarioId.HasValue)
            .Select(d => d.AutorizadoPorUsuarioId!.Value)
            .Distinct()
            .ToList();

        var usuariosDict = usuarioIds.Count > 0
            ? await _context.Usuarios
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email)
            : new Dictionary<int, string>();

        var resultado = devoluciones
            .Select(d => VentaService.MapDevolucionToDto(
                d, venta.NumeroVenta,
                d.AutorizadoPorUsuarioId.HasValue
                    ? usuariosDict.GetValueOrDefault(d.AutorizadoPorUsuarioId.Value)
                    : null))
            .ToList();

        return Ok(resultado);
    }

    /// <summary>
    /// Obtener detalle de una devolución específica.
    /// </summary>
    /// <response code="200">DevolucionVentaDto con líneas y datos de autorización.</response>
    /// <response code="404">Devolución no encontrada.</response>
    [HttpGet("devoluciones/{devolucionId:int}")]
    [ProducesResponseType(typeof(DevolucionVentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DevolucionVentaDto>> ObtenerDevolucion(int devolucionId)
    {
        var devolucion = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Include(d => d.Venta)
            .FirstOrDefaultAsync(d => d.Id == devolucionId);

        if (devolucion == null)
            return Problem(detail: "Devolución no encontrada.", statusCode: StatusCodes.Status404NotFound);

        var autorizadoPor = devolucion.AutorizadoPorUsuarioId.HasValue
            ? await _context.Usuarios
                .Where(u => u.Id == devolucion.AutorizadoPorUsuarioId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync()
            : null;

        return Ok(VentaService.MapDevolucionToDto(devolucion, devolucion.Venta.NumeroVenta, autorizadoPor));
    }
}
