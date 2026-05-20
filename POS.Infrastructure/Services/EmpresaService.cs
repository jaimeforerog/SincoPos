using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed class EmpresaService : IEmpresaService
{
    private readonly AppDbContext _context;
    private readonly IIdentityProviderService _identityProvider;
    private readonly ILogger<EmpresaService> _logger;

    public EmpresaService(
        AppDbContext context,
        IIdentityProviderService identityProvider,
        ILogger<EmpresaService> logger)
    {
        _context = context;
        _identityProvider = identityProvider;
        _logger = logger;
    }

    public async Task<List<EmpresaDto>> ObtenerTodasAsync()
    {
        return await _context.Empresas
            .IgnoreQueryFilters()
            .OrderBy(e => e.Nombre)
            .Select(e => new EmpresaDto(
                e.Id,
                e.Nombre,
                e.Nit,
                e.RazonSocial,
                e.Activo,
                e.FechaCreacion,
                e.Sucursales.Count))
            .ToListAsync();
    }

    public async Task<EmpresaDto?> ObtenerPorIdAsync(int id)
    {
        var e = await _context.Empresas
            .IgnoreQueryFilters()
            .Include(x => x.Sucursales)
            .FirstOrDefaultAsync(x => x.Id == id);

        return e == null ? null : ToDto(e);
    }

    public async Task<(EmpresaDto? result, string? error)> CrearAsync(CrearEmpresaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return (null, "El nombre de la empresa es requerido.");

        if (!string.IsNullOrEmpty(dto.Nit) &&
            await _context.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Nit == dto.Nit))
            return (null, "Ya existe una empresa con ese NIT.");

        var empresa = new Empresa
        {
            Nombre      = dto.Nombre.Trim(),
            Nit         = dto.Nit?.Trim(),
            RazonSocial = dto.RazonSocial?.Trim(),
        };

        _context.Empresas.Add(empresa);
        await _context.SaveChangesAsync();

        // Sincronizar con WorkOS Organization
        var (orgId, orgError) = await _identityProvider.CrearOrganizacionAsync(empresa.Nombre);
        if (orgId != null)
        {
            empresa.WorkOsOrganizationId = orgId;
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning(
                "No se pudo crear Organization en WorkOS para empresa {Id} ({Nombre}): {Error}. " +
                "Se puede sincronizar luego via /admin/sync-workos.",
                empresa.Id, empresa.Nombre, orgError);
        }

        return (ToDto(empresa), null);
    }

    public async Task<(EmpresaDto? result, string? error)> ActualizarAsync(int id, ActualizarEmpresaDto dto)
    {
        var empresa = await _context.Empresas
            .IgnoreQueryFilters()
            .Include(x => x.Sucursales)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (empresa == null) return (null, "NOT_FOUND");

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return (null, "El nombre de la empresa es requerido.");

        if (!string.IsNullOrEmpty(dto.Nit) &&
            await _context.Empresas.IgnoreQueryFilters()
                .AnyAsync(e => e.Nit == dto.Nit && e.Id != id))
            return (null, "Ya existe otra empresa con ese NIT.");

        var nombreCambio = empresa.Nombre != dto.Nombre.Trim();

        empresa.Nombre      = dto.Nombre.Trim();
        empresa.Nit         = dto.Nit?.Trim();
        empresa.RazonSocial = dto.RazonSocial?.Trim();
        empresa.Activo      = dto.Activo;

        await _context.SaveChangesAsync();

        if (nombreCambio && !string.IsNullOrEmpty(empresa.WorkOsOrganizationId))
        {
            var (_, err) = await _identityProvider.ActualizarOrganizacionAsync(
                empresa.WorkOsOrganizationId, empresa.Nombre);
            if (err != null)
                _logger.LogWarning(
                    "No se pudo actualizar Organization {OrgId} en WorkOS: {Error}",
                    empresa.WorkOsOrganizationId, err);
        }

        return (ToDto(empresa), null);
    }

    public async Task<(int Sincronizadas, int Fallidas)> SincronizarOrganizacionesAsync()
    {
        var pendientes = await _context.Empresas
            .IgnoreQueryFilters()
            .Where(e => e.WorkOsOrganizationId == null)
            .ToListAsync();

        int ok = 0, fail = 0;
        foreach (var empresa in pendientes)
        {
            var (orgId, err) = await _identityProvider.CrearOrganizacionAsync(empresa.Nombre);
            if (orgId != null)
            {
                empresa.WorkOsOrganizationId = orgId;
                ok++;
            }
            else
            {
                _logger.LogWarning(
                    "Backfill: no se pudo crear Organization para empresa {Id} ({Nombre}): {Error}",
                    empresa.Id, empresa.Nombre, err);
                fail++;
            }
        }
        if (ok > 0)
            await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Backfill Organizations: {Ok} sincronizadas, {Fail} fallidas (de {Total} pendientes)",
            ok, fail, pendientes.Count);
        return (ok, fail);
    }

    private static EmpresaDto ToDto(Empresa e) => new(
        e.Id, e.Nombre, e.Nit, e.RazonSocial, e.Activo, e.FechaCreacion, e.Sucursales.Count);
}
