using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

public sealed partial class UsuarioService
{
    public async Task<PerfilUsuarioDto?> ConstruirPerfilCompletoAsync(
        string externalId, string email, string nombreCompleto, IReadOnlyList<string> idpRoles)
    {
        var rolPrincipal = idpRoles.Count > 0
            ? RolPermisos.DeterminarRolPrincipal(idpRoles)
            : null;

        _logger.LogInformation(
            "ConstruirPerfilCompleto: externalId={Id}, roles=[{Roles}], rolDeterminado={Rol}",
            externalId, string.Join(",", idpRoles), rolPrincipal ?? "(sin rol)");

        // Las APIs de IUsuarioService están implementadas explícitamente (Task<UsuarioDto> IUsuarioService.X),
        // por lo que sólo son accesibles a través del cast a la interfaz.
        IUsuarioService self = this;

        await self.ObtenerOCrearUsuarioAsync(externalId, email, nombreCompleto, rolPrincipal);

        // Reintento con back-off: la creación puede aún no ser visible por latencia de BD.
        PerfilUsuarioDto? perfil = null;
        for (int intento = 0; intento < 3 && perfil == null; intento++)
        {
            if (intento > 0) await Task.Delay(150 * intento);
            perfil = await self.ObtenerPerfilPorExternalIdAsync(externalId);
        }
        if (perfil == null) return null;

        var permisos = RolPermisos.ObtenerPermisosPorRol(perfil.Rol);
        var sucursalesAsignadas = perfil.SucursalesAsignadas;

        // Si el usuario es admin o supervisor y no tiene sucursales asignadas,
        // darle acceso a todas las sucursales activas.
        var esAdminOSupervisor =
            perfil.Rol.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            perfil.Rol.Equals("supervisor", StringComparison.OrdinalIgnoreCase);

        if (sucursalesAsignadas.Count == 0 && esAdminOSupervisor)
        {
            sucursalesAsignadas = await self.ObtenerTodasSucursalesActivasAsync();
            _logger.LogInformation(
                "Usuario {Email} ({Rol}) sin sucursales asignadas, usando todas las activas ({Count})",
                perfil.Email, perfil.Rol, sucursalesAsignadas.Count);
        }

        // Resolver empresa actual a partir de las sucursales asignadas.
        var sucursalIds = sucursalesAsignadas.Select(s => s.Id).ToList();
        var empresaInfo = sucursalIds.Count > 0
            ? await _context.Sucursales
                .IgnoreQueryFilters()
                .Where(s => sucursalIds.Contains(s.Id))
                .Select(s => new { s.EmpresaId, s.Empresa!.Nombre })
                .FirstOrDefaultAsync()
            : null;

        // Empresas disponibles: admin/supervisor → todas las activas (incluso sin sucursales,
        // para poder crearlas). Otros roles → solo las derivadas de sus sucursales asignadas.
        List<EmpresaResumenDto> empresasDisponibles;
        if (esAdminOSupervisor)
        {
            empresasDisponibles = await _context.Empresas
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

        return new PerfilUsuarioDto(
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
    }
}
