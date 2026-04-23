using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed class TerceroLocalService : ITerceroService
{
    private readonly AppDbContext _context;
    private readonly ICurrentEmpresaProvider _empresaProvider;

    public TerceroLocalService(AppDbContext context, ICurrentEmpresaProvider empresaProvider)
    {
        _context = context;
        _empresaProvider = empresaProvider;
    }

    public async Task<TerceroDto?> ObtenerPorIdAsync(int id)
    {
        var t = await _context.Terceros
            .Include(t => t.Actividades)
            .FirstOrDefaultAsync(t => t.Id == id);
        return t == null ? null : MapToDtoLocal(t);
    }

    public async Task<TerceroDto?> ObtenerPorIdentificacionAsync(string identificacion)
    {
        var t = await _context.Terceros
            .Include(t => t.Actividades)
            .FirstOrDefaultAsync(t => t.Identificacion == identificacion);
        return t == null ? null : MapToDtoLocal(t);
    }

    public async Task<PaginatedResult<TerceroDto>> BuscarAsync(
        string? query, string? tipoTercero, bool incluirInactivos, int page = 1, int pageSize = 50)
    {
        var q = _context.Terceros
            .Include(t => t.Actividades)
            .AsNoTracking();

        if (incluirInactivos)
            q = q.IgnoreQueryFilters();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(t =>
                t.Nombre.Contains(query) ||
                t.Identificacion.Contains(query) ||
                (t.Email != null && t.Email.Contains(query)));

        if (!string.IsNullOrWhiteSpace(tipoTercero) &&
            Enum.TryParse<TipoTercero>(tipoTercero, true, out var tipo))
            q = q.Where(t => t.TipoTercero == tipo);

        var totalCount = await q.CountAsync();
        var items = await q
            .OrderBy(t => t.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedResult<TerceroDto>(
            items.Select(MapToDtoLocal).ToList(), totalCount, page, pageSize, totalPages);
    }

    public async Task<(TerceroDto? Result, string? Error)> CrearAsync(CrearTerceroDto dto)
    {
        if (!Enum.TryParse<TipoIdentificacion>(dto.TipoIdentificacion, true, out var tipoId))
            return (null, $"Tipo de identificacion invalido. Valores: {string.Join(", ", Enum.GetNames<TipoIdentificacion>())}");

        if (!Enum.TryParse<TipoTercero>(dto.TipoTercero, true, out var tipoTercero))
            return (null, $"Tipo de tercero invalido. Valores: {string.Join(", ", Enum.GetNames<TipoTercero>())}");

        var existe = await _context.Terceros.AnyAsync(t => t.Identificacion == dto.Identificacion);
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
            CodigoDepartamento = dto.CodigoDepartamento,
            CodigoMunicipio = dto.CodigoMunicipio,
            PerfilTributario = dto.PerfilTributario ?? "REGIMEN_COMUN",
            EsGranContribuyente = dto.EsGranContribuyente,
            EsAutorretenedor = dto.EsAutorretenedor,
            EsResponsableIVA = dto.EsResponsableIVA,
            EmpresaId = _empresaProvider.EmpresaId ?? throw new InvalidOperationException("EmpresaId es requerido."),
            OrigenDatos = OrigenDatos.Local,
        };

        if (tipoId == TipoIdentificacion.NIT)
            tercero.DigitoVerificacion = CalcularDV(dto.Identificacion);

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
        tercero.CodigoDepartamento = dto.CodigoDepartamento;
        tercero.CodigoMunicipio = dto.CodigoMunicipio;
        tercero.EsGranContribuyente = dto.EsGranContribuyente;
        tercero.EsAutorretenedor = dto.EsAutorretenedor;
        tercero.EsResponsableIVA = dto.EsResponsableIVA;
        tercero.FechaModificacion = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.PerfilTributario))
            tercero.PerfilTributario = dto.PerfilTributario;

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
        var tercero = await _context.Terceros
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        if (!tercero.Activo)
            return (false, $"Tercero {id} ya está inactivo.");

        tercero.Activo = false;
        tercero.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ActivarAsync(int id)
    {
        var tercero = await _context.Terceros
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        if (tercero.Activo)
            return (false, $"Tercero {id} ya está activo.");

        tercero.Activo = true;
        tercero.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    // ── Actividades CIIU ──────────────────────────────────────────────────────

    public async Task<(TerceroActividadDto? Result, string? Error)> AgregarActividadAsync(int terceroId, AgregarActividadDto dto)
    {
        var tercero = await _context.Terceros
            .Include(t => t.Actividades)
            .FirstOrDefaultAsync(t => t.Id == terceroId);

        if (tercero == null)
            return (null, $"Tercero {terceroId} no encontrado.");

        var existe = tercero.Actividades.Any(a => a.CodigoCIIU == dto.CodigoCIIU);
        if (existe)
            return (null, $"El tercero ya tiene la actividad CIIU {dto.CodigoCIIU}.");

        if (dto.EsPrincipal)
        {
            foreach (var act in tercero.Actividades.Where(a => a.EsPrincipal))
                act.EsPrincipal = false;
        }

        var actividad = new TerceroActividad
        {
            TerceroId = terceroId,
            CodigoCIIU = dto.CodigoCIIU,
            Descripcion = dto.Descripcion,
            EsPrincipal = dto.EsPrincipal,
        };

        _context.TerceroActividades.Add(actividad);
        await _context.SaveChangesAsync();

        return (new TerceroActividadDto(actividad.Id, actividad.CodigoCIIU, actividad.Descripcion, actividad.EsPrincipal), null);
    }

    public async Task<(bool Success, string? Error)> EliminarActividadAsync(int terceroId, int actividadId)
    {
        var actividad = await _context.TerceroActividades
            .FirstOrDefaultAsync(a => a.Id == actividadId && a.TerceroId == terceroId);

        if (actividad == null)
            return (false, $"Actividad {actividadId} no encontrada para el tercero {terceroId}.");

        _context.TerceroActividades.Remove(actividad);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> EstablecerPrincipalAsync(int terceroId, int actividadId)
    {
        var actividades = await _context.TerceroActividades
            .Where(a => a.TerceroId == terceroId)
            .ToListAsync();

        if (!actividades.Any())
            return (false, $"El tercero {terceroId} no tiene actividades.");

        var target = actividades.FirstOrDefault(a => a.Id == actividadId);
        if (target == null)
            return (false, $"Actividad {actividadId} no encontrada para el tercero {terceroId}.");

        foreach (var act in actividades)
            act.EsPrincipal = act.Id == actividadId;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ── DV Módulo 11 DIAN ─────────────────────────────────────────────────────

    public static string CalcularDV(string nit)
    {
        var pesos = new[] { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };
        var digits = nit.Where(char.IsDigit).Reverse().ToArray();
        int suma = digits.Select((c, i) => (c - '0') * pesos[i]).Sum();
        int r = suma % 11;
        return (r < 2 ? r : 11 - r).ToString();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TerceroDto MapToDtoLocal(Tercero t) => new TerceroDto(
        t.Id,
        t.TipoIdentificacion.ToString(),
        t.Identificacion,
        t.DigitoVerificacion,
        t.Nombre,
        t.TipoTercero.ToString(),
        t.Telefono,
        t.Email,
        t.Direccion,
        t.Ciudad,
        t.CodigoDepartamento,
        t.CodigoMunicipio,
        t.PerfilTributario,
        t.EsGranContribuyente,
        t.EsAutorretenedor,
        t.EsResponsableIVA,
        t.OrigenDatos.ToString(),
        t.ExternalId,
        t.Activo,
        t.Actividades
            .Select(a => new TerceroActividadDto(a.Id, a.CodigoCIIU, a.Descripcion, a.EsPrincipal))
            .ToList()
    );
}
