using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class VentaService : IVentaService
{
    private readonly AppDbContext _context;
    private readonly global::Marten.IDocumentSession _session;
    private readonly PrecioService _precioService;
    private readonly CosteoService _costeoService;
    private readonly ITaxEngine _taxEngine;
    private readonly ILogger<VentaService> _logger;
    private readonly IActivityLogService _activityLogService;

    public VentaService(
        AppDbContext context,
        global::Marten.IDocumentSession session,
        PrecioService precioService,
        CosteoService costeoService,
        ITaxEngine taxEngine,
        ILogger<VentaService> logger,
        IActivityLogService activityLogService)
    {
        _context = context;
        _session = session;
        _precioService = precioService;
        _costeoService = costeoService;
        _taxEngine = taxEngine;
        _logger = logger;
        _activityLogService = activityLogService;
    }

    public async Task<(VentaDto? venta, string? error)> CrearVentaAsync(CrearVentaDto dto)
    {
        // Verificar caja abierta
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.Id == dto.CajaId && c.SucursalId == dto.SucursalId);
        if (caja == null)
            return (null, "Caja no encontrada en esta sucursal.");
        if (caja.Estado != EstadoCaja.Abierta)
            return (null, "La caja no esta abierta.");

        // Obtener sucursal para metodo de costeo
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada.");

        // Verificar cliente (si aplica)
        string? nombreCliente = null;
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente == null) return (null, "Cliente no encontrado.");
            nombreCliente = cliente.Nombre;
        }

        // Generar numero de venta
        var ultimaVenta = await _context.Ventas
            .Where(v => v.SucursalId == dto.SucursalId)
            .OrderByDescending(v => v.Id)
            .Select(v => v.NumeroVenta)
            .FirstOrDefaultAsync();
        var consecutivo = 1;
        if (ultimaVenta != null && ultimaVenta.Contains('-'))
        {
            int.TryParse(ultimaVenta.Split('-').Last(), out consecutivo);
            consecutivo++;
        }
        var numeroVenta = $"V-{consecutivo:D6}";

        // Cargar reglas de retención activas de la sucursal
        var reglasRetencion = await _context.RetencionesReglas
            .Where(r => r.Activo)
            .ToListAsync();

        // Perfil del comprador (si aplica)
        string perfilComprador = "REGIMEN_COMUN";
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente != null) perfilComprador = cliente.PerfilTributario;
        }

        // Procesar cada linea con el TaxEngine
        var detalles = new List<DetalleVenta>();
        decimal subtotal = 0;
        decimal descuentoTotal = 0;
        decimal totalImpuestos = 0;
        bool requiereFacturaElectronica = false;

        foreach (var linea in dto.Lineas)
        {
            // Obtener producto con su impuesto
            var producto = await _context.Productos
                .Include(p => p.Impuesto)
                .FirstOrDefaultAsync(p => p.Id == linea.ProductoId);

            if (producto == null)
                return (null, $"Producto {linea.ProductoId} no encontrado.");
            if (!producto.Activo)
                return (null, $"Producto {producto.Nombre} esta inactivo.");

            // Verificar stock
            var stock = await _context.Stock
                .FirstOrDefaultAsync(s => s.ProductoId == linea.ProductoId
                    && s.SucursalId == dto.SucursalId);
            if (stock == null || stock.Cantidad < linea.Cantidad)
                return (null, $"Stock insuficiente para {producto.Nombre}. " +
                    $"Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");

            // Resolver precio
            decimal precioUnitario;
            if (linea.PrecioUnitario.HasValue)
            {
                var (valido, errorPrecio) = await _precioService.ValidarPrecio(
                    linea.ProductoId, dto.SucursalId, linea.PrecioUnitario.Value);
                if (!valido) return (null, errorPrecio);
                precioUnitario = linea.PrecioUnitario.Value;
            }
            else
            {
                var precio = await _precioService.ResolverPrecio(linea.ProductoId, dto.SucursalId);
                precioUnitario = precio.PrecioVenta;
            }

            // ── Calcular impuestos con el TaxEngine ────────────────────────────
            var taxResult = _taxEngine.Calcular(new TaxRequest(
                ProductoId: linea.ProductoId,
                Cantidad: linea.Cantidad,
                PrecioUnitario: precioUnitario,
                Impuesto: producto.Impuesto,
                EsAlimentoUltraprocesado: producto.EsAlimentoUltraprocesado,
                GramosAzucarPor100ml: producto.GramosAzucarPor100ml,
                PerfilVendedor: sucursal.PerfilTributario,
                PerfilComprador: perfilComprador,
                CodigoMunicipio: sucursal.CodigoMunicipio ?? string.Empty,
                ValorUVT: sucursal.ValorUVT,
                ReglasRetencion: reglasRetencion
            ));

            // Acumular flag de factura electrónica (si cualquier línea lo requiere)
            if (taxResult.RequiereFacturaElectronica)
                requiereFacturaElectronica = true;

            // Usar el primer impuesto aplicado para compatibilidad con DetalleVenta
            var primerImpuesto = taxResult.Impuestos.FirstOrDefault();
            decimal porcentajeImpuesto = primerImpuesto?.Porcentaje ?? 0;
            decimal montoImpuesto = taxResult.TotalImpuestos;
            totalImpuestos += montoImpuesto;

            // Consumir inventario via Event Sourcing
            var streamId = InventarioAggregate.GenerarStreamId(linea.ProductoId, dto.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
            if (aggregate == null)
                return (null, $"No hay registro de inventario para {producto.Nombre}.");

            var eventoVenta = aggregate.RegistrarSalidaVenta(
                linea.Cantidad, precioUnitario, porcentajeImpuesto, montoImpuesto, numeroVenta, null);
            _session.Events.Append(streamId, eventoVenta);

            // Consumir stock con metodo de costeo
            var (costoTotal, costoUnitario) = await _costeoService.ConsumirStock(
                linea.ProductoId, dto.SucursalId, linea.Cantidad, sucursal.MetodoCosteo);

            // Actualizar stock en EF Core
            stock.Cantidad -= linea.Cantidad;
            stock.UltimaActualizacion = DateTime.UtcNow;

            // Crear detalle
            var lineaSubtotal = (precioUnitario * linea.Cantidad) - linea.Descuento;
            var detalle = new DetalleVenta
            {
                ProductoId = linea.ProductoId,
                NombreProducto = producto.Nombre,
                Cantidad = linea.Cantidad,
                PrecioUnitario = precioUnitario,
                CostoUnitario = costoUnitario,
                Descuento = linea.Descuento,
                PorcentajeImpuesto = porcentajeImpuesto,
                MontoImpuesto = montoImpuesto,
                Subtotal = lineaSubtotal
            };
            detalles.Add(detalle);

            subtotal += precioUnitario * linea.Cantidad;
            descuentoTotal += linea.Descuento;
        }

        var total = subtotal - descuentoTotal + totalImpuestos;
        var cambio = dto.MontoPagado.HasValue ? dto.MontoPagado.Value - total : (decimal?)null;

        if (dto.MontoPagado.HasValue && dto.MontoPagado.Value < total)
            return (null, $"Monto pagado ({dto.MontoPagado.Value}) es menor al total ({total}).");

        // Crear venta
        var venta = new Venta
        {
            NumeroVenta = numeroVenta,
            SucursalId = dto.SucursalId,
            CajaId = dto.CajaId,
            ClienteId = dto.ClienteId,
            Subtotal = subtotal,
            Descuento = descuentoTotal,
            Impuestos = totalImpuestos,
            Total = total,
            Estado = EstadoVenta.Completada,
            MetodoPago = (MetodoPago)dto.MetodoPago,
            MontoPagado = dto.MontoPagado,
            Cambio = cambio,
            Observaciones = dto.Observaciones,
            FechaVenta = DateTime.UtcNow,
            RequiereFacturaElectronica = requiereFacturaElectronica,
            Detalles = detalles
        };
        _context.Ventas.Add(venta);

        // Actualizar monto de caja
        caja.MontoActual += total;

        // Guardar todo
        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Venta {NumeroVenta} completada. Total: {Total}, Items: {Items}",
            numeroVenta, total, detalles.Count);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "CrearVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {numeroVenta} creada. Total: ${total:N2}, Items: {detalles.Count}",
            SucursalId: dto.SucursalId,
            TipoEntidad: "Venta",
            EntidadId: venta.Id.ToString(),
            EntidadNombre: numeroVenta,
            DatosNuevos: new
            {
                NumeroVenta = numeroVenta,
                Total = total,
                Subtotal = subtotal,
                Descuento = descuentoTotal,
                Impuestos = totalImpuestos,
                MetodoPago = ((MetodoPago)dto.MetodoPago).ToString(),
                CantidadItems = detalles.Count,
                ClienteId = dto.ClienteId,
                CajaId = dto.CajaId,
                Productos = detalles.Select(d => new {
                    d.ProductoId,
                    d.NombreProducto,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
            }
        ));

        return (MapToDto(venta, sucursal.Nombre, caja.Nombre, nombreCliente), null);
    }

    public async Task<(bool success, string? error)> AnularVentaAsync(int id, string? motivo)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return (false, "NOT_FOUND");
        if (venta.Estado == EstadoVenta.Anulada)
            return (false, "La venta ya esta anulada.");

        var sucursal = await _context.Sucursales.FindAsync(venta.SucursalId);

        // Revertir cada linea
        foreach (var detalle in venta.Detalles)
        {
            // Registrar entrada de devolucion en ES
            var streamId = InventarioAggregate.GenerarStreamId(detalle.ProductoId, venta.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate != null)
            {
                var entradaEvento = aggregate.AgregarEntrada(
                    detalle.Cantidad, detalle.CostoUnitario,
                    null, null, $"Anulacion venta {venta.NumeroVenta}",
                    motivo ?? "Venta anulada", null);
                _session.Events.Append(streamId, entradaEvento);
            }

            // Registrar lote de entrada y actualizar stock
            var montoImpuestoUnitario = detalle.Cantidad > 0
                ? detalle.MontoImpuesto / detalle.Cantidad
                : 0;
            await _costeoService.RegistrarLoteEntrada(
                detalle.ProductoId, venta.SucursalId,
                detalle.Cantidad, detalle.CostoUnitario,
                detalle.PorcentajeImpuesto, montoImpuestoUnitario,
                $"Anulacion {venta.NumeroVenta}", null);

            var stock = await _context.Stock
                .FirstOrDefaultAsync(s => s.ProductoId == detalle.ProductoId
                    && s.SucursalId == venta.SucursalId);
            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock, detalle.Cantidad, detalle.CostoUnitario, sucursal!.MetodoCosteo);
            }
        }

        // Marcar venta como anulada
        venta.Estado = EstadoVenta.Anulada;
        venta.Observaciones = $"{venta.Observaciones} | ANULADA: {motivo ?? "Sin motivo"}";

        // Revertir monto de caja
        var caja = await _context.Cajas.FindAsync(venta.CajaId);
        if (caja != null)
            caja.MontoActual -= venta.Total;

        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Venta {NumeroVenta} anulada. Motivo: {Motivo}",
            venta.NumeroVenta, motivo);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "AnularVenta",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Venta {venta.NumeroVenta} anulada. Motivo: {motivo ?? "Sin motivo"}. Total revertido: ${venta.Total:N2}",
            SucursalId: venta.SucursalId,
            TipoEntidad: "Venta",
            EntidadId: id.ToString(),
            EntidadNombre: venta.NumeroVenta,
            DatosAnteriores: new
            {
                Estado = "Completada",
                Total = venta.Total,
                CantidadItems = venta.Detalles.Count
            },
            DatosNuevos: new
            {
                Estado = "Anulada",
                Motivo = motivo ?? "Sin motivo",
                ItemsRevertidos = venta.Detalles.Select(d => new {
                    d.ProductoId,
                    d.NombreProducto,
                    d.Cantidad
                })
            }
        ));

        return (true, null);
    }

    public async Task<(DevolucionVentaDto? devolucion, string? error)> CrearDevolucionParcialAsync(
        int ventaId, CrearDevolucionParcialDto dto, string? emailUsuario)
    {
        // Cargar venta con detalles
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta == null)
            return (null, "NOT_FOUND");

        // Validación: Solo ventas completadas pueden tener devoluciones
        if (venta.Estado != EstadoVenta.Completada)
            return (null, "Solo se pueden hacer devoluciones de ventas completadas.");

        // Validación: Límite de tiempo (30 días)
        var diasTranscurridos = (DateTime.UtcNow - venta.FechaVenta).TotalDays;
        if (diasTranscurridos > 30)
            return (null, $"La venta tiene {Math.Floor(diasTranscurridos)} días. Solo se permiten devoluciones dentro de 30 días.");

        // Cargar devoluciones anteriores de esta venta
        var devolucionesAnteriores = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Where(d => d.VentaId == ventaId)
            .ToListAsync();

        // Validar cada línea de devolución
        var detallesDevolucion = new List<DetalleDevolucion>();
        decimal totalDevuelto = 0;

        foreach (var linea in dto.Lineas)
        {
            // Verificar que el producto esté en la venta original
            var detalleOriginal = venta.Detalles.FirstOrDefault(d => d.ProductoId == linea.ProductoId);
            if (detalleOriginal == null)
                return (null, $"El producto {linea.ProductoId} no está en la venta original.");

            // Calcular cantidad ya devuelta de este producto
            var cantidadYaDevuelta = devolucionesAnteriores
                .SelectMany(d => d.Detalles)
                .Where(dd => dd.ProductoId == linea.ProductoId)
                .Sum(dd => dd.CantidadDevuelta);

            // Validar que no se exceda la cantidad vendida
            var cantidadDisponibleParaDevolver = detalleOriginal.Cantidad - cantidadYaDevuelta;
            if (linea.Cantidad > cantidadDisponibleParaDevolver)
                return (null,
                    $"No se puede devolver {linea.Cantidad} unidades del producto {detalleOriginal.NombreProducto}. " +
                    $"Vendido: {detalleOriginal.Cantidad}, Ya devuelto: {cantidadYaDevuelta}, Disponible: {cantidadDisponibleParaDevolver}");

            // Calcular subtotal devuelto (proporcional)
            var subtotalDevuelto = (detalleOriginal.PrecioUnitario * linea.Cantidad);
            totalDevuelto += subtotalDevuelto;

            // Crear detalle de devolución
            var detalleDevolucion = new DetalleDevolucion
            {
                ProductoId = linea.ProductoId,
                NombreProducto = detalleOriginal.NombreProducto,
                CantidadDevuelta = linea.Cantidad,
                PrecioUnitario = detalleOriginal.PrecioUnitario,
                CostoUnitario = detalleOriginal.CostoUnitario,
                SubtotalDevuelto = subtotalDevuelto
            };

            detallesDevolucion.Add(detalleDevolucion);
        }

        // Generar número de devolución
        var ultimaDevolucion = await _context.DevolucionesVenta
            .OrderByDescending(d => d.Id)
            .Select(d => d.NumeroDevolucion)
            .FirstOrDefaultAsync();

        var consecutivo = 1;
        if (ultimaDevolucion != null && ultimaDevolucion.Contains('-'))
        {
            int.TryParse(ultimaDevolucion.Split('-').Last(), out consecutivo);
            consecutivo++;
        }
        var numeroDevolucion = $"DEV-{consecutivo:D6}";

        // Cargar sucursal para método de costeo
        var sucursal = await _context.Sucursales.FindAsync(venta.SucursalId);
        if (sucursal == null)
            return (null, "Sucursal no encontrada.");

        // Procesar cada línea de devolución: restaurar inventario
        foreach (var detalleDevolucion in detallesDevolucion)
        {
            var detalleOriginal = venta.Detalles.First(d => d.ProductoId == detalleDevolucion.ProductoId);

            // Event Sourcing: Registrar entrada por devolución
            var streamId = InventarioAggregate.GenerarStreamId(
                detalleDevolucion.ProductoId, venta.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);

            if (aggregate != null)
            {
                var eventoEntrada = aggregate.AgregarEntrada(
                    detalleDevolucion.CantidadDevuelta,
                    detalleDevolucion.CostoUnitario,
                    null, null,
                    $"Devolución {numeroDevolucion}",
                    dto.Motivo,
                    null);
                _session.Events.Append(streamId, eventoEntrada);
            }

            // Registrar lote de entrada
            var montoImpuestoUnitario = detalleOriginal.Cantidad > 0
                ? detalleOriginal.MontoImpuesto / detalleOriginal.Cantidad
                : 0;

            await _costeoService.RegistrarLoteEntrada(
                detalleDevolucion.ProductoId,
                venta.SucursalId,
                detalleDevolucion.CantidadDevuelta,
                detalleDevolucion.CostoUnitario,
                detalleOriginal.PorcentajeImpuesto,
                montoImpuestoUnitario,
                $"Devolución {numeroDevolucion}",
                null);

            // Actualizar stock
            var stock = await _context.Stock.FirstOrDefaultAsync(
                s => s.ProductoId == detalleDevolucion.ProductoId && s.SucursalId == venta.SucursalId);

            if (stock != null)
            {
                await _costeoService.ActualizarCostoEntrada(
                    stock,
                    detalleDevolucion.CantidadDevuelta,
                    detalleDevolucion.CostoUnitario,
                    sucursal.MetodoCosteo);
            }
        }

        // Resolver usuarioId desde email
        int? usuarioId = null;
        if (!string.IsNullOrEmpty(emailUsuario))
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailUsuario);
            usuarioId = usuario?.Id;
        }

        // Crear registro de devolución
        var devolucion = new DevolucionVenta
        {
            VentaId = ventaId,
            NumeroDevolucion = numeroDevolucion,
            Motivo = dto.Motivo,
            TotalDevuelto = totalDevuelto,
            FechaDevolucion = DateTime.UtcNow,
            AutorizadoPorUsuarioId = usuarioId,
            Detalles = detallesDevolucion
        };

        _context.DevolucionesVenta.Add(devolucion);

        // Ajustar monto de caja
        var caja = await _context.Cajas.FindAsync(venta.CajaId);
        if (caja != null)
        {
            caja.MontoActual -= totalDevuelto;
        }

        // Guardar cambios
        await _session.SaveChangesAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Devolución parcial {NumeroDevolucion} creada para venta {NumeroVenta}. Total devuelto: {Total}",
            numeroDevolucion, venta.NumeroVenta, totalDevuelto);

        // Activity Log
        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "DevolucionParcial",
            Tipo: TipoActividad.Venta,
            Descripcion: $"Devolución parcial {numeroDevolucion} de venta {venta.NumeroVenta}. " +
                        $"Total devuelto: ${totalDevuelto:N2}, Items: {detallesDevolucion.Count}",
            SucursalId: venta.SucursalId,
            TipoEntidad: "DevolucionVenta",
            EntidadId: devolucion.Id.ToString(),
            EntidadNombre: numeroDevolucion,
            DatosNuevos: new
            {
                NumeroDevolucion = numeroDevolucion,
                VentaId = ventaId,
                NumeroVenta = venta.NumeroVenta,
                Motivo = dto.Motivo,
                TotalDevuelto = totalDevuelto,
                AutorizadoPor = emailUsuario,
                Productos = detallesDevolucion.Select(d => new
                {
                    d.ProductoId,
                    d.NombreProducto,
                    d.CantidadDevuelta,
                    d.SubtotalDevuelto
                })
            }
        ));

        return (MapDevolucionToDto(devolucion, venta.NumeroVenta, emailUsuario), null);
    }

    // ─── Mappers ───────────────────────────────────────

    public static VentaDto MapToDto(Venta v, string sucNombre, string cajaNombre, string? clienteNombre) =>
        new(
            v.Id, v.NumeroVenta,
            v.SucursalId, sucNombre,
            v.CajaId, cajaNombre,
            v.ClienteId, clienteNombre,
            v.Subtotal, v.Descuento, v.Impuestos, v.Total,
            v.Estado.ToString(), v.MetodoPago.ToString(),
            v.MontoPagado, v.Cambio,
            v.Observaciones, v.FechaVenta,
            v.Detalles.Select(d =>
            {
                var margen = d.PrecioUnitario > 0
                    ? Math.Round((d.PrecioUnitario - d.CostoUnitario) / d.PrecioUnitario * 100, 2)
                    : 0;
                return new DetalleVentaDto(
                    d.Id, d.ProductoId, d.NombreProducto,
                    d.Cantidad, d.PrecioUnitario, d.CostoUnitario,
                    d.Descuento, d.PorcentajeImpuesto, d.MontoImpuesto,
                    d.Subtotal, margen);
            }).ToList(),
            v.RequiereFacturaElectronica
        );

    public static DevolucionVentaDto MapDevolucionToDto(
        DevolucionVenta d, string numeroVenta, string? autorizadoPor) =>
        new(
            d.Id,
            d.VentaId,
            numeroVenta,
            d.NumeroDevolucion,
            d.Motivo,
            d.TotalDevuelto,
            d.FechaDevolucion,
            autorizadoPor,
            d.Detalles.Select(dd => new DetalleDevolucionDto(
                dd.Id,
                dd.ProductoId,
                dd.NombreProducto,
                dd.CantidadDevuelta,
                dd.PrecioUnitario,
                dd.CostoUnitario,
                dd.SubtotalDevuelto
            )).ToList()
        );
}
