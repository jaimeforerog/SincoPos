using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;

namespace POS.Api.Controllers;

/// <summary>
/// Gestión de usuarios del sistema
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly ILogger<UsuariosController> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly AppDbContext _db;

    public UsuariosController(
        IUsuarioService usuarioService,
        ILogger<UsuariosController> logger,
        IActivityLogService activityLogService,
        AppDbContext db)
    {
        _usuarioService = usuarioService;
        _logger = logger;
        _activityLogService = activityLogService;
        _db = db;
    }

    /// <summary>
    /// Listar todos los usuarios con filtros opcionales
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<List<UsuarioDto>>> ListarUsuarios(
        [FromQuery] string? busqueda = null,
        [FromQuery] string? rol = null,
        [FromQuery] bool? activo = null,
        [FromQuery] int? sucursalId = null)
    {
        var dtos = await _usuarioService.ListarUsuariosAsync(
            busqueda, rol, activo, sucursalId);

        _logger.LogInformation(
            "Usuario {Email} listó {Count} usuarios con filtros: busqueda={Busqueda}, rol={Rol}, activo={Activo}",
            User.GetEmail(), dtos.Count, busqueda, rol, activo);

        return Ok(dtos);
    }

    /// <summary>
    /// Obtener perfil del usuario actual
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<PerfilUsuarioDto>> ObtenerPerfil()
    {
        var externalId = User.GetExternalId();
        if (string.IsNullOrEmpty(externalId))
            return Unauthorized("No se pudo identificar al usuario");

        var email = User.GetEmail() ?? "unknown@sincopos.com";
        var nombreCompleto = User.GetNombreCompleto() ?? email;
        var idpRoles = User.GetRoles().ToList();

        var rolPrincipal = idpRoles.Count > 0
            ? DeterminarRolPrincipal(idpRoles)
            : null;

        _logger.LogInformation(
            "ObtenerPerfil: externalId={Id}, roles=[{Roles}], rolDeterminado={Rol}",
            externalId, string.Join(",", idpRoles), rolPrincipal ?? "(sin rol)");

        await _usuarioService.ObtenerOCrearUsuarioAsync(externalId, email, nombreCompleto, rolPrincipal);

        var perfil = await _usuarioService.ObtenerPerfilPorExternalIdAsync(externalId);
        // Reintento: puede ocurrir en condición de carrera donde la creación aún no es visible
        if (perfil == null)
        {
            await Task.Delay(100);
            perfil = await _usuarioService.ObtenerPerfilPorExternalIdAsync(externalId);
        }
        if (perfil == null)
            return StatusCode(500, "Error al obtener perfil de usuario");

        var permisos = ObtenerPermisosPorRol(perfil.Rol);
        var sucursalesAsignadas = perfil.SucursalesAsignadas;

        // Si el usuario es admin o supervisor y no tiene sucursales asignadas,
        // darle acceso a todas las sucursales activas
        if (sucursalesAsignadas.Count == 0 &&
            (perfil.Rol.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
             perfil.Rol.Equals("supervisor", StringComparison.OrdinalIgnoreCase)))
        {
            sucursalesAsignadas = await _usuarioService.ObtenerTodasSucursalesActivasAsync();

            _logger.LogInformation(
                "Usuario {Email} ({Rol}) sin sucursales asignadas, usando todas las activas ({Count})",
                perfil.Email, perfil.Rol, sucursalesAsignadas.Count);
        }

        // Resolver empresa a partir de las sucursales asignadas
        var sucursalIds = sucursalesAsignadas.Select(s => s.Id).ToList();
        var empresaInfo = sucursalIds.Any()
            ? await _db.Sucursales
                .IgnoreQueryFilters()
                .Where(s => sucursalIds.Contains(s.Id) && s.EmpresaId != null)
                .Select(s => new { s.EmpresaId, s.Empresa!.Nombre })
                .FirstOrDefaultAsync()
            : null;

        // Empresas disponibles: para admin/supervisor incluir TODAS las empresas activas
        // (incluso las que no tienen sucursales aún, para que puedan crearlas).
        // Para otros roles, derivar únicamente de sus sucursales asignadas.
        List<EmpresaResumenDto> empresasDisponibles;
        if (perfil.Rol.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            perfil.Rol.Equals("supervisor", StringComparison.OrdinalIgnoreCase))
        {
            empresasDisponibles = await _db.Empresas
                .IgnoreQueryFilters()
                .Where(e => e.Activo)
                .OrderBy(e => e.Nombre)
                .Select(e => new EmpresaResumenDto(e.Id, e.Nombre))
                .ToListAsync();
        }
        else
        {
            empresasDisponibles = sucursalesAsignadas
                .Where(s => s.EmpresaId != null)
                .GroupBy(s => s.EmpresaId!)
                .Select(g => new EmpresaResumenDto(g.Key!.Value, g.First().EmpresaNombre ?? $"Empresa {g.Key}"))
                .ToList();
        }

        // Rebuild with permisos and potentially expanded sucursales
        var dto = new PerfilUsuarioDto(
            perfil.Id,
            perfil.Email,
            perfil.NombreCompleto,
            perfil.Telefono,
            perfil.Rol,
            perfil.SucursalDefaultId,
            perfil.SucursalDefaultNombre,
            perfil.UltimoAcceso,
            permisos,
            sucursalesAsignadas,
            empresaInfo?.EmpresaId,
            empresaInfo?.Nombre,
            empresasDisponibles
        );

        return Ok(dto);
    }

    /// <summary>
    /// Obtener usuario por ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<UsuarioDto>> ObtenerUsuario(int id)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        return Ok(usuario);
    }

    /// <summary>
    /// Crear un nuevo usuario
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<CrearUsuarioResultDto>> CrearUsuario(
        [FromBody] CrearUsuarioDto dto,
        [FromServices] IValidator<CrearUsuarioDto> validator)
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

        var externalId = User.GetExternalId();
        if (string.IsNullOrEmpty(externalId))
            return Unauthorized("No se pudo identificar al usuario");

        var creadorRol = ObtenerRolActual();
        var (result, error) = await _usuarioService.CrearUsuarioAsync(dto, externalId, creadorRol);
        if (result == null)
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        _logger.LogInformation(
            "{Rol} {Email} creó usuario {NuevoEmail} con rol {RolNuevo}",
            creadorRol, User.GetEmail(), result.Email, result.Rol);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearUsuario",
            Tipo: TipoActividad.Usuario,
            Descripcion: $"Usuario '{result.NombreCompleto}' ({result.Email}) creado con rol '{result.Rol}' por {User.GetEmail()}",
            SucursalId: dto.SucursalDefaultId,
            TipoEntidad: "Usuario",
            EntidadId: result.Id.ToString(),
            EntidadNombre: result.NombreCompleto,
            DatosAnteriores: null,
            DatosNuevos: new
            {
                result.Email,
                result.NombreCompleto,
                result.Rol,
                dto.SucursalDefaultId,
                dto.SucursalIds,
                CreadoPor = User.GetEmail()
            }
        ));

        return CreatedAtAction(nameof(ObtenerUsuario), new { id = result.Id }, result);
    }

    /// <summary>
    /// Actualizar un usuario existente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<IActionResult> ActualizarUsuario(
        int id,
        [FromBody] ActualizarUsuarioDto dto,
        [FromServices] IValidator<ActualizarUsuarioDto> validator)
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

        // Obtener datos anteriores para auditoría
        var usuarioAnterior = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuarioAnterior == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        // Self-protection: no puede cambiar su propio rol
        var currentExternalId = User.GetExternalId();
        if (dto.Rol != null && usuarioAnterior.KeycloakId == currentExternalId)
            return Problem(detail: "No puedes cambiar tu propio rol", statusCode: StatusCodes.Status400BadRequest);

        var creadorRol = ObtenerRolActual();
        var (success, error) = await _usuarioService.ActualizarUsuarioAsync(id, dto, creadorRol);
        if (!success)
        {
            if (error == "NOT_FOUND")
                return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        _logger.LogInformation(
            "{Rol} {Email} actualizó usuario {Id}",
            creadorRol, User.GetEmail(), id);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ActualizarUsuario",
            Tipo: TipoActividad.Usuario,
            Descripcion: $"Usuario '{usuarioAnterior.NombreCompleto}' ({usuarioAnterior.Email}) actualizado por {User.GetEmail()}",
            SucursalId: usuarioAnterior.SucursalDefaultId,
            TipoEntidad: "Usuario",
            EntidadId: id.ToString(),
            EntidadNombre: usuarioAnterior.NombreCompleto,
            DatosAnteriores: new
            {
                usuarioAnterior.NombreCompleto,
                usuarioAnterior.Telefono,
                usuarioAnterior.Rol,
                usuarioAnterior.SucursalDefaultId
            },
            DatosNuevos: new
            {
                NombreCompleto = dto.NombreCompleto ?? usuarioAnterior.NombreCompleto,
                Telefono = dto.Telefono ?? usuarioAnterior.Telefono,
                Rol = dto.Rol ?? usuarioAnterior.Rol,
                SucursalDefaultId = dto.SucursalDefaultId ?? usuarioAnterior.SucursalDefaultId,
                dto.SucursalIds,
                ModificadoPor = User.GetEmail()
            }
        ));

        return NoContent();
    }

    /// <summary>
    /// Cambiar rol de un usuario
    /// </summary>
    [HttpPut("{id}/rol")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CambiarRol(
        int id,
        [FromBody] CambiarRolDto dto,
        [FromServices] IValidator<CambiarRolDto> validator)
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

        // Self-protection: no puede cambiar su propio rol
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        var currentExternalId = User.GetExternalId();
        if (usuario.KeycloakId == currentExternalId)
            return Problem(detail: "No puedes cambiar tu propio rol", statusCode: StatusCodes.Status400BadRequest);

        var creadorRol = ObtenerRolActual();
        var rolAnterior = usuario.Rol;

        var (success, error) = await _usuarioService.CambiarRolAsync(id, dto.Rol, creadorRol);
        if (!success)
        {
            if (error == "NOT_FOUND")
                return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        _logger.LogInformation(
            "{Rol} {Email} cambió rol de usuario {Id} de {RolAnterior} a {RolNuevo}",
            creadorRol, User.GetEmail(), id, rolAnterior, dto.Rol);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CambiarRol",
            Tipo: TipoActividad.Usuario,
            Descripcion: $"Rol de usuario '{usuario.NombreCompleto}' ({usuario.Email}) cambiado de '{rolAnterior}' a '{dto.Rol}' por {User.GetEmail()}",
            SucursalId: usuario.SucursalDefaultId,
            TipoEntidad: "Usuario",
            EntidadId: id.ToString(),
            EntidadNombre: usuario.NombreCompleto,
            DatosAnteriores: new
            {
                Rol = rolAnterior,
                usuario.Email
            },
            DatosNuevos: new
            {
                Rol = dto.Rol.ToLower(),
                ModificadoPor = User.GetEmail()
            }
        ));

        return NoContent();
    }

    /// <summary>
    /// Resetear contrasena de un usuario
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<object>> ResetPassword(int id)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        var (tempPassword, error) = await _usuarioService.ResetPasswordAsync(id);
        if (tempPassword == null)
        {
            if (error == "NOT_FOUND")
                return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        _logger.LogInformation(
            "Admin {Email} reseteó contrasena de usuario {Id} ({UsuarioEmail})",
            User.GetEmail(), id, usuario.Email);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ResetPassword",
            Tipo: TipoActividad.Usuario,
            Descripcion: $"Contrasena de usuario '{usuario.NombreCompleto}' ({usuario.Email}) reseteada por {User.GetEmail()}",
            SucursalId: usuario.SucursalDefaultId,
            TipoEntidad: "Usuario",
            EntidadId: id.ToString(),
            EntidadNombre: usuario.NombreCompleto,
            DatosAnteriores: null,
            DatosNuevos: new
            {
                usuario.Email,
                ResetPor = User.GetEmail(),
                Fecha = DateTime.UtcNow
            }
        ));

        return Ok(new { passwordTemporal = tempPassword });
    }

    /// <summary>
    /// Actualizar sucursal default del usuario actual
    /// </summary>
    [HttpPut("me/sucursal")]
    public async Task<IActionResult> ActualizarMiSucursal([FromBody] ActualizarSucursalDefaultDto dto)
    {
        var externalId = User.GetExternalId();
        if (string.IsNullOrEmpty(externalId))
            return Unauthorized("No se pudo identificar al usuario");

        var perfil = await _usuarioService.ObtenerPerfilPorExternalIdAsync(externalId);
        if (perfil == null)
            return Problem(detail: "Usuario no encontrado", statusCode: StatusCodes.Status404NotFound);

        var actualizado = await _usuarioService.ActualizarSucursalDefaultAsync(
            perfil.Id, dto.SucursalId);

        if (!actualizado)
            return Problem(detail: "No se pudo actualizar la sucursal. Verifique que la sucursal existe y está activa.", statusCode: StatusCodes.Status400BadRequest);

        _logger.LogInformation(
            "Usuario {Email} cambió su sucursal default a {SucursalId}",
            perfil.Email, dto.SucursalId);

        return NoContent();
    }

    /// <summary>
    /// Asignar sucursal default a un usuario (Admin)
    /// </summary>
    [HttpPut("{id}/sucursal")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ActualizarSucursalUsuario(int id, [FromBody] ActualizarSucursalDefaultDto dto)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        var actualizado = await _usuarioService.ActualizarSucursalDefaultAsync(id, dto.SucursalId);
        if (!actualizado)
            return Problem(detail: "No se pudo actualizar la sucursal. Verifique que existe y está activa.", statusCode: StatusCodes.Status400BadRequest);

        _logger.LogInformation(
            "Admin {Email} asignó sucursal {SucursalId} a usuario {UsuarioEmail}",
            User.GetEmail(), dto.SucursalId, usuario.Email);

        return NoContent();
    }

    /// <summary>
    /// Asignar múltiples sucursales a un usuario (Admin)
    /// </summary>
    [HttpPut("{id}/sucursales")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> AsignarSucursales(int id, [FromBody] AsignarSucursalesDto dto)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        try
        {
            await _usuarioService.AsignarSucursalesAsync(id, dto.SucursalIds);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }

        _logger.LogInformation(
            "Admin {Email} asignó {Count} sucursales a usuario {UsuarioEmail}",
            User.GetEmail(), dto.SucursalIds.Count, usuario.Email);

        return NoContent();
    }

    /// <summary>
    /// Activar o desactivar un usuario
    /// </summary>
    [HttpPut("{id}/estado")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoUsuarioDto dto)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return Problem(detail: $"Usuario con ID {id} no encontrado", statusCode: StatusCodes.Status404NotFound);

        var currentExternalId = User.GetExternalId();
        if (usuario.KeycloakId == currentExternalId && !dto.Activo)
        {
            return Problem(detail: "No puedes desactivarte a ti mismo", statusCode: StatusCodes.Status400BadRequest);
        }

        var estadoAnterior = usuario.Activo;
        var actualizado = await _usuarioService.CambiarEstadoAsync(id, dto.Activo);
        if (!actualizado)
            return Problem(detail: "No se pudo cambiar el estado del usuario", statusCode: StatusCodes.Status400BadRequest);

        _logger.LogWarning(
            "Admin {Email} cambió estado de usuario {UsuarioEmail} a {Estado}. Motivo: {Motivo}",
            User.GetEmail(), usuario.Email, dto.Activo ? "Activo" : "Inactivo", dto.Motivo ?? "No especificado");

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CambiarEstadoUsuario",
            Tipo: TipoActividad.Usuario,
            Descripcion: $"Estado de usuario '{usuario.NombreCompleto}' ({usuario.Email}) cambiado de {(estadoAnterior ? "Activo" : "Inactivo")} a {(dto.Activo ? "Activo" : "Inactivo")}. Motivo: {dto.Motivo ?? "No especificado"}",
            SucursalId: usuario.SucursalDefaultId,
            TipoEntidad: "Usuario",
            EntidadId: id.ToString(),
            EntidadNombre: usuario.NombreCompleto,
            DatosAnteriores: new
            {
                Activo = estadoAnterior,
                Email = usuario.Email,
                Rol = usuario.Rol
            },
            DatosNuevos: new
            {
                Activo = dto.Activo,
                Motivo = dto.Motivo ?? "No especificado"
            }
        ));

        return NoContent();
    }

    /// <summary>
    /// Obtener estadísticas de usuarios
    /// </summary>
    [HttpGet("estadisticas")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<EstadisticasUsuariosDto>> ObtenerEstadisticas()
    {
        var dto = await _usuarioService.ObtenerEstadisticasAsync();
        return Ok(dto);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Obtiene el rol del usuario actual desde los claims.
    /// </summary>
    private string ObtenerRolActual()
    {
        var roles = User.GetRoles().ToList();
        return roles.Count > 0 ? DeterminarRolPrincipal(roles) : "vendedor";
    }

    private static string DeterminarRolPrincipal(List<string> roles)
    {
        if (roles.Any(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase)))
            return "admin";

        if (roles.Any(r => r.Equals("supervisor", StringComparison.OrdinalIgnoreCase)))
            return "supervisor";

        if (roles.Any(r => r.Equals("cajero", StringComparison.OrdinalIgnoreCase)))
            return "cajero";

        return "vendedor";
    }

    private static IEnumerable<string> ObtenerPermisosPorRol(string rol)
    {
        return rol.ToLower() switch
        {
            "admin" => new[]
            {
                "usuarios.listar",
                "usuarios.ver",
                "usuarios.activar",
                "usuarios.estadisticas",
                "sucursales.crear",
                "sucursales.modificar",
                "sucursales.eliminar",
                "impuestos.crear",
                "impuestos.modificar",
                "impuestos.eliminar",
                "categorias.crear",
                "categorias.modificar",
                "categorias.eliminar",
                "productos.crear",
                "productos.modificar",
                "productos.eliminar",
                "inventario.ajustar",
                "precios.modificar",
                "ventas.crear",
                "ventas.anular",
                "reportes.ver"
            },
            "supervisor" => new[]
            {
                "usuarios.listar",
                "categorias.crear",
                "categorias.modificar",
                "productos.crear",
                "productos.modificar",
                "inventario.ajustar",
                "precios.modificar",
                "ventas.crear",
                "ventas.anular",
                "reportes.ver"
            },
            "cajero" => new[]
            {
                "productos.ver",
                "ventas.crear",
                "cajas.abrir",
                "cajas.cerrar",
                "terceros.crear",
                "terceros.modificar"
            },
            "vendedor" => new[]
            {
                "productos.ver",
                "categorias.ver",
                "terceros.ver",
                "inventario.ver"
            },
            _ => Array.Empty<string>()
        };
    }
}
