using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio de costeo que implementa los metodos:
/// - PromedioPonderado: (stockActual * costoAnterior + cantidadNueva * costoNuevo) / total
/// - PEPS (FIFO): consume lotes del mas antiguo al mas reciente
/// - UEPS (LIFO): consume lotes del mas reciente al mas antiguo
/// </summary>
public sealed class CosteoService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CosteoService> _logger;

    public CosteoService(AppDbContext context, ILogger<CosteoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Registrar un lote al recibir mercancia
    /// </summary>
    public Task RegistrarLoteEntrada(Guid productoId, int sucursalId,
        decimal cantidad, decimal costoUnitario, decimal porcentajeImpuesto, decimal montoImpuestoUnitario,
        string? referencia, int? terceroId,
        string? numeroLote = null, DateOnly? fechaVencimiento = null, int? ordenCompraId = null,
        DateTime? fechaEntrada = null)
    {
        var lote = new LoteInventario
        {
            ProductoId = productoId,
            SucursalId = sucursalId,
            CantidadInicial = cantidad,
            CantidadDisponible = cantidad,
            CostoUnitario = costoUnitario,
            PorcentajeImpuesto = porcentajeImpuesto,
            MontoImpuestoUnitario = montoImpuestoUnitario,
            NumeroLote = numeroLote,
            FechaVencimiento = fechaVencimiento,
            OrdenCompraId = ordenCompraId,
            Referencia = referencia,
            TerceroId = terceroId,
            FechaEntrada = fechaEntrada.HasValue
                ? DateTime.SpecifyKind(fechaEntrada.Value, DateTimeKind.Utc)
                : DateTime.UtcNow
        };
        _context.LotesInventario.Add(lote);
        _logger.LogDebug("Lote registrado: Producto {ProductoId} Sucursal {SucursalId} Cantidad {Cantidad} Costo {Costo} Lote {NumeroLote}",
            productoId, sucursalId, cantidad, costoUnitario, numeroLote ?? "sin número");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reintegra unidades a un lote existente (devolución de venta con trazabilidad de lote).
    /// Incrementa CantidadDisponible del lote original en lugar de crear uno nuevo.
    /// </summary>
    public async Task<bool> ReintegrarLoteAsync(int loteId, decimal cantidad)
    {
        var lote = await _context.LotesInventario.FindAsync(loteId);
        if (lote == null)
        {
            _logger.LogWarning("ReintegrarLote: Lote {LoteId} no encontrado. No se pudo reintegrar {Cantidad} unidades.", loteId, cantidad);
            return false;
        }
        lote.CantidadDisponible += cantidad;
        _logger.LogInformation("Lote {LoteId} reintegrado: +{Cantidad} unidades (nuevo disponible: {Disponible})",
            loteId, cantidad, lote.CantidadDisponible);
        return true;
    }

    /// <summary>
    /// Calcular y actualizar el costo en stock segun el metodo de la sucursal
    /// </summary>
    public async Task ActualizarCostoEntrada(Stock stock, decimal cantidadNueva,
        decimal costoUnitario, MetodoCosteo metodo, List<LoteInventario>? lotesExistentes = null)
    {
        switch (metodo)
        {
            case MetodoCosteo.PromedioPonderado:
                var costoTotalAnterior = stock.Cantidad * stock.CostoPromedio;
                var costoEntrada = cantidadNueva * costoUnitario;
                var cantidadTotal = stock.Cantidad + cantidadNueva;
                stock.CostoPromedio = cantidadTotal > 0
                    ? (costoTotalAnterior + costoEntrada) / cantidadTotal
                    : costoUnitario;
                break;

            case MetodoCosteo.PEPS: // FIFO - costo promedio se recalcula de lotes disponibles
            case MetodoCosteo.UEPS: // LIFO - igual, costo promedio es referencial
                stock.CostoPromedio = await CalcularCostoPromedioDesdelotes(
                    stock.ProductoId, stock.SucursalId, cantidadNueva, costoUnitario, lotesExistentes);
                break;
        }

        stock.Cantidad += cantidadNueva;
        stock.UltimaActualizacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Consumir stock segun el metodo de la sucursal.
    /// Retorna el costo total de las unidades consumidas.
    /// </summary>
    public async Task<(decimal costoTotal, decimal costoUnitarioPromedio)> ConsumirStock(
        Guid productoId, int sucursalId, decimal cantidad, MetodoCosteo metodo)
    {
        _logger.LogDebug("ConsumirStock: Producto {ProductoId} Sucursal {SucursalId} Cantidad {Cantidad} Método {Metodo}",
            productoId, sucursalId, cantidad, metodo);

        switch (metodo)
        {
            case MetodoCosteo.PEPS: // FIFO - del mas antiguo al mas reciente
                return await ConsumirLotes(productoId, sucursalId, cantidad, ordenAscendente: true);

            case MetodoCosteo.UEPS: // LIFO - del mas reciente al mas antiguo
                return await ConsumirLotes(productoId, sucursalId, cantidad, ordenAscendente: false);

            case MetodoCosteo.PromedioPonderado:
            default:
                // Usa costo promedio del stock
                var stock = await _context.Stock
                    .FirstAsync(s => s.ProductoId == productoId && s.SucursalId == sucursalId);
                var costo = cantidad * stock.CostoPromedio;
                // Tambien consumir lotes proporcionalmente (para mantener consistencia)
                await ConsumirLotesProporcional(productoId, sucursalId, cantidad);
                return (costo, stock.CostoPromedio);
        }
    }

    /// <summary>
    /// FEFO (First Expired, First Out): consume el lote con fecha de vencimiento más próxima primero.
    /// Usado para productos con ManejaLotes = true.
    /// Retorna el costo total, costo unitario, id del primer lote consumido y su número de lote.
    /// </summary>
    public async Task<(decimal costoTotal, decimal costoUnitarioPromedio, List<ConsumoLoteItem> lotesConsumidos)> ConsumirLotesFEFO(
        Guid productoId, int sucursalId, decimal cantidadAConsumir)
    {
        // Primero lotes con vencimiento (FEFO), luego los sin fecha por FechaEntrada (FIFO)
        var lotesConVencimiento = await _context.LotesInventario
            .Where(l => l.ProductoId == productoId
                     && l.SucursalId == sucursalId
                     && l.CantidadDisponible > 0
                     && l.FechaVencimiento != null)
            .OrderBy(l => l.FechaVencimiento)
            .ThenBy(l => l.FechaEntrada)
            .ToListAsync();

        var lotesSinVencimiento = await _context.LotesInventario
            .Where(l => l.ProductoId == productoId
                     && l.SucursalId == sucursalId
                     && l.CantidadDisponible > 0
                     && l.FechaVencimiento == null)
            .OrderBy(l => l.FechaEntrada)
            .ToListAsync();

        var lotes = lotesConVencimiento.Concat(lotesSinVencimiento).ToList();

        decimal costoTotal = 0;
        decimal cantidadRestante = cantidadAConsumir;
        var lotesConsumidos = new List<ConsumoLoteItem>();

        foreach (var lote in lotes)
        {
            if (cantidadRestante <= 0) break;

            var cantidadDelLote = Math.Min(lote.CantidadDisponible, cantidadRestante);
            costoTotal += cantidadDelLote * lote.CostoUnitario;
            lote.CantidadDisponible -= cantidadDelLote;
            cantidadRestante -= cantidadDelLote;

            lotesConsumidos.Add(new ConsumoLoteItem(lote.Id, lote.NumeroLote, cantidadDelLote, lote.CostoUnitario));
        }

        var costoUnitario = cantidadAConsumir > 0 ? costoTotal / cantidadAConsumir : 0;

        if (cantidadRestante > 0)
            _logger.LogWarning("ConsumirLotesFEFO: Stock insuficiente para Producto {ProductoId} Sucursal {SucursalId}. Faltaron {Faltante} unidades.",
                productoId, sucursalId, cantidadRestante);
        else
            _logger.LogDebug("FEFO consumido: Producto {ProductoId} Lotes {Count} CostoTotal {Costo}",
                productoId, lotesConsumidos.Count, costoTotal);

        return (costoTotal, costoUnitario, lotesConsumidos);
    }

    /// <summary>
    /// FIFO/LIFO: consumir de lotes especificos
    /// </summary>
    private async Task<(decimal costoTotal, decimal costoUnitarioPromedio)> ConsumirLotes(
        Guid productoId, int sucursalId, decimal cantidadAConsumir, bool ordenAscendente)
    {
        var lotesQuery = _context.LotesInventario
            .Where(l => l.ProductoId == productoId
                     && l.SucursalId == sucursalId
                     && l.CantidadDisponible > 0);

        var lotes = ordenAscendente
            ? await lotesQuery.OrderBy(l => l.FechaEntrada).ToListAsync()        // FIFO
            : await lotesQuery.OrderByDescending(l => l.FechaEntrada).ToListAsync(); // LIFO

        decimal costoTotal = 0;
        decimal cantidadRestante = cantidadAConsumir;

        foreach (var lote in lotes)
        {
            if (cantidadRestante <= 0) break;

            var cantidadDelLote = Math.Min(lote.CantidadDisponible, cantidadRestante);
            costoTotal += cantidadDelLote * lote.CostoUnitario;
            lote.CantidadDisponible -= cantidadDelLote;
            cantidadRestante -= cantidadDelLote;
        }

        var costoUnitario = cantidadAConsumir > 0 ? costoTotal / cantidadAConsumir : 0;
        return (costoTotal, costoUnitario);
    }

    /// <summary>
    /// Para PromedioPonderado/UltimaCompra: consumir lotes proporcionalmente (FIFO por defecto)
    /// </summary>
    private async Task ConsumirLotesProporcional(Guid productoId, int sucursalId, decimal cantidad)
    {
        var lotes = await _context.LotesInventario
            .Where(l => l.ProductoId == productoId
                     && l.SucursalId == sucursalId
                     && l.CantidadDisponible > 0)
            .OrderBy(l => l.FechaEntrada)
            .ToListAsync();

        decimal restante = cantidad;
        foreach (var lote in lotes)
        {
            if (restante <= 0) break;
            var consumir = Math.Min(lote.CantidadDisponible, restante);
            lote.CantidadDisponible -= consumir;
            restante -= consumir;
        }
    }

    /// <summary>
    /// Recalcular costo promedio ponderado desde lotes disponibles
    /// </summary>
    private async Task<decimal> CalcularCostoPromedioDesdelotes(
        Guid productoId, int sucursalId, decimal cantidadNueva, decimal costoNuevo, List<LoteInventario>? lotesExistentes = null)
    {
        var lotes = lotesExistentes ?? await _context.LotesInventario
            .Where(l => l.ProductoId == productoId
                     && l.SucursalId == sucursalId
                     && l.CantidadDisponible > 0)
            .ToListAsync();

        decimal totalCosto = lotes.Sum(l => l.CantidadDisponible * l.CostoUnitario)
                           + (cantidadNueva * costoNuevo);
        decimal totalCantidad = lotes.Sum(l => l.CantidadDisponible) + cantidadNueva;

        return totalCantidad > 0 ? totalCosto / totalCantidad : costoNuevo;
    }
}
