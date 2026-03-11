using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.Infrastructure.Services;

public class VentaService : IVentaService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly global::Marten.IDocumentStore _store;
    private readonly IPrecioService _precioService;
    private readonly CosteoService _costeoService;
    private readonly ITaxEngine _taxEngine;
    private readonly ILogger<VentaService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly FacturacionBackgroundService _facturacionBackground;
    private readonly INotificationService _notificationService;
    private readonly ErpSincoOptions _erpOptions;

    public VentaService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        IPrecioService precioService,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<VentaService> logger,
        IActivityLogService activityLogService,
        FacturacionBackgroundService facturacionBackground,
        INotificationService notificationService,
        IOptions<ErpSincoOptions> erpOptions)
    {
        _context = context;
        _session = session;
        _store = store;
        _precioService = precioService;
        _costeoService = costeoService;
        _taxEngine = taxEngine;
        _logger = logger;
        _activityLogService = activityLogService;
        _facturacionBackground = facturacionBackground;
        _notificationService = notificationService;
        _erpOptions = erpOptions.Value;
    }

    public async Task<(VentaDto? venta, string? error)> CrearVentaAsync(CrearVentaDto dto)
    {
        // Verificar caja abierta
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.Id == dto.CajaId && c.SucursalId == dto.SucursalId);
        if (caja == null)
            return (null, "Caja no encontrada en esta sucursal.");
        if (caja.Estado != EstadoCaja.Abierta)
            return (null, "La caja no esta abierta.");

        // Obtener sucursal para metodo de costeo
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada.");

        // Verificar cliente (si aplica)
        string? nombreCliente = null;
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente == null) return (null, "Cliente no encontrado.");
            nombreCliente = cliente.Nombre;
        }

        // Generar numero de venta
        var ultimaVenta = await _context.Ventas
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

        // Cargar reglas de retención activas de la sucursal
        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo)
            .ToListAsync();

        // Perfil del comprador (si aplica)
        string perfilComprador = "REGIMEN_COMUN";
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente != null) perfilComprador = cliente.PerfilTributario;
        }

        // Pre-cargar productos y stocks en lote (elimina N+1)
        var productoIds = dto.Lineas.Select(l => l.ProductoId).Distinct().ToList();

        var productosMap = await _context.Productos
            .Include(p => p.Impuesto)
            .Include(p => p.ConceptoRetencion)
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var stocksMap = await _context.Stock
            .Where(s => productoIds.Contains(s.ProductoId) && s.SucursalId == dto.SucursalId)
            .ToDictionaryAsync(s => s.ProductoId);

        // Procesar cada linea con el TaxEngine
        var detalles = new List<DetalleVenta>();
        var stocksVerificar = new List<(Stock stock, string nombre)>();
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        decimal subtotal = 0;
        decimal descuentoTotal = 0;
        decimal totalImpuestos = 0;
        bool requiereFacturaElectronica = false;

        foreach (var linea in dto.Lineas)
        {
            // Obtener producto con su impuesto (desde cache en memoria)
            if (!productosMap.TryGetValue(linea.ProductoId, out var producto))
                return (null, $"Producto {linea.ProductoId} no encontrado.");
            if (!producto.Activo)
                return (null, $"Producto {producto.Nombre} esta inactivo.");

            // Verificar stock (desde cache en memoria)
            if (!stocksMap.TryGetValue(linea.ProductoId, out var stock) || stock.Cantidad < linea.Cantidad)
                return (null, $"Stock insuficiente para {producto.Nombre}. " +
                    $"Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");

            // Resolver precio
            decimal precioUnitario;
            if (linea.PrecioUnitario.HasValue)
            {
                var (valido, errorPrecio) = await _precioService.ValidarPrecio(
                    linea.ProductoId, dto.SucursalId, linea.PrecioUnitario.Value);
                if (!valido) return (null, errorPrecio);
                precioUnitario = linea.PrecioUnitario.Value;
            }
            else
            {
                var precio = await _precioService.ResolverPrecio(linea.ProductoId, dto.SucursalId);
                precioUnitario = precio.PrecioVenta;
            }

            // ── Calcular impuestos con el TaxEngine ────────────────────────────
            var taxResult = _taxEngine.Calcular(new TaxRequest(
                ProductoId: linea.ProductoId,
                Cantidad: linea.Cantidad,
                PrecioUnitario: precioUnitario,
                Impuesto: producto.Impuesto,
                EsAlimentoUltraprocesado: producto.EsAlimentoUltraprocesado,
                GramosAzucarPor100ml: producto.GramosAzucarPor100ml,
                PerfilVendedor: sucursal.PerfilTributario,
                PerfilComprador: perfilComprador,
                CodigoMunicipio: sucursal.CodigoMunicipio ?? string.Empty,
                ConceptoRetencionId: producto.ConceptoRetencionId,
                ValorUVT: sucursal.ValorUVT,
                ReglasRetencion: reglasRetencion
            ));

            // Acumular flag de factura electrónica (si cualquier línea lo requiere)
            if (taxResult.RequiereFacturaElectronica)
                requiereFacturaElectronica = true;

            // Usar el primer impuesto aplicado para compatibilidad con DetalleVenta
            var primerImpuesto = taxResult.Impuestos.FirstOrDefault();
            decimal porcentajeImpuesto = primerImpuesto?.Porcentaje ?? 0;
            decimal montoImpuesto = taxResult.TotalImpuestos;
            totalImpuestos += montoImpuesto;

            // Consumir inventario via Event Sourcing
            var streamId = InventarioAggregate.GenerarStreamId(linea.ProductoId, dto.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
            if (aggregate == null)
                return (null, $"No hay registro de inventario para {producto.Nombre}.");

            var eventoVenta = aggregate.RegistrarSalidaVenta(
                linea.Cantidad, precioUnitario, porcentajeImpuesto, montoImpuesto, numeroVenta, null);
            pendingMartenEvents.Add((streamId, eventoVenta));

            // Consumir stock con metodo de costeo (FEFO si el producto maneja lotes)
            decimal costoUnitario;
            int? loteId = null;
            string? numeroLoteSnapshot = null;

            if (producto.ManejaLotes)
            {
                var (ct, cu, lid, nlote) = await _costeoService.ConsumirLotesFEFO(
                    linea.ProductoId, dto.SucursalId, linea.Cantidad);
                costoUnitario = cu;
                loteId = lid;
                numeroLoteSnapshot = nlote;
            }
            else
            {
                var (_, cu) = await _costeoService.ConsumirStock(
                    linea.ProductoId, dto.SucursalId, linea.Cantidad, sucursal.MetodoCosteo);
                costoUnitario = cu;
            }

            // Actualizar stock en EF Core
            stock.Cantidad -= linea.Cantidad;
            stock.UltimaActualizacion = DateTime.UtcNow;
            stocksVerificar.Add((stock, producto.Nombre));

            // Crear detalle
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
                Subtotal = lineaSubtotal
            };
            detalles.Add(detalle);

            subtotal += precioUnitario * linea.Cantidad;
            descuentoTotal += linea.Descuento;
        }

        var total = subtotal - descuentoTotal + totalImpuestos;
        var cambio = dto.MontoPagado.HasValue ? dto.MontoPagado.Value - total : (decimal?)null;

        if (dto.MontoPagado.HasValue && dto.MontoPagado.Value < total)
            return (null, $"Monto pagado ({dto.MontoPagado.Value}) es menor al total ({total}).");

        // Crear venta
        var venta = new Venta
        {
            NumeroVenta = numeroVenta,
            SucursalId = dto.SucursalId,
            CajaId = dto.CajaId,
            ClienteId = dto.ClienteId,
            Subtotal = subtotal,
            Descuento = descuentoTotal,
            Impuestos = totalImpuestos,
            Total = total,
            Estado = EstadoVenta.Completada,
            MetodoPago = (MetodoPago)dto.MetodoPago,
            MontoPagado = dto.MontoPagado,
            Cambio = cambio,
            Observaciones = dto.Observaciones,
            FechaVenta = DateTime.UtcNow,
            RequiereFacturaElectronica = requiereFacturaElectronica,
            Detalles = detalles
        };
        _context.Ventas.Add(venta);

        // Actualizar monto de caja
        caja.MontoActual += total;

        // Guardar todo en una sola transacción atómica (Marten + EF Core)
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var npgsqlConn = (Npgsql.NpgsqlConnection)_context.Database.GetDbConnection();
            if (npgsqlConn.State != System.Data.ConnectionState.Open)
                await npgsqlConn.OpenAsync();
            await using var npgsqlTx = await npgsqlConn.BeginTransactionAsync();
            await _context.Database.UseTransactionAsync(npgsqlTx);
            await using var martenTx = _store.LightweightSession(
                global::Marten.Services.SessionOptions.ForTransaction(npgsqlTx));
            foreach (var (sid, evt) in pendingMartenEvents)
                martenTx.Events.Append(sid, evt);
            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync();
            await npgsqlTx.CommitAsync();
        });

        _logger.LogInformation("Venta {NumeroVenta} completada. Total: {Total}, Items: {Items}",
            numeroVenta, total, detalles.Count);

        // Facturación electrónica fire-and-forget
        if (venta.RequiereFacturaElectronica)
            _facturacionBackground.Encolar(venta.Id);

        // Notificaciones en tiempo real
        await _notificationService.EnviarNotificacionSucursalAsync(dto.SucursalId, new NotificacionDto(
            "venta_completada", "Venta completada",
            $"Venta {numeroVenta} — ${total:N0}", "success", DateTime.UtcNow,
            new { VentaId = venta.Id, NumeroVenta = numeroVenta, Total = total }));

        foreach (var (stockItem, nombre) in stocksVerificar)
            if (stockItem.Cantidad >= 0 && stockItem.Cantidad <= stockItem.StockMinimo)
                await _notificationService.EnviarNotificacionSucursalAsync(dto.SucursalId, new NotificacionDto(
                    "stock_bajo", "Stock bajo",
                    $"{nombre}: quedan {stockItem.Cantidad:F0} unidades", "warning", DateTime.UtcNow,
                    new { stockItem.ProductoId, NombreProducto = nombre, StockActual = stockItem.Cantidad }));

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {numeroVenta} creada. Total: ${total:N2}, Items: {detalles.Count}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Venta",
            EntidadId: venta.Id.ToString(),
            EntidadNombre: numeroVenta,
            DatosNuevos: new
            {
                NumeroVenta = numeroVenta,
                Total = total,
                Subtotal = subtotal,
                Descuento = descuentoTotal,
                Impuestos = totalImpuestos,
                MetodoPago = ((MetodoPago)dto.MetodoPago).ToString(),
                CantidadItems = detalles.Count,
                ClienteId = dto.ClienteId,
                CajaId = dto.CajaId,
                Productos = detalles.Select(d => new {
                    d.ProductoId,
                    d.NombreProducto,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
            }
        ));

        return (MapToDto(venta, sucursal.Nombre, caja.Nombre, nombreCliente), null);
    }

    public async Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return (false, "NOT_FOUND");
        if (venta.Estado == EstadoVenta.Anulada)
            return (false, "La venta ya esta anulada.");

        var sucursal = await _context.Sucursales.FindAsync(venta.SucursalId);

        // Revertir cada linea
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        foreach (var detalle in venta.Detalles)
        {
            // Registrar entrada de devolucion en ES
            var streamId = InventarioAggregate.GenerarStreamId(detalle.ProductoId, venta.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate != null)
            {
                var entradaEvento = aggregate.AgregarEntrada(
                    detalle.Cantidad, detalle.CostoUnitario,
                    null, null, $"Anulacion venta {venta.NumeroVenta}",
                    motivo ?? "Venta anulada", null);
                pendingMartenEvents.Add((streamId, entradaEvento));
            }

            // Registrar lote de entrada y actualizar stock
            var montoImpuestoUnitario = detalle.Cantidad > 0
                ? detalle.MontoImpuesto / detalle.Cantidad
                : 0;
            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId, venta.SucursalId,
                detalle.Cantidad, detalle.CostoUnitario,
                detalle.PorcentajeImpuesto, montoImpuestoUnitario,
                $"Anulacion {venta.NumeroVenta}", null);

            var stock = await _context.Stock
                .FirstOrDefaultAsync(s => s.ProductoId == detalle.ProductoId
                    && s.SucursalId == venta.SucursalId);
            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock, detalle.Cantidad, detalle.CostoUnitario, sucursal!.MetodoCosteo);
            }
        }

        // Marcar venta como anulada
        venta.Estado = EstadoVenta.Anulada;
        venta.Observaciones = $"{venta.Observaciones} | ANULADA: {motivo ?? "Sin motivo"}";

        // Revertir monto de caja
        var caja = await _context.Cajas.FindAsync(venta.CajaId);
        if (caja != null)
            caja.MontoActual -= venta.Total;

        // Guardar en una sola transacción atómica (Marten + EF Core)
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var npgsqlConn = (Npgsql.NpgsqlConnection)_context.Database.GetDbConnection();
            if (npgsqlConn.State != System.Data.ConnectionState.Open)
                await npgsqlConn.OpenAsync();
            await using var npgsqlTx = await npgsqlConn.BeginTransactionAsync();
            await _context.Database.UseTransactionAsync(npgsqlTx);
            await using var martenTx = _store.LightweightSession(
                global::Marten.Services.SessionOptions.ForTransaction(npgsqlTx));
            foreach (var (sid, evt) in pendingMartenEvents)
                martenTx.Events.Append(sid, evt);
            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync();
            await npgsqlTx.CommitAsync();
        });

        _logger.LogInformation("Venta {NumeroVenta} anulada. Motivo: {Motivo}",
            venta.NumeroVenta, motivo);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AnularVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {venta.NumeroVenta} anulada. Motivo: {motivo ?? "Sin motivo"}. Total revertido: ${venta.Total:N2}",
            SucursalId: venta.SucursalId,
            TipoEntidad: "Venta",
            EntidadId: id.ToString(),
            EntidadNombre: venta.NumeroVenta,
            DatosAnteriores: new
            {
                Estado = "Completada",
                Total = venta.Total,
                CantidadItems = venta.Detalles.Count
            },
            DatosNuevos: new
            {
                Estado = "Anulada",
                Motivo = motivo ?? "Sin motivo",
                ItemsRevertidos = venta.Detalles.Select(d => new {
                    d.ProductoId,
                    d.NombreProducto,
                    d.Cantidad
                })
            }
        ));

        return (true, null);
    }

    public async Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario)
    {
        // Cargar venta con detalles
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta == null)
            return (null, "NOT_FOUND");

        // Validación: Solo ventas completadas pueden tener devoluciones
        if (venta.Estado != EstadoVenta.Completada)
            return (null, "Solo se pueden hacer devoluciones de ventas completadas.");

        // Validación: Límite de tiempo (30 días)
        var diasTranscurridos = (DateTime.UtcNow - venta.FechaVenta).TotalDays;
        if (diasTranscurridos > 30)
            return (null, $"La venta tiene {Math.Floor(diasTranscurridos)} días. Solo se permiten devoluciones dentro de 30 días.");

        // Cargar devoluciones anteriores de esta venta
        var devolucionesAnteriores = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Where(d => d.VentaId == ventaId)
            .ToListAsync();

        // Validar cada línea de devolución
        var detallesDevolucion = new List<DetalleDevolucion>();
        decimal totalDevuelto = 0;

        foreach (var linea in dto.Lineas)
        {
            // Verificar que el producto esté en la venta original
            var detalleOriginal = venta.Detalles.FirstOrDefault(d => d.ProductoId == linea.ProductoId);
            if (detalleOriginal == null)
                return (null, $"El producto {linea.ProductoId} no está en la venta original.");

            // Calcular cantidad ya devuelta de este producto
            var cantidadYaDevuelta = devolucionesAnteriores
                .SelectMany(d => d.Detalles)
                .Where(dd => dd.ProductoId == linea.ProductoId)
                .Sum(dd => dd.CantidadDevuelta);

            // Validar que no se exceda la cantidad vendida
            var cantidadDisponibleParaDevolver = detalleOriginal.Cantidad - cantidadYaDevuelta;
            if (linea.Cantidad > cantidadDisponibleParaDevolver)
                return (null,
                    $"No se puede devolver {linea.Cantidad} unidades del producto {detalleOriginal.NombreProducto}. " +
                    $"Vendido: {detalleOriginal.Cantidad}, Ya devuelto: {cantidadYaDevuelta}, Disponible: {cantidadDisponibleParaDevolver}");

            // Calcular subtotal devuelto (proporcional)
            var subtotalDevuelto = (detalleOriginal.PrecioUnitario * linea.Cantidad);
            totalDevuelto += subtotalDevuelto;

            // Crear detalle de devolución
            var detalleDevolucion = new DetalleDevolucion
            {
                ProductoId = linea.ProductoId,
                NombreProducto = detalleOriginal.NombreProducto,
                CantidadDevuelta = linea.Cantidad,
                PrecioUnitario = detalleOriginal.PrecioUnitario,
                CostoUnitario = detalleOriginal.CostoUnitario,
                SubtotalDevuelto = subtotalDevuelto
            };

            detallesDevolucion.Add(detalleDevolucion);
        }

        // Generar número de devolución
        var ultimaDevolucion = await _context.DevolucionesVenta
            .OrderByDescending(d => d.Id)
            .Select(d => d.NumeroDevolucion)
            .FirstOrDefaultAsync();

        var consecutivo = 1;
        if (ultimaDevolucion != null && ultimaDevolucion.Contains('-'))
        {
            int.TryParse(ultimaDevolucion.Split('-').Last(), out consecutivo);
            consecutivo++;
        }
        var numeroDevolucion = $"DEV-{consecutivo:D6}";

        // Cargar sucursal para método de costeo
        var sucursal = await _context.Sucursales.FindAsync(venta.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada.");

        // Procesar cada línea de devolución: restaurar inventario
        var pendingMartenEvents = new List<(Guid StreamId, object Evento)>();
        foreach (var detalleDevolucion in detallesDevolucion)
        {
            var detalleOriginal = venta.Detalles.First(d => d.ProductoId == detalleDevolucion.ProductoId);

            // Event Sourcing: Registrar entrada por devolución
            var streamId = InventarioAggregate.GenerarStreamId(
                detalleDevolucion.ProductoId, venta.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate != null)
            {
                var eventoEntrada = aggregate.AgregarEntrada(
                    detalleDevolucion.CantidadDevuelta,
                    detalleDevolucion.CostoUnitario,
                    null, null,
                    $"Devolución {numeroDevolucion}",
                    dto.Motivo,
                    null);
                pendingMartenEvents.Add((streamId, eventoEntrada));
            }

            // Registrar lote de entrada
            var montoImpuestoUnitario = detalleOriginal.Cantidad > 0
                ? detalleOriginal.MontoImpuesto / detalleOriginal.Cantidad
                : 0;

            await _costeoService.RegistrarLoteEntrada(
                detalleDevolucion.ProductoId,
                venta.SucursalId,
                detalleDevolucion.CantidadDevuelta,
                detalleDevolucion.CostoUnitario,
                detalleOriginal.PorcentajeImpuesto,
                montoImpuestoUnitario,
                $"Devolución {numeroDevolucion}",
                null);

            // Actualizar stock
            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalleDevolucion.ProductoId && s.SucursalId == venta.SucursalId);

            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock,
                    detalleDevolucion.CantidadDevuelta,
                    detalleDevolucion.CostoUnitario,
                    sucursal.MetodoCosteo);
            }
        }

        // Resolver usuarioId desde email
        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario);

        // Crear registro de devolución
        var devolucion = new DevolucionVenta
        {
            VentaId = ventaId,
            NumeroDevolucion = numeroDevolucion,
            Motivo = dto.Motivo,
            TotalDevuelto = totalDevuelto,
            FechaDevolucion = DateTime.UtcNow,
            AutorizadoPorUsuarioId = usuarioId,
            Detalles = detallesDevolucion
        };

        _context.DevolucionesVenta.Add(devolucion);

        // Ajustar monto de caja
        var caja = await _context.Cajas.FindAsync(venta.CajaId);
        if (caja != null)
        {
            caja.MontoActual -= totalDevuelto;
        }

        // ===== NOTA CRÉDITO CONTABLE — ERP OUTBOX =====
        // Preparar asientos en memoria (antes de la transacción)
        var productosIds = detallesDevolucion.Select(d => d.ProductoId).Distinct().ToList();
        var productosBatch = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Impuesto)
            .Where(p => productosIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var asientosNC = new List<AsientoContableErp>();
        var centroCosto = sucursal.CentroCosto ?? string.Empty;

        var ingresosAcumulados = new Dictionary<string, decimal>();
        var ivaAcumulado = new Dictionary<string, (string? Cuenta, decimal Total)>();

        foreach (var det in detallesDevolucion)
        {
            var detalleOrig = venta.Detalles.First(d => d.ProductoId == det.ProductoId);
            var lineaSubtotal = det.CantidadDevuelta * det.PrecioUnitario;
            var lineaImpuesto = lineaSubtotal * detalleOrig.PorcentajeImpuesto;

            var prod = productosBatch.GetValueOrDefault(det.ProductoId);
            var cuentaIngreso = prod?.Categoria?.CuentaIngreso ?? "4135";
            if (!ingresosAcumulados.ContainsKey(cuentaIngreso))
                ingresosAcumulados[cuentaIngreso] = 0;
            ingresosAcumulados[cuentaIngreso] += lineaSubtotal;

            if (detalleOrig.PorcentajeImpuesto > 0)
            {
                var nombreImp = $"IVA {detalleOrig.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImp = prod?.Impuesto?.CodigoCuentaContable ?? "2408";
                if (!ivaAcumulado.ContainsKey(nombreImp))
                    ivaAcumulado[nombreImp] = (cuentaImp, 0);
                var actual = ivaAcumulado[nombreImp];
                ivaAcumulado[nombreImp] = (actual.Cuenta, actual.Total + lineaImpuesto);
            }
        }

        foreach (var ing in ingresosAcumulados)
            asientosNC.Add(new AsientoContableErp(ing.Key, centroCosto, "Debito", ing.Value,
                $"NC - Reversión Ingreso por devolución {numeroDevolucion}"));

        foreach (var iva in ivaAcumulado)
            asientosNC.Add(new AsientoContableErp(iva.Value.Cuenta ?? "2408", centroCosto, "Debito", iva.Value.Total,
                $"NC - Reversión {iva.Key} por devolución {numeroDevolucion}"));

        var totalIvaNC = ivaAcumulado.Sum(i => i.Value.Total);
        var totalIngresoNC = ingresosAcumulados.Sum(i => i.Value);
        asientosNC.Add(new AsientoContableErp(_erpOptions.CuentaCaja, centroCosto, "Credito",
            totalIngresoNC + totalIvaNC,
            $"NC - Reembolso devolución {numeroDevolucion} de venta {venta.NumeroVenta}"));

        var ncPayload = new CompraErpPayload(
            NumeroOrden: numeroDevolucion,
            NitProveedor: "",
            FormaPago: venta.MetodoPago.ToString(),
            FechaVencimientoErp: DateTime.UtcNow,
            FechaRecepcion: DateTime.UtcNow,
            SucursalId: venta.SucursalId,
            Asientos: asientosNC,
            TotalOriginalDocumento: totalDevuelto
        );
        // ===================================================

        // Guardar todo en una sola transacción atómica (Marten + EF Core)
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var npgsqlConn = (Npgsql.NpgsqlConnection)_context.Database.GetDbConnection();
            if (npgsqlConn.State != System.Data.ConnectionState.Open)
                await npgsqlConn.OpenAsync();
            await using var npgsqlTx = await npgsqlConn.BeginTransactionAsync();
            await _context.Database.UseTransactionAsync(npgsqlTx);
            await using var martenTx = _store.LightweightSession(
                global::Marten.Services.SessionOptions.ForTransaction(npgsqlTx));
            foreach (var (sid, evt) in pendingMartenEvents)
                martenTx.Events.Append(sid, evt);
            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync(); // genera devolucion.Id

            // Crear DocumentoContable + ErpOutbox con devolucion.Id ya disponible
            _context.DocumentosContables.Add(new DocumentoContable
            {
                NumeroSoporte = numeroDevolucion,
                TipoDocumento = "NotaCredito",
                TerceroId = venta.ClienteId,
                SucursalId = venta.SucursalId,
                FechaCausacion = DateTime.UtcNow,
                FormaPago = venta.MetodoPago.ToString(),
                TotalDebito = asientosNC.Where(a => a.Naturaleza == "Debito").Sum(a => a.Valor),
                TotalCredito = asientosNC.Where(a => a.Naturaleza == "Credito").Sum(a => a.Valor),
                Detalles = asientosNC.Select(a => new DetalleDocumentoContable
                {
                    CuentaContable = a.Cuenta,
                    CentroCosto = a.CentroCosto,
                    Naturaleza = a.Naturaleza,
                    Valor = a.Valor,
                    Nota = a.Nota
                }).ToList()
            });
            _context.ErpOutboxMessages.Add(new ErpOutboxMessage
            {
                TipoDocumento = "NotaCreditoVenta",
                EntidadId = devolucion.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(ncPayload),
                FechaCreacion = DateTime.UtcNow,
                Estado = EstadoOutbox.Pendiente
            });
            await _context.SaveChangesAsync();
            await npgsqlTx.CommitAsync();
        });

        _logger.LogInformation(
            "Devolución parcial {NumeroDevolucion} creada para venta {NumeroVenta}. Total devuelto: {Total}",
            numeroDevolucion, venta.NumeroVenta, totalDevuelto);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DevolucionParcial",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Devolución parcial {numeroDevolucion} de venta {venta.NumeroVenta}. " +
                        $"Total devuelto: ${totalDevuelto:N2}, Items: {detallesDevolucion.Count}",
            SucursalId: venta.SucursalId,
            TipoEntidad: "DevolucionVenta",
            EntidadId: devolucion.Id.ToString(),
            EntidadNombre: numeroDevolucion,
            DatosNuevos: new
            {
                NumeroDevolucion = numeroDevolucion,
                VentaId = ventaId,
                NumeroVenta = venta.NumeroVenta,
                Motivo = dto.Motivo,
                TotalDevuelto = totalDevuelto,
                AutorizadoPor = emailUsuario,
                Productos = detallesDevolucion.Select(d => new
                {
                    d.ProductoId,
                    d.NombreProducto,
                    d.CantidadDevuelta,
                    d.SubtotalDevuelto
                })
            }
        ));

        return (MapDevolucionToDto(devolucion, venta.NumeroVenta, emailUsuario), null);
    }

    // ─── Mappers ───────────────────────────────────────

    public static VentaDto MapToDto(Venta v, string sucNombre, string cajaNombre, string? clienteNombre) =>
        new(
            v.Id, v.NumeroVenta,
            v.SucursalId, sucNombre,
            v.CajaId, cajaNombre,
            v.ClienteId, clienteNombre,
            v.Subtotal, v.Descuento, v.Impuestos, v.Total,
            v.Estado.ToString(), v.MetodoPago.ToString(),
            v.MontoPagado, v.Cambio,
            v.Observaciones, v.FechaVenta,
            v.Detalles.Select(d =>
            {
                var margen = d.PrecioUnitario > 0
                    ? Math.Round((d.PrecioUnitario - d.CostoUnitario) / d.PrecioUnitario * 100, 2)
                    : 0;
                return new DetalleVentaDto(
                    d.Id, d.ProductoId, d.NombreProducto, d.NumeroLote,
                    d.Cantidad, d.PrecioUnitario, d.CostoUnitario,
                    d.Descuento, d.PorcentajeImpuesto, d.MontoImpuesto,
                    d.Subtotal, margen);
            }).ToList(),
            v.RequiereFacturaElectronica
        );

    public static DevolucionVentaDto MapDevolucionToDto(
        DevolucionVenta d, string numeroVenta, string? autorizadoPor) =>
        new(
            d.Id,
            d.VentaId,
            numeroVenta,
            d.NumeroDevolucion,
            d.Motivo,
            d.TotalDevuelto,
            d.FechaDevolucion,
            autorizadoPor,
            d.Detalles.Select(dd => new DetalleDevolucionDto(
                dd.Id,
                dd.ProductoId,
                dd.NombreProducto,
                dd.CantidadDevuelta,
                dd.PrecioUnitario,
                dd.CostoUnitario,
                dd.SubtotalDevuelto
            )).ToList()
        );
}
