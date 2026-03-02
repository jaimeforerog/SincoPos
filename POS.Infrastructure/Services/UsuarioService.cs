using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio para gestionar usuarios sincronizados con Keycloak
/// </summary>
public class UsuarioService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsuarioService> _logger;

    public UsuarioService(AppDbContext context, ILogger<UsuarioService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene o crea un usuario basado en los claims de Keycloak
    /// </summary>
    public async Task<Usuario> ObtenerOCrearUsuarioAsync(
        string keycloakId,
        string email,
        string? nombreCompleto = null,
        string? rol = null)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (usuario == null)
        {
            // Crear nuevo usuario
            usuario = new Usuario
            {
                KeycloakId = keycloakId,
                Email = email,
                NombreCompleto = nombreCompleto ?? email,
                Rol = rol ?? Roles.Vendedor, // Rol por defecto
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Usuario creado: {KeycloakId}, Email: {Email}, Rol: {Rol}",
                keycloakId, email, usuario.Rol);
        }
        else
        {
            // Actualizar último acceso
            usuario.UltimoAcceso = DateTime.UtcNow;

            // Actualizar rol si cambió en Keycloak
            if (!string.IsNullOrEmpty(rol) && usuario.Rol != rol)
            {
                _logger.LogInformation(
                    "Rol actualizado para usuario {Email}: {RolAnterior} -> {RolNuevo}",
                    email, usuario.Rol, rol);
                usuario.Rol = rol;
                usuario.FechaModificacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        return usuario;
    }

    /// <summary>
    /// Obtiene un usuario por su ID de Keycloak
    /// </summary>
    public async Task<Usuario?> ObtenerPorKeycloakIdAsync(string keycloakId)
    {
        return await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
    }

    /// <summary>
    /// Actualiza la sucursal por defecto de un usuario
    /// </summary>
    public async Task<bool> ActualizarSucursalDefaultAsync(int usuarioId, int sucursalId)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null)
            return false;

        // Verificar que la sucursal existe
        var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == sucursalId && s.Activo);
        if (!sucursalExiste)
            return false;

        usuario.SucursalDefaultId = sucursalId;
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Obtiene todos los usuarios con filtros opcionales
    /// </summary>
    public async Task<List<Usuario>> ListarUsuariosAsync(
        string? busqueda = null,
        string? rol = null,
        bool? activo = null,
        int? sucursalId = null)
    {
        var query = _context.Usuarios
            .Include(u => u.SucursalDefault)
            .AsQueryable();

        // Filtro por búsqueda (nombre o email)
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            query = query.Where(u =>
                u.NombreCompleto.Contains(busqueda) ||
                u.Email.Contains(busqueda));
        }

        // Filtro por rol
        if (!string.IsNullOrWhiteSpace(rol))
        {
            query = query.Where(u => u.Rol == rol);
        }

        // Filtro por estado activo
        if (activo.HasValue)
        {
            query = query.Where(u => u.Activo == activo.Value);
        }

        // Filtro por sucursal
        if (sucursalId.HasValue)
        {
            query = query.Where(u => u.SucursalDefaultId == sucursalId.Value);
        }

        return await query
            .OrderByDescending(u => u.FechaCreacion)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene un usuario por su ID
    /// </summary>
    public async Task<Usuario?> ObtenerPorIdAsync(int id)
    {
        return await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Activa o desactiva un usuario
    /// </summary>
    public async Task<bool> CambiarEstadoAsync(int usuarioId, bool activo)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null)
            return false;

        usuario.Activo = activo;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Estado de usuario {Email} cambiado a {Estado}",
            usuario.Email, activo ? "Activo" : "Inactivo");

        return true;
    }

    /// <summary>
    /// Obtiene estadísticas de usuarios
    /// </summary>
    public async Task<Dictionary<string, object>> ObtenerEstadisticasAsync()
    {
        var ahora = DateTime.UtcNow;
        var hace24Horas = ahora.AddDays(-1);
        var hace7Dias = ahora.AddDays(-7);

        var totalUsuarios = await _context.Usuarios.CountAsync();
        var usuariosActivos = await _context.Usuarios.CountAsync(u => u.Activo);
        var usuariosInactivos = totalUsuarios - usuariosActivos;

        var usuariosPorRol = await _context.Usuarios
            .GroupBy(u => u.Rol)
            .Select(g => new { Rol = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Rol, x => x.Cantidad);

        var conectadosHoy = await _context.Usuarios
            .CountAsync(u => u.UltimoAcceso.HasValue && u.UltimoAcceso.Value >= hace24Horas);

        var conectadosSemana = await _context.Usuarios
            .CountAsync(u => u.UltimoAcceso.HasValue && u.UltimoAcceso.Value >= hace7Dias);

        var usuariosRecientes = await _context.Usuarios
            .OrderByDescending(u => u.FechaCreacion)
            .Take(5)
            .ToListAsync();

        return new Dictionary<string, object>
        {
            ["totalUsuarios"] = totalUsuarios,
            ["usuariosActivos"] = usuariosActivos,
            ["usuariosInactivos"] = usuariosInactivos,
            ["usuariosPorRol"] = usuariosPorRol,
            ["conectadosHoy"] = conectadosHoy,
            ["conectadosSemana"] = conectadosSemana,
            ["usuariosRecientes"] = usuariosRecientes
        };
    }
}
