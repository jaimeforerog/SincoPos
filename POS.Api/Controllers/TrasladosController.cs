using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TrasladosController : ControllerBase
{
    private readonly Marten.IDocumentSession _session;
    private readonly AppDbContext _context;
    private readonly ILogger<TrasladosController> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly CosteoService _costeoService;

    public TrasladosController(
        Marten.IDocumentSession session,
        AppDbContext context,
        ILogger<TrasladosController> logger,
        IActivityLogService activityLogService,
        CosteoService costeoService)
    {
        _session = session;
        _context = context;
        _logger = logger;
        _activityLogService = activityLogService;
        _costeoService = costeoService;
    }

    /// <summary>
    /// POST /api/Traslados - Crear traslado en estado Pendiente
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> CrearTraslado(
        CrearTrasladoDto dto,
        [FromServices] IValidator<CrearTrasladoDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        // Validar sucursales
        var sucursalOrigen = await _context.Sucursales.FindAsync(dto.SucursalOrigenId);
        if (sucursalOrigen == null)
            return BadRequest(new { error = "Sucursal origen no encontrada." });

        var sucursalDestino = await _context.Sucursales.FindAsync(dto.SucursalDestinoId);
        if (sucursalDestino == null)
            return BadRequest(new { error = "Sucursal destino no encontrada." });

        // Validar stock suficiente
        foreach (var linea in dto.Lineas)
        {
            var producto = await _context.Productos.FindAsync(linea.ProductoId);
            if (producto == null)
                return BadRequest(new { error = $"Producto {linea.ProductoId} no encontrado." });

            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == linea.ProductoId && s.SucursalId == dto.SucursalOrigenId);

            if (stock == null || stock.Cantidad < linea.Cantidad)
                return BadRequest(new
                {
                    error = $"Stock insuficiente para {producto.Nombre}. Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}"
                });
        }

        // Generar número de traslado
        var ultimoTraslado = await _context.Traslados
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();
        var numeroTraslado = $"TRAS-{(ultimoTraslado?.Id ?? 0) + 1:000000}";

        // Crear traslado
        var traslado = new Traslado
        {
            NumeroTraslado = numeroTraslado,
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
                CostoUnitario = 0,  // Se calcula al enviar
                CostoTotal = 0,
                Observaciones = l.Observaciones
            }).ToList()
        };

        _context.Traslados.Add(traslado);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} creado", numeroTraslado);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {numeroTraslado} creado: {sucursalOrigen.Nombre} → {sucursalDestino.Nombre}",
            SucursalId: dto.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: numeroTraslado,
            DatosNuevos: new { traslado, dto }
        ));

        return Ok(new
        {
            mensaje = $"Traslado {numeroTraslado} creado exitosamente",
            trasladoId = traslado.Id,
            numeroTraslado
        });
    }

    /// <summary>
    /// POST /api/Traslados/{id}/enviar - Enviar traslado (consume stock en origen)
    /// </summary>
    [HttpPost("{id:int}/enviar")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> EnviarTraslado(int id)
    {
        var traslado = await _context.Traslados
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return NotFound();
        if (traslado.Estado != EstadoTraslado.Pendiente)
            return BadRequest(new { error = "Solo se pueden enviar traslados en estado Pendiente" });

        var sucursal = await _context.Sucursales.FindAsync(traslado.SucursalOrigenId);

        // Procesar cada línea: consumir stock en origen
        foreach (var detalle in traslado.Detalles)
        {
            // Validar stock disponible
            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalle.ProductoId && s.SucursalId == traslado.SucursalOrigenId);

            if (stock == null || stock.Cantidad < detalle.CantidadSolicitada)
                return BadRequest(new
                {
                    error = $"Stock insuficiente para {detalle.NombreProducto}. Disponible: {stock?.Cantidad ?? 0}, Solicitado: {detalle.CantidadSolicitada}"
                });

            // 1. Consumir stock y obtener costo real (procesa lotes)
            var (costoTotal, costoUnitario) = await _costeoService.ConsumirStock(
                detalle.ProductoId, traslado.SucursalOrigenId,
                detalle.CantidadSolicitada, sucursal!.MetodoCosteo);

            // 2. Actualizar detalle con costo real
            detalle.CostoUnitario = costoUnitario;
            detalle.CostoTotal = costoTotal;

            // 3. Event Sourcing: Crear evento
            var streamId = InventarioAggregate.GenerarStreamId(
                detalle.ProductoId, traslado.SucursalOrigenId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate == null)
            {
                return BadRequest(new
                {
                    error = $"No existe inventario inicializado para el producto {detalle.NombreProducto} en la sucursal origen. " +
                            "Por favor, realice una entrada de inventario primero."
                });
            }

            var eventoSalida = aggregate.RegistrarSalidaTraslado(
                detalle.CantidadSolicitada,
                costoUnitario,
                traslado.SucursalDestinoId,
                traslado.NumeroTraslado,
                detalle.Observaciones,
                null);

            _session.Events.Append(streamId, eventoSalida);

            // 4. Actualizar stock en EF (ConsumirStock ya actualizó lotes)
            stock.Cantidad -= detalle.CantidadSolicitada;
            stock.UltimaActualizacion = DateTime.UtcNow;
        }

        // Cambiar estado del traslado
        traslado.Estado = EstadoTraslado.EnTransito;
        traslado.FechaEnvio = DateTime.UtcNow;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} enviado", traslado.NumeroTraslado);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "EnviarTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} enviado",
            SucursalId: traslado.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new { traslado }
        ));

        return Ok(new { mensaje = $"Traslado {traslado.NumeroTraslado} enviado exitosamente" });
    }

    /// <summary>
    /// POST /api/Traslados/{id}/recibir - Recibir traslado (ingresa stock en destino)
    /// </summary>
    [HttpPost("{id:int}/recibir")]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult> RecibirTraslado(
        int id,
        [FromBody] RecibirTrasladoDto dto,
        [FromServices] IValidator<RecibirTrasladoDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var traslado = await _context.Traslados
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return NotFound();
        if (traslado.Estado != EstadoTraslado.EnTransito)
            return BadRequest(new { error = "Solo se pueden recibir traslados en estado EnTransito" });

        // Procesar cada línea: ingresar stock en destino
        foreach (var lineaRecibida in dto.Lineas)
        {
            var detalle = traslado.Detalles.FirstOrDefault(d => d.ProductoId == lineaRecibida.ProductoId);
            if (detalle == null)
                return BadRequest(new { error = $"Producto {lineaRecibida.ProductoId} no está en el traslado" });

            if (lineaRecibida.CantidadRecibida > detalle.CantidadSolicitada)
                return BadRequest(new
                {
                    error = $"La cantidad recibida no puede exceder la solicitada para {detalle.NombreProducto}"
                });

            // Actualizar detalle
            detalle.CantidadRecibida = lineaRecibida.CantidadRecibida;
            if (!string.IsNullOrEmpty(lineaRecibida.Observaciones))
                detalle.Observaciones = lineaRecibida.Observaciones;

            // Event Sourcing: Registrar entrada en destino
            var streamId = InventarioAggregate.GenerarStreamId(
                detalle.ProductoId, traslado.SucursalDestinoId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            // Si el aggregate no existe (producto nunca ha estado en esta sucursal), crear stream
            if (aggregate == null)
            {
                // Crear stream con el evento de entrada del traslado
                var (newAggregate, primerEvento) = InventarioAggregate.RegistrarEntrada(
                    streamId,
                    detalle.ProductoId,
                    traslado.SucursalDestinoId,
                    lineaRecibida.CantidadRecibida,
                    detalle.CostoUnitario,
                    0, 0,  // Sin impuestos
                    null, null,
                    traslado.NumeroTraslado,
                    $"Traslado desde sucursal {traslado.SucursalOrigenId}",
                    null,
                    traslado.SucursalDestinoId);

                _session.Events.StartStream<InventarioAggregate>(streamId, primerEvento);
            }
            else
            {
                // Agregar evento al stream existente
                var eventoEntrada = aggregate.RegistrarEntradaTraslado(
                    lineaRecibida.CantidadRecibida,
                    detalle.CostoUnitario,  // Costo del origen
                    traslado.SucursalOrigenId,
                    traslado.NumeroTraslado,
                    lineaRecibida.Observaciones,
                    null);

                _session.Events.Append(streamId, eventoEntrada);
            }

            // Registrar lote de entrada
            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId,
                traslado.SucursalDestinoId,
                lineaRecibida.CantidadRecibida,
                detalle.CostoUnitario,
                0, 0,  // Sin impuestos
                traslado.NumeroTraslado,
                null);

            // Actualizar stock en destino
            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalle.ProductoId && s.SucursalId == traslado.SucursalDestinoId);

            var sucursalDestino = await _context.Sucursales.FindAsync(traslado.SucursalDestinoId);
            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock,
                    lineaRecibida.CantidadRecibida,
                    detalle.CostoUnitario,
                    sucursalDestino!.MetodoCosteo);
            }
        }

        // Obtener usuario actual
        var emailUsuario = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        int? usuarioId = null;
        if (!string.IsNullOrEmpty(emailUsuario))
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailUsuario);
            usuarioId = usuario?.Id;
        }

        // Cambiar estado del traslado
        traslado.Estado = EstadoTraslado.Recibido;
        traslado.FechaRecepcion = DateTime.UtcNow;
        traslado.RecibidoPorUsuarioId = usuarioId;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} recibido", traslado.NumeroTraslado);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "RecibirTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} recibido",
            SucursalId: traslado.SucursalDestinoId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new { traslado, dto }
        ));

        return Ok(new { mensaje = $"Traslado {traslado.NumeroTraslado} recibido exitosamente" });
    }

    /// <summary>
    /// POST /api/Traslados/{id}/rechazar - Rechazar traslado (revierte stock en origen)
    /// </summary>
    [HttpPost("{id:int}/rechazar")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> RechazarTraslado(
        int id,
        [FromBody] RechazarTrasladoDto dto,
        [FromServices] IValidator<RechazarTrasladoDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var traslado = await _context.Traslados
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return NotFound();
        if (traslado.Estado != EstadoTraslado.EnTransito)
            return BadRequest(new { error = "Solo se pueden rechazar traslados en estado EnTransito" });

        var sucursal = await _context.Sucursales.FindAsync(traslado.SucursalOrigenId);

        // Revertir: Registrar entrada en origen (devuelve el stock)
        foreach (var detalle in traslado.Detalles)
        {
            // Event Sourcing: Registrar entrada de reversión
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
                    null);

                _session.Events.Append(streamId, eventoReversion);
            }

            // Registrar lote de entrada
            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId, traslado.SucursalOrigenId,
                detalle.CantidadSolicitada, detalle.CostoUnitario,
                0, 0, $"REV-{traslado.NumeroTraslado}", null);

            // Actualizar stock
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

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "RechazarTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} rechazado: {dto.MotivoRechazo}",
            SucursalId: traslado.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new { traslado, dto }
        ));

        return Ok(new { mensaje = $"Traslado {traslado.NumeroTraslado} rechazado y stock revertido" });
    }

    /// <summary>
    /// POST /api/Traslados/{id}/cancelar - Cancelar traslado (solo si está Pendiente)
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> CancelarTraslado(
        int id,
        [FromBody] CancelarTrasladoDto dto,
        [FromServices] IValidator<CancelarTrasladoDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { errors });
        }

        var traslado = await _context.Traslados.FindAsync(id);

        if (traslado == null) return NotFound();
        if (traslado.Estado != EstadoTraslado.Pendiente)
            return BadRequest(new { error = "Solo se pueden cancelar traslados en estado Pendiente" });

        traslado.Estado = EstadoTraslado.Cancelado;
        traslado.MotivoRechazo = dto.Motivo;  // Reutilizamos el campo para el motivo

        await _context.SaveChangesAsync();

        _logger.LogInformation("Traslado {NumeroTraslado} cancelado", traslado.NumeroTraslado);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CancelarTraslado",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Traslado {traslado.NumeroTraslado} cancelado: {dto.Motivo}",
            SucursalId: traslado.SucursalOrigenId,
            TipoEntidad: "Traslado",
            EntidadId: traslado.Id.ToString(),
            EntidadNombre: traslado.NumeroTraslado,
            DatosNuevos: new { traslado, dto }
        ));

        return Ok(new { mensaje = $"Traslado {traslado.NumeroTraslado} cancelado" });
    }

    /// <summary>
    /// GET /api/Traslados - Listar traslados con filtros
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult<IEnumerable<TrasladoDto>>> ListarTraslados(
        [FromQuery] int? sucursalOrigenId,
        [FromQuery] int? sucursalDestinoId,
        [FromQuery] EstadoTraslado? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int limite = 50)
    {
        var query = _context.Traslados
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Detalles)
            .AsQueryable();

        if (sucursalOrigenId.HasValue)
            query = query.Where(t => t.SucursalOrigenId == sucursalOrigenId.Value);

        if (sucursalDestinoId.HasValue)
            query = query.Where(t => t.SucursalDestinoId == sucursalDestinoId.Value);

        if (estado.HasValue)
            query = query.Where(t => t.Estado == estado.Value);

        if (desde.HasValue)
            query = query.Where(t => t.FechaTraslado >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(t => t.FechaTraslado <= hasta.Value);

        var traslados = await query
            .OrderByDescending(t => t.FechaTraslado)
            .Take(Math.Min(limite, 50))
            .ToListAsync();

        var resultado = traslados.Select(t => new TrasladoDto(
            Id: t.Id,
            NumeroTraslado: t.NumeroTraslado,
            SucursalOrigenId: t.SucursalOrigenId,
            NombreSucursalOrigen: t.SucursalOrigen.Nombre,
            SucursalDestinoId: t.SucursalDestinoId,
            NombreSucursalDestino: t.SucursalDestino.Nombre,
            Estado: t.Estado.ToString(),
            FechaTraslado: t.FechaTraslado,
            FechaEnvio: t.FechaEnvio,
            FechaRecepcion: t.FechaRecepcion,
            RecibidoPor: t.RecibidoPorUsuarioId.HasValue
                ? _context.Usuarios.Find(t.RecibidoPorUsuarioId.Value)?.Email
                : null,
            Observaciones: t.Observaciones,
            MotivoRechazo: t.MotivoRechazo,
            Detalles: t.Detalles.Select(d => new DetalleTrasladoDto(
                Id: d.Id,
                ProductoId: d.ProductoId,
                NombreProducto: d.NombreProducto,
                CantidadSolicitada: d.CantidadSolicitada,
                CantidadRecibida: d.CantidadRecibida,
                CostoUnitario: d.CostoUnitario,
                CostoTotal: d.CostoTotal,
                Observaciones: d.Observaciones
            )).ToList()
        )).ToList();

        return Ok(resultado);
    }

    /// <summary>
    /// GET /api/Traslados/{id} - Obtener detalle de un traslado específico
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult<TrasladoDto>> ObtenerTraslado(int id)
    {
        var traslado = await _context.Traslados
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Detalles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (traslado == null) return NotFound();

        var resultado = new TrasladoDto(
            Id: traslado.Id,
            NumeroTraslado: traslado.NumeroTraslado,
            SucursalOrigenId: traslado.SucursalOrigenId,
            NombreSucursalOrigen: traslado.SucursalOrigen.Nombre,
            SucursalDestinoId: traslado.SucursalDestinoId,
            NombreSucursalDestino: traslado.SucursalDestino.Nombre,
            Estado: traslado.Estado.ToString(),
            FechaTraslado: traslado.FechaTraslado,
            FechaEnvio: traslado.FechaEnvio,
            FechaRecepcion: traslado.FechaRecepcion,
            RecibidoPor: traslado.RecibidoPorUsuarioId.HasValue
                ? _context.Usuarios.Find(traslado.RecibidoPorUsuarioId.Value)?.Email
                : null,
            Observaciones: traslado.Observaciones,
            MotivoRechazo: traslado.MotivoRechazo,
            Detalles: traslado.Detalles.Select(d => new DetalleTrasladoDto(
                Id: d.Id,
                ProductoId: d.ProductoId,
                NombreProducto: d.NombreProducto,
                CantidadSolicitada: d.CantidadSolicitada,
                CantidadRecibida: d.CantidadRecibida,
                CostoUnitario: d.CostoUnitario,
                CostoTotal: d.CostoTotal,
                Observaciones: d.Observaciones
            )).ToList()
        );

        return Ok(resultado);
    }
}
