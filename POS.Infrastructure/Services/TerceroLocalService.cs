using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion local del servicio de terceros.
/// Almacena los datos directamente en la base de datos del POS.
/// </summary>
public class TerceroLocalService : ITerceroService
{
    private readonly AppDbContext _context;

    public TerceroLocalService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TerceroDto?> ObtenerPorIdAsync(int id)
    {
        return await _context.Terceros
            .Where(t => t.Id == id)
            .Select(t => MapToDto(t))
            .FirstOrDefaultAsync();
    }

    public async Task<TerceroDto?> ObtenerPorIdentificacionAsync(string identificacion)
    {
        return await _context.Terceros
            .Where(t => t.Identificacion == identificacion)
            .Select(t => MapToDto(t))
            .FirstOrDefaultAsync();
    }

    public async Task<List<TerceroDto>> BuscarAsync(string? query, string? tipoTercero, bool incluirInactivos)
    {
        var q = _context.Terceros.AsQueryable();

        if (!incluirInactivos)
            q = q.Where(t => t.Activo);

        if (!string.IsNullOrEmpty(tipoTercero) && Enum.TryParse<TipoTercero>(tipoTercero, true, out var tipo))
        {
            q = tipo == TipoTercero.Ambos
                ? q
                : q.Where(t => t.TipoTercero == tipo || t.TipoTercero == TipoTercero.Ambos);
        }

        if (!string.IsNullOrEmpty(query))
        {
            var lower = query.ToLower();
            q = q.Where(t =>
                t.Nombre.ToLower().Contains(lower) ||
                t.Identificacion.Contains(query));
        }

        return await q
            .OrderBy(t => t.Nombre)
            .Take(50)
            .Select(t => MapToDto(t))
            .ToListAsync();
    }

    public async Task<(TerceroDto? Result, string? Error)> CrearAsync(CrearTerceroDto dto)
    {
        if (!Enum.TryParse<TipoIdentificacion>(dto.TipoIdentificacion, true, out var tipoId))
            return (null, $"Tipo de identificacion invalido. Valores: {string.Join(", ", Enum.GetNames<TipoIdentificacion>())}");

        if (!Enum.TryParse<TipoTercero>(dto.TipoTercero, true, out var tipoTercero))
            return (null, $"Tipo de tercero invalido. Valores: {string.Join(", ", Enum.GetNames<TipoTercero>())}");

        var existe = await _context.Terceros
            .AnyAsync(t => t.Identificacion == dto.Identificacion);

        if (existe)
            return (null, $"Ya existe un tercero con identificacion {dto.Identificacion}.");

        var tercero = new Tercero
        {
            TipoIdentificacion = tipoId,
            Identificacion = dto.Identificacion,
            Nombre = dto.Nombre,
            TipoTercero = tipoTercero,
            Telefono = dto.Telefono,
            Email = dto.Email,
            Direccion = dto.Direccion,
            Ciudad = dto.Ciudad,
            OrigenDatos = OrigenDatos.Local,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Terceros.Add(tercero);
        await _context.SaveChangesAsync();

        return (MapToDtoLocal(tercero), null);
    }

    public async Task<(bool Success, string? Error)> ActualizarAsync(int id, ActualizarTerceroDto dto)
    {
        var tercero = await _context.Terceros.FindAsync(id);
        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        if (tercero.OrigenDatos == OrigenDatos.ERP)
            return (false, "No se pueden modificar terceros que provienen del ERP.");

        tercero.Nombre = dto.Nombre;
        tercero.Telefono = dto.Telefono;
        tercero.Email = dto.Email;
        tercero.Direccion = dto.Direccion;
        tercero.Ciudad = dto.Ciudad;
        tercero.FechaModificacion = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.TipoTercero))
        {
            if (!Enum.TryParse<TipoTercero>(dto.TipoTercero, true, out var tipo))
                return (false, $"Tipo de tercero invalido. Valores: {string.Join(", ", Enum.GetNames<TipoTercero>())}");
            tercero.TipoTercero = tipo;
        }

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DesactivarAsync(int id)
    {
        var tercero = await _context.Terceros.FindAsync(id);
        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        tercero.Activo = false;
        tercero.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    // Mapping para queries (expresion que EF puede traducir)
    private static TerceroDto MapToDto(Tercero t) => new(
        t.Id, t.TipoIdentificacion.ToString(), t.Identificacion,
        t.Nombre, t.TipoTercero.ToString(),
        t.Telefono, t.Email, t.Direccion, t.Ciudad,
        t.OrigenDatos.ToString(), t.ExternalId, t.Activo);

    // Mapping para objetos en memoria
    private static TerceroDto MapToDtoLocal(Tercero t) => new(
        t.Id, t.TipoIdentificacion.ToString(), t.Identificacion,
        t.Nombre, t.TipoTercero.ToString(),
        t.Telefono, t.Email, t.Direccion, t.Ciudad,
        t.OrigenDatos.ToString(), t.ExternalId, t.Activo);
}
