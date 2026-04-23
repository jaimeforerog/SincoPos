using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed class InventarioService : IInventarioService
{
    private readonly global::Marten.IDocumentSession _session;
    private readonly AppDbContext _context;
    private readonly CosteoService _costeoService;
    private readonly ILogger<InventarioService> _logger;
    private readonly IActivityLogService _activityLogService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InventarioService(
        global::Marten.IDocumentSession session,
        AppDbContext context,
        CosteoService costeoService,
        ILogger<InventarioService> logger,
        IActivityLogService activityLogService,
        IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _context = context;
        _costeoService = costeoService;
        _logger = logger;
        _activityLogService = activityLogService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(object? resultado, string? error)> RegistrarEntradaAsync(EntradaInventarioDto dto, string? emailUsuario)
    {
        var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == dto.ProductoId);
        if (producto == null)
            return (null, "Producto no encontrado.");

        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada.");

        string? nombreTercero = null;
        if (dto.TerceroId.HasValue)
        {
            var tercero = await _context.Terceros.FindAsync(dto.TerceroId.Value);
            if (tercero == null)
                return (null, "Tercero (proveedor) no encontrado.");
            nombreTercero = tercero.Nombre;
        }

        var currentUserId = await _context.ResolverUsuarioIdAsync(emailUsuario, ObtenerSubClaim());
        var streamId = InventarioAggregate.GenerarStreamId(dto.ProductoId, dto.SucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        var costoTotal = dto.Cantidad * dto.CostoUnitario;
        var montoImpuesto = costoTotal * (dto.PorcentajeImpuesto / 100m);

        if (aggregate == null)
        {
            var (newAggregate, newEvento) = InventarioAggregate.RegistrarEntradaManual(
                streamId, dto.ProductoId, dto.SucursalId,
                dto.Cantidad, dto.CostoUnitario,
                dto.PorcentajeImpuesto, montoImpuesto,
                dto.TerceroId, nombreTercero,
                dto.Referencia ?? string.Empty, dto.Observaciones,
                usuarioId: currentUserId, sucursalUsuarioId: dto.SucursalId,
                fechaMovimiento: dto.FechaMovimiento);
            aggregate = newAggregate;
            _session.Events.StartStream<InventarioAggregate>(streamId, newEvento);
        }
        else
        {
            var evento = aggregate.AgregarEntradaManual(
                dto.Cantidad, dto.CostoUnitario,
                dto.TerceroId, nombreTercero,
                dto.Referencia ?? string.Empty, dto.Observaciones,
                usuarioId: currentUserId,
                fechaMovimiento: dto.FechaMovimiento);
            _session.Events.Append(streamId, evento);
        }

        await _session.SaveChangesAsync();

        _logger.LogInformation(
            "Entrada registrada via Event Sourcing. Producto: {ProductoId}, Sucursal: {SucursalId}, Cantidad: {Cantidad}",
            dto.ProductoId, dto.SucursalId, dto.Cantidad);

        // Actualizar stock y lotes directamente (la proyección es no-op para este evento)
        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId && s.SucursalId == dto.SucursalId);

        if (stock == null)
        {
            stock = new Stock
            {
                ProductoId = dto.ProductoId,
                SucursalId = dto.SucursalId,
                Cantidad = 0,
                StockMinimo = 0,
                CostoPromedio = 0
            };
            _context.Stock.Add(stock);
        }

        var montoImpuestoUnitario = dto.Cantidad > 0 ? montoImpuesto / dto.Cantidad : 0;
        var fechaVencimientoLote = dto.FechaVencimiento
            ?? (producto.DiasVidaUtil.HasValue
                ? DateOnly.FromDateTime(DateTime.Today.AddDays(producto.DiasVidaUtil.Value))
                : (DateOnly?)null);

        await _costeoService.RegistrarLoteEntrada(
            dto.ProductoId, dto.SucursalId, dto.Cantidad,
            dto.CostoUnitario, dto.PorcentajeImpuesto,
            montoImpuestoUnitario,
            dto.Referencia, dto.TerceroId,
            numeroLote: dto.NumeroLote, fechaVencimiento: fechaVencimientoLote,
            fechaEntrada: dto.FechaMovimiento);

        await _costeoService.ActualizarCostoEntrada(stock, dto.Cantidad, dto.CostoUnitario, sucursal.MetodoCosteo);

        await _context.SaveChangesAsync();

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "EntradaInventario",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Entrada de mercancía: {producto.Nombre} x {dto.Cantidad}. Costo unitario: ${dto.CostoUnitario:N2}. Proveedor: {nombreTercero ?? "N/A"}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Inventario",
            EntidadId: $"{dto.ProductoId}_{dto.SucursalId}",
            EntidadNombre: producto.Nombre,
            DatosNuevos: new
            {
                ProductoId = dto.ProductoId,
                NombreProducto = producto.Nombre,
                Cantidad = dto.Cantidad,
                CostoUnitario = dto.CostoUnitario,
                CostoTotal = costoTotal,
                PorcentajeImpuesto = dto.PorcentajeImpuesto,
                MontoImpuesto = montoImpuesto,
                TerceroId = dto.TerceroId,
                NombreTercero = nombreTercero,
                Referencia = dto.Referencia,
                Observaciones = dto.Observaciones,
                StockActual = stock?.Cantidad ?? dto.Cantidad,
                CostoPromedio = stock?.CostoPromedio ?? dto.CostoUnitario
            }
        ));

        return (new
        {
            mensaje = "Entrada de mercancia registrada.",
            metodoCosteo = sucursal.MetodoCosteo.ToString(),
            stockActual = stock?.Cantidad ?? dto.Cantidad,
            costoPromedio = stock?.CostoPromedio ?? dto.CostoUnitario,
            eventosTotales = aggregate.Lotes.Count
        }, null);
    }

    public async Task<(object? resultado, string? error)> DevolucionProveedorAsync(DevolucionProveedorDto dto, string? emailUsuario)
    {
        var tercero = await _context.Terceros.FindAsync(dto.TerceroId);
        if (tercero == null)
            return (null, "Proveedor no encontrado.");

        var streamId = InventarioAggregate.GenerarStreamId(dto.ProductoId, dto.SucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        if (aggregate == null)
            return (null, "No existe inventario para este producto en esta sucursal.");

        var currentUserId = await _context.ResolverUsuarioIdAsync(emailUsuario, ObtenerSubClaim());

        try
        {
            var evento = aggregate.RegistrarDevolucion(
                dto.Cantidad, dto.TerceroId,
                tercero.Nombre, dto.Referencia,
                dto.Observaciones, usuarioId: currentUserId);

            _session.Events.Append(streamId, evento);
            await _session.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return (null, ex.Message);
        }

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId && s.SucursalId == dto.SucursalId);
        var producto = await _context.Productos.FindAsync(dto.ProductoId);

        _logger.LogInformation(
            "Devolucion registrada via Event Sourcing. Producto: {ProductoId}, Proveedor: {Proveedor}, Cantidad: {Cantidad}",
            dto.ProductoId, tercero.Nombre, dto.Cantidad);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DevolucionProveedor",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Devolución a proveedor: {producto?.Nombre ?? "Producto"} x {dto.Cantidad}. Proveedor: {tercero.Nombre}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Inventario",
            EntidadId: $"{dto.ProductoId}_{dto.SucursalId}",
            EntidadNombre: producto?.Nombre,
            DatosNuevos: new
            {
                ProductoId = dto.ProductoId,
                NombreProducto = producto?.Nombre,
                CantidadDevuelta = dto.Cantidad,
                TerceroId = dto.TerceroId,
                NombreTercero = tercero.Nombre,
                Referencia = dto.Referencia,
                Observaciones = dto.Observaciones,
                StockActual = stock?.Cantidad ?? 0,
                CostoPromedio = stock?.CostoPromedio ?? 0
            }
        ));

        return (new
        {
            mensaje = "Devolucion a proveedor registrada.",
            cantidadDevuelta = dto.Cantidad,
            stockActual = stock?.Cantidad ?? 0,
            costoPromedio = stock?.CostoPromedio ?? 0
        }, null);
    }

    public async Task<(object? resultado, string? error)> AjustarInventarioAsync(AjusteInventarioDto dto, string? emailUsuario)
    {
        var currentUserId = await _context.ResolverUsuarioIdAsync(emailUsuario, ObtenerSubClaim());
        var streamId = InventarioAggregate.GenerarStreamId(dto.ProductoId, dto.SucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        decimal cantidadAnterior;

        if (aggregate == null)
        {
            cantidadAnterior = 0;
            var ajusteEvento = new AjusteInventarioRegistrado
            {
                ProductoId = dto.ProductoId,
                SucursalId = dto.SucursalId,
                CantidadAnterior = 0,
                CantidadNueva = dto.CantidadNueva,
                Diferencia = dto.CantidadNueva,
                EsPositivo = true,
                CostoUnitario = 0,
                CostoTotal = 0,
                Observaciones = dto.Observaciones ?? $"Ajuste manual inicial: 0 → {dto.CantidadNueva}",
                UsuarioId = currentUserId
            };
            _session.Events.StartStream<InventarioAggregate>(streamId, ajusteEvento);
        }
        else
        {
            cantidadAnterior = aggregate.Cantidad;
            try
            {
                var evento = aggregate.RegistrarAjuste(dto.CantidadNueva, dto.Observaciones, usuarioId: currentUserId);
                _session.Events.Append(streamId, evento);
            }
            catch (InvalidOperationException ex)
            {
                return (null, ex.Message);
            }
        }

        await _session.SaveChangesAsync();

        var producto = await _context.Productos.FindAsync(dto.ProductoId);
        var diferencia = dto.CantidadNueva - cantidadAnterior;

        _logger.LogInformation(
            "Ajuste registrado via Event Sourcing. Producto: {ProductoId}, Anterior: {Anterior}, Nuevo: {Nuevo}",
            dto.ProductoId, cantidadAnterior, dto.CantidadNueva);

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AjusteInventario",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Ajuste de inventario: {producto?.Nombre ?? "Producto"}. De {cantidadAnterior} a {dto.CantidadNueva} (Diferencia: {diferencia:+#;-#;0}). Motivo: {dto.Observaciones ?? "No especificado"}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Inventario",
            EntidadId: $"{dto.ProductoId}_{dto.SucursalId}",
            EntidadNombre: producto?.Nombre,
            DatosAnteriores: new { CantidadAnterior = cantidadAnterior },
            DatosNuevos: new
            {
                ProductoId = dto.ProductoId,
                NombreProducto = producto?.Nombre,
                CantidadNueva = dto.CantidadNueva,
                Diferencia = diferencia,
                Observaciones = dto.Observaciones
            }
        ));

        return (new
        {
            mensaje = "Ajuste de inventario registrado.",
            cantidadAnterior,
            cantidadNueva = dto.CantidadNueva,
            diferencia
        }, null);
    }

    public async Task<(bool success, string? error)> ActualizarStockMinimoAsync(
        Guid productoId, int sucursalId, decimal stockMinimo, string? emailUsuario)
    {
        if (stockMinimo < 0)
            return (false, "El stock minimo no puede ser negativo.");

        var streamId = InventarioAggregate.GenerarStreamId(productoId, sucursalId);
        var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

        if (aggregate == null)
            return (false, "NOT_FOUND");

        var currentUserId = await _context.ResolverUsuarioIdAsync(emailUsuario, ObtenerSubClaim());

        try
        {
            var evento = aggregate.ActualizarStockMinimo(stockMinimo, usuarioId: currentUserId);
            _session.Events.Append(streamId, evento);
            await _session.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        _logger.LogInformation("Stock minimo actualizado via Event Sourcing. Producto: {P}, Sucursal: {S}, Minimo: {M}",
            productoId, sucursalId, stockMinimo);

        return (true, null);
    }

    /// <summary>
    /// Extrae el claim "sub" (WorkOS user ID) del token actual como fallback
    /// cuando la búsqueda por email no encuentra al usuario en la tabla Usuarios.
    /// </summary>
    private string? ObtenerSubClaim() =>
        _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

}
