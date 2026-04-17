using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class TrasladoService : ITrasladoService
{
    private readonly global::Marten.IDocumentSession _session;
    private readonly AppDbContext _context;
    private readonly ILogger<TrasladoService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly CosteoService _costeoService;
    private readonly INotificationService _notificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TrasladoService(
        global::Marten.IDocumentSession session,
        AppDbContext context,
        ILogger<TrasladoService> logger,
        IActivityLogService activityLogService,
        CosteoService costeoService,
        INotificationService notificationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _context = context;
        _logger = logger;
        _activityLogService = activityLogService;
        _costeoService = costeoService;
        _notificationService = notificationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(object? resultado, string? error)> CrearTrasladoAsync(CrearTrasladoDto dto)
    {
        // Validar sucursales
        var sucursalOrigen = await _context.Sucursales.FindAsync(dto.SucursalOrigenId);
        if (sucursalOrigen == null)
            return (null, "Sucursal origen no encontrada.");

        var sucursalDestino = await _context.Sucursales.FindAsync(dto.SucursalDestinoId);
        if (sucursalDestino == null)
            return (null, "Sucursal destino no encontrada.");

        // Validar stock suficiente
        foreach (var linea in dto.Lineas)
        {
            var producto = await _context.Productos.FindAsync(linea.ProductoId);
            if (producto == null)
                return (null, $"Producto {linea.ProductoId} no encontrado.");

            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == linea.ProductoId && s.SucursalId == dto.SucursalOrigenId);

            if (stock == null || stock.Cantidad < linea.Cantidad)
                return (null, $"Stock insuficiente para {producto.Nombre}. Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");
        }

        // Generar número de traslado (IgnoreQueryFilters evita colisión entre empresas)
        var ultimoTraslado = await _context.Traslados
            .IgnoreQueryFilters()
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();
        var numeroTraslado = $"TRAS-{(ultimoTraslado?.Id ?? 0) + 1:000000}";

        // Crear traslado
        var traslado = new Traslado
        {
            NumeroTraslado = numeroTraslado,
            EmpresaId = sucursalOrigen.EmpresaId,
            SucursalOrigenId = dto.SucursalOrigenId,
            SucursalDestinoId = dto.SucursalDestinoId,
            Estado = EstadoTraslado.Pendiente,
            FechaTraslado = DateTime.UtcNow,
            Observaciones = dto.Observaciones,
            Detalles = dto.Lineas.Select(l => new DetalleTraslado
            {
                ProductoId = l.ProductoId,
                NombreProducto = _context.Productos.Find(l.ProductoId)!.Nombre,
                CantidadSolicitada = l.Cantidad,
                CantidadRecibida = 0,
                CostoUnitario = 0,
                CostoTotal = 0,
                Observaciones = l.Observaciones
            }).ToList()
        };

        _context.Traslados.Add(traslado);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} creado", numeroTraslado);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {numeroTraslado} creado: {sucursalOrigen.Nombre} → {sucursalDestino.Nombre}",
            SucursalId: dto.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: numeroTraslado,
            DatosNuevos: new
            {
                trasladoId = traslado.Id,
                numeroTraslado,
                sucursalOrigen = sucursalOrigen.Nombre,
                sucursalDestino = sucursalDestino.Nombre,
                request = dto
            }
        ));

        return (new
        {
            mensaje = $"Traslado {numeroTraslado} creado exitosamente",
            trasladoId = traslado.Id,
            numeroTraslado
        }, null);
    }

    public async Task<(bool success, string? error)> EnviarTrasladoAsync(int id)
    {
        var traslado = await _context.Traslados
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return (false, "NOT_FOUND");
        if (traslado.Estado != EstadoTraslado.Pendiente)
            return (false, "Solo se pueden enviar traslados en estado Pendiente");

        var sucursal = await _context.Sucursales.FindAsync(traslado.SucursalOrigenId);

        // Resolver usuario que ejecuta el envío
        var emailEnvio = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var subEnvio = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioEnvioId = await _context.ResolverUsuarioIdAsync(emailEnvio, subEnvio);

        // Pre-cargar productos para verificar ManejaLotes
        var productoIds = traslado.Detalles.Select(d => d.ProductoId).ToList();
        var productosDict = await _context.Productos
            .Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var detalle in traslado.Detalles)
        {
            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalle.ProductoId && s.SucursalId == traslado.SucursalOrigenId);

            if (stock == null || stock.Cantidad < detalle.CantidadSolicitada)
                return (false, $"Stock insuficiente para {detalle.NombreProducto}. Disponible: {stock?.Cantidad ?? 0}, Solicitado: {detalle.CantidadSolicitada}");

            var producto = productosDict.GetValueOrDefault(detalle.ProductoId);
            decimal costoTotal, costoUnitario;
            int? loteId = null;
            string? numeroLote = null;
            DateOnly? fechaVencimiento = null;

            if (producto?.ManejaLotes == true)
            {
                // Consumir por FEFO y capturar datos del lote para trazabilidad
                decimal costoUnitarioFefo;
                (costoTotal, costoUnitarioFefo, loteId, numeroLote) = await _costeoService.ConsumirLotesFEFO(
                    detalle.ProductoId, traslado.SucursalOrigenId, detalle.CantidadSolicitada);
                costoUnitario = costoUnitarioFefo;

                // Capturar fecha de vencimiento del lote consumido
                if (loteId.HasValue)
                {
                    var lote = await _context.LotesInventario.FindAsync(loteId.Value);
                    fechaVencimiento = lote?.FechaVencimiento;
                }
            }
            else
            {
                (costoTotal, costoUnitario) = await _costeoService.ConsumirStock(
                    detalle.ProductoId, traslado.SucursalOrigenId,
                    detalle.CantidadSolicitada, sucursal!.MetodoCosteo);
            }

            detalle.CostoUnitario = costoUnitario;
            detalle.CostoTotal = costoTotal;
            detalle.LoteInventarioId = loteId;
            detalle.NumeroLote = numeroLote;
            detalle.FechaVencimiento = fechaVencimiento;

            var streamId = InventarioAggregate.GenerarStreamId(
                detalle.ProductoId, traslado.SucursalOrigenId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate == null)
                return (false, $"No existe inventario inicializado para el producto {detalle.NombreProducto} en la sucursal origen. " +
                               "Por favor, realice una entrada de inventario primero.");

            var eventoSalida = aggregate.RegistrarSalidaTraslado(
                detalle.CantidadSolicitada,
                costoUnitario,
                traslado.SucursalDestinoId,
                traslado.NumeroTraslado,
                detalle.Observaciones,
                usuarioEnvioId);

            _session.Events.Append(streamId, eventoSalida);

            stock.Cantidad -= detalle.CantidadSolicitada;
            stock.UltimaActualizacion = DateTime.UtcNow;
        }

        traslado.Estado = EstadoTraslado.EnTransito;
        traslado.FechaEnvio = DateTime.UtcNow;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} enviado", traslado.NumeroTraslado);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "EnviarTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} enviado",
            SucursalId: traslado.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new
            {
                trasladoId = traslado.Id,
                numeroTraslado = traslado.NumeroTraslado,
                estado = traslado.Estado.ToString(),
                fechaEnvio = traslado.FechaEnvio
            }
        ));

        return (true, null);
    }

    public async Task<(bool success, string? error)> RecibirTrasladoAsync(int id, RecibirTrasladoDto dto, string? emailUsuario)
    {
        var traslado = await _context.Traslados
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return (false, "NOT_FOUND");
        if (traslado.Estado != EstadoTraslado.EnTransito)
            return (false, "Solo se pueden recibir traslados en estado EnTransito");

        // Load sucursal once outside the loop (same for all lines)
        var sucursalDestino = await _context.Sucursales.FindAsync(traslado.SucursalDestinoId);

        try
        {
            foreach (var lineaRecibida in dto.Lineas)
            {
                var detalle = traslado.Detalles.FirstOrDefault(d => d.ProductoId == lineaRecibida.ProductoId);
                if (detalle == null)
                    return (false, $"Producto {lineaRecibida.ProductoId} no está en el traslado");

                if (lineaRecibida.CantidadRecibida > detalle.CantidadSolicitada)
                    return (false, $"La cantidad recibida no puede exceder la solicitada para {detalle.NombreProducto}");

                if (lineaRecibida.CantidadRecibida <= 0)
                    return (false, $"La cantidad recibida debe ser mayor a 0 para {detalle.NombreProducto}");

                detalle.CantidadRecibida = lineaRecibida.CantidadRecibida;
                if (!string.IsNullOrEmpty(lineaRecibida.Observaciones))
                    detalle.Observaciones = lineaRecibida.Observaciones;

                var streamId = InventarioAggregate.GenerarStreamId(
                    detalle.ProductoId, traslado.SucursalDestinoId);
                var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

                if (aggregate == null)
                {
                    var (_, primerEvento) = InventarioAggregate.RegistrarEntrada(
                        streamId,
                        detalle.ProductoId,
                        traslado.SucursalDestinoId,
                        lineaRecibida.CantidadRecibida,
                        detalle.CostoUnitario,
                        0, 0,
                        null, null,
                        traslado.NumeroTraslado,
                        $"Traslado desde sucursal {traslado.SucursalOrigenId}",
                        null,
                        traslado.SucursalDestinoId);

                    _session.Events.StartStream<InventarioAggregate>(streamId, primerEvento);
                }
                else
                {
                    var eventoEntrada = aggregate.RegistrarEntradaTraslado(
                        lineaRecibida.CantidadRecibida,
                        detalle.CostoUnitario,
                        traslado.SucursalOrigenId,
                        traslado.NumeroTraslado,
                        lineaRecibida.Observaciones,
                        null);

                    _session.Events.Append(streamId, eventoEntrada);
                }

                // Propagar número de lote y fecha de vencimiento del origen al destino
                await _costeoService.RegistrarLoteEntrada(
                    detalle.ProductoId,
                    traslado.SucursalDestinoId,
                    lineaRecibida.CantidadRecibida,
                    detalle.CostoUnitario,
                    0, 0,
                    traslado.NumeroTraslado,
                    null,
                    numeroLote: detalle.NumeroLote,
                    fechaVencimiento: detalle.FechaVencimiento);

                var stock = await _context.Stock.FirstOrDefaultAsync(
                    s => s.ProductoId == detalle.ProductoId && s.SucursalId == traslado.SucursalDestinoId);

                if (stock == null)
                {
                    stock = new Stock
                    {
                        ProductoId = detalle.ProductoId,
                        SucursalId = traslado.SucursalDestinoId,
                        Cantidad = 0,
                        StockMinimo = 0,
                        CostoPromedio = 0
                    };
                    _context.Stock.Add(stock);
                }

                await _costeoService.ActualizarCostoEntrada(
                    stock,
                    lineaRecibida.CantidadRecibida,
                    detalle.CostoUnitario,
                    sucursalDestino!.MetodoCosteo);
            }
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        var subUsuario = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioId = await _context.ResolverUsuarioIdAsync(emailUsuario, subUsuario);

        traslado.Estado = EstadoTraslado.Recibido;
        traslado.FechaRecepcion = DateTime.UtcNow;
        traslado.RecibidoPorUsuarioId = usuarioId;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} recibido", traslado.NumeroTraslado);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "RecibirTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} recibido",
            SucursalId: traslado.SucursalDestinoId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new
            {
                trasladoId = traslado.Id,
                numeroTraslado = traslado.NumeroTraslado,
                estado = traslado.Estado.ToString(),
                fechaRecepcion = traslado.FechaRecepcion,
                recepcion = dto
            }
        ));

        await _notificationService.EnviarNotificacionSucursalAsync(traslado.SucursalDestinoId,
            new NotificacionDto("traslado_recibido", "Traslado recibido",
                $"Traslado {traslado.NumeroTraslado} recibido en destino", "info", DateTime.UtcNow,
                new { traslado.Id, traslado.NumeroTraslado }));

        return (true, null);
    }

    public async Task<(bool success, string? error)> RechazarTrasladoAsync(int id, RechazarTrasladoDto dto)
    {
        var traslado = await _context.Traslados
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return (false, "NOT_FOUND");
        if (traslado.Estado != EstadoTraslado.EnTransito)
            return (false, "Solo se pueden rechazar traslados en estado EnTransito");

        var sucursal = await _context.Sucursales.FindAsync(traslado.SucursalOrigenId);

        // Resolver usuario que ejecuta el rechazo
        var emailRechazo = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var subRechazo = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? usuarioRechazoId = await _context.ResolverUsuarioIdAsync(emailRechazo, subRechazo);

        foreach (var detalle in traslado.Detalles)
        {
            var streamId = InventarioAggregate.GenerarStreamId(
                detalle.ProductoId, traslado.SucursalOrigenId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate != null)
            {
                var eventoReversion = aggregate.AgregarEntrada(
                    detalle.CantidadSolicitada,
                    detalle.CostoUnitario,
                    null, null,
                    $"REV-{traslado.NumeroTraslado}",
                    $"Reversión traslado rechazado: {dto.MotivoRechazo}",
                    usuarioRechazoId);

                _session.Events.Append(streamId, eventoReversion);
            }

            if (detalle.LoteInventarioId.HasValue)
                await _costeoService.ReintegrarLoteAsync(detalle.LoteInventarioId.Value, detalle.CantidadSolicitada);
            else
                await _costeoService.RegistrarLoteEntrada(
                    detalle.ProductoId, traslado.SucursalOrigenId,
                    detalle.CantidadSolicitada, detalle.CostoUnitario,
                    0, 0, $"REV-{traslado.NumeroTraslado}", null);

            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalle.ProductoId && s.SucursalId == traslado.SucursalOrigenId);
            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock, detalle.CantidadSolicitada, detalle.CostoUnitario, sucursal!.MetodoCosteo);
            }
        }

        traslado.Estado = EstadoTraslado.Rechazado;
        traslado.MotivoRechazo = dto.MotivoRechazo;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} rechazado", traslado.NumeroTraslado);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "RechazarTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} rechazado: {dto.MotivoRechazo}",
            SucursalId: traslado.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new
            {
                trasladoId = traslado.Id,
                numeroTraslado = traslado.NumeroTraslado,
                estado = traslado.Estado.ToString(),
                motivoRechazo = dto.MotivoRechazo
            }
        ));

        return (true, null);
    }

    public async Task<(bool success, string? error)> CancelarTrasladoAsync(int id, CancelarTrasladoDto dto)
    {
        var traslado = await _context.Traslados.FindAsync(id);

        if (traslado == null) return (false, "NOT_FOUND");
        if (traslado.Estado != EstadoTraslado.Pendiente)
            return (false, "Solo se pueden cancelar traslados en estado Pendiente");

        traslado.Estado = EstadoTraslado.Cancelado;
        traslado.MotivoRechazo = dto.Motivo;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} cancelado", traslado.NumeroTraslado);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CancelarTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} cancelado: {dto.Motivo}",
            SucursalId: traslado.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new
            {
                trasladoId = traslado.Id,
                numeroTraslado = traslado.NumeroTraslado,
                estado = traslado.Estado.ToString(),
                motivo = dto.Motivo
            }
        ));

        return (true, null);
    }
}
