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
[Route("api/[controller]")]
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
    /// <remarks>
    /// Requiere rol Admin o Supervisor.
    ///
    /// Filtros disponibles:
    /// - busqueda: Buscar por nombre o email
    /// - rol: Filtrar por rol específico (admin, supervisor, cajero, vendedor)
    /// - activo: Filtrar por estado activo (true/false)
    /// - sucursalId: Filtrar por sucursal default
    /// </remarks>
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

        var dtos = usuarios.Select(u => new UsuarioDto(
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
            u.ModificadoPor
        )).ToList();

        _logger.LogInformation(
            "Usuario {Email} listó {Count} usuarios con filtros: busqueda={Busqueda}, rol={Rol}, activo={Activo}",
            User.GetEmail(), dtos.Count, busqueda, rol, activo);

        return Ok(dtos);
    }

    /// <summary>
    /// Obtener perfil del usuario actual
    /// </summary>
    /// <remarks>
    /// Retorna información del usuario autenticado actualmente.
    /// Incluye permisos basados en el rol.
    /// </remarks>
    [HttpGet("me")]
    public async Task<ActionResult<PerfilUsuarioDto>> ObtenerPerfil()
    {
        var keycloakId = User.GetKeycloakId();
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized("No se pudo identificar al usuario");

        var email = User.GetEmail() ?? "unknown@sincopos.com";
        var nombreCompleto = User.GetNombreCompleto() ?? email;
        var keycloakRoles = User.GetRoles().ToList();

        // Solo actualizar rol si Keycloak reporta roles; si no, pasar null para evitar
        // degradar un rol existente cuando la lectura de claims falla
        var rolKeycloak = keycloakRoles.Count > 0
            ? DeterminarRolPrincipal(keycloakRoles)
            : null;

        _logger.LogInformation(
            "ObtenerPerfil: keycloakId={Id}, rolesKeycloak=[{Roles}], rolDeterminado={Rol}",
            keycloakId, string.Join(",", keycloakRoles), rolKeycloak ?? "(sin rol)");

        await _usuarioService.ObtenerOCrearUsuarioAsync(keycloakId, email, nombreCompleto, rolKeycloak);

        // Re-cargar con propiedades de navegación (SucursalDefault)
        var usuario = await _usuarioService.ObtenerPorKeycloakIdAsync(keycloakId);
        if (usuario == null)
            return StatusCode(500, "Error al obtener perfil de usuario");

        var permisos = ObtenerPermisosPorRol(usuario.Rol);

        var dto = new PerfilUsuarioDto(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Telefono,
            usuario.Rol,
            usuario.SucursalDefaultId,
            usuario.SucursalDefault?.Nombre,
            usuario.UltimoAcceso,
            permisos
        );

        return Ok(dto);
    }

    /// <summary>
    /// Obtener usuario por ID
    /// </summary>
    /// <remarks>
    /// Requiere rol Admin.
    /// Retorna información completa de un usuario específico.
    /// </remarks>
    [HttpGet("{id}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<UsuarioDto>> ObtenerUsuario(int id)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return NotFound($"Usuario con ID {id} no encontrado");

        var dto = new UsuarioDto(
            usuario.Id,
            usuario.KeycloakId,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Telefono,
            usuario.Rol,
            usuario.SucursalDefaultId,
            usuario.SucursalDefault?.Nombre,
            usuario.Activo,
            usuario.FechaCreacion,
            usuario.UltimoAcceso,
            usuario.CreadoPor,
            usuario.FechaModificacion,
            usuario.ModificadoPor
        );

        return Ok(dto);
    }

    /// <summary>
    /// Actualizar sucursal default del usuario actual
    /// </summary>
    /// <remarks>
    /// Permite al usuario cambiar su sucursal por defecto.
    /// Útil para usuarios que trabajan en múltiples sucursales.
    /// </remarks>
    [HttpPut("me/sucursal")]
    public async Task<IActionResult> ActualizarMiSucursal([FromBody] ActualizarSucursalDefaultDto dto)
    {
        var keycloakId = User.GetKeycloakId();
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized("No se pudo identificar al usuario");

        var usuario = await _usuarioService.ObtenerPorKeycloakIdAsync(keycloakId);
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
    /// Activar o desactivar un usuario
    /// </summary>
    /// <remarks>
    /// Requiere rol Admin.
    ///
    /// Permite activar o desactivar usuarios del sistema.
    /// Los usuarios desactivados no podrán iniciar sesión.
    ///
    /// NOTA: Esto NO desactiva al usuario en Keycloak, solo en la aplicación.
    /// Para desactivar completamente, también debe hacerse en Keycloak.
    /// </remarks>
    [HttpPut("{id}/estado")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoUsuarioDto dto)
    {
        var usuario = await _usuarioService.ObtenerPorIdAsync(id);
        if (usuario == null)
            return NotFound($"Usuario con ID {id} no encontrado");

        // Prevenir que el admin se desactive a sí mismo
        var keycloakIdActual = User.GetKeycloakId();
        if (usuario.KeycloakId == keycloakIdActual && !dto.Activo)
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

        // Activity Log
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
    /// <remarks>
    /// Requiere rol Admin.
    ///
    /// Retorna información estadística sobre los usuarios:
    /// - Total de usuarios activos/inactivos
    /// - Distribución por rol
    /// - Usuarios conectados recientemente
    /// - Usuarios creados recientemente
    /// </remarks>
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

    /// <summary>
    /// Determina el rol principal basado en los roles de Keycloak
    /// Prioridad: admin > supervisor > cajero > vendedor
    /// </summary>
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

    /// <summary>
    /// Retorna los permisos basados en el rol del usuario
    /// </summary>
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
