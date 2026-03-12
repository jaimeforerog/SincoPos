using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class CajasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CajasController> _logger;
    private readonly POS.Infrastructure.Services.UsuarioService _usuarioService;
    private readonly IActivityLogService _activityLogService;

    public CajasController(
        AppDbContext context,
        ILogger<CajasController> logger,
        POS.Infrastructure.Services.UsuarioService usuarioService,
        IActivityLogService activityLogService)
    {
        _context = context;
        _logger = logger;
        _usuarioService = usuarioService;
        _activityLogService = activityLogService;
    }

    /// <summary>
    /// Crear una nueva caja en una sucursal. El nombre debe ser único dentro de la sucursal.
    /// </summary>
    /// <response code="201">Caja creada en estado Cerrada.</response>
    /// <response code="400">Sucursal inexistente o validación fallida.</response>
    /// <response code="409">Ya existe una caja con ese nombre en la sucursal.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CajaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CajaDto>> CrearCaja(
        CrearCajaDto dto,
        [FromServices] IValidator<CrearCajaDto> validator)
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

        // Verificar que la sucursal existe
        var sucursal = await _context.Sucursales
            .IgnoreQueryFilters() // Permitir crear caja en sucursal si se conoce el ID (aunque esté inactiva?)
            .FirstOrDefaultAsync(s => s.Id == dto.SucursalId);

        if (sucursal == null)
            return Problem(detail: $"La sucursal {dto.SucursalId} no existe.", statusCode: StatusCodes.Status400BadRequest);

        // Verificar nombre unico dentro de la sucursal
        var existeNombre = await _context.Cajas
            .AnyAsync(c => c.SucursalId == dto.SucursalId && c.Nombre == dto.Nombre);

        if (existeNombre)
            return Problem(detail: $"Ya existe la caja '{dto.Nombre}' en esta sucursal.", statusCode: StatusCodes.Status409Conflict);

        var caja = new Caja
        {
            Nombre = dto.Nombre,
            SucursalId = dto.SucursalId,
            Estado = EstadoCaja.Cerrada,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Cajas.Add(caja);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Caja creada. Id: {Id}, Nombre: {Nombre}, Sucursal: {SucursalId}",
            caja.Id, caja.Nombre, caja.SucursalId);

        return CreatedAtAction(nameof(ObtenerCaja), new { id = caja.Id },
            MapToDto(caja, sucursal.Nombre));
    }

    /// <summary>
    /// Obtener una caja por ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CajaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CajaDto>> ObtenerCaja(int id)
    {
        var caja = await _context.Cajas
            .IgnoreQueryFilters() // Permitir ver por ID incluso si está inactiva
            .Include(c => c.Sucursal) // Se hereda el IgnoreQueryFilters de la raíz
            .FirstOrDefaultAsync(c => c.Id == id);

        if (caja == null)
            return Problem(detail: $"Caja {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        return Ok(MapToDto(caja, caja.Sucursal.Nombre));
    }

    /// <summary>
    /// Listar cajas. Por defecto solo muestra cajas activas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CajaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CajaDto>>> ObtenerCajas(
        [FromQuery] int? sucursalId = null,
        [FromQuery] string? estado = null,
        [FromQuery] bool incluirInactivas = false)
    {
        var query = incluirInactivas
            ? _context.Cajas.IgnoreQueryFilters().Include(c => c.Sucursal).AsQueryable()
            : _context.Cajas.Include(c => c.Sucursal).Where(c => c.Activo).AsQueryable();

        if (sucursalId.HasValue)
            query = query.Where(c => c.SucursalId == sucursalId.Value);

        if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoCaja>(estado, true, out var estadoCaja))
            query = query.Where(c => c.Estado == estadoCaja);

        var cajas = await query
            .OrderBy(c => c.Sucursal.Nombre)
            .ThenBy(c => c.Nombre)
            .Select(c => MapToDto(c, c.Sucursal.Nombre))
            .ToListAsync();

        return Ok(cajas);
    }

    /// <summary>
    /// Obtener las cajas abiertas de la sucursal asignada al usuario autenticado.
    /// Útil para que el cajero seleccione su caja al iniciar el POS.
    /// </summary>
    [HttpGet("mis-abiertas")]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(List<CajaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<CajaDto>>> ObtenerMisCajasAbiertas()
    {
        // Obtener el usuario autenticado
        var externalId = User.GetExternalId();
        var email = User.GetEmail();

        if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(email))
            return Problem(detail: "Usuario no autenticado correctamente", statusCode: StatusCodes.Status400BadRequest);

        // Buscar el usuario en la base de datos
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.KeycloakId == externalId || u.Email == email);

        if (usuario == null || !usuario.SucursalDefaultId.HasValue)
            return Ok(new List<CajaDto>()); // Usuario sin sucursal asignada = sin cajas

        // Obtener cajas abiertas de la sucursal del usuario
        var cajas = await _context.Cajas
            .Include(c => c.Sucursal)
            .Where(c => c.SucursalId == usuario.SucursalDefaultId.Value
                     && c.Estado == EstadoCaja.Abierta
                     && c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => MapToDto(c, c.Sucursal.Nombre))
            .ToListAsync();

        return Ok(cajas);
    }

    /// <summary>
    /// Abrir una caja (iniciar turno). Registra el monto de apertura y el usuario que abre.
    /// </summary>
    /// <response code="200">Caja abierta exitosamente.</response>
    /// <response code="404">Caja no encontrada.</response>
    /// <response code="409">Caja ya está abierta.</response>
    /// <response code="400">Caja inactiva u otro error.</response>
    [HttpPost("{id:int}/abrir")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AbrirCaja(
        int id,
        AbrirCajaDto dto,
        [FromServices] IValidator<AbrirCajaDto> validator)
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

        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return Problem(detail: $"Caja {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        if (caja.Estado == EstadoCaja.Abierta)
            return Problem(detail: "La caja ya esta abierta.", statusCode: StatusCodes.Status409Conflict);

        if (!caja.Activo)
            return Problem(detail: "No se puede abrir una caja inactiva.", statusCode: StatusCodes.Status400BadRequest);

        caja.Estado = EstadoCaja.Abierta;
        caja.MontoApertura = dto.MontoApertura;
        caja.MontoActual = dto.MontoApertura;
        caja.FechaApertura = DateTime.UtcNow;
        caja.FechaCierre = null;

        // Obtener usuario autenticado de Keycloak
        var externalId = User.GetExternalId();
        var email = User.GetEmail();
        var nombreCompleto = User.GetNombreCompleto();
        var roles = User.GetRoles().ToList();
        var rol = roles.FirstOrDefault() ?? "vendedor";

        if (!string.IsNullOrEmpty(externalId) && !string.IsNullOrEmpty(email))
        {
            var usuario = await _usuarioService.ObtenerOCrearUsuarioEntityAsync(
                externalId, email, nombreCompleto, rol);
            caja.AbiertaPorUsuarioId = usuario.Id;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Caja {Id} abierta con monto {Monto}.", id, dto.MontoApertura);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AperturaCaja",
            Tipo: TipoActividad.Caja,
            Descripcion: $"Caja '{caja.Nombre}' abierta con ${dto.MontoApertura:N2}",
            SucursalId: caja.SucursalId,
            TipoEntidad: "Caja",
            EntidadId: id.ToString(),
            EntidadNombre: caja.Nombre,
            DatosNuevos: new
            {
                Estado = "Abierta",
                MontoApertura = dto.MontoApertura,
                FechaApertura = caja.FechaApertura,
                AbiertaPorUsuarioId = caja.AbiertaPorUsuarioId
            }
        ));

        return Ok(new { mensaje = $"Caja {id} abierta exitosamente.", montoApertura = dto.MontoApertura });
    }

    /// <summary>
    /// Cerrar una caja (fin de turno con cuadre).
    /// Retorna la diferencia entre el monto real contado y el monto esperado (apertura + ventas efectivo).
    /// </summary>
    /// <response code="200">Caja cerrada. Retorna montoEsperado, montoReal, diferencia y cuadra (bool).</response>
    /// <response code="404">Caja no encontrada.</response>
    /// <response code="409">Caja ya está cerrada.</response>
    [HttpPost("{id:int}/cerrar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CerrarCaja(
        int id,
        CerrarCajaDto dto,
        [FromServices] IValidator<CerrarCajaDto> validator)
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

        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return Problem(detail: $"Caja {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        if (caja.Estado == EstadoCaja.Cerrada)
            return Problem(detail: "La caja ya esta cerrada.", statusCode: StatusCodes.Status409Conflict);

        var diferencia = dto.MontoReal - caja.MontoActual;
        var montoEsperado = caja.MontoActual;

        caja.Estado = EstadoCaja.Cerrada;
        caja.FechaCierre = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Caja {Id} cerrada. Esperado: {Esperado}, Real: {Real}, Diferencia: {Diff}.",
            id, montoEsperado, dto.MontoReal, diferencia);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CierreCaja",
            Tipo: TipoActividad.Caja,
            Descripcion: $"Caja '{caja.Nombre}' cerrada. Esperado: ${montoEsperado:N2}, Real: ${dto.MontoReal:N2}, Diferencia: ${diferencia:N2}",
            SucursalId: caja.SucursalId,
            TipoEntidad: "Caja",
            EntidadId: id.ToString(),
            EntidadNombre: caja.Nombre,
            DatosAnteriores: new
            {
                Estado = "Abierta",
                MontoActual = montoEsperado,
                MontoApertura = caja.MontoApertura
            },
            DatosNuevos: new
            {
                Estado = "Cerrada",
                MontoReal = dto.MontoReal,
                Diferencia = diferencia,
                Cuadra = diferencia == 0,
                FechaCierre = caja.FechaCierre,
                Observaciones = dto.Observaciones
            }
        ));

        return Ok(new
        {
            mensaje = $"Caja {id} cerrada exitosamente.",
            montoEsperado,
            montoReal = dto.MontoReal,
            diferencia,
            cuadra = diferencia == 0,
            observaciones = dto.Observaciones
        });
    }

    /// <summary>
    /// Desactivar una caja (soft delete). No se puede desactivar una caja abierta.
    /// </summary>
    /// <response code="204">Desactivada exitosamente.</response>
    /// <response code="404">Caja no encontrada.</response>
    /// <response code="409">Caja abierta — debe cerrarse primero.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DesactivarCaja(int id)
    {
        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return Problem(detail: $"Caja {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        if (caja.Estado == EstadoCaja.Abierta)
            return Problem(detail: "No se puede desactivar una caja abierta. Cierre la caja primero.", statusCode: StatusCodes.Status409Conflict);

        caja.Activo = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Caja {Id} desactivada.", id);
        return NoContent();
    }

    private static CajaDto MapToDto(Caja c, string? nombreSucursal) => new(
        c.Id, c.Nombre, c.SucursalId, nombreSucursal,
        c.Estado.ToString(), c.MontoApertura, c.MontoActual,
        c.FechaApertura, c.FechaCierre, c.Activo);
}
