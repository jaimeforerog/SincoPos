using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Api.Extensions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

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
    private readonly UsuarioService _usuarioService;
    private readonly ILogger<UsuariosController> _logger;
    private readonly IActivityLogService _activityLogService;

    public UsuariosController(
        UsuarioService usuarioService,
        ILogger<UsuariosController> logger,
        IActivityLogService activityLogService)
    {
        _usuarioService = usuarioService;
        _logger = logger;
        _activityLogService = activityLogService;
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
        var usuarios = await _usuarioService.ListarUsuariosAsync(
            busqueda, rol, activo, sucursalId);

        var dtos = usuarios.Select(ToDto).ToList();

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

        var usuario = await _usuarioService.ObtenerPorKeycloakIdAsync(externalId);
        if (usuario == null)
            return StatusCode(500, "Error al obtener perfil de usuario");

        var permisos = ObtenerPermisosPorRol(usuario.Rol);
        var sucursalesAsignadas = usuario.Sucursales
            .Select(us => new SucursalResumenDto(us.Sucursal.Id, us.Sucursal.Nombre))
            .ToList();

        // Si el usuario es admin o supervisor y no tiene sucursales asignadas,
        // darle acceso a todas las sucursales activas
        if (sucursalesAsignadas.Count == 0 &&
            (usuario.Rol.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase) ||
             usuario.Rol.Equals(Roles.Supervisor, StringComparison.OrdinalIgnoreCase)))
        {
            var todas = await _usuarioService.ObtenerTodasSucursalesActivasAsync();
            sucursalesAsignadas = todas
                .Select(s => new SucursalResumenDto(s.Id, s.Nombre))
                .ToList();

            _logger.LogInformation(
                "Usuario {Email} ({Rol}) sin sucursales asignadas, usando todas las activas ({Count})",
                usuario.Email, usuario.Rol, sucursalesAsignadas.Count);
        }

        var dto = new PerfilUsuarioDto(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Telefono,
            usuario.Rol,
            usuario.SucursalDefaultId,
            usuario.SucursalDefault?.Nombre,
            usuario.UltimoAcceso,
            permisos,
            sucursalesAsignadas
        );

        return Ok(dto);
    }

    /// <summary>
    /// Obtener usuario por ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<UsuarioDto>> ObtenerUsuario(int id)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return NotFound($"Usuario con ID {id} no encontrado");

        return Ok(ToDto(usuario));
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

        var usuario = await _usuarioService.ObtenerPorKeycloakIdAsync(externalId);
        if (usuario == null)
            return NotFound("Usuario no encontrado");

        var actualizado = await _usuarioService.ActualizarSucursalDefaultAsync(
            usuario.Id, dto.SucursalId);

        if (!actualizado)
            return BadRequest("No se pudo actualizar la sucursal. Verifique que la sucursal existe y está activa.");

        _logger.LogInformation(
            "Usuario {Email} cambió su sucursal default a {SucursalId}",
            usuario.Email, dto.SucursalId);

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
            return NotFound($"Usuario con ID {id} no encontrado");

        var actualizado = await _usuarioService.ActualizarSucursalDefaultAsync(id, dto.SucursalId);
        if (!actualizado)
            return BadRequest("No se pudo actualizar la sucursal. Verifique que existe y está activa.");

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
            return NotFound($"Usuario con ID {id} no encontrado");

        try
        {
            await _usuarioService.AsignarSucursalesAsync(id, dto.SucursalIds);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
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
            return NotFound($"Usuario con ID {id} no encontrado");

        var currentExternalId = User.GetExternalId();
        if (usuario.KeycloakId == currentExternalId && !dto.Activo)
        {
            return BadRequest("No puedes desactivarte a ti mismo");
        }

        var estadoAnterior = usuario.Activo;
        var actualizado = await _usuarioService.CambiarEstadoAsync(id, dto.Activo);
        if (!actualizado)
            return BadRequest("No se pudo cambiar el estado del usuario");

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
        var stats = await _usuarioService.ObtenerEstadisticasAsync();

        var usuariosRecientes = (List<Usuario>)stats["usuariosRecientes"];

        var dto = new EstadisticasUsuariosDto(
            (int)stats["totalUsuarios"],
            (int)stats["usuariosActivos"],
            (int)stats["usuariosInactivos"],
            (Dictionary<string, int>)stats["usuariosPorRol"],
            (int)stats["conectadosHoy"],
            (int)stats["conectadosSemana"],
            usuariosRecientes.Select(u => new UsuarioRecienteDto(
                u.Id,
                u.Email,
                u.NombreCompleto,
                u.Rol,
                u.FechaCreacion,
                u.UltimoAcceso
            )).ToList()
        );

        return Ok(dto);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static UsuarioDto ToDto(Usuario u) => new(
        u.Id,
        u.KeycloakId,
        u.Email,
        u.NombreCompleto,
        u.Telefono,
        u.Rol,
        u.SucursalDefaultId,
        u.SucursalDefault?.Nombre,
        u.Activo,
        u.FechaCreacion,
        u.UltimoAcceso,
        u.CreadoPor,
        u.FechaModificacion,
        u.ModificadoPor,
        u.Sucursales.Select(us => new SucursalResumenDto(us.Sucursal.Id, us.Sucursal.Nombre)).ToList()
    );

    private static string DeterminarRolPrincipal(List<string> roles)
    {
        if (roles.Any(r => r.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)))
            return Roles.Admin;

        if (roles.Any(r => r.Equals(Roles.Supervisor, StringComparison.OrdinalIgnoreCase)))
            return Roles.Supervisor;

        if (roles.Any(r => r.Equals(Roles.Cajero, StringComparison.OrdinalIgnoreCase)))
            return Roles.Cajero;

        return Roles.Vendedor;
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
