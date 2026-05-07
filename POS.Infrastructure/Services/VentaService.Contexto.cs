using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed partial class VentaService
{
    private sealed record VentaContexto(
        Caja Caja,
        Sucursal Sucursal,
        string? NombreCliente,
        string? NitCliente,
        string NumeroVenta,
        List<RetencionRegla> ReglasRetencion,
        List<TramoBebidasAzucaradas> TramosBebidasAzucaradas,
        string PerfilComprador,
        Dictionary<Guid, Producto> ProductosMap,
        Dictionary<Guid, Stock> StocksMap,
        DateTime FechaVentaEfectiva,
        int? UsuarioIdVenta
    );

    private sealed record LineaProcessada(
        DetalleVenta Detalle,
        (Guid StreamId, object Evento) MartenEvento,
        (Stock Stock, string Nombre) StockVerificar,
        decimal Subtotal,
        decimal Descuento,
        decimal TotalImpuestos,
        bool RequiereFactura
    );

    private async Task<(VentaContexto? ctx, string? error)> CargarContextoVentaAsync(CrearVentaDto dto)
    {
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.Id == dto.CajaId && c.SucursalId == dto.SucursalId);
        if (caja == null) return (null, "Caja no encontrada en esta sucursal.");
        if (caja.Estado != EstadoCaja.Abierta) return (null, "La caja no esta abierta.");

        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null) return (null, "Sucursal no encontrada.");

        string? nombreCliente = null, nitCliente = null;
        string perfilComprador = "REGIMEN_COMUN";
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente == null) return (null, "Cliente no encontrado.");
            nombreCliente = cliente.Nombre;
            nitCliente = cliente.Identificacion;
            perfilComprador = cliente.PerfilTributario;
        }

        var ultimaVenta = await _context.Ventas
            .IgnoreQueryFilters()
            .Where(v => v.SucursalId == dto.SucursalId)
            .OrderByDescending(v => v.Id)
            .Select(v => v.NumeroVenta)
            .FirstOrDefaultAsync();
        var consecutivo = 1;
        if (ultimaVenta != null && ultimaVenta.Contains('-'))
        {
            int.TryParse(ultimaVenta.Split('-').Last(), out consecutivo);
            consecutivo++;
        }
        var numeroVenta = $"V-{consecutivo:D6}";

        var reglasRetencion = await _context.RetencionesReglas.Where(r => r.Activo).ToListAsync();
        var hoyVenta = DateOnly.FromDateTime(DateTime.UtcNow);
        var tramosBebidasAzucaradas = await _context.TramosBebidasAzucaradas
            .Where(t => t.Activo && t.VigenciaDesde <= hoyVenta)
            .OrderBy(t => t.MaxGramosPor100ml)
            .ToListAsync();

        var productoIds = dto.Lineas.Select(l => l.ProductoId).Distinct().ToList();
        var productosMap = await _context.Productos
            .Include(p => p.ConceptoRetencion)
            .Include(p => p.Categoria)
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var productoImpuestoIds = productosMap.Values
            .Where(p => p.ImpuestoId.HasValue)
            .Select(p => p.ImpuestoId!.Value)
            .Distinct()
            .ToList();
        if (productoImpuestoIds.Count > 0)
        {
            var impuestosDict = await _context.Impuestos
                .IgnoreQueryFilters()
                .Where(i => productoImpuestoIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);
            foreach (var p in productosMap.Values)
                if (p.ImpuestoId.HasValue && impuestosDict.TryGetValue(p.ImpuestoId.Value, out var imp))
                    p.Impuesto = imp;
        }

        var stocksMap = await _context.Stock
            .Where(s => productoIds.Contains(s.ProductoId) && s.SucursalId == dto.SucursalId)
            .ToDictionaryAsync(s => s.ProductoId);

        var fechaVentaEfectiva = dto.FechaVenta.HasValue
            ? DateTime.SpecifyKind(dto.FechaVenta.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        var emailCajero = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;
        var subCajero = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioIdVenta = await _context.ResolverUsuarioIdAsync(emailCajero, subCajero);

        return (new VentaContexto(
            caja, sucursal, nombreCliente, nitCliente, numeroVenta,
            reglasRetencion, tramosBebidasAzucaradas, perfilComprador,
            productosMap, stocksMap, fechaVentaEfectiva, usuarioIdVenta), null);
    }

    private async Task<(LineaProcessada? resultado, string? error)> ProcesarLineaAsync(
        LineaVentaDto linea, VentaContexto ctx, int sucursalId)
    {
        if (!ctx.ProductosMap.TryGetValue(linea.ProductoId, out var producto))
            return (null, $"Producto {linea.ProductoId} no encontrado.");
        if (!producto.Activo)
            return (null, $"Producto {producto.Nombre} esta inactivo.");

        if (!ctx.StocksMap.TryGetValue(linea.ProductoId, out var stock) || stock.Cantidad < linea.Cantidad)
            return (null, $"Stock insuficiente para {producto.Nombre}. " +
                $"Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");

        decimal precioUnitario;
        if (linea.PrecioUnitario.HasValue)
        {
            var (valido, errorPrecio) = await _precioService.ValidarPrecio(
                linea.ProductoId, sucursalId, linea.PrecioUnitario.Value, producto.Nombre);
            if (!valido) return (null, errorPrecio);
            precioUnitario = linea.PrecioUnitario.Value;
        }
        else
        {
            var precio = await _precioService.ResolverPrecio(linea.ProductoId, sucursalId);
            precioUnitario = precio.PrecioVenta;
        }

        var taxResult = _taxEngine.Calcular(new TaxRequest(
            ProductoId: linea.ProductoId,
            Cantidad: linea.Cantidad,
            PrecioUnitario: precioUnitario,
            Impuesto: producto.Impuesto,
            EsAlimentoUltraprocesado: producto.EsAlimentoUltraprocesado,
            GramosAzucarPor100ml: producto.GramosAzucarPor100ml,
            PerfilVendedor: ctx.Sucursal.PerfilTributario,
            PerfilComprador: ctx.PerfilComprador,
            CodigoMunicipio: ctx.Sucursal.CodigoMunicipio ?? string.Empty,
            ConceptoRetencionId: producto.ConceptoRetencionId,
            ValorUVT: ctx.Sucursal.ValorUVT,
            ReglasRetencion: ctx.ReglasRetencion,
            TramosBebidasAzucaradas: ctx.TramosBebidasAzucaradas
        ));

        var primerImpuesto = taxResult.Impuestos.FirstOrDefault();
        decimal porcentajeImpuesto = primerImpuesto?.Porcentaje ?? 0;
        decimal montoImpuesto = taxResult.TotalImpuestos;

        var streamId = InventarioAggregate.GenerarStreamId(linea.ProductoId, sucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
        if (aggregate == null)
            return (null, $"No hay registro de inventario para {producto.Nombre}.");

        SalidaVentaRegistrada eventoVenta;
        try
        {
            eventoVenta = aggregate.RegistrarSalidaVenta(
                linea.Cantidad, precioUnitario, porcentajeImpuesto, montoImpuesto, ctx.NumeroVenta,
                ctx.UsuarioIdVenta, fechaMovimiento: ctx.FechaVentaEfectiva);
        }
        catch (InvalidOperationException)
        {
            return (null, $"Stock insuficiente para {producto.Nombre}. " +
                $"Disponible: {aggregate.Cantidad}, Solicitado: {linea.Cantidad}");
        }

        var (costoUnitario, loteId, numeroLoteSnapshot, lotesConsumidos) = await _ventaCosteoService.ConsumirAsync(
            linea.ProductoId, sucursalId, linea.Cantidad,
            ctx.Sucursal.MetodoCosteo, producto.ManejaLotes);

        stock.Cantidad -= linea.Cantidad;
        stock.UltimaActualizacion = DateTime.UtcNow;

        var lineaSubtotal = (precioUnitario * linea.Cantidad) - linea.Descuento;
        var detalle = new DetalleVenta
        {
            ProductoId = linea.ProductoId,
            NombreProducto = producto.Nombre,
            LoteInventarioId = loteId,
            NumeroLote = numeroLoteSnapshot,
            Cantidad = linea.Cantidad,
            PrecioUnitario = precioUnitario,
            CostoUnitario = costoUnitario,
            Descuento = linea.Descuento,
            PorcentajeImpuesto = porcentajeImpuesto,
            MontoImpuesto = montoImpuesto,
            Subtotal = lineaSubtotal,
            Lotes = lotesConsumidos.Select(l => new DetalleVentaLote
            {
                LoteInventarioId = l.LoteId,
                NumeroLote       = l.NumeroLote,
                Cantidad         = l.Cantidad,
                CostoUnitario    = l.CostoUnitario,
            }).ToList()
        };

        return (new LineaProcessada(
            detalle,
            (streamId, eventoVenta),
            (stock, producto.Nombre),
            precioUnitario * linea.Cantidad,
            linea.Descuento,
            montoImpuesto,
            taxResult.RequiereFacturaElectronica
        ), null);
    }
}
