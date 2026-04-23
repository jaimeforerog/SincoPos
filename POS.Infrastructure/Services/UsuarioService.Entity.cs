using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed partial class UsuarioService
{
    private async Task SincronizarRolIdpAsync(string externalId, string email, string rol)
    {
        var (_, error) = await _identityProvider.AsignarRolAsync(externalId, rol.ToLower());
        if (error != null)
            _logger.LogWarning(
                "No se pudo sincronizar rol {Rol} en IdP para {Email}: {Error}", rol, email, error);
    }

    public async Task<Usuario> ObtenerOCrearUsuarioEntityAsync(
        string externalId,
        string email,
        string? nombreCompleto = null,
        string? rol = null)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.ExternalId == externalId);

        if (usuario == null)
        {
            usuario = await _context.Usuarios
                .Include(u => u.Sucursales)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (usuario != null)
            {
                _logger.LogInformation(
                    "Vinculando externalId {NewId} al usuario existente {Email} (anterior: {OldId})",
                    externalId, email, usuario.ExternalId);
                usuario.ExternalId = externalId;
                usuario.UltimoAcceso = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        if (usuario == null)
        {
            usuario = new Usuario
            {
                ExternalId = externalId,
                Email = email,
                NombreCompleto = nombreCompleto ?? email,
                Rol = rol ?? Roles.Vendedor,
                Activo = true,
                Sucursales = new List<UsuarioSucursal>()
            };

            _context.Usuarios.Add(usuario);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Usuario creado: ExternalId={ExternalId}, Email={Email}, Rol={Rol}",
                    externalId, email, usuario.Rol);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                usuario = await _context.Usuarios
                    .Include(u => u.Sucursales)
                    .FirstAsync(u => u.ExternalId == externalId);
                _logger.LogInformation(
                    "Usuario ExternalId={ExternalId} ya existía (carrera), recargado desde DB.", externalId);
            }
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

    public async Task<Usuario?> ObtenerPorExternalIdAsync(string externalId) =>
        await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstOrDefaultAsync(u => u.ExternalId == externalId);

    public async Task<Usuario?> ObtenerPorIdEntityAsync(int id) =>
        await _context.Usuarios
            .Include(u => u.SucursalDefault)
            .Include(u => u.Sucursales).ThenInclude(us => us.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id);

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
            query = query.Where(u => u.NombreCompleto.Contains(busqueda) || u.Email.Contains(busqueda));
        if (!string.IsNullOrWhiteSpace(rol))
            query = query.Where(u => u.Rol == rol);
        if (activo.HasValue)
            query = query.Where(u => u.Activo == activo.Value);
        if (sucursalId.HasValue)
            query = query.Where(u =>
                u.SucursalDefaultId == sucursalId.Value ||
                u.Sucursales.Any(us => us.SucursalId == sucursalId.Value));

        return await query.OrderByDescending(u => u.FechaCreacion).ToListAsync();
    }

    public async Task<List<(int Id, string Nombre)>> ObtenerTodasSucursalesActivasTupleAsync() =>
        await _context.Sucursales
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new ValueTuple<int, string>(s.Id, s.Nombre))
            .ToListAsync();
}
