using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class EmpresaService : IEmpresaService
{
    private readonly AppDbContext _context;

    public EmpresaService(AppDbContext context) => _context = context;

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

        empresa.Nombre      = dto.Nombre.Trim();
        empresa.Nit         = dto.Nit?.Trim();
        empresa.RazonSocial = dto.RazonSocial?.Trim();
        empresa.Activo      = dto.Activo;

        await _context.SaveChangesAsync();
        return (ToDto(empresa), null);
    }

    private static EmpresaDto ToDto(Empresa e) => new(
        e.Id, e.Nombre, e.Nit, e.RazonSocial, e.Activo, e.FechaCreacion, e.Sucursales.Count);
}
