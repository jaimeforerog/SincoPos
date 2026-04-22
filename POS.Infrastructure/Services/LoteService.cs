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

        // ── Movimientos: ventas (por DetalleVentaLote), devoluciones, traslados ──
        // Estructura intermedia para calcular saldo al final
        var items = new List<(DateTime Fecha, string Tipo, string Referencia, decimal Cantidad, string? Detalle)>();

        // Ventas: usar DetalleVentaLotes para capturar exactamente cuánto salió de este lote,
        // incluso cuando la venta consumió varios lotes distintos para el mismo producto.
        var lotesVenta = await _context.DetalleVentaLotes
            .Include(dvl => dvl.DetalleVenta).ThenInclude(dv => dv.Venta)
            .Where(dvl => dvl.LoteInventarioId == loteId)
            .OrderBy(dvl => dvl.DetalleVenta.Venta.FechaVenta)
            .ToListAsync();

        foreach (var dvl in lotesVenta)
            items.Add((dvl.DetalleVenta.Venta.FechaVenta, "Venta",
                dvl.DetalleVenta.Venta.NumeroVenta, dvl.Cantidad,
                $"Precio: ${dvl.DetalleVenta.PrecioUnitario:N0}"));

        // Devoluciones que reintegraron a este lote
        var detallesDev = await _context.DetallesDevolucion
            .Include(dd => dd.DevolucionVenta)
            .Where(dd => dd.LoteInventarioId == loteId)
            .OrderBy(dd => dd.DevolucionVenta.FechaDevolucion)
            .ToListAsync();

        foreach (var dd in detallesDev)
            items.Add((dd.DevolucionVenta.FechaDevolucion, "Devolucion",
                dd.DevolucionVenta.NumeroDevolucion, dd.CantidadDevuelta,
                dd.DevolucionVenta.Motivo));

        // Traslados donde este lote fue enviado
        var detallesTraslado = await _context.DetallesTraslado
            .Include(dt => dt.Traslado).ThenInclude(t => t.SucursalDestino)
            .Where(dt => dt.LoteInventarioId == loteId)
            .OrderBy(dt => dt.Traslado.FechaEnvio)
            .ToListAsync();

        foreach (var dt in detallesTraslado)
            items.Add((dt.Traslado.FechaEnvio ?? dt.Traslado.FechaTraslado, "Traslado",
                dt.Traslado.NumeroTraslado, dt.CantidadSolicitada,
                $"Destino: {dt.Traslado.SucursalDestino?.Nombre ?? "—"}"));

        // Calcular saldo acumulado en orden cronológico
        var saldo = lote.CantidadInicial;
        var movimientos = items
            .OrderBy(x => x.Fecha)
            .Select(x =>
            {
                saldo += x.Tipo == "Devolucion" ? x.Cantidad : -x.Cantidad;
                return new TrazabilidadMovimientoDto(x.Tipo, x.Referencia, x.Fecha, x.Cantidad, x.Detalle, saldo);
            })
            .ToList();

        return (new TrazabilidadLoteDto(loteDto, entrada, movimientos), null);
    }

    public async Task<ReporteLotesDto> ObtenerReporteAsync(ReporteLotesQueryDto query)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var q = _context.LotesInventario
            .Include(l => l.Producto)
            .Include(l => l.Sucursal)
            .Where(l => l.Producto.ManejaLotes)
            .AsQueryable();

        if (query.SoloConStock)
            q = q.Where(l => l.CantidadDisponible > 0);

        if (query.SucursalId.HasValue)
            q = q.Where(l => l.SucursalId == query.SucursalId.Value);

        if (query.ProductoId.HasValue)
            q = q.Where(l => l.ProductoId == query.ProductoId.Value);

        var lotes = await q.OrderBy(l => l.FechaVencimiento).ThenBy(l => l.Producto.Nombre).ToListAsync();

        var items = lotes.Select(l =>
        {
            int? dias = l.FechaVencimiento.HasValue
                ? l.FechaVencimiento.Value.DayNumber - hoy.DayNumber
                : null;

            string estado = dias switch
            {
                null        => "SinFecha",
                <= 0        => "Vencido",
                <= 7        => "Critico",
                <= 30       => "Proximo",
                _           => "Vigente"
            };

            return new LoteReporteItemDto(
                l.Id, l.ProductoId, l.Producto.Nombre, l.Producto.CodigoBarras,
                l.SucursalId, l.Sucursal.Nombre, l.NumeroLote,
                l.FechaVencimiento, dias,
                l.CantidadDisponible, l.CostoUnitario,
                l.CantidadDisponible * l.CostoUnitario,
                l.Referencia, l.FechaEntrada, estado);
        }).ToList();

        if (!string.IsNullOrEmpty(query.EstadoVencimiento))
            items = items.Where(i => i.EstadoVencimiento == query.EstadoVencimiento).ToList();

        return new ReporteLotesDto(
            TotalLotes: items.Count,
            TotalUnidades: items.Sum(i => i.CantidadDisponible),
            ValorTotalInventario: items.Sum(i => i.ValorTotal),
            LotesVencidos: items.Count(i => i.EstadoVencimiento == "Vencido"),
            LotesCriticos: items.Count(i => i.EstadoVencimiento == "Critico"),
            LotesProximos: items.Count(i => i.EstadoVencimiento == "Proximo"),
            LotesVigentes: items.Count(i => i.EstadoVencimiento == "Vigente"),
            LotesSinFecha: items.Count(i => i.EstadoVencimiento == "SinFecha"),
            Items: items
        );
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
