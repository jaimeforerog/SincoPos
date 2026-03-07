using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ComprasController : ControllerBase
{
    private readonly ICompraService _compraService;
    private readonly AppDbContext _context;
    private readonly ILogger<ComprasController> _logger;

    public ComprasController(
        ICompraService compraService,
        AppDbContext context,
        ILogger<ComprasController> logger)
    {
        _compraService = compraService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crear orden de compra en estado Pendiente.
    /// </summary>
    /// <remarks>
    /// Flujo de estados: <c>Pendiente → Aprobada → Recibida</c> o <c>Pendiente → Rechazada/Cancelada</c>.<br/>
    /// Cada línea puede incluir <c>impuestoId</c> O <c>porcentajeImpuesto</c> (no ambos).
    /// </remarks>
    /// <response code="200">Orden creada en estado Pendiente.</response>
    /// <response code="400">Proveedor inexistente, líneas vacías u otro error.</response>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(OrdenCompraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrdenCompraDto>> CrearOrden(
        CrearOrdenCompraDto dto,
        [FromServices] IValidator<CrearOrdenCompraDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var (orden, error) = await _compraService.CrearOrdenAsync(dto);
        return error != null ? BadRequest(new { error }) : Ok(orden);
    }

    /// <summary>
    /// Listar órdenes de compra con filtros opcionales. Máximo 100 resultados.
    /// </summary>
    /// <param name="estado">Pendiente=0, Aprobada=1, Recibida=2, Rechazada=3, Cancelada=4.</param>
    [HttpGet]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(IEnumerable<OrdenCompraDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrdenCompraDto>>> ListarOrdenes(
        [FromQuery] int? sucursalId,
        [FromQuery] int? proveedorId,
        [FromQuery] EstadoOrdenCompra? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int limite = 50)
    {
        var query = _context.OrdenesCompra
            .Include(o => o.Sucursal)
            .Include(o => o.Proveedor)
            .Include(o => o.Detalles)
            .AsQueryable();

        if (sucursalId.HasValue) query = query.Where(o => o.SucursalId == sucursalId.Value);
        if (proveedorId.HasValue) query = query.Where(o => o.ProveedorId == proveedorId.Value);
        if (estado.HasValue) query = query.Where(o => o.Estado == estado.Value);
        if (desde.HasValue) query = query.Where(o => o.FechaOrden >= desde.Value);
        if (hasta.HasValue) query = query.Where(o => o.FechaOrden <= hasta.Value);

        var ordenes = await query
            .OrderByDescending(o => o.FechaOrden)
            .Take(Math.Min(limite, 100))
            .ToListAsync();

        var usuarioIds = ordenes
            .SelectMany(o => new[] { o.AprobadoPorUsuarioId, o.RecibidoPorUsuarioId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var usuariosDict = usuarioIds.Count > 0
            ? await _context.Usuarios
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => (string?)u.Email)
            : new Dictionary<int, string?>();

        return Ok(ordenes.Select(o => CompraService.MapearOrdenCompraDtoSync(o, usuariosDict)).ToList());
    }

    /// <summary>Obtener detalle de una orden de compra incluyendo líneas y estado de aprobación.</summary>
    /// <response code="200">OrdenCompraDto completo.</response>
    /// <response code="404">Orden no encontrada.</response>
    [HttpGet("{id:int}")]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(OrdenCompraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenCompraDto>> ObtenerOrden(int id)
    {
        var orden = await _context.OrdenesCompra
            .Include(o => o.Sucursal)
            .Include(o => o.Proveedor)
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orden == null)
            return NotFound(new { error = "Orden de compra no encontrada" });

        return Ok(await CompraService.MapearOrdenCompraDtoAsync(orden, _context));
    }

    /// <summary>Aprobar orden de compra (Pendiente → Aprobada).</summary>
    /// <response code="200">Orden aprobada.</response>
    /// <response code="404">Orden no encontrada.</response>
    /// <response code="400">Orden no está en estado Pendiente.</response>
    [HttpPost("{id:int}/aprobar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AprobarOrden(int id, [FromBody] AprobarOrdenCompraDto? dto)
    {
        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (success, error) = await _compraService.AprobarOrdenAsync(id, dto, email);
        if (!success) return error == "NOT_FOUND" ? NotFound(new { error }) : BadRequest(new { error });
        return Ok(new { mensaje = "Orden de compra aprobada exitosamente" });
    }

    /// <summary>Rechazar orden de compra (Pendiente → Rechazada). Requiere motivo.</summary>
    /// <response code="200">Orden rechazada.</response>
    /// <response code="404">Orden no encontrada.</response>
    /// <response code="400">Orden no está en estado Pendiente.</response>
    [HttpPost("{id:int}/rechazar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RechazarOrden(
        int id,
        [FromBody] RechazarOrdenCompraDto dto,
        [FromServices] IValidator<RechazarOrdenCompraDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var (success, error) = await _compraService.RechazarOrdenAsync(id, dto);
        if (!success) return error == "NOT_FOUND" ? NotFound(new { error }) : BadRequest(new { error });
        return Ok(new { mensaje = "Orden de compra rechazada" });
    }

    /// <summary>
    /// Recibir mercancía de una orden aprobada (Aprobada → Recibida).
    /// Registra entradas al inventario vía Event Sourcing para cada línea recibida.
    /// </summary>
    /// <response code="200">Mercancía recibida. Stock actualizado.</response>
    /// <response code="404">Orden no encontrada.</response>
    /// <response code="400">Orden no está en estado Aprobada.</response>
    [HttpPost("{id:int}/recibir")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RecibirOrden(
        int id,
        [FromBody] RecibirOrdenCompraDto dto,
        [FromServices] IValidator<RecibirOrdenCompraDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (success, error) = await _compraService.RecibirOrdenAsync(id, dto, email);
        if (!success) return error == "NOT_FOUND" ? NotFound(new { error }) : BadRequest(new { error });
        return Ok(new { mensaje = "Orden de compra recibida exitosamente" });
    }

    /// <summary>Cancelar una orden de compra (Pendiente o Aprobada → Cancelada).</summary>
    /// <response code="200">Orden cancelada.</response>
    /// <response code="404">Orden no encontrada.</response>
    /// <response code="400">Orden ya recibida o en estado que no permite cancelación.</response>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CancelarOrden(
        int id,
        [FromBody] CancelarOrdenCompraDto dto,
        [FromServices] IValidator<CancelarOrdenCompraDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var (success, error) = await _compraService.CancelarOrdenAsync(id, dto);
        if (!success) return error == "NOT_FOUND" ? NotFound(new { error }) : BadRequest(new { error });
        return Ok(new { mensaje = "Orden de compra cancelada" });
    }
}
