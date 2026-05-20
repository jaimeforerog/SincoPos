using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed class UsuarioAdminService : IUsuarioAdminService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsuarioAdminService> _logger;
    private readonly IIdentityProviderService _identityProvider;

    public UsuarioAdminService(
        AppDbContext context,
        ILogger<UsuarioAdminService> logger,
        IIdentityProviderService identityProvider)
    {
        _context = context;
        _logger = logger;
        _identityProvider = identityProvider;
    }

    public async Task<(CrearUsuarioResultDto? Result, string? Error)> CrearUsuarioAsync(
        CrearUsuarioDto dto, string creadorExternalId, string creadorRol)
    {
        if (!Roles.PuedeAsignarRol(creadorRol, dto.Rol))
            return (null, $"No tiene permisos para asignar el rol '{dto.Rol}'. Su rol ({creadorRol}) no puede crear usuarios con ese nivel de privilegios.");

        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
        if (emailExiste)
            return (null, $"Ya existe un usuario con el email '{dto.Email}'");

        var passwordTemporal = PasswordGenerator.Generate();

        var (externalId, idpError) = await _identityProvider.CrearUsuarioAsync(
            dto.Email, dto.NombreCompleto, passwordTemporal);
        if (externalId == null)
            return (null, $"Error al crear usuario en proveedor de identidad: {idpError}");

        await SincronizarRolIdpAsync(externalId, dto.Email, dto.Rol);

        var usuario = new Usuario
        {
            ExternalId = externalId,
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto,
            Telefono = dto.Telefono,
            Rol = dto.Rol.ToLower(),
            SucursalDefaultId = dto.SucursalDefaultId,
            Sucursales = new List<UsuarioSucursal>()
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var idsConDefault = new HashSet<int>(dto.SucursalIds ?? []);
        if (dto.SucursalDefaultId.HasValue) idsConDefault.Add(dto.SucursalDefaultId.Value);
        if (idsConDefault.Count > 0)
            await AsignarSucursalesInternalAsync(usuario.Id, idsConDefault.ToList());

        await SincronizarMembershipsWorkOsAsync(usuario.Id);

        _logger.LogInformation(
            "Usuario creado por admin: Id={Id}, Email={Email}, Rol={Rol}, ExternalId={ExternalId}",
            usuario.Id, usuario.Email, usuario.Rol, externalId);

        // Disparar email para que el invitado configure su propia contrasena.
        // ResetPasswordAsync settea un password fresco en WorkOS y dispara el email.
        // Si tiene exito devuelve el password real (el initial queda reemplazado);
        // si falla, devolvemos el initial — que tambien quedo seteado al crear.
        var (passwordReset, resetError) = await _identityProvider.ResetPasswordAsync(externalId);
        if (resetError != null)
            _logger.LogWarning(
                "No se pudo enviar email de configuracion de contrasena a {Email} (Id={Id}): {Error}. " +
                "El admin debera compartir manualmente el password temporal generado.",
                usuario.Email, usuario.Id, resetError);

        return (new CrearUsuarioResultDto(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Rol,
            passwordReset ?? passwordTemporal
        ), null);
    }


    public async Task<(bool Success, string? Error)> ActualizarUsuarioAsync(int id, ActualizarUsuarioDto dto, string creadorRol)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
            return (false, "NOT_FOUND");

        if (!Roles.PuedeAsignarRol(creadorRol, usuario.Rol) && creadorRol.ToLower() != Roles.Admin)
            return (false, $"No tiene permisos para modificar un usuario con rol '{usuario.Rol}'.");

        var rolAnterior = usuario.Rol;

        if (dto.NombreCompleto != null) usuario.NombreCompleto = dto.NombreCompleto;
        if (dto.Telefono != null) usuario.Telefono = dto.Telefono;

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

        var rolCambio = dto.Rol != null && dto.Rol.ToLower() != rolAnterior;
        if (rolCambio)
            await SincronizarRolIdpAsync(usuario.ExternalId, usuario.Email, dto.Rol!);

        if (dto.SucursalIds != null)
            await AsignarSucursalesInternalAsync(id, dto.SucursalIds);

        if (rolCambio || dto.SucursalIds != null)
            await SincronizarMembershipsWorkOsAsync(id);

        _logger.LogInformation("Usuario actualizado: Id={Id}, Email={Email}", id, usuario.Email);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CambiarRolAsync(int id, string nuevoRol, string creadorRol)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return (false, "NOT_FOUND");

        if (!Roles.EsValido(nuevoRol))
            return (false, $"Rol '{nuevoRol}' no es valido. Roles validos: {string.Join(", ", Roles.TodosLosRoles)}");

        if (!Roles.PuedeAsignarRol(creadorRol, nuevoRol))
            return (false, $"No tiene permisos para asignar el rol '{nuevoRol}'.");

        var rolAnterior = usuario.Rol;
        usuario.Rol = nuevoRol.ToLower();
        usuario.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await SincronizarRolIdpAsync(usuario.ExternalId, usuario.Email, nuevoRol);
        await SincronizarMembershipsWorkOsAsync(id);

        _logger.LogInformation(
            "Rol de usuario {Email} cambiado: {RolAnterior} -> {RolNuevo}",
            usuario.Email, rolAnterior, nuevoRol.ToLower());

        return (true, null);
    }

    public async Task<(string? TempPassword, string? Error)> ResetPasswordAsync(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return (null, "NOT_FOUND");

        var (tempPassword, error) = await _identityProvider.ResetPasswordAsync(usuario.ExternalId);
        if (error != null)
            return (null, $"Error al resetear contrasena: {error}");

        _logger.LogInformation(
            "Password reseteado para usuario {Email} (Id={Id})",
            usuario.Email, id);

        return (tempPassword, null);
    }

    private async Task SincronizarRolIdpAsync(string externalId, string email, string rol)
    {
        var (_, error) = await _identityProvider.AsignarRolAsync(externalId, rol.ToLower());
        if (error != null)
            _logger.LogWarning(
                "No se pudo sincronizar rol {Rol} en IdP para {Email}: {Error}", rol, email, error);
    }

    private async Task AsignarSucursalesInternalAsync(int usuarioId, List<int> sucursalIds)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario == null) return;

        _context.UsuarioSucursales.RemoveRange(usuario.Sucursales);

        var sucursalesValidas = await _context.Sucursales
            .Where(s => sucursalIds.Contains(s.Id) && s.Activo)
            .Select(s => s.Id)
            .ToListAsync();

        foreach (var sid in sucursalesValidas)
            _context.UsuarioSucursales.Add(new UsuarioSucursal { UsuarioId = usuarioId, SucursalId = sid });

        await _context.SaveChangesAsync();
    }

    public Task SincronizarMembershipsAsync(int usuarioId) => SincronizarMembershipsWorkOsAsync(usuarioId);

    /// <summary>
    /// Sincroniza memberships del usuario en WorkOS con las empresas accesibles.
    /// - admin: todas las empresas activas con WorkOsOrganizationId.
    /// - otros: empresas derivadas de las sucursales asignadas.
    /// Idempotente: crea las faltantes, elimina las sobrantes.
    /// </summary>
    private async Task SincronizarMembershipsWorkOsAsync(int usuarioId)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursales)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario == null || string.IsNullOrEmpty(usuario.ExternalId)) return;

        IReadOnlyList<string> orgsEsperadas;
        if (usuario.Rol == Roles.Admin)
        {
            orgsEsperadas = await _context.Empresas
                .IgnoreQueryFilters()
                .Where(e => e.Activo && e.WorkOsOrganizationId != null)
                .Select(e => e.WorkOsOrganizationId!)
                .ToListAsync();
        }
        else
        {
            var sucursalIds = usuario.Sucursales.Select(us => us.SucursalId).ToList();
            orgsEsperadas = await _context.Sucursales
                .IgnoreQueryFilters()
                .Where(s => sucursalIds.Contains(s.Id))
                .Select(s => s.EmpresaId)
                .Distinct()
                .Join(_context.Empresas.IgnoreQueryFilters().Where(e => e.WorkOsOrganizationId != null),
                    sid => sid, e => e.Id, (sid, e) => e.WorkOsOrganizationId!)
                .Distinct()
                .ToListAsync();
        }

        var (orgsActuales, listError) = await _identityProvider.ListarMembresiasAsync(usuario.ExternalId);
        if (listError != null)
        {
            _logger.LogWarning(
                "No se pudieron listar memberships de {Email}: {Error}",
                usuario.Email, listError);
            return;
        }

        var aAgregar = orgsEsperadas.Except(orgsActuales).ToList();
        var aQuitar = orgsActuales.Except(orgsEsperadas).ToList();

        foreach (var orgId in aAgregar)
        {
            var (_, err) = await _identityProvider.CrearMembresiaAsync(usuario.ExternalId, orgId);
            if (err != null)
                _logger.LogWarning(
                    "No se pudo crear membership {OrgId} para {Email}: {Error}",
                    orgId, usuario.Email, err);
        }
        foreach (var orgId in aQuitar)
        {
            var (_, err) = await _identityProvider.EliminarMembresiaAsync(usuario.ExternalId, orgId);
            if (err != null)
                _logger.LogWarning(
                    "No se pudo eliminar membership {OrgId} para {Email}: {Error}",
                    orgId, usuario.Email, err);
        }

        if (aAgregar.Count > 0 || aQuitar.Count > 0)
            _logger.LogInformation(
                "Memberships sincronizadas para {Email}: +{Agregadas}/-{Quitadas}",
                usuario.Email, aAgregar.Count, aQuitar.Count);
    }
}
