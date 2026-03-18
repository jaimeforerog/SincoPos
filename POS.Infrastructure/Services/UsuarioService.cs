using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio para gestionar usuarios sincronizados con el proveedor de identidad.
/// Implementa IUsuarioService (DTO-based) y expone metodos adicionales con entidades
/// para uso interno (CajasController).
/// </summary>
public class UsuarioService : IUsuarioService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsuarioService> _logger;
    private readonly IIdentityProviderService _identityProvider;

    public UsuarioService(
        AppDbContext context,
        ILogger<UsuarioService> logger,
        IIdentityProviderService identityProvider)
    {
        _context = context;
        _logger = logger;
        _identityProvider = identityProvider;
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

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

    // ── IUsuarioService implementation ───────────────────────────────────────

    /// <inheritdoc />
    async Task<UsuarioDto> IUsuarioService.ObtenerOCrearUsuarioAsync(
        string externalId, string email, string? nombreCompleto, string? rol)
    {
        var usuario = await ObtenerOCrearUsuarioEntityAsync(externalId, email, nombreCompleto, rol);
        // Reload with includes for DTO mapping
        var loaded = await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstAsync(u => u.Id == usuario.Id);
        return ToDto(loaded);
    }

    /// <inheritdoc />
    async Task<PerfilUsuarioDto?> IUsuarioService.ObtenerPerfilPorExternalIdAsync(string externalId)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal).ThenInclude(s => s.Empresa)
            .FirstOrDefaultAsync(u => u.KeycloakId == externalId);

        if (usuario == null)
            return null;

        var sucursalesAsignadas = usuario.Sucursales
            .Select(us => new SucursalResumenDto(us.Sucursal.Id, us.Sucursal.Nombre, us.Sucursal.EmpresaId, us.Sucursal.Empresa?.Nombre))
            .ToList();

        return new PerfilUsuarioDto(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Telefono,
            usuario.Rol,
            usuario.SucursalDefaultId,
            usuario.SucursalDefault?.Nombre,
            usuario.UltimoAcceso,
            Enumerable.Empty<string>(), // Permisos se asignan en el controller
            sucursalesAsignadas
        );
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    async Task<List<UsuarioDto>> IUsuarioService.ListarUsuariosAsync(
        string? busqueda, string? rol, bool? activo, int? sucursalId)
    {
        var usuarios = await ListarUsuariosEntityAsync(busqueda, rol, activo, sucursalId);
        return usuarios.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    async Task<UsuarioDto?> IUsuarioService.ObtenerPorIdAsync(int id)
    {
        var usuario = await ObtenerPorIdEntityAsync(id);
        return usuario == null ? null : ToDto(usuario);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    async Task<List<SucursalResumenDto>> IUsuarioService.ObtenerTodasSucursalesActivasAsync()
    {
        return await _context.Sucursales
            .Include(s => s.Empresa)
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalResumenDto(s.Id, s.Nombre, s.EmpresaId, s.Empresa!.Nombre))
            .ToListAsync();
    }

    /// <inheritdoc />
    async Task<EstadisticasUsuariosDto> IUsuarioService.ObtenerEstadisticasAsync()
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

        return new EstadisticasUsuariosDto(
            totalUsuarios,
            usuariosActivos,
            usuariosInactivos,
            usuariosPorRol,
            conectadosHoy,
            conectadosSemana,
            usuariosRecientes.Select(u => new UsuarioRecienteDto(
                u.Id,
                u.Email,
                u.NombreCompleto,
                u.Rol,
                u.FechaCreacion,
                u.UltimoAcceso
            )).ToList()
        );
    }

    // ── New CRUD methods (IUsuarioService) ───────────────────────────────────

    /// <inheritdoc />
    public async Task<(CrearUsuarioResultDto? Result, string? Error)> CrearUsuarioAsync(
        CrearUsuarioDto dto, string creadorExternalId, string creadorRol)
    {
        // 0. Validar jerarquía de roles
        if (!Roles.PuedeAsignarRol(creadorRol, dto.Rol))
            return (null, $"No tiene permisos para asignar el rol '{dto.Rol}'. Su rol ({creadorRol}) no puede crear usuarios con ese nivel de privilegios.");

        // 1. Validar email unico en BD
        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
        if (emailExiste)
            return (null, $"Ya existe un usuario con el email '{dto.Email}'");

        // 2. Crear usuario en IdP
        var (externalId, idpError) = await _identityProvider.CrearUsuarioAsync(
            dto.Email, dto.NombreCompleto, null);
        if (externalId == null)
            return (null, $"Error al crear usuario en proveedor de identidad: {idpError}");

        // 3. Asignar rol en IdP
        var (rolSuccess, rolError) = await _identityProvider.AsignarRolAsync(externalId, dto.Rol.ToLower());
        if (!rolSuccess)
        {
            _logger.LogWarning(
                "No se pudo asignar rol {Rol} en IdP para {Email}: {Error}",
                dto.Rol, dto.Email, rolError);
        }

        // 4. Crear entidad local
        var creador = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakId == creadorExternalId);

        var usuario = new Usuario
        {
            KeycloakId = externalId,
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto,
            Telefono = dto.Telefono,
            Rol = dto.Rol.ToLower(),
            SucursalDefaultId = dto.SucursalDefaultId,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor = creador?.Email ?? "system",
            Sucursales = new List<UsuarioSucursal>()
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        // 5. Asignar sucursales
        if (dto.SucursalIds is { Count: > 0 })
        {
            var sucursalesValidas = await _context.Sucursales
                .Where(s => dto.SucursalIds.Contains(s.Id) && s.Activo)
                .Select(s => s.Id)
                .ToListAsync();

            foreach (var sid in sucursalesValidas)
            {
                _context.UsuarioSucursales.Add(new UsuarioSucursal
                {
                    UsuarioId = usuario.Id,
                    SucursalId = sid
                });
            }

            // Si hay sucursal default, asegurar que este en la lista
            if (dto.SucursalDefaultId.HasValue &&
                !sucursalesValidas.Contains(dto.SucursalDefaultId.Value))
            {
                var defaultExiste = await _context.Sucursales
                    .AnyAsync(s => s.Id == dto.SucursalDefaultId.Value && s.Activo);
                if (defaultExiste)
                {
                    _context.UsuarioSucursales.Add(new UsuarioSucursal
                    {
                        UsuarioId = usuario.Id,
                        SucursalId = dto.SucursalDefaultId.Value
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
        else if (dto.SucursalDefaultId.HasValue)
        {
            var defaultExiste = await _context.Sucursales
                .AnyAsync(s => s.Id == dto.SucursalDefaultId.Value && s.Activo);
            if (defaultExiste)
            {
                _context.UsuarioSucursales.Add(new UsuarioSucursal
                {
                    UsuarioId = usuario.Id,
                    SucursalId = dto.SucursalDefaultId.Value
                });
                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation(
            "Usuario creado por admin: Id={Id}, Email={Email}, Rol={Rol}, ExternalId={ExternalId}",
            usuario.Id, usuario.Email, usuario.Rol, externalId);

        // 6. Obtener password temporal del IdP
        var (tempPassword, _) = await _identityProvider.ResetPasswordAsync(externalId);

        return (new CrearUsuarioResultDto(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Rol,
            tempPassword
        ), null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? Error)> ActualizarUsuarioAsync(int id, ActualizarUsuarioDto dto, string creadorRol)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
            return (false, "NOT_FOUND");

        // Validar jerarquía: no puede modificar un usuario con rol igual o superior (excepto admin)
        if (!Roles.PuedeAsignarRol(creadorRol, usuario.Rol) && creadorRol.ToLower() != Roles.Admin)
            return (false, $"No tiene permisos para modificar un usuario con rol '{usuario.Rol}'.");

        var rolAnterior = usuario.Rol;

        if (dto.NombreCompleto != null)
            usuario.NombreCompleto = dto.NombreCompleto;

        if (dto.Telefono != null)
            usuario.Telefono = dto.Telefono;

        if (dto.Rol != null)
        {
            if (!Roles.PuedeAsignarRol(creadorRol, dto.Rol))
                return (false, $"No tiene permisos para asignar el rol '{dto.Rol}'.");
            usuario.Rol = dto.Rol.ToLower();
        }

        if (dto.SucursalDefaultId.HasValue)
        {
            var sucursalExiste = await _context.Sucursales
                .AnyAsync(s => s.Id == dto.SucursalDefaultId.Value && s.Activo);
            if (sucursalExiste)
                usuario.SucursalDefaultId = dto.SucursalDefaultId.Value;
        }

        usuario.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Si cambio el rol, sincronizar con IdP
        if (dto.Rol != null && dto.Rol.ToLower() != rolAnterior)
        {
            var (_, rolError) = await _identityProvider.AsignarRolAsync(usuario.KeycloakId, dto.Rol.ToLower());
            if (rolError != null)
            {
                _logger.LogWarning(
                    "No se pudo sincronizar rol {Rol} en IdP para {Email}: {Error}",
                    dto.Rol, usuario.Email, rolError);
            }
        }

        // Si se proporcionaron sucursales, reasignar
        if (dto.SucursalIds != null)
        {
            await AsignarSucursalesAsync(id, dto.SucursalIds);
        }

        _logger.LogInformation(
            "Usuario actualizado: Id={Id}, Email={Email}",
            id, usuario.Email);

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? Error)> CambiarRolAsync(int id, string nuevoRol, string creadorRol)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return (false, "NOT_FOUND");

        if (!Roles.EsValido(nuevoRol))
            return (false, $"Rol '{nuevoRol}' no es valido. Roles validos: {string.Join(", ", Roles.TodosLosRoles)}");

        // Validar jerarquía de roles
        if (!Roles.PuedeAsignarRol(creadorRol, nuevoRol))
            return (false, $"No tiene permisos para asignar el rol '{nuevoRol}'.");

        var rolAnterior = usuario.Rol;
        usuario.Rol = nuevoRol.ToLower();
        usuario.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var (_, rolError) = await _identityProvider.AsignarRolAsync(usuario.KeycloakId, nuevoRol.ToLower());
        if (rolError != null)
        {
            _logger.LogWarning(
                "No se pudo sincronizar rol {Rol} en IdP para {Email}: {Error}",
                nuevoRol, usuario.Email, rolError);
        }

        _logger.LogInformation(
            "Rol de usuario {Email} cambiado: {RolAnterior} -> {RolNuevo}",
            usuario.Email, rolAnterior, nuevoRol.ToLower());

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(string? TempPassword, string? Error)> ResetPasswordAsync(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return (null, "NOT_FOUND");

        var (tempPassword, error) = await _identityProvider.ResetPasswordAsync(usuario.KeycloakId);
        if (error != null)
            return (null, $"Error al resetear contrasena: {error}");

        _logger.LogInformation(
            "Password reseteado para usuario {Email} (Id={Id})",
            usuario.Email, id);

        return (tempPassword, null);
    }

    // ── Entity-returning methods (for backward compat: CajasController) ──────

    /// <summary>
    /// Obtiene o crea un usuario basado en los claims del IdP. Returns entity.
    /// Used by CajasController which needs the entity directly.
    /// </summary>
    public async Task<Usuario> ObtenerOCrearUsuarioEntityAsync(
        string keycloakId,
        string email,
        string? nombreCompleto = null,
        string? rol = null)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        // Si no se encuentra por externalId, buscar por email (usuario creado via admin con otro externalId)
        if (usuario == null)
        {
            usuario = await _context.Usuarios
                .Include(u => u.Sucursales)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (usuario != null)
            {
                // Vincular el externalId real del login con el usuario existente
                _logger.LogInformation(
                    "Vinculando externalId {NewId} al usuario existente {Email} (anterior: {OldId})",
                    keycloakId, email, usuario.KeycloakId);
                usuario.KeycloakId = keycloakId;
                usuario.UltimoAcceso = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        if (usuario == null)
        {
            usuario = new Usuario
            {
                KeycloakId = keycloakId,
                Email = email,
                NombreCompleto = nombreCompleto ?? email,
                Rol = rol ?? Roles.Vendedor,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                Sucursales = new List<UsuarioSucursal>()
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
    /// Obtiene un usuario entity por su ExternalId. Used by CajasController.
    /// </summary>
    public async Task<Usuario?> ObtenerPorKeycloakIdAsync(string keycloakId)
    {
        return await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
    }

    /// <summary>
    /// Obtiene un usuario entity por su ID. Used internally and by controllers needing entity.
    /// </summary>
    public async Task<Usuario?> ObtenerPorIdEntityAsync(int id)
    {
        return await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Lists users returning entities. Used internally.
    /// </summary>
    public async Task<List<Usuario>> ListarUsuariosEntityAsync(
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
    /// Obtiene todas las sucursales activas como tuples. Used by backward-compat callers.
    /// </summary>
    public async Task<List<(int Id, string Nombre)>> ObtenerTodasSucursalesActivasTupleAsync()
    {
        return await _context.Sucursales
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new ValueTuple<int, string>(s.Id, s.Nombre))
            .ToListAsync();
    }
}
