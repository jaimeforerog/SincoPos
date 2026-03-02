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
[Route("api/[controller]")]
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
    /// Crear una nueva caja
    /// </summary>
    [HttpPost]
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
            return BadRequest(new { errors });
        }

        // Verificar que la sucursal existe
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return BadRequest(new { error = $"La sucursal {dto.SucursalId} no existe." });

        // Verificar nombre unico dentro de la sucursal
        var existeNombre = await _context.Cajas
            .AnyAsync(c => c.SucursalId == dto.SucursalId && c.Nombre == dto.Nombre);

        if (existeNombre)
            return Conflict(new { error = $"Ya existe la caja '{dto.Nombre}' en esta sucursal." });

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
    /// Obtener una caja por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CajaDto>> ObtenerCaja(int id)
    {
        var caja = await _context.Cajas
            .Include(c => c.Sucursal)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (caja == null)
            return NotFound(new { error = $"Caja {id} no encontrada." });

        return Ok(MapToDto(caja, caja.Sucursal.Nombre));
    }

    /// <summary>
    /// Listar cajas (opcionalmente filtrar por sucursal)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CajaDto>>> ObtenerCajas(
        [FromQuery] int? sucursalId = null,
        [FromQuery] bool incluirInactivas = false)
    {
        var query = _context.Cajas
            .Include(c => c.Sucursal)
            .AsQueryable();

        if (sucursalId.HasValue)
            query = query.Where(c => c.SucursalId == sucursalId.Value);

        if (!incluirInactivas)
            query = query.Where(c => c.Activo);

        var cajas = await query
            .OrderBy(c => c.Sucursal.Nombre)
            .ThenBy(c => c.Nombre)
            .Select(c => MapToDto(c, c.Sucursal.Nombre))
            .ToListAsync();

        return Ok(cajas);
    }

    /// <summary>
    /// Abrir una caja (iniciar turno)
    /// </summary>
    [HttpPost("{id:int}/abrir")]
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
            return BadRequest(new { errors });
        }

        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return NotFound(new { error = $"Caja {id} no encontrada." });

        if (caja.Estado == EstadoCaja.Abierta)
            return Conflict(new { error = "La caja ya esta abierta." });

        if (!caja.Activo)
            return BadRequest(new { error = "No se puede abrir una caja inactiva." });

        caja.Estado = EstadoCaja.Abierta;
        caja.MontoApertura = dto.MontoApertura;
        caja.MontoActual = dto.MontoApertura;
        caja.FechaApertura = DateTime.UtcNow;
        caja.FechaCierre = null;

        // Obtener usuario autenticado de Keycloak
        var keycloakId = User.GetKeycloakId();
        var email = User.GetEmail();
        var nombreCompleto = User.GetNombreCompleto();
        var roles = User.GetRoles().ToList();
        var rol = roles.FirstOrDefault() ?? "vendedor";

        if (!string.IsNullOrEmpty(keycloakId) && !string.IsNullOrEmpty(email))
        {
            var usuario = await _usuarioService.ObtenerOCrearUsuarioAsync(
                keycloakId, email, nombreCompleto, rol);
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
    /// Cerrar una caja (fin de turno con cuadre)
    /// </summary>
    [HttpPost("{id:int}/cerrar")]
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
            return BadRequest(new { errors });
        }

        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return NotFound(new { error = $"Caja {id} no encontrada." });

        if (caja.Estado == EstadoCaja.Cerrada)
            return Conflict(new { error = "La caja ya esta cerrada." });

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
    /// Desactivar una caja (soft delete)
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DesactivarCaja(int id)
    {
        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return NotFound(new { error = $"Caja {id} no encontrada." });

        if (caja.Estado == EstadoCaja.Abierta)
            return Conflict(new { error = "No se puede desactivar una caja abierta. Cierre la caja primero." });

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
