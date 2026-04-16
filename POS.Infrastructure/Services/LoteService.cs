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
            l.CantidadDisponible,
            l.FechaEntrada
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
                l.CantidadDisponible,
                l.FechaEntrada
            )));
        }

        return alertas.OrderBy(a => a.DiasParaVencer).ToList();
    }

    public async Task<(TrazabilidadLoteDto? result, string? error)> ObtenerTrazabilidadAsync(int loteId)
    {
        var lote = await _context.LotesInventario
            .Include(l => l.Producto)
            .Include(l => l.Sucursal)
            .FirstOrDefaultAsync(l => l.Id == loteId);

        if (lote == null) return (null, "NOT_FOUND");

        var loteDto = new LoteDto(
            lote.Id, lote.ProductoId, lote.Producto.Nombre, lote.Producto.CodigoBarras,
            lote.SucursalId, lote.Sucursal.Nombre, lote.NumeroLote, lote.FechaVencimiento,
            lote.OrdenCompraId, lote.CantidadInicial, lote.CantidadDisponible,
            lote.CostoUnitario, lote.Referencia, lote.FechaEntrada);

        // ── Entrada original ──────────────────────────────────────────────────
        TrazabilidadEntradaDto? entrada = null;
        if (lote.OrdenCompraId.HasValue)
        {
            var orden = await _context.OrdenesCompra
                .Include(o => o.Proveedor)
                .FirstOrDefaultAsync(o => o.Id == lote.OrdenCompraId.Value);
            if (orden != null)
                entrada = new TrazabilidadEntradaDto(
                    "OrdenCompra", orden.NumeroOrden,
                    orden.FechaRecepcion ?? orden.FechaOrden,
                    orden.Proveedor.Nombre,
                    lote.CantidadInicial, lote.CostoUnitario);
        }
        else if (!string.IsNullOrEmpty(lote.Referencia))
        {
            var tipo = lote.Referencia.StartsWith("TRAS-") ? "Traslado" : "EntradaManual";
            entrada = new TrazabilidadEntradaDto(
                tipo, lote.Referencia, lote.FechaEntrada,
                null, lote.CantidadInicial, lote.CostoUnitario);
        }

        // ── Movimientos: ventas, devoluciones, traslados ──────────────────────
        var movimientos = new List<TrazabilidadMovimientoDto>();

        // Ventas que consumieron este lote
        var detallesVenta = await _context.DetalleVentas
            .Include(dv => dv.Venta)
            .Where(dv => dv.LoteInventarioId == loteId)
            .OrderBy(dv => dv.Venta.FechaVenta)
            .ToListAsync();

        foreach (var dv in detallesVenta)
            movimientos.Add(new TrazabilidadMovimientoDto(
                "Venta", dv.Venta.NumeroVenta, dv.Venta.FechaVenta,
                dv.Cantidad, $"Precio: ${dv.PrecioUnitario:N0}"));

        // Devoluciones que reintegraron a este lote
        var detallesDev = await _context.DetallesDevolucion
            .Include(dd => dd.DevolucionVenta)
            .Where(dd => dd.LoteInventarioId == loteId)
            .OrderBy(dd => dd.DevolucionVenta.FechaDevolucion)
            .ToListAsync();

        foreach (var dd in detallesDev)
            movimientos.Add(new TrazabilidadMovimientoDto(
                "Devolucion", dd.DevolucionVenta.NumeroDevolucion,
                dd.DevolucionVenta.FechaDevolucion,
                dd.CantidadDevuelta, dd.DevolucionVenta.Motivo));

        // Traslados donde este lote fue enviado
        var detallesTraslado = await _context.DetallesTraslado
            .Include(dt => dt.Traslado).ThenInclude(t => t.SucursalDestino)
            .Where(dt => dt.LoteInventarioId == loteId)
            .OrderBy(dt => dt.Traslado.FechaEnvio)
            .ToListAsync();

        foreach (var dt in detallesTraslado)
            movimientos.Add(new TrazabilidadMovimientoDto(
                "Traslado", dt.Traslado.NumeroTraslado,
                dt.Traslado.FechaEnvio ?? dt.Traslado.FechaTraslado,
                dt.CantidadSolicitada,
                $"Destino: {dt.Traslado.SucursalDestino?.Nombre ?? "—"}"));

        movimientos = movimientos.OrderBy(m => m.Fecha).ToList();

        return (new TrazabilidadLoteDto(loteDto, entrada, movimientos), null);
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
