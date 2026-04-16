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

/// <summary>
/// Gestiona la recepción de órdenes de compra: actualiza inventario (Event Sourcing),
/// aplica costeo (PEPS/UEPS/PP), genera asientos contables y registra en el ERP Outbox.
/// Extraído de CompraService para mantener responsabilidad única.
/// </summary>
public class CompraRecepcionService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly global::Marten.IDocumentStore _store;
    private readonly CosteoService _costeoService;
    private readonly ITaxEngine _taxEngine;
    private readonly ILogger<CompraRecepcionService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly ErpSincoOptions _erpOptions;
    private readonly ICompraErpService _compraErpService;

    public CompraRecepcionService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<CompraRecepcionService> logger,
        IActivityLogService activityLogService,
        IOptions<ErpSincoOptions> erpOptions,
        ICompraErpService compraErpService)
    {
        _context = context;
        _session = session;
        _store = store;
        _costeoService = costeoService;
        _taxEngine = taxEngine;
        _logger = logger;
        _activityLogService = activityLogService;
        _erpOptions = erpOptions.Value;
        _compraErpService = compraErpService;
    }

    public async Task<(bool success, string? error)> RecibirOrdenAsync(
        int id, RecibirOrdenCompraDto dto, string? emailUsuario)
    {
        var orden = await _context.OrdenesCompra
            .Include(o => o.Detalles)
            .Include(o => o.Proveedor)
            .Include(o => o.Sucursal)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orden == null) return (false, "NOT_FOUND");

        if (orden.Estado != EstadoOrdenCompra.Aprobada && orden.Estado != EstadoOrdenCompra.RecibidaParcial)
            return (false, "Solo se pueden recibir órdenes en estado Aprobada o RecibidaParcial");

        // PRE-CARGA DE DATOS EN LOTE (Batching) para evitar N+1 queries
        var productosIds = dto.Lineas.Select(l => l.ProductoId).Distinct().ToList();

        var productosBatch = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Impuesto)
            .Include(p => p.ConceptoRetencion)
            .Where(p => productosIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Cargar reglas de retención activas para el TaxEngine
        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo).ToListAsync();

        // 1. Cargar Stocks
        var stocksDict = await _context.Stock
            .Where(s => s.SucursalId == orden.SucursalId && productosIds.Contains(s.ProductoId))
            .ToDictionaryAsync(s => s.ProductoId);

        // 2. Cargar Lotes (solo si el método requiere recálculo sobre lotes antiguos)
        Dictionary<Guid, List<LoteInventario>> lotesDict = new();
        if (orden.Sucursal!.MetodoCosteo == MetodoCosteo.PEPS || orden.Sucursal!.MetodoCosteo == MetodoCosteo.UEPS)
        {
            var todosLotes = await _context.LotesInventario
                .Where(l => l.SucursalId == orden.SucursalId
                         && productosIds.Contains(l.ProductoId)
                         && l.CantidadDisponible > 0)
                .ToListAsync();

            lotesDict = todosLotes.GroupBy(l => l.ProductoId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        // Pre-cargar impuestos de la orden (por nombre) para actualizar el ImpuestoId del producto
        // al recibirla — mantiene el catálogo de productos sincronizado con el IVA real de compra.
        var nombresImpuestoOrden = orden.Detalles
            .Where(d => !string.IsNullOrEmpty(d.NombreImpuesto))
            .Select(d => d.NombreImpuesto!)
            .Distinct()
            .ToList();

        var impuestosOrdenDict = nombresImpuestoOrden.Count > 0
            ? await _context.Impuestos
                .IgnoreQueryFilters()
                .Where(i => nombresImpuestoOrden.Contains(i.Nombre) && i.EmpresaId == orden.EmpresaId)
                .ToDictionaryAsync(i => i.Nombre)
            : new Dictionary<string, Impuesto>();

        // ERP Payload Builders
        var inventarioAcumulado = new Dictionary<string, decimal>(); // Key: CuentaInventario, Value: Total
        var impuestosAcumulados = new Dictionary<string, (decimal Porcentaje, string? Cuenta, decimal MontoBase, decimal Total)>();
        var retencionesAcumuladas = new Dictionary<string, (string Nombre, string? Cuenta, decimal Total)>();

        // Fecha efectiva de recepción (usuario la puede editar; no puede ser < fechaOrden)
        var fechaRecepcionEfectiva = dto.FechaRecepcion.HasValue
            ? DateTime.SpecifyKind(dto.FechaRecepcion.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Eventos Marten pendientes para la transacción atómica
        var pendingMartenEvents = new List<(Guid StreamId, object Evento, bool IsNew)>();

        // Procesar cada línea recibida
        foreach (var lineaRecibida in dto.Lineas)
        {
            var detalle = orden.Detalles.FirstOrDefault(d => d.ProductoId == lineaRecibida.ProductoId);
            if (detalle == null)
                return (false, $"Producto {lineaRecibida.ProductoId} no está en la orden");

            var cantidadPendiente = detalle.CantidadSolicitada - detalle.CantidadRecibida;
            if (lineaRecibida.CantidadRecibida > cantidadPendiente)
                return (false, $"La cantidad recibida no puede exceder la cantidad pendiente para {detalle.NombreProducto}. Pendiente: {cantidadPendiente}");

            // Actualizar cantidad recibida
            detalle.CantidadRecibida += lineaRecibida.CantidadRecibida;
            if (!string.IsNullOrEmpty(lineaRecibida.Observaciones))
                detalle.Observaciones = lineaRecibida.Observaciones;

            // ===== RECOLECCIÓN PAYLOAD ERP =====
            var productoCompleto = productosBatch[detalle.ProductoId];
            var lineaSubtotal = lineaRecibida.CantidadRecibida * detalle.PrecioUnitario;
            var lineaImpuestoValor = lineaSubtotal * detalle.PorcentajeImpuesto;

            var cuentaInventario = productoCompleto.Categoria.CuentaInventario ?? "N/A";
            if (!inventarioAcumulado.ContainsKey(cuentaInventario))
                inventarioAcumulado[cuentaInventario] = 0;
            inventarioAcumulado[cuentaInventario] += lineaSubtotal;

            if (detalle.PorcentajeImpuesto > 0)
            {
                var nombreImpuesto = detalle.NombreImpuesto ?? $"IVA {detalle.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImpuesto = productoCompleto.Impuesto?.CodigoCuentaContable ?? "2408";
                if (!impuestosAcumulados.ContainsKey(nombreImpuesto))
                    impuestosAcumulados[nombreImpuesto] = (detalle.PorcentajeImpuesto, cuentaImpuesto, 0, 0);
                var actual = impuestosAcumulados[nombreImpuesto];
                impuestosAcumulados[nombreImpuesto] = (actual.Porcentaje, actual.Cuenta, actual.MontoBase + lineaSubtotal, actual.Total + lineaImpuestoValor);
            }

            // Reconstruir impuesto para TaxEngine si fue especificado por porcentaje en la orden
            var impuestoParaTax = productoCompleto.Impuesto;
            if (impuestoParaTax == null && detalle.PorcentajeImpuesto > 0)
            {
                impuestoParaTax = new Impuesto
                {
                    Nombre = detalle.NombreImpuesto ?? $"IVA {detalle.PorcentajeImpuesto * 100:0.##}%",
                    Porcentaje = detalle.PorcentajeImpuesto,
                    Tipo = TipoImpuesto.IVA,
                    AplicaSobreBase = true,
                    CodigoCuentaContable = "2408"
                };
            }

            // Calcular retenciones via TaxEngine (ReteFuente, ReteICA, ReteIVA)
            var taxResultRecepcion = _taxEngine.Calcular(new TaxRequest(
                ProductoId: detalle.ProductoId,
                Cantidad: lineaRecibida.CantidadRecibida,
                PrecioUnitario: detalle.PrecioUnitario,
                Impuesto: impuestoParaTax,
                EsAlimentoUltraprocesado: productoCompleto.EsAlimentoUltraprocesado,
                GramosAzucarPor100ml: productoCompleto.GramosAzucarPor100ml,
                PerfilVendedor: orden.Proveedor.PerfilTributario,
                PerfilComprador: orden.Sucursal!.PerfilTributario,
                CodigoMunicipio: orden.Sucursal!.CodigoMunicipio ?? string.Empty,
                ConceptoRetencionId: productoCompleto.ConceptoRetencionId,
                ValorUVT: orden.Sucursal!.ValorUVT,
                ReglasRetencion: reglasRetencion
            ));

            foreach (var ret in taxResultRecepcion.Retenciones)
            {
                var claveRetencion = $"{ret.Tipo}_{ret.CuentaContable ?? ret.Nombre}";
                if (!retencionesAcumuladas.ContainsKey(claveRetencion))
                    retencionesAcumuladas[claveRetencion] = (ret.Nombre, ret.CuentaContable, 0);
                var actualRet = retencionesAcumuladas[claveRetencion];
                retencionesAcumuladas[claveRetencion] = (actualRet.Nombre, actualRet.Cuenta, actualRet.Total + ret.Monto);
            }

            // ===== INTEGRACIÓN CON INVENTARIO =====

            // 1. Event Sourcing: Registrar entrada
            var streamId = InventarioAggregate.GenerarStreamId(detalle.ProductoId, orden.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate == null)
            {
                var (_, primerEvento) = InventarioAggregate.RegistrarEntrada(
                    streamId,
                    detalle.ProductoId,
                    orden.SucursalId,
                    lineaRecibida.CantidadRecibida,
                    detalle.PrecioUnitario,
                    detalle.PorcentajeImpuesto,
                    detalle.MontoImpuesto,
                    orden.ProveedorId,
                    orden.Proveedor.Nombre,
                    orden.NumeroOrden,
                    $"Recepción de compra - {orden.Proveedor.Nombre}",
                    null,
                    orden.SucursalId,
                    fechaMovimiento: fechaRecepcionEfectiva);

                pendingMartenEvents.Add((streamId, primerEvento, true));
            }
            else
            {
                var eventoEntrada = aggregate.AgregarEntrada(
                    lineaRecibida.CantidadRecibida,
                    detalle.PrecioUnitario,
                    orden.ProveedorId,
                    orden.Proveedor.Nombre,
                    orden.NumeroOrden,
                    $"Recepción de compra - {orden.Proveedor.Nombre}",
                    null,
                    fechaMovimiento: fechaRecepcionEfectiva);

                pendingMartenEvents.Add((streamId, eventoEntrada, false));
            }

            // 2. Registrar lote de entrada (con número de lote y vencimiento si fue informado)
            var fechaVencimientoLote = lineaRecibida.FechaVencimiento
                ?? (productoCompleto.DiasVidaUtil.HasValue
                    ? DateOnly.FromDateTime(DateTime.Today.AddDays(productoCompleto.DiasVidaUtil.Value))
                    : (DateOnly?)null);

            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId,
                orden.SucursalId,
                lineaRecibida.CantidadRecibida,
                detalle.PrecioUnitario,
                detalle.PorcentajeImpuesto,
                detalle.MontoImpuesto,
                orden.NumeroOrden,
                orden.ProveedorId,
                numeroLote: lineaRecibida.NumeroLote,
                fechaVencimiento: fechaVencimientoLote,
                ordenCompraId: orden.Id,
                fechaEntrada: fechaRecepcionEfectiva);

            // 3. Registrar MovimientoInventario con la fecha efectiva de recepción
            //    (usado para cálculo de stock diario y reportes de kardex)
            _context.MovimientosInventario.Add(new MovimientoInventario
            {
                ProductoId      = detalle.ProductoId,
                SucursalId      = orden.SucursalId,
                TipoMovimiento  = TipoMovimiento.EntradaCompra,
                Cantidad        = lineaRecibida.CantidadRecibida,
                CostoUnitario   = detalle.PrecioUnitario,
                CostoTotal      = lineaRecibida.CantidadRecibida * detalle.PrecioUnitario,
                PorcentajeImpuesto = detalle.PorcentajeImpuesto,
                MontoImpuesto   = detalle.MontoImpuesto,
                TerceroId       = orden.ProveedorId,
                Referencia      = orden.NumeroOrden,
                Observaciones   = $"Recepción de compra - {orden.Proveedor.Nombre}",
                UsuarioId       = 0,
                FechaMovimiento = fechaRecepcionEfectiva
            });

            // 4. Actualizar stock desde el diccionario pre-cargado (crear si no existe)
            stocksDict.TryGetValue(detalle.ProductoId, out var stock);

            if (stock == null)
            {
                stock = new Stock
                {
                    ProductoId = detalle.ProductoId,
                    SucursalId = orden.SucursalId,
                    Cantidad = 0,
                    StockMinimo = 0,
                    CostoPromedio = 0
                };
                _context.Stock.Add(stock);
                stocksDict[detalle.ProductoId] = stock;
            }

            {
                lotesDict.TryGetValue(detalle.ProductoId, out var lotesProducto);
                var lotesParaCosteo = lotesProducto ?? new List<LoteInventario>();

                await _costeoService.ActualizarCostoEntrada(
                    stock,
                    lineaRecibida.CantidadRecibida,
                    detalle.PrecioUnitario,
                    orden.Sucursal!.MetodoCosteo,
                    lotesParaCosteo);

                // Actualizar ImpuestoId del producto según el IVA usado en esta compra.
                // Garantiza que el producto quede configurado con la tasa correcta para
                // el cálculo de IVA en el punto de venta.
                if (!string.IsNullOrEmpty(detalle.NombreImpuesto) &&
                    impuestosOrdenDict.TryGetValue(detalle.NombreImpuesto, out var impuestoDeCompra) &&
                    productoCompleto.ImpuestoId != impuestoDeCompra.Id)
                {
                    productoCompleto.ImpuestoId = impuestoDeCompra.Id;
                }

                // Simular adición del lote en RAM para cálculos de coste en iteraciones subsecuentes
                lotesParaCosteo.Add(new LoteInventario
                {
                    ProductoId = detalle.ProductoId,
                    SucursalId = orden.SucursalId,
                    CantidadDisponible = lineaRecibida.CantidadRecibida,
                    CostoUnitario = detalle.PrecioUnitario
                });
            }
        }

        // Resolver usuario y actualizar UsuarioId en los movimientos recién añadidos
        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario);
        if (usuarioId.HasValue)
        {
            var movimientosNuevos = _context.ChangeTracker.Entries<MovimientoInventario>()
                .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                .Select(e => e.Entity);
            foreach (var m in movimientosNuevos)
                m.UsuarioId = usuarioId.Value;
        }

        // Actualizar estado de la orden
        var todasRecibidas = orden.Detalles.All(d => d.CantidadRecibida >= d.CantidadSolicitada);
        orden.Estado = todasRecibidas ? EstadoOrdenCompra.RecibidaCompleta : EstadoOrdenCompra.RecibidaParcial;
        orden.FechaRecepcion = fechaRecepcionEfectiva;
        orden.RecibidoPorUsuarioId = usuarioId;

        // ===== OUTBOX ERP EMISSION =====
        var asientos = new List<AsientoContableErp>();
        var centroCosto = orden.Sucursal?.CentroCosto ?? string.Empty;

        foreach (var inv in inventarioAcumulado)
            asientos.Add(new AsientoContableErp(inv.Key, centroCosto, "Debito", inv.Value, $"Ingreso Inventario {inv.Key}"));

        foreach (var imp in impuestosAcumulados)
            asientos.Add(new AsientoContableErp(imp.Value.Cuenta ?? "N/A", centroCosto, "Debito", imp.Value.Total, $"Impuesto {imp.Key}"));

        decimal totalRetencionesErp = 0;
        foreach (var ret in retencionesAcumuladas)
        {
            asientos.Add(new AsientoContableErp(ret.Value.Cuenta ?? "N/A", centroCosto, "Credito", ret.Value.Total, $"Retención {ret.Value.Nombre}"));
            totalRetencionesErp += ret.Value.Total;
        }

        var subtotalErp = inventarioAcumulado.Sum(x => x.Value);
        var totalImpuestosErp = impuestosAcumulados.Sum(i => i.Value.Total);
        asientos.Add(new AsientoContableErp(
            _erpOptions.CuentaCxPProveedores, centroCosto, "Credito",
            subtotalErp + totalImpuestosErp - totalRetencionesErp,
            $"CXP Proveedor {orden.Proveedor.Nombre}"));

        var fechaVencimientoErp = fechaRecepcionEfectiva.AddDays(orden.DiasPlazo);
        var erpPayload = new CompraErpPayload(
            NumeroOrden: orden.NumeroOrden,
            NitProveedor: orden.Proveedor.Identificacion,
            FormaPago: orden.FormaPago,
            FechaVencimientoErp: fechaVencimientoErp,
            FechaRecepcion: fechaRecepcionEfectiva,
            SucursalId: orden.SucursalId,
            Asientos: asientos,
            TotalOriginalDocumento: subtotalErp + totalImpuestosErp);

        var recepcionesAnteriores = await _context.DocumentosContables
            .CountAsync(d => d.TipoDocumento == "RecepcionCompra" && d.NumeroSoporte.StartsWith(orden.NumeroOrden));
        var numeroRecepcion = recepcionesAnteriores + 1;
        var soporteRecepcion = numeroRecepcion == 1 ? orden.NumeroOrden : $"{orden.NumeroOrden}-R{numeroRecepcion}";

        await _compraErpService.EmitirAsync(orden, asientos, erpPayload, soporteRecepcion, numeroRecepcion);
        // ============================================================

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
            foreach (var (sid, evt, isNew) in pendingMartenEvents)
                if (isNew)
                    martenTx.Events.StartStream<InventarioAggregate>(sid, evt);
                else
                    martenTx.Events.Append(sid, evt);
            await martenTx.SaveChangesAsync();
            await _context.SaveChangesAsync();
            await npgsqlTx.CommitAsync();
        });

        _logger.LogInformation("Orden de compra {NumeroOrden} recibida ({Estado})",
            orden.NumeroOrden, orden.Estado);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "RecibirOrdenCompra",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Orden de compra {orden.NumeroOrden} recibida ({orden.Estado})",
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        return (true, null);
    }
}
