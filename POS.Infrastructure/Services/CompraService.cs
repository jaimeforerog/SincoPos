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

public class CompraService : ICompraService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly global::Marten.IDocumentStore _store;
    private readonly CosteoService _costeoService;
    private readonly ITaxEngine _taxEngine;
    private readonly ILogger<CompraService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly ErpSincoOptions _erpOptions;
    private readonly ICompraErpService _compraErpService;

    public CompraService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<CompraService> logger,
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

    public async Task<(OrdenCompraDto? orden, string? error)> CrearOrdenAsync(CrearOrdenCompraDto dto)
    {
        // Validar sucursal y proveedor en una sola query
        var productosIds = dto.Lineas.Select(l => l.ProductoId).ToList();
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada");

        var proveedor = await _context.Terceros.FindAsync(dto.ProveedorId);
        if (proveedor == null)
            return (null, "Proveedor no encontrado");

        // Cargar productos, retenciones e impuestos override
        var productos = await _context.Productos
            .Include(p => p.Impuesto)
            .Include(p => p.ConceptoRetencion)
            .Where(p => productosIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var linea in dto.Lineas)
        {
            if (!productos.ContainsKey(linea.ProductoId))
                return (null, $"Producto {linea.ProductoId} no encontrado");
        }

        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo).ToListAsync();

        var impuestoIdsOverride = dto.Lineas
            .Where(l => l.ImpuestoId.HasValue)
            .Select(l => l.ImpuestoId!.Value)
            .Distinct()
            .ToList();
        var impuestosOverride = impuestoIdsOverride.Count > 0
            ? await _context.Impuestos
                .Where(i => impuestoIdsOverride.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id)
            : new Dictionary<int, Impuesto>();

        // Generar número de orden con MAX(Id) global (IgnoreQueryFilters evita colisión entre empresas)
        var maxId = await _context.OrdenesCompra.IgnoreQueryFilters().MaxAsync(o => (int?)o.Id) ?? 0;
        var numeroOrden = $"OC-{maxId + 1:000000}";

        // Calcular totales usando TaxEngine
        decimal subtotal = 0;
        decimal impuestosTotal = 0;
        bool requiereFacturaElectronica = false;

        var detalles = new List<DetalleOrdenCompra>();
        foreach (var linea in dto.Lineas)
        {
            var producto = productos[linea.ProductoId];

            // Resolver impuesto: ImpuestoId explícito > PorcentajeImpuesto directo > impuesto del producto
            Impuesto? impuesto = linea.ImpuestoId.HasValue && impuestosOverride.TryGetValue(linea.ImpuestoId.Value, out var imp)
                ? imp
                : linea.PorcentajeImpuesto.HasValue
                    ? new Impuesto { Nombre = $"IVA {linea.PorcentajeImpuesto}%", Porcentaje = linea.PorcentajeImpuesto.Value / 100m, Tipo = TipoImpuesto.IVA, AplicaSobreBase = true }
                    : producto.Impuesto;

            // En compras los roles son invertidos: proveedor=vendedor, sucursal=comprador
            var taxResult = _taxEngine.Calcular(new TaxRequest(
                ProductoId: linea.ProductoId,
                Cantidad: linea.Cantidad,
                PrecioUnitario: linea.PrecioUnitario,
                Impuesto: impuesto,
                EsAlimentoUltraprocesado: producto.EsAlimentoUltraprocesado,
                GramosAzucarPor100ml: producto.GramosAzucarPor100ml,
                PerfilVendedor: proveedor.PerfilTributario,
                PerfilComprador: sucursal.PerfilTributario,
                CodigoMunicipio: sucursal.CodigoMunicipio ?? string.Empty,
                ConceptoRetencionId: producto.ConceptoRetencionId,
                ValorUVT: sucursal.ValorUVT,
                ReglasRetencion: reglasRetencion
            ));

            var primerImpuesto = taxResult.Impuestos.FirstOrDefault();

            detalles.Add(new DetalleOrdenCompra
            {
                ProductoId = linea.ProductoId,
                NombreProducto = producto.Nombre,
                CantidadSolicitada = linea.Cantidad,
                CantidadRecibida = 0,
                PrecioUnitario = linea.PrecioUnitario,
                PorcentajeImpuesto = primerImpuesto?.Porcentaje ?? 0,
                MontoImpuesto = taxResult.TotalImpuestos,
                Subtotal = taxResult.BaseImponible,
                NombreImpuesto = primerImpuesto?.Nombre
            });

            subtotal += taxResult.BaseImponible;
            impuestosTotal += taxResult.TotalImpuestos;
            requiereFacturaElectronica |= taxResult.RequiereFacturaElectronica;
        }

        // Crear orden de compra
        var orden = new OrdenCompra
        {
            NumeroOrden = numeroOrden,
            EmpresaId = sucursal.EmpresaId,
            SucursalId = dto.SucursalId,
            ProveedorId = dto.ProveedorId,
            Estado = EstadoOrdenCompra.Pendiente,
            FechaOrden = DateTime.UtcNow,
            FechaEntregaEsperada = dto.FechaEntregaEsperada.HasValue
                ? DateTime.SpecifyKind(dto.FechaEntregaEsperada.Value, DateTimeKind.Utc)
                : null,
            Observaciones = dto.Observaciones,
            FormaPago = dto.FormaPago,
            DiasPlazo = dto.DiasPlazo,
            Subtotal = subtotal,
            Impuestos = impuestosTotal,
            Total = subtotal + impuestosTotal,
            RequiereFacturaElectronica = requiereFacturaElectronica,
            Detalles = detalles
        };

        _context.OrdenesCompra.Add(orden);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} creada", numeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearOrdenCompra",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Orden de compra {numeroOrden} creada para proveedor {proveedor.Nombre}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: orden.Id.ToString(),
            EntidadNombre: numeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        // Asignar navigations ya cargadas (evitar 2 queries extra)
        orden.Sucursal = sucursal;
        orden.Proveedor = proveedor;

        return (BuildOrdenCompraDto(orden, null, null), null);
    }

    public async Task<(bool success, string? error)> AprobarOrdenAsync(
        int id, AprobarOrdenCompraDto? dto, string? emailUsuario)
    {
        var orden = await _context.OrdenesCompra
            .Include(o => o.Proveedor)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orden == null) return (false, "NOT_FOUND");

        if (orden.Estado != EstadoOrdenCompra.Pendiente)
            return (false, "Solo se pueden aprobar órdenes en estado Pendiente");

        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario);

        orden.Estado = EstadoOrdenCompra.Aprobada;
        orden.FechaAprobacion = DateTime.UtcNow;
        orden.AprobadoPorUsuarioId = usuarioId;
        if (!string.IsNullOrEmpty(dto?.Observaciones))
            orden.Observaciones = dto.Observaciones;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} aprobada", orden.NumeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AprobarOrdenCompra",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Orden de compra {orden.NumeroOrden} aprobada",
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        return (true, null);
    }

    public async Task<(bool success, string? error)> RechazarOrdenAsync(int id, RechazarOrdenCompraDto dto)
    {
        var orden = await _context.OrdenesCompra.FindAsync(id);

        if (orden == null) return (false, "NOT_FOUND");

        if (orden.Estado != EstadoOrdenCompra.Pendiente)
            return (false, "Solo se pueden rechazar órdenes en estado Pendiente");

        orden.Estado = EstadoOrdenCompra.Rechazada;
        orden.MotivoRechazo = dto.MotivoRechazo;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} rechazada", orden.NumeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "RechazarOrdenCompra",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Orden de compra {orden.NumeroOrden} rechazada: {dto.MotivoRechazo}",
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        return (true, null);
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

        // ERP Payload Builders
        var inventarioAcumulado = new Dictionary<string, decimal>(); // Key: CuentaInventario, Value: Total
        var impuestosAcumulados = new Dictionary<string, (decimal Porcentaje, string? Cuenta, decimal MontoBase, decimal Total)>();
        var retencionesAcumuladas = new Dictionary<string, (string Nombre, string? Cuenta, decimal Total)>();

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
            {
                inventarioAcumulado[cuentaInventario] = 0;
            }
            inventarioAcumulado[cuentaInventario] += lineaSubtotal;
            if (detalle.PorcentajeImpuesto > 0)
            {
                // Usar datos del impuesto del producto si existe, o fallback con datos del detalle
                var nombreImpuesto = detalle.NombreImpuesto ?? $"IVA {detalle.PorcentajeImpuesto * 100:0.##}%";
                var cuentaImpuesto = productoCompleto.Impuesto?.CodigoCuentaContable ?? "2408";
                if (!impuestosAcumulados.ContainsKey(nombreImpuesto))
                {
                    impuestosAcumulados[nombreImpuesto] = (detalle.PorcentajeImpuesto, cuentaImpuesto, 0, 0);
                }
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
                {
                    retencionesAcumuladas[claveRetencion] = (ret.Nombre, ret.CuentaContable, 0);
                }
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
                    orden.SucursalId);

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
                    null);

                pendingMartenEvents.Add((streamId, eventoEntrada, false));
            }

            // 2. Registrar lote de entrada (con número de lote y vencimiento si fue informado)
            // Si no se informó fecha de vencimiento pero el producto tiene DiasVidaUtil, calcularla
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
                ordenCompraId: orden.Id);

            // 3. Actualizar stock desde el diccionario pre-cargado (crear si no existe)
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

                // Si recibimos el mismo producto varias veces, simulamos la adición del lote en RAM para los futuros cálculos de coste de iteraciones subsecuentes.
                lotesParaCosteo.Add(new LoteInventario
                {
                    ProductoId = detalle.ProductoId,
                    SucursalId = orden.SucursalId,
                    CantidadDisponible = lineaRecibida.CantidadRecibida,
                    CostoUnitario = detalle.PrecioUnitario
                });
            }
        }

        // Resolver usuario
        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario);

        // Actualizar estado de la orden
        var todasRecibidas = orden.Detalles.All(d => d.CantidadRecibida >= d.CantidadSolicitada);
        orden.Estado = todasRecibidas ? EstadoOrdenCompra.RecibidaCompleta : EstadoOrdenCompra.RecibidaParcial;
        orden.FechaRecepcion = DateTime.UtcNow;
        orden.RecibidoPorUsuarioId = usuarioId;

        // ===== OUTBOX ERP EMISSION (delegado a ICompraErpService) =====
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

        var fechaVencimientoErp = DateTime.UtcNow.AddDays(orden.DiasPlazo);
        var erpPayload = new CompraErpPayload(
            NumeroOrden: orden.NumeroOrden,
            NitProveedor: orden.Proveedor.Identificacion,
            FormaPago: orden.FormaPago,
            FechaVencimientoErp: fechaVencimientoErp,
            FechaRecepcion: DateTime.UtcNow,
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

    public async Task<(bool success, string? error)> CancelarOrdenAsync(int id, CancelarOrdenCompraDto dto)
    {
        var orden = await _context.OrdenesCompra
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orden == null) return (false, "NOT_FOUND");

        if (orden.Estado != EstadoOrdenCompra.Pendiente && orden.Estado != EstadoOrdenCompra.Aprobada)
            return (false, "Solo se pueden cancelar órdenes en estado Pendiente o Aprobada");

        if (orden.Estado == EstadoOrdenCompra.Aprobada)
        {
            var tieneRecepciones = orden.Detalles.Any(d => d.CantidadRecibida > 0);
            if (tieneRecepciones)
                return (false, "No se puede cancelar una orden que ya tiene recepciones parciales");
        }

        orden.Estado = EstadoOrdenCompra.Cancelada;
        orden.MotivoRechazo = dto.Motivo;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} cancelada", orden.NumeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CancelarOrdenCompra",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Orden de compra {orden.NumeroOrden} cancelada: {dto.Motivo}",
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        return (true, null);
    }

    // ─── Mappers públicos (usados también por el controller para lecturas) ────

    public static OrdenCompraDto MapearOrdenCompraDtoSync(OrdenCompra orden, Dictionary<int, string?> usuariosDict)
    {
        var aprobadoPor = orden.AprobadoPorUsuarioId.HasValue
            ? usuariosDict.GetValueOrDefault(orden.AprobadoPorUsuarioId.Value)
            : null;
        var recibidoPor = orden.RecibidoPorUsuarioId.HasValue
            ? usuariosDict.GetValueOrDefault(orden.RecibidoPorUsuarioId.Value)
            : null;
        return BuildOrdenCompraDto(orden, aprobadoPor, recibidoPor);
    }

    public static async Task<OrdenCompraDto> MapearOrdenCompraDtoAsync(OrdenCompra orden, AppDbContext context)
    {
        string? aprobadoPor = null;
        if (orden.AprobadoPorUsuarioId.HasValue)
        {
            var u = await context.Usuarios.FindAsync(orden.AprobadoPorUsuarioId.Value);
            aprobadoPor = u?.Email;
        }
        string? recibidoPor = null;
        if (orden.RecibidoPorUsuarioId.HasValue)
        {
            var u = await context.Usuarios.FindAsync(orden.RecibidoPorUsuarioId.Value);
            recibidoPor = u?.Email;
        }
        return BuildOrdenCompraDto(orden, aprobadoPor, recibidoPor);
    }

    public static OrdenCompraDto BuildOrdenCompraDto(OrdenCompra orden, string? aprobadoPor, string? recibidoPor)
        => new OrdenCompraDto(
            Id: orden.Id,
            NumeroOrden: orden.NumeroOrden,
            SucursalId: orden.SucursalId,
            NombreSucursal: orden.Sucursal.Nombre,
            ProveedorId: orden.ProveedorId,
            NombreProveedor: orden.Proveedor.Nombre,
            Estado: orden.Estado.ToString(),
            FormaPago: orden.FormaPago,
            DiasPlazo: orden.DiasPlazo,
            FechaOrden: orden.FechaOrden,
            FechaEntregaEsperada: orden.FechaEntregaEsperada,
            FechaAprobacion: orden.FechaAprobacion,
            FechaRecepcion: orden.FechaRecepcion,
            AprobadoPor: aprobadoPor,
            RecibidoPor: recibidoPor,
            Observaciones: orden.Observaciones,
            MotivoRechazo: orden.MotivoRechazo,
            Subtotal: orden.Subtotal,
            Impuestos: orden.Impuestos,
            Total: orden.Total,
            RequiereFacturaElectronica: orden.RequiereFacturaElectronica,
            SincronizadoErp: orden.SincronizadoErp,
            FechaSincronizacionErp: orden.FechaSincronizacionErp,
            ErpReferencia: orden.ErpReferencia,
            ErrorSincronizacion: orden.ErrorSincronizacion,
            Detalles: orden.Detalles.Select(d => new DetalleOrdenCompraDto(
                Id: d.Id,
                ProductoId: d.ProductoId,
                NombreProducto: d.NombreProducto,
                CantidadSolicitada: d.CantidadSolicitada,
                CantidadRecibida: d.CantidadRecibida,
                PrecioUnitario: d.PrecioUnitario,
                PorcentajeImpuesto: d.PorcentajeImpuesto * 100,
                MontoImpuesto: d.MontoImpuesto,
                Subtotal: d.Subtotal,
                NombreImpuesto: d.NombreImpuesto,
                Observaciones: d.Observaciones,
                ManejaLotes: d.Producto?.ManejaLotes ?? false,
                DiasVidaUtil: d.Producto?.DiasVidaUtil
            )).ToList()
        );
}
