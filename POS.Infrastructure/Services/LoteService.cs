using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public class LoteService : ILoteService
{
    private readonly AppDbContext _context;

    public LoteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<LoteDto>> ObtenerLotesAsync(Guid productoId, int sucursalId, bool soloVigentes = true)
    {
        var query = _context.LotesInventario
            .Include(l => l.Producto)
            .Include(l => l.Sucursal)
            .Where(l => l.ProductoId == productoId && l.SucursalId == sucursalId);

        if (soloVigentes)
            query = query.Where(l => l.CantidadDisponible > 0);

        var lotes = await query.OrderBy(l => l.FechaVencimiento).ThenBy(l => l.FechaEntrada).ToListAsync();

        return lotes.Select(l => new LoteDto(
            l.Id,
            l.ProductoId,
            l.Producto.Nombre,
            l.Producto.CodigoBarras,
            l.SucursalId,
            l.Sucursal.Nombre,
            l.NumeroLote,
            l.FechaVencimiento,
            l.OrdenCompraId,
            l.CantidadInicial,
            l.CantidadDisponible,
            l.CostoUnitario,
            l.Referencia,
            l.FechaEntrada
        )).ToList();
    }

    public async Task<List<AlertaLoteDto>> ObtenerProximosAVencerAsync(int sucursalId, int diasAnticipacion)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var limite = hoy.AddDays(diasAnticipacion);

        var lotes = await _context.LotesInventario
            .Include(l => l.Producto)
            .Include(l => l.Sucursal)
            .Where(l => l.SucursalId == sucursalId
                     && l.CantidadDisponible > 0
                     && l.FechaVencimiento != null
                     && l.FechaVencimiento <= limite)
            .OrderBy(l => l.FechaVencimiento)
            .ToListAsync();

        return lotes.Select(l => new AlertaLoteDto(
            l.Id,
            l.ProductoId,
            l.Producto.Nombre,
            l.Producto.CodigoBarras,
            l.SucursalId,
            l.Sucursal.Nombre,
            l.NumeroLote,
            l.FechaVencimiento!.Value,
            l.FechaVencimiento!.Value.DayNumber - hoy.DayNumber,
            l.CantidadDisponible
        )).ToList();
    }

    public async Task<List<AlertaLoteDto>> ObtenerTodasLasAlertasAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Obtener todas las sucursales con su configuración de días de alerta
        var sucursales = await _context.Sucursales
            .Where(s => s.Activo && s.DiasAlertaVencimientoLotes > 0)
            .ToListAsync();

        var alertas = new List<AlertaLoteDto>();

        foreach (var sucursal in sucursales)
        {
            var limite = hoy.AddDays(sucursal.DiasAlertaVencimientoLotes);

            var lotes = await _context.LotesInventario
                .Include(l => l.Producto)
                .Include(l => l.Sucursal)
                .Where(l => l.SucursalId == sucursal.Id
                         && l.CantidadDisponible > 0
                         && l.FechaVencimiento != null
                         && l.FechaVencimiento <= limite)
                .OrderBy(l => l.FechaVencimiento)
                .ToListAsync();

            alertas.AddRange(lotes.Select(l => new AlertaLoteDto(
                l.Id,
                l.ProductoId,
                l.Producto.Nombre,
                l.Producto.CodigoBarras,
                l.SucursalId,
                l.Sucursal.Nombre,
                l.NumeroLote,
                l.FechaVencimiento!.Value,
                l.FechaVencimiento!.Value.DayNumber - hoy.DayNumber,
                l.CantidadDisponible
            )));
        }

        return alertas.OrderBy(a => a.DiasParaVencer).ToList();
    }

    public async Task<(LoteDto? result, string? error)> ActualizarLoteAsync(int id, ActualizarLoteDto dto)
    {
        var lote = await _context.LotesInventario
            .Include(l => l.Producto)
            .Include(l => l.Sucursal)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lote == null)
            return (null, "NOT_FOUND");

        lote.NumeroLote = dto.NumeroLote;
        lote.FechaVencimiento = dto.FechaVencimiento;
        await _context.SaveChangesAsync();

        return (new LoteDto(
            lote.Id, lote.ProductoId, lote.Producto.Nombre, lote.Producto.CodigoBarras,
            lote.SucursalId, lote.Sucursal.Nombre, lote.NumeroLote, lote.FechaVencimiento,
            lote.OrdenCompraId, lote.CantidadInicial, lote.CantidadDisponible,
            lote.CostoUnitario, lote.Referencia, lote.FechaEntrada
        ), null);
    }
}
