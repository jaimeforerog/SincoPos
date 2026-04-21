using Microsoft.AspNetCore.Http;
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
    private readonly CompraRecepcionService _recepcionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CompraService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        global::Marten.IDocumentStore store,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<CompraService> logger,
        IActivityLogService activityLogService,
        IOptions<ErpSincoOptions> erpOptions,
        ICompraErpService compraErpService,
        CompraRecepcionService recepcionService,
        IHttpContextAccessor httpContextAccessor)
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
        _recepcionService = recepcionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(OrdenCompraDto? orden, string? error)> CrearOrdenAsync(CrearOrdenCompraDto dto)
    {
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null) return (null, "Sucursal no encontrada");

        var proveedor = await _context.Terceros.FindAsync(dto.ProveedorId);
        if (proveedor == null) return (null, "Proveedor no encontrado");

        // Generar número de orden secuencial POR SUCURSAL usando MAX del número actual
        // (no COUNT, que colisiona cuando los números no son consecutivos por la migración desde índice global)
        var lastNumeroOrden = await _context.OrdenesCompra
            .IgnoreQueryFilters()
            .Where(o => o.SucursalId == dto.SucursalId)
            .MaxAsync(o => (string?)o.NumeroOrden);
        int nextNum = 1;
        if (lastNumeroOrden != null && lastNumeroOrden.Length >= 9 && lastNumeroOrden.StartsWith("OC-"))
        {
            int.TryParse(lastNumeroOrden.AsSpan(3), out nextNum);
            nextNum++;
        }
        var numeroOrden = $"OC-{nextNum:000000}";

        var (detalles, subtotal, impuestosTotal, requiereFactura, calcError) =
            await CalcularDetallesLineasAsync(dto.Lineas, proveedor, sucursal);
        if (calcError != null) return (null, calcError);

        var orden = new OrdenCompra
        {
            NumeroOrden = numeroOrden,
            EmpresaId = sucursal.EmpresaId,
            SucursalId = dto.SucursalId,
            ProveedorId = dto.ProveedorId,
            Estado = EstadoOrdenCompra.Pendiente,
            FechaOrden = dto.FechaOrden.HasValue
                ? DateTime.SpecifyKind(dto.FechaOrden.Value, DateTimeKind.Utc)
                : DateTime.UtcNow,
            FechaEntregaEsperada = dto.FechaEntregaEsperada.HasValue
                ? DateTime.SpecifyKind(dto.FechaEntregaEsperada.Value, DateTimeKind.Utc)
                : null,
            Observaciones = dto.Observaciones,
            FormaPago = dto.FormaPago,
            DiasPlazo = dto.DiasPlazo,
            Subtotal = subtotal,
            Impuestos = impuestosTotal,
            Total = subtotal + impuestosTotal,
            RequiereFacturaElectronica = requiereFactura,
            Detalles = detalles!
        };

        _context.OrdenesCompra.Add(orden);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} creada", numeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearOrdenCompra",
            Tipo: TipoActividad.Compra,
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

        return (CompraMapper.BuildOrdenCompraDto(orden, null, null), null);
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

        var subUsuario = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario, subUsuario);

        orden.Estado = EstadoOrdenCompra.Aprobada;
        orden.FechaAprobacion = DateTime.UtcNow;
        orden.AprobadoPorUsuarioId = usuarioId;
        if (!string.IsNullOrEmpty(dto?.Observaciones))
            orden.Observaciones = dto.Observaciones;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} aprobada", orden.NumeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AprobarOrdenCompra",
            Tipo: TipoActividad.Compra,
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
            Tipo: TipoActividad.Compra,
            Descripcion: $"Orden de compra {orden.NumeroOrden} rechazada: {dto.MotivoRechazo}",
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        return (true, null);
    }

    public Task<(bool success, string? error)> RecibirOrdenAsync(
        int id, RecibirOrdenCompraDto dto, string? emailUsuario)
        => _recepcionService.RecibirOrdenAsync(id, dto, emailUsuario);

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
            Tipo: TipoActividad.Compra,
            Descripcion: $"Orden de compra {orden.NumeroOrden} cancelada: {dto.Motivo}",
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosNuevos: new { orden, dto }
        ));

        return (true, null);
    }

    public async Task<(OrdenCompraDto? orden, string? error)> ActualizarOrdenAsync(int id, ActualizarOrdenCompraDto dto)
    {
        var orden = await _context.OrdenesCompra
            .Include(o => o.Sucursal)
            .Include(o => o.Proveedor)
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orden == null) return (null, "NOT_FOUND");
        if (orden.Estado != EstadoOrdenCompra.Pendiente)
            return (null, "Solo se pueden editar órdenes en estado Pendiente");

        var anterior = new
        {
            orden.Observaciones,
            orden.FormaPago,
            orden.DiasPlazo,
            FechaEntregaEsperada = orden.FechaEntregaEsperada,
            Lineas = orden.Detalles.Select(d => new { d.ProductoId, d.CantidadSolicitada, d.PrecioUnitario }).ToList()
        };

        if (dto.FechaEntregaEsperada.HasValue)
            orden.FechaEntregaEsperada = DateTime.SpecifyKind(dto.FechaEntregaEsperada.Value, DateTimeKind.Utc);
        if (dto.Observaciones != null)
            orden.Observaciones = dto.Observaciones;
        if (dto.FormaPago != null)
            orden.FormaPago = dto.FormaPago;
        if (dto.DiasPlazo.HasValue)
            orden.DiasPlazo = dto.DiasPlazo.Value;

        if (dto.Lineas != null && dto.Lineas.Count > 0)
        {
            var (detalles, subtotal, impuestosTotal, requiereFactura, calcError) =
                await CalcularDetallesLineasAsync(dto.Lineas, orden.Proveedor, orden.Sucursal);
            if (calcError != null) return (null, calcError);

            _context.RemoveRange(orden.Detalles);
            orden.Detalles = detalles!;
            orden.Subtotal = subtotal;
            orden.Impuestos = impuestosTotal;
            orden.Total = subtotal + impuestosTotal;
            orden.RequiereFacturaElectronica = requiereFactura;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Orden de compra {NumeroOrden} actualizada", orden.NumeroOrden);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ActualizarOrdenCompra",
            Tipo: TipoActividad.Compra,
            Descripcion: $"Orden de compra {orden.NumeroOrden} editada" + (dto.Lineas != null ? $" — {dto.Lineas.Count} línea(s)" : ""),
            SucursalId: orden.SucursalId,
            TipoEntidad: "OrdenCompra",
            EntidadId: orden.Id.ToString(),
            EntidadNombre: orden.NumeroOrden,
            DatosAnteriores: anterior,
            DatosNuevos: new { dto }
        ));

        return (CompraMapper.BuildOrdenCompraDto(orden, null, null), null);
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private async Task<(List<DetalleOrdenCompra>? Detalles, decimal Subtotal, decimal ImpuestosTotal, bool RequiereFactura, string? Error)>
        CalcularDetallesLineasAsync(
            IReadOnlyList<LineaOrdenCompraDto> lineas,
            Tercero proveedor,
            Sucursal sucursal)
    {
        var productosIds = lineas.Select(l => l.ProductoId).ToList();

        var productos = await _context.Productos
            .Include(p => p.ConceptoRetencion)
            .Where(p => productosIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var linea in lineas)
            if (!productos.ContainsKey(linea.ProductoId))
                return (null, 0, 0, false, $"Producto {linea.ProductoId} no encontrado");

        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo).ToListAsync();

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var tramosBebidasAzucaradas = await _context.TramosBebidasAzucaradas
            .Where(t => t.Activo && t.VigenciaDesde <= hoy)
            .OrderBy(t => t.MaxGramosPor100ml)
            .ToListAsync();

        // Cargar impuestos de los productos y los overrides por línea (IgnoreQueryFilters para registros globales con EmpresaId = null)
        var productoImpuestoIds = productos.Values
            .Where(p => p.ImpuestoId.HasValue)
            .Select(p => p.ImpuestoId!.Value).Distinct().ToList();
        var impuestosProducto = productoImpuestoIds.Count > 0
            ? await _context.Impuestos.IgnoreQueryFilters()
                .Where(i => productoImpuestoIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id)
            : new Dictionary<int, Impuesto>();

        var impuestoIdsOverride = lineas
            .Where(l => l.ImpuestoId.HasValue)
            .Select(l => l.ImpuestoId!.Value).Distinct().ToList();
        var impuestosOverride = impuestoIdsOverride.Count > 0
            ? await _context.Impuestos.IgnoreQueryFilters()
                .Where(i => impuestoIdsOverride.Contains(i.Id)).ToDictionaryAsync(i => i.Id)
            : new Dictionary<int, Impuesto>();

        decimal subtotal = 0, impuestosTotal = 0;
        bool requiereFactura = false;
        var detalles = new List<DetalleOrdenCompra>();

        foreach (var linea in lineas)
        {
            var producto = productos[linea.ProductoId];
            var impuestoProducto = producto.ImpuestoId.HasValue && impuestosProducto.TryGetValue(producto.ImpuestoId.Value, out var ip) ? ip : null;

            // Resolver impuesto: ImpuestoId explícito > PorcentajeImpuesto directo > impuesto del producto
            Impuesto? impuesto = linea.ImpuestoId.HasValue && impuestosOverride.TryGetValue(linea.ImpuestoId.Value, out var imp)
                ? imp
                : linea.PorcentajeImpuesto.HasValue
                    ? new Impuesto { Nombre = $"IVA {linea.PorcentajeImpuesto}%", Porcentaje = linea.PorcentajeImpuesto.Value / 100m, Tipo = TipoImpuesto.IVA, AplicaSobreBase = true }
                    : impuestoProducto;

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
                ReglasRetencion: reglasRetencion,
                TramosBebidasAzucaradas: tramosBebidasAzucaradas
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
            requiereFactura |= taxResult.RequiereFacturaElectronica;
        }

        return (detalles, subtotal, impuestosTotal, requiereFactura, null);
    }
}
