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
        // Validar sucursal y proveedor en una sola query
        var productosIds = dto.Lineas.Select(l => l.ProductoId).ToList();
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada");

        var proveedor = await _context.Terceros.FindAsync(dto.ProveedorId);
        if (proveedor == null)
            return (null, "Proveedor no encontrado");

        // Cargar productos (sin impuesto vía navigation — se carga separado con IgnoreQueryFilters)
        var productos = await _context.Productos
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

        // Cargar impuestos de los productos (IgnoreQueryFilters para acceder a registros globales con EmpresaId = null)
        var productoImpuestoIds = productos.Values
            .Where(p => p.ImpuestoId.HasValue)
            .Select(p => p.ImpuestoId!.Value)
            .Distinct()
            .ToList();
        var impuestosProducto = productoImpuestoIds.Count > 0
            ? await _context.Impuestos
                .IgnoreQueryFilters()
                .Where(i => productoImpuestoIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id)
            : new Dictionary<int, Impuesto>();

        // Impuestos seleccionados explícitamente por línea (también con IgnoreQueryFilters)
        var impuestoIdsOverride = dto.Lineas
            .Where(l => l.ImpuestoId.HasValue)
            .Select(l => l.ImpuestoId!.Value)
            .Distinct()
            .ToList();
        var impuestosOverride = impuestoIdsOverride.Count > 0
            ? await _context.Impuestos
                .IgnoreQueryFilters()
                .Where(i => impuestoIdsOverride.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id)
            : new Dictionary<int, Impuesto>();

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

        // Calcular totales usando TaxEngine
        decimal subtotal = 0;
        decimal impuestosTotal = 0;
        bool requiereFacturaElectronica = false;

        var detalles = new List<DetalleOrdenCompra>();
        foreach (var linea in dto.Lineas)
        {
            var producto = productos[linea.ProductoId];

            // Resolver impuesto: ImpuestoId explícito > PorcentajeImpuesto directo > impuesto del producto
            // Usa IgnoreQueryFilters en la carga para acceder también a registros globales (EmpresaId = null)
            var impuestoProducto = producto.ImpuestoId.HasValue && impuestosProducto.TryGetValue(producto.ImpuestoId.Value, out var ip) ? ip : null;
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
                NombreImpuesto: d.NombreImpuesto
                    ?? (d.PorcentajeImpuesto > 0 ? $"IVA {d.PorcentajeImpuesto * 100:0.##}%" : "Exento 0%"),
                Observaciones: d.Observaciones,
                ManejaLotes: d.Producto?.ManejaLotes ?? false,
                DiasVidaUtil: d.Producto?.DiasVidaUtil
            )).ToList()
        );
}
