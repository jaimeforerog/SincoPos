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
    /// Obtiene o crea un usuario basado en los claims de Keycloak.
    /// Si el usuario es nuevo y tiene SucursalDefaultId, la agrega automáticamente a UsuarioSucursales.
    /// </summary>
    public async Task<Usuario> ObtenerOCrearUsuarioAsync(
        string keycloakId,
        string email,
        string? nombreCompleto = null,
        string? rol = null)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (usuario == null)
        {
            usuario = new Usuario
            {
                KeycloakId = keycloakId,
                Email = email,
                NombreCompleto = nombreCompleto ?? email,
                Rol = rol ?? Roles.Vendedor,
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
            usuario.UltimoAcceso = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(rol) && usuario.Rol != rol)
            {
                _logger.LogInformation(
                    "Rol actualizado para usuario {Email}: {RolAnterior} -> {RolNuevo}",
                    email, usuario.Rol, rol);
                usuario.Rol = rol;
                usuario.FechaModificacion = DateTime.UtcNow;
            }

            // Si tiene sucursal default pero no está en la tabla pivot, agregarla
            if (usuario.SucursalDefaultId.HasValue &&
                !usuario.Sucursales.Any(us => us.SucursalId == usuario.SucursalDefaultId.Value))
            {
                _context.UsuarioSucursales.Add(new UsuarioSucursal
                {
                    UsuarioId = usuario.Id,
                    SucursalId = usuario.SucursalDefaultId.Value
                });
            }

            await _context.SaveChangesAsync();
        }

        return usuario;
    }

    /// <summary>
    /// Obtiene un usuario por su ID de Keycloak, incluyendo sucursales asignadas
    /// </summary>
    public async Task<Usuario?> ObtenerPorKeycloakIdAsync(string keycloakId)
    {
        return await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
    }

    /// <summary>
    /// Actualiza la sucursal por defecto de un usuario
    /// </summary>
    public async Task<bool> ActualizarSucursalDefaultAsync(int usuarioId, int sucursalId)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario == null)
            return false;

        var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == sucursalId && s.Activo);
        if (!sucursalExiste)
            return false;

        usuario.SucursalDefaultId = sucursalId;

        // Asegurar que la sucursal default esté en la tabla pivot
        if (!usuario.Sucursales.Any(us => us.SucursalId == sucursalId))
        {
            _context.UsuarioSucursales.Add(new UsuarioSucursal
            {
                UsuarioId = usuarioId,
                SucursalId = sucursalId
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Asigna múltiples sucursales a un usuario (reemplaza las existentes)
    /// </summary>
    public async Task AsignarSucursalesAsync(int usuarioId, List<int> sucursalIds)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario == null)
            throw new KeyNotFoundException($"Usuario {usuarioId} no encontrado");

        // Eliminar asignaciones existentes
        _context.UsuarioSucursales.RemoveRange(usuario.Sucursales);

        // Insertar nuevas (solo sucursales activas)
        var sucursalesValidas = await _context.Sucursales
            .Where(s => sucursalIds.Contains(s.Id) && s.Activo)
            .Select(s => s.Id)
            .ToListAsync();

        foreach (var sid in sucursalesValidas)
        {
            _context.UsuarioSucursales.Add(new UsuarioSucursal
            {
                UsuarioId = usuarioId,
                SucursalId = sid
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Sucursales asignadas a usuario {UsuarioId}: [{SucursalIds}]",
            usuarioId, string.Join(",", sucursalesValidas));
    }

    /// <summary>
    /// Obtiene todos los usuarios con filtros opcionales, incluyendo sucursales asignadas
    /// </summary>
    public async Task<List<Usuario>> ListarUsuariosAsync(
        string? busqueda = null,
        string? rol = null,
        bool? activo = null,
        int? sucursalId = null)
    {
        var query = _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            query = query.Where(u =>
                u.NombreCompleto.Contains(busqueda) ||
                u.Email.Contains(busqueda));
        }

        if (!string.IsNullOrWhiteSpace(rol))
        {
            query = query.Where(u => u.Rol == rol);
        }

        if (activo.HasValue)
        {
            query = query.Where(u => u.Activo == activo.Value);
        }

        if (sucursalId.HasValue)
        {
            query = query.Where(u =>
                u.SucursalDefaultId == sucursalId.Value ||
                u.Sucursales.Any(us => us.SucursalId == sucursalId.Value));
        }

        return await query
            .OrderByDescending(u => u.FechaCreacion)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene un usuario por su ID, incluyendo sucursales asignadas
    /// </summary>
    public async Task<Usuario?> ObtenerPorIdAsync(int id)
    {
        return await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
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
    /// Obtiene todas las sucursales activas (para usuarios admin/supervisor sin asignaciones)
    /// </summary>
    public async Task<List<(int Id, string Nombre)>> ObtenerTodasSucursalesActivasAsync()
    {
        return await _context.Sucursales
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new ValueTuple<int, string>(s.Id, s.Nombre))
            .ToListAsync();
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
