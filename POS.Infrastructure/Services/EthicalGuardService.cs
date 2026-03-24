using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class EthicalGuardService : IEthicalGuardService
{
    private readonly AppDbContext _context;

    public EthicalGuardService(AppDbContext context) => _context = context;

    // ── Evaluación principal ───────────────────────────────────────────────

    public async Task<(bool permitido, string? error)> EvaluarVentaAsync(EvaluarVentaEticaDto dto)
    {
        var reglas = await _context.ReglasEticas
            .Where(r => r.Activo && r.Contexto == ContextoReglaEtica.Venta)
            .ToListAsync();

        foreach (var regla in reglas)
        {
            var (disparada, detalle) = EvaluarRegla(regla, dto);
            if (!disparada) continue;

            // Registrar activación
            var activacion = new ActivacionReglaEtica
            {
                ReglaEticaId = regla.Id,
                SucursalId = dto.SucursalId,
                UsuarioId = dto.UsuarioId,
                Detalle = detalle,
                AccionTomada = regla.Accion,
            };
            _context.ActivacionesReglaEtica.Add(activacion);
            await _context.SaveChangesAsync();

            if (regla.Accion == AccionReglaEtica.Bloquear)
            {
                var msg = regla.Mensaje ?? $"Regla ética '{regla.Nombre}' bloqueó la transacción: {detalle}";
                return (false, msg);
            }
        }

        return (true, null);
    }

    private static (bool disparada, string detalle) EvaluarRegla(ReglaEtica regla, EvaluarVentaEticaDto dto)
    {
        return regla.Condicion switch
        {
            TipoCondicionEtica.DescuentoMaximoPorcentaje => EvaluarDescuento(regla, dto),
            TipoCondicionEtica.MontoMaximoTransaccion    => EvaluarMontoMax(regla, dto),
            TipoCondicionEtica.MaximoLineasVenta         => EvaluarLineas(regla, dto),
            TipoCondicionEtica.PrecioMinimoSobreBase     => EvaluarPrecioMinimo(regla, dto),
            _                                            => (false, string.Empty),
        };
    }

    private static (bool, string) EvaluarDescuento(ReglaEtica regla, EvaluarVentaEticaDto dto)
    {
        if (dto.Subtotal == 0) return (false, string.Empty);
        var pctDescuento = dto.DescuentoTotal / dto.Subtotal * 100;
        if (pctDescuento <= regla.ValorLimite) return (false, string.Empty);
        return (true, $"Descuento {pctDescuento:N2}% supera límite {regla.ValorLimite}%");
    }

    private static (bool, string) EvaluarMontoMax(ReglaEtica regla, EvaluarVentaEticaDto dto)
    {
        var total = dto.Subtotal - dto.DescuentoTotal;
        if (total <= regla.ValorLimite) return (false, string.Empty);
        return (true, $"Monto ${total:N2} supera límite ${regla.ValorLimite:N2}");
    }

    private static (bool, string) EvaluarLineas(ReglaEtica regla, EvaluarVentaEticaDto dto)
    {
        if (dto.NumeroLineas <= (int)regla.ValorLimite) return (false, string.Empty);
        return (true, $"{dto.NumeroLineas} líneas supera límite {regla.ValorLimite}");
    }

    private static (bool, string) EvaluarPrecioMinimo(ReglaEtica regla, EvaluarVentaEticaDto dto)
    {
        foreach (var linea in dto.Lineas)
        {
            if (linea.PrecioBase == 0) continue;
            var pct = linea.PrecioUnitario / linea.PrecioBase * 100;
            if (pct < regla.ValorLimite)
                return (true, $"Precio {linea.PrecioUnitario:N2} es {pct:N1}% del base, mínimo {regla.ValorLimite}%");
        }
        return (false, string.Empty);
    }

    // ── CRUD ──────────────────────────────────────────────────────────────

    public async Task<List<ReglaEticaDto>> ObtenerReglasAsync() =>
        await _context.ReglasEticas
            .OrderBy(r => r.Nombre)
            .Select(r => ToDto(r))
            .ToListAsync();

    public async Task<ReglaEticaDto?> ObtenerPorIdAsync(int id)
    {
        var r = await _context.ReglasEticas.FindAsync(id);
        return r == null ? null : ToDto(r);
    }

    public async Task<ReglaEticaDto> CrearAsync(CrearReglaEticaDto dto)
    {
        var regla = new ReglaEtica
        {
            Nombre      = dto.Nombre,
            Contexto    = Enum.Parse<ContextoReglaEtica>(dto.Contexto),
            Condicion   = Enum.Parse<TipoCondicionEtica>(dto.Condicion),
            ValorLimite = dto.ValorLimite,
            Accion      = Enum.Parse<AccionReglaEtica>(dto.Accion),
            Mensaje     = dto.Mensaje,
            Activo      = dto.Activo,
        };
        _context.ReglasEticas.Add(regla);
        await _context.SaveChangesAsync();
        return ToDto(regla);
    }

    public async Task<(ReglaEticaDto? result, string? error)> ActualizarAsync(int id, CrearReglaEticaDto dto)
    {
        var regla = await _context.ReglasEticas.FindAsync(id);
        if (regla == null) return (null, "NOT_FOUND");

        regla.Nombre      = dto.Nombre;
        regla.Contexto    = Enum.Parse<ContextoReglaEtica>(dto.Contexto);
        regla.Condicion   = Enum.Parse<TipoCondicionEtica>(dto.Condicion);
        regla.ValorLimite = dto.ValorLimite;
        regla.Accion      = Enum.Parse<AccionReglaEtica>(dto.Accion);
        regla.Mensaje     = dto.Mensaje;
        regla.Activo      = dto.Activo;

        await _context.SaveChangesAsync();
        return (ToDto(regla), null);
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var regla = await _context.ReglasEticas.FindAsync(id);
        if (regla == null) return false;
        _context.ReglasEticas.Remove(regla);
        await _context.SaveChangesAsync();
        return true;
    }

    // ── Historial ─────────────────────────────────────────────────────────

    public async Task<List<ActivacionReglaEticaDto>> ObtenerActivacionesAsync(int? reglaId = null, int take = 50) =>
        await _context.ActivacionesReglaEtica
            .Include(a => a.Regla)
            .Where(a => reglaId == null || a.ReglaEticaId == reglaId)
            .OrderByDescending(a => a.FechaActivacion)
            .Take(take)
            .Select(a => new ActivacionReglaEticaDto(
                a.Id,
                a.ReglaEticaId,
                a.Regla.Nombre,
                a.VentaId,
                a.SucursalId,
                a.Detalle,
                a.AccionTomada.ToString(),
                a.FechaActivacion))
            .ToListAsync();

    // ── Mapeo ─────────────────────────────────────────────────────────────

    private static ReglaEticaDto ToDto(ReglaEtica r) => new(
        r.Id, r.EmpresaId, r.Nombre,
        r.Contexto.ToString(), r.Condicion.ToString(),
        r.ValorLimite, r.Accion.ToString(),
        r.Mensaje, r.Activo, r.FechaCreacion);
}
