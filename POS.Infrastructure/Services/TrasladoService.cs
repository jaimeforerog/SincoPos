using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed partial class TrasladoService : ITrasladoService
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

    private async Task<int?> ResolverUsuarioActualAsync(string? emailOverride = null)
    {
        var email = emailOverride
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return await _context.ResolverUsuarioIdAsync(email, sub);
    }
}
