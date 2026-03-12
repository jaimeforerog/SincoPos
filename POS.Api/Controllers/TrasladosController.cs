using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TrasladosController : ControllerBase
{
    private readonly ITrasladoService _trasladoService;
    private readonly AppDbContext _context;
    private readonly ILogger<TrasladosController> _logger;

    public TrasladosController(
        ITrasladoService trasladoService,
        AppDbContext context,
        ILogger<TrasladosController> logger)
    {
        _trasladoService = trasladoService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crear un traslado entre sucursales en estado Pendiente.
    /// </summary>
    /// <remarks>
    /// Flujo de estados: <c>Pendiente → Enviado → Recibido</c> o <c>Pendiente → Cancelado</c> o <c>Enviado → Rechazado</c>.<br/>
    /// El stock de origen se descuenta al <em>Enviar</em>, no al crear.
    /// </remarks>
    /// <response code="200">Traslado creado en estado Pendiente.</response>
    /// <response code="400">Sucursal origen = destino, stock insuficiente u otro error.</response>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(TrasladoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CrearTraslado(
        CrearTrasladoDto dto,
        [FromServices] IValidator<CrearTrasladoDto> validator)
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

        var (resultado, error) = await _trasladoService.CrearTrasladoAsync(dto);
        return error != null ? Problem(detail: error, statusCode: StatusCodes.Status400BadRequest) : Ok(resultado);
    }

    /// <summary>
    /// Enviar traslado (Pendiente → Enviado). Consume el stock en la sucursal de origen vía Event Sourcing.
    /// </summary>
    /// <response code="200">Traslado enviado. Stock de origen decrementado.</response>
    /// <response code="404">Traslado no encontrado.</response>
    /// <response code="400">Stock insuficiente o estado incorrecto.</response>
    [HttpPost("{id:int}/enviar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> EnviarTraslado(int id)
    {
        var (success, error) = await _trasladoService.EnviarTrasladoAsync(id);
        if (!success) return error == "NOT_FOUND" ? NotFound() : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        return Ok(new { mensaje = $"Traslado enviado exitosamente" });
    }

    /// <summary>
    /// Recibir traslado (Enviado → Recibido). Ingresa el stock en la sucursal de destino vía Event Sourcing.
    /// Las cantidades recibidas pueden diferir de las solicitadas (recepción parcial).
    /// </summary>
    /// <response code="200">Traslado recibido. Stock de destino incrementado.</response>
    /// <response code="404">Traslado no encontrado.</response>
    /// <response code="400">Estado incorrecto (no está Enviado).</response>
    [HttpPost("{id:int}/recibir")]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RecibirTraslado(
        int id,
        [FromBody] RecibirTrasladoDto dto,
        [FromServices] IValidator<RecibirTrasladoDto> validator)
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

        var emailUsuario = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var (success, error) = await _trasladoService.RecibirTrasladoAsync(id, dto, emailUsuario);
        if (!success) return error == "NOT_FOUND" ? NotFound() : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        return Ok(new { mensaje = "Traslado recibido exitosamente" });
    }

    /// <summary>
    /// Rechazar traslado (Enviado → Rechazado). Revierte el stock en la sucursal de origen.
    /// </summary>
    /// <response code="200">Traslado rechazado. Stock de origen revertido.</response>
    /// <response code="404">Traslado no encontrado.</response>
    /// <response code="400">Estado incorrecto (no está Enviado).</response>
    [HttpPost("{id:int}/rechazar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RechazarTraslado(
        int id,
        [FromBody] RechazarTrasladoDto dto,
        [FromServices] IValidator<RechazarTrasladoDto> validator)
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

        var (success, error) = await _trasladoService.RechazarTrasladoAsync(id, dto);
        if (!success) return error == "NOT_FOUND" ? NotFound() : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        return Ok(new { mensaje = "Traslado rechazado y stock revertido" });
    }

    /// <summary>
    /// Cancelar traslado (Pendiente → Cancelado). Solo es posible antes de enviar.
    /// </summary>
    /// <response code="200">Traslado cancelado.</response>
    /// <response code="404">Traslado no encontrado.</response>
    /// <response code="400">Traslado ya enviado, recibido u otro estado que no permite cancelación.</response>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CancelarTraslado(
        int id,
        [FromBody] CancelarTrasladoDto dto,
        [FromServices] IValidator<CancelarTrasladoDto> validator)
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

        var (success, error) = await _trasladoService.CancelarTrasladoAsync(id, dto);
        if (!success) return error == "NOT_FOUND" ? NotFound() : Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        return Ok(new { mensaje = "Traslado cancelado" });
    }

    /// <summary>
    /// Listar traslados con filtros opcionales. Máximo 50 resultados.
    /// </summary>
    /// <param name="estado">Pendiente=0, Enviado=1, Recibido=2, Rechazado=3, Cancelado=4.</param>
    /// <param name="page">Número de página (default 1).</param>
    /// <param name="pageSize">Tamaño de página (default 50, máx 100).</param>
    [HttpGet]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(PaginatedResult<TrasladoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<TrasladoDto>>> ListarTraslados(
        [FromQuery] int? sucursalOrigenId,
        [FromQuery] int? sucursalDestinoId,
        [FromQuery] EstadoTraslado? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var query = _context.Traslados
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Detalles)
            .AsQueryable();

        if (sucursalOrigenId.HasValue)
            query = query.Where(t => t.SucursalOrigenId == sucursalOrigenId.Value);
        if (sucursalDestinoId.HasValue)
            query = query.Where(t => t.SucursalDestinoId == sucursalDestinoId.Value);
        if (estado.HasValue)
            query = query.Where(t => t.Estado == estado.Value);
        if (desde.HasValue)
            query = query.Where(t => t.FechaTraslado >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(t => t.FechaTraslado <= hasta.Value);

        var totalCount = await query.CountAsync();
        var traslados = await query
            .OrderByDescending(t => t.FechaTraslado)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var usuariosDict = await CargarUsuariosAsync(traslados.Select(t => t.RecibidoPorUsuarioId));
        var items = traslados.Select(t => MapToDto(t, usuariosDict)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Ok(new PaginatedResult<TrasladoDto>(items, totalCount, page, pageSize, totalPages));
    }

    /// <summary>Obtener detalle de un traslado incluyendo todas sus líneas.</summary>
    /// <response code="200">TrasladoDto completo con detalles.</response>
    /// <response code="404">Traslado no encontrado.</response>
    [HttpGet("{id:int}")]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(TrasladoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrasladoDto>> ObtenerTraslado(int id)
    {
        var traslado = await _context.Traslados
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return NotFound();

        var usuariosDict = await CargarUsuariosAsync([traslado.RecibidoPorUsuarioId]);
        return Ok(MapToDto(traslado, usuariosDict));
    }

    private async Task<Dictionary<int, string?>> CargarUsuariosAsync(IEnumerable<int?> ids)
    {
        var usuarioIds = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (usuarioIds.Count == 0) return new Dictionary<int, string?>();
        return await _context.Usuarios
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (string?)u.Email);
    }

    private static TrasladoDto MapToDto(Traslado t, Dictionary<int, string?> usuarios) => new(
        Id: t.Id,
        NumeroTraslado: t.NumeroTraslado,
        SucursalOrigenId: t.SucursalOrigenId,
        NombreSucursalOrigen: t.SucursalOrigen.Nombre,
        SucursalDestinoId: t.SucursalDestinoId,
        NombreSucursalDestino: t.SucursalDestino.Nombre,
        Estado: t.Estado.ToString(),
        FechaTraslado: t.FechaTraslado,
        FechaEnvio: t.FechaEnvio,
        FechaRecepcion: t.FechaRecepcion,
        RecibidoPor: t.RecibidoPorUsuarioId.HasValue
            ? usuarios.GetValueOrDefault(t.RecibidoPorUsuarioId.Value)
            : null,
        Observaciones: t.Observaciones,
        MotivoRechazo: t.MotivoRechazo,
        Detalles: t.Detalles.Select(d => new DetalleTrasladoDto(
            Id: d.Id,
            ProductoId: d.ProductoId,
            NombreProducto: d.NombreProducto,
            CantidadSolicitada: d.CantidadSolicitada,
            CantidadRecibida: d.CantidadRecibida,
            CostoUnitario: d.CostoUnitario,
            CostoTotal: d.CostoTotal,
            Observaciones: d.Observaciones,
            NumeroLote: d.NumeroLote,
            FechaVencimiento: d.FechaVencimiento?.ToString("yyyy-MM-dd")
        )).ToList()
    );
}
