using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Gestiona devoluciones de mercancía a proveedores originadas en órdenes de compra.
/// Una devolución reduce el stock en la sucursal y queda registrada como trazabilidad.
/// </summary>
public class CompraDevolucionService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public CompraDevolucionService(AppDbContext context, IActivityLogService activityLogService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    public async Task<(DevolucionCompraDto? devolucion, string? error)> CrearAsync(
        int ordenId,
        CrearDevolucionCompraDto dto,
        string? emailUsuario)
    {
        // ── 1. Cargar la orden con sus detalles ────────────────────────────────
        var orden = await _context.OrdenesCompra
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden == null)
            return (null, "Orden de compra no encontrada.");

        if (orden.Estado != EstadoOrdenCompra.RecibidaParcial &&
            orden.Estado != EstadoOrdenCompra.RecibidaCompleta)
            return (null, "Solo se pueden hacer devoluciones de órdenes en estado Recibida Parcial o Recibida Completa.");

        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return (null, "El motivo de la devolución es obligatorio.");

        if (dto.Lineas == null || dto.Lineas.Count == 0)
            return (null, "Debe incluir al menos una línea en la devolución.");

        // ── 2. Cantidades ya devueltas anteriormente por producto ──────────────
        var devolucionesAnteriores = await _context.DevolucionesCompra
            .Where(d => d.OrdenCompraId == ordenId)
            .Include(d => d.Detalles)
            .ToListAsync();

        var yaDevuelto = devolucionesAnteriores
            .SelectMany(d => d.Detalles)
            .GroupBy(dd => dd.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(dd => dd.CantidadDevuelta));

        // ── 3. Cargar stock actual de la sucursal (necesario para validación) ───
        var productoIds = dto.Lineas.Select(l => l.ProductoId).ToList();
        var stocksMap = await _context.Stock
            .Where(s => productoIds.Contains(s.ProductoId) && s.SucursalId == orden.SucursalId)
            .ToDictionaryAsync(s => s.ProductoId);

        // ── 4. Validar líneas de la solicitud ──────────────────────────────────
        var detalleDict = orden.Detalles.ToDictionary(d => d.ProductoId);

        foreach (var linea in dto.Lineas)
        {
            if (!detalleDict.TryGetValue(linea.ProductoId, out var detalle))
                return (null, $"El producto {linea.ProductoId} no pertenece a esta orden de compra.");

            if (linea.Cantidad <= 0)
                return (null, $"La cantidad a devolver de '{detalle.NombreProducto}' debe ser mayor a 0.");

            // Máximo por OC: lo recibido menos lo ya devuelto anteriormente
            var maxPorOC = detalle.CantidadRecibida - (yaDevuelto.GetValueOrDefault(linea.ProductoId, 0));
            if (linea.Cantidad > maxPorOC)
                return (null,
                    $"'{detalle.NombreProducto}': cantidad a devolver ({linea.Cantidad}) supera " +
                    $"lo disponible según la orden ({maxPorOC}).");

            // Máximo por stock físico actual: no se puede devolver lo que ya no está en almacén
            if (stocksMap.TryGetValue(linea.ProductoId, out var stock) && linea.Cantidad > stock.Cantidad)
                return (null,
                    $"'{detalle.NombreProducto}': cantidad a devolver ({linea.Cantidad}) supera " +
                    $"el stock físico actual ({stock.Cantidad}). Es posible que parte del inventario ya fue vendido.");
        }

        // ── 5. Generar número de devolución ────────────────────────────────────
        var maxId = await _context.DevolucionesCompra.IgnoreQueryFilters().MaxAsync(d => (int?)d.Id) ?? 0;
        var numeroDevolucion = $"DC-{maxId + 1:D6}";

        // ── 6. Resolver usuario ────────────────────────────────────────────────
        int? usuarioId = null;
        if (!string.IsNullOrEmpty(emailUsuario))
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailUsuario);
            usuarioId = usuario?.Id;
        }

        // ── 7. Construir entidad de devolución ─────────────────────────────────
        decimal totalDevolucion = 0;
        var detallesDevolucion = new List<DetalleDevolucionCompra>();

        foreach (var linea in dto.Lineas)
        {
            var detalle = detalleDict[linea.ProductoId];
            var subtotal = linea.Cantidad * detalle.PrecioUnitario;
            totalDevolucion += subtotal;

            detallesDevolucion.Add(new DetalleDevolucionCompra
            {
                ProductoId = linea.ProductoId,
                NombreProducto = detalle.NombreProducto,
                CantidadDevuelta = linea.Cantidad,
                PrecioUnitario = detalle.PrecioUnitario,
                Subtotal = subtotal,
            });

            // Reducir stock (devolución = salida de inventario)
            if (stocksMap.TryGetValue(linea.ProductoId, out var stock))
                stock.Cantidad -= linea.Cantidad;
        }

        var devolucion = new DevolucionCompra
        {
            OrdenCompraId = ordenId,
            NumeroDevolucion = numeroDevolucion,
            Motivo = dto.Motivo,
            Total = totalDevolucion,
            FechaDevolucion = DateTime.UtcNow,
            AutorizadoPorUsuarioId = usuarioId,
            Detalles = detallesDevolucion,
        };

        // ── 8. Persistir ───────────────────────────────────────────────────────
        _context.DevolucionesCompra.Add(devolucion);
        await _context.SaveChangesAsync();

        _ = _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DevolucionCompra",
            Tipo: TipoActividad.Compra,
            Descripcion: $"Devolución {numeroDevolucion} de orden {orden.NumeroOrden}. Total: ${totalDevolucion:N2}",
            SucursalId: orden.SucursalId,
            TipoEntidad: "DevolucionCompra",
            EntidadId: devolucion.Id.ToString(),
            EntidadNombre: numeroDevolucion,
            DatosNuevos: new { OrdenId = ordenId, NumeroDevolucion = numeroDevolucion, dto.Motivo, Total = totalDevolucion }
        ));

        // ── 9. Construir respuesta ─────────────────────────────────────────────
        return (BuildDto(devolucion, orden.NumeroOrden, null), null);
    }

    public async Task<List<DevolucionCompraDto>> ObtenerPorOrdenAsync(int ordenId)
    {
        var orden = await _context.OrdenesCompra
            .AsNoTracking()
            .Select(o => new { o.Id, o.NumeroOrden })
            .FirstOrDefaultAsync(o => o.Id == ordenId);

        if (orden == null) return new();

        var devoluciones = await _context.DevolucionesCompra
            .AsNoTracking()
            .Include(d => d.Detalles)
            .Where(d => d.OrdenCompraId == ordenId)
            .OrderByDescending(d => d.FechaDevolucion)
            .ToListAsync();

        var usuarioIds = devoluciones
            .Where(d => d.AutorizadoPorUsuarioId.HasValue)
            .Select(d => d.AutorizadoPorUsuarioId!.Value)
            .Distinct()
            .ToList();

        var usuariosDict = usuarioIds.Count > 0
            ? await _context.Usuarios
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.NombreCompleto) ? u.Email : u.NombreCompleto)
            : new Dictionary<int, string>();

        return devoluciones.Select(d =>
            BuildDto(d, orden.NumeroOrden,
                d.AutorizadoPorUsuarioId.HasValue
                    ? usuariosDict.GetValueOrDefault(d.AutorizadoPorUsuarioId.Value)
                    : null))
            .ToList();
    }

    private static DevolucionCompraDto BuildDto(
        DevolucionCompra d, string numeroOrden, string? autorizadoPor) =>
        new(
            Id: d.Id,
            OrdenCompraId: d.OrdenCompraId,
            NumeroOrden: numeroOrden,
            NumeroDevolucion: d.NumeroDevolucion,
            Motivo: d.Motivo,
            Total: d.Total,
            FechaDevolucion: d.FechaDevolucion,
            AutorizadoPor: autorizadoPor,
            Detalles: d.Detalles.Select(dd => new DetalleDevolucionCompraDto(
                Id: dd.Id,
                ProductoId: dd.ProductoId,
                NombreProducto: dd.NombreProducto,
                CantidadDevuelta: dd.CantidadDevuelta,
                PrecioUnitario: dd.PrecioUnitario,
                Subtotal: dd.Subtotal
            )).ToList()
        );
}
