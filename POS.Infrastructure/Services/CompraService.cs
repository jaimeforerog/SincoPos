using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class CompraService : ICompraService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly CosteoService _costeoService;
    private readonly ITaxEngine _taxEngine;
    private readonly ILogger<CompraService> _logger;
    private readonly IActivityLogService _activityLogService;

    public CompraService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<CompraService> logger,
        IActivityLogService activityLogService)
    {
        _context = context;
        _session = session;
        _costeoService = costeoService;
        _taxEngine = taxEngine;
        _logger = logger;
        _activityLogService = activityLogService;
    }

    public async Task<(OrdenCompraDto? orden, string? error)> CrearOrdenAsync(CrearOrdenCompraDto dto)
    {
        // Validar sucursal
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada");

        // Validar proveedor
        var proveedor = await _context.Terceros.FindAsync(dto.ProveedorId);
        if (proveedor == null)
            return (null, "Proveedor no encontrado");

        // Cargar productos con impuesto en una sola query (evitar N+1)
        var productosIds = dto.Lineas.Select(l => l.ProductoId).ToList();
        var productos = await _context.Productos
            .Include(p => p.Impuesto)
            .Where(p => productosIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Validar que todos los productos existan
        foreach (var linea in dto.Lineas)
        {
            if (!productos.ContainsKey(linea.ProductoId))
                return (null, $"Producto {linea.ProductoId} no encontrado");
        }

        // Cargar retenciones activas
        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo).ToListAsync();

        // Cargar impuestos por override (ImpuestoId explícito en línea)
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

        // Generar número de orden
        var ultimaOrden = await _context.OrdenesCompra
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();
        var numeroOrden = $"OC-{(ultimaOrden?.Id ?? 0) + 1:000000}";

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
            SucursalId = dto.SucursalId,
            ProveedorId = dto.ProveedorId,
            Estado = EstadoOrdenCompra.Pendiente,
            FechaOrden = DateTime.UtcNow,
            FechaEntregaEsperada = dto.FechaEntregaEsperada.HasValue
                ? DateTime.SpecifyKind(dto.FechaEntregaEsperada.Value, DateTimeKind.Utc)
                : null,
            Observaciones = dto.Observaciones,
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

        // Recargar con navigations para el mapper
        await _context.Entry(orden).Reference(o => o.Sucursal).LoadAsync();
        await _context.Entry(orden).Reference(o => o.Proveedor).LoadAsync();

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

        int? usuarioId = await ResolverUsuarioIdAsync(emailUsuario);

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

                _session.Events.StartStream<InventarioAggregate>(streamId, primerEvento);
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

                _session.Events.Append(streamId, eventoEntrada);
            }

            // 2. Registrar lote de entrada
            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId,
                orden.SucursalId,
                lineaRecibida.CantidadRecibida,
                detalle.PrecioUnitario,
                detalle.PorcentajeImpuesto,
                detalle.MontoImpuesto,
                orden.NumeroOrden,
                orden.ProveedorId);

            // 3. Actualizar stock
            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalle.ProductoId && s.SucursalId == orden.SucursalId);

            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock,
                    lineaRecibida.CantidadRecibida,
                    detalle.PrecioUnitario,
                    orden.Sucursal!.MetodoCosteo);
            }
        }

        // Resolver usuario
        int? usuarioId = await ResolverUsuarioIdAsync(emailUsuario);

        // Actualizar estado de la orden
        var todasRecibidas = orden.Detalles.All(d => d.CantidadRecibida >= d.CantidadSolicitada);
        orden.Estado = todasRecibidas ? EstadoOrdenCompra.RecibidaCompleta : EstadoOrdenCompra.RecibidaParcial;
        orden.FechaRecepcion = DateTime.UtcNow;
        orden.RecibidoPorUsuarioId = usuarioId;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

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

    // ─── Helpers privados ─────────────────────────────────────────────────────

    private async Task<int?> ResolverUsuarioIdAsync(string? email)
    {
        if (string.IsNullOrEmpty(email)) return null;
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);
        return usuario?.Id;
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
                Observaciones: d.Observaciones
            )).ToList()
        );
}
