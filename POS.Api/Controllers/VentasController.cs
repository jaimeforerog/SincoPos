using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Validators;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VentasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly Marten.IDocumentSession _session;
    private readonly PrecioService _precioService;
    private readonly CosteoService _costeoService;
    private readonly ILogger<VentasController> _logger;
    private readonly IActivityLogService _activityLogService;

    public VentasController(
        AppDbContext context,
        Marten.IDocumentSession session,
        PrecioService precioService,
        CosteoService costeoService,
        ILogger<VentasController> logger,
        IActivityLogService activityLogService)
    {
        _context = context;
        _session = session;
        _precioService = precioService;
        _costeoService = costeoService;
        _logger = logger;
        _activityLogService = activityLogService;
    }

    /// <summary>
    /// Crear una venta completa.
    /// Valida caja, resuelve precios, consume inventario via ES, y registra la venta.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Cajero")]
    public async Task<ActionResult<VentaDto>> CrearVenta([FromBody] CrearVentaDto dto)
    {
        // Validar
        var validator = new CrearVentaValidator();
        var validResult = await validator.ValidateAsync(dto);
        if (!validResult.IsValid)
            return BadRequest(validResult.Errors.Select(e => e.ErrorMessage));

        // Verificar caja abierta
        var caja = await _context.Cajas
            .FirstOrDefaultAsync(c => c.Id == dto.CajaId && c.SucursalId == dto.SucursalId);
        if (caja == null)
            return BadRequest("Caja no encontrada en esta sucursal.");
        if (caja.Estado != EstadoCaja.Abierta)
            return BadRequest("La caja no esta abierta.");

        // Obtener sucursal para metodo de costeo
        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
            return BadRequest("Sucursal no encontrada.");

        // Verificar cliente (si aplica)
        string? nombreCliente = null;
        if (dto.ClienteId.HasValue)
        {
            var cliente = await _context.Terceros.FindAsync(dto.ClienteId.Value);
            if (cliente == null) return BadRequest("Cliente no encontrado.");
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

        // Procesar cada linea
        var detalles = new List<DetalleVenta>();
        decimal subtotal = 0;
        decimal descuentoTotal = 0;
        decimal totalImpuestos = 0;

        foreach (var linea in dto.Lineas)
        {
            // Obtener producto con su impuesto
            var producto = await _context.Productos
                .Include(p => p.Impuesto)
                .FirstOrDefaultAsync(p => p.Id == linea.ProductoId);

            if (producto == null)
                return BadRequest($"Producto {linea.ProductoId} no encontrado.");
            if (!producto.Activo)
                return BadRequest($"Producto {producto.Nombre} esta inactivo.");

            // Verificar stock
            var stock = await _context.Stock
                .FirstOrDefaultAsync(s => s.ProductoId == linea.ProductoId
                    && s.SucursalId == dto.SucursalId);
            if (stock == null || stock.Cantidad < linea.Cantidad)
                return BadRequest($"Stock insuficiente para {producto.Nombre}. " +
                    $"Disponible: {stock?.Cantidad ?? 0}, Solicitado: {linea.Cantidad}");

            // Resolver precio
            decimal precioUnitario;
            if (linea.PrecioUnitario.HasValue)
            {
                // Precio manual — validar que no este por debajo del minimo
                var (valido, error) = await _precioService.ValidarPrecio(
                    linea.ProductoId, dto.SucursalId, linea.PrecioUnitario.Value);
                if (!valido) return BadRequest(error);
                precioUnitario = linea.PrecioUnitario.Value;
            }
            else
            {
                // Resolver automaticamente
                var precio = await _precioService.ResolverPrecio(linea.ProductoId, dto.SucursalId);
                precioUnitario = precio.PrecioVenta;
            }

            // Calculo de impuestos
            decimal porcentajeImpuesto = producto.Impuesto?.Porcentaje ?? 0;
            decimal montoImpuesto = precioUnitario * linea.Cantidad * porcentajeImpuesto;
            totalImpuestos += montoImpuesto;

            // Consumir inventario via Event Sourcing
            var streamId = InventarioAggregate.GenerarStreamId(linea.ProductoId, dto.SucursalId);
            var aggregate = await _session.Events.AggregateStreamAsync<InventarioAggregate>(streamId);
            if (aggregate == null)
                return BadRequest($"No hay registro de inventario para {producto.Nombre}.");

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
            return BadRequest($"Monto pagado ({dto.MontoPagado.Value}) es menor al total ({total}).");

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

        return Ok(MapToDto(venta, sucursal.Nombre, caja.Nombre, nombreCliente));
    }

    /// <summary>
    /// Obtener detalle de una venta por ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VentaDto>> ObtenerVenta(int id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Sucursal)
            .Include(v => v.Caja)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return NotFound();

        return Ok(MapToDto(venta, venta.Sucursal.Nombre, venta.Caja.Nombre, venta.Cliente?.Nombre));
    }

    /// <summary>
    /// Listar ventas con filtros.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<VentaDto>>> ListarVentas(
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? cajaId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] EstadoVenta? estado = null,
        [FromQuery] int limite = 50)
    {
        var query = _context.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Sucursal)
            .Include(v => v.Caja)
            .Include(v => v.Cliente)
            .AsQueryable();

        if (sucursalId.HasValue) query = query.Where(v => v.SucursalId == sucursalId.Value);
        if (cajaId.HasValue) query = query.Where(v => v.CajaId == cajaId.Value);
        if (desde.HasValue) query = query.Where(v => v.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(v => v.FechaVenta <= hasta.Value);
        if (estado.HasValue) query = query.Where(v => v.Estado == estado.Value);

        var ventas = await query
            .OrderByDescending(v => v.FechaVenta)
            .Take(limite)
            .ToListAsync();

        return Ok(ventas.Select(v =>
            MapToDto(v, v.Sucursal.Nombre, v.Caja.Nombre, v.Cliente?.Nombre)));
    }

    /// <summary>
    /// Anular una venta. Revierte el stock via Event Sourcing.
    /// </summary>
    [HttpPost("{id:int}/anular")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> AnularVenta(int id, [FromQuery] string? motivo = null)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return NotFound();
        if (venta.Estado == EstadoVenta.Anulada)
            return BadRequest("La venta ya esta anulada.");

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

        return Ok(new { mensaje = $"Venta {venta.NumeroVenta} anulada", stockRevertido = true });
    }

    /// <summary>
    /// Resumen de ventas por periodo/sucursal.
    /// </summary>
    [HttpGet("resumen")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<ResumenVentaDto>> ObtenerResumen(
        [FromQuery] int? sucursalId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        var query = _context.Ventas
            .Include(v => v.Detalles)
            .Where(v => v.Estado == EstadoVenta.Completada)
            .AsQueryable();

        if (sucursalId.HasValue) query = query.Where(v => v.SucursalId == sucursalId.Value);
        if (desde.HasValue) query = query.Where(v => v.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(v => v.FechaVenta <= hasta.Value);

        var ventas = await query.ToListAsync();

        var totalVentas = ventas.Count;
        var montoTotal = ventas.Sum(v => v.Total);
        var costoTotal = ventas.Sum(v => v.Detalles.Sum(d => d.CostoUnitario * d.Cantidad));
        var gananciaTotal = montoTotal - costoTotal;
        var margenPromedio = montoTotal > 0 ? Math.Round(gananciaTotal / montoTotal * 100, 2) : 0;

        return Ok(new ResumenVentaDto(totalVentas, montoTotal, costoTotal, gananciaTotal, margenPromedio));
    }

    /// <summary>
    /// Crear devolución parcial de productos de una venta.
    /// Permite devolver solo algunos productos sin anular toda la venta.
    /// </summary>
    [HttpPost("{ventaId:int}/devolucion-parcial")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<DevolucionVentaDto>> CrearDevolucionParcial(
        int ventaId,
        [FromBody] CrearDevolucionParcialDto dto)
    {
        // Validar DTO
        var validator = new CrearDevolucionParcialValidator();
        var validResult = await validator.ValidateAsync(dto);
        if (!validResult.IsValid)
            return BadRequest(validResult.Errors.Select(e => e.ErrorMessage));

        // Cargar venta con detalles
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta == null)
            return NotFound("Venta no encontrada.");

        // Validación: Solo ventas completadas pueden tener devoluciones
        if (venta.Estado != EstadoVenta.Completada)
            return BadRequest("Solo se pueden hacer devoluciones de ventas completadas.");

        // Validación: Límite de tiempo (30 días)
        var diasTranscurridos = (DateTime.UtcNow - venta.FechaVenta).TotalDays;
        if (diasTranscurridos > 30)
            return BadRequest($"La venta tiene {Math.Floor(diasTranscurridos)} días. Solo se permiten devoluciones dentro de 30 días.");

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
                return BadRequest($"El producto {linea.ProductoId} no está en la venta original.");

            // Calcular cantidad ya devuelta de este producto
            var cantidadYaDevuelta = devolucionesAnteriores
                .SelectMany(d => d.Detalles)
                .Where(dd => dd.ProductoId == linea.ProductoId)
                .Sum(dd => dd.CantidadDevuelta);

            // Validar que no se exceda la cantidad vendida
            var cantidadDisponibleParaDevolver = detalleOriginal.Cantidad - cantidadYaDevuelta;
            if (linea.Cantidad > cantidadDisponibleParaDevolver)
                return BadRequest(
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
            return BadRequest("Sucursal no encontrada.");

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
                    detalleDevolucion.CostoUnitario,  // Costo original de la venta
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

        // Obtener usuario actual (si está disponible)
        var emailUsuario = User.FindFirst("email")?.Value ?? User.Identity?.Name;
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
        await _session.SaveChangesAsync();  // Marten (Event Sourcing)
        await _context.SaveChangesAsync();   // EF Core

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

        return Ok(MapDevolucionToDto(devolucion, venta.NumeroVenta, emailUsuario));
    }

    /// <summary>
    /// Obtener todas las devoluciones de una venta específica.
    /// </summary>
    [HttpGet("{ventaId:int}/devoluciones")]
    public async Task<ActionResult<List<DevolucionVentaDto>>> ObtenerDevolucionesPorVenta(int ventaId)
    {
        var venta = await _context.Ventas.FindAsync(ventaId);
        if (venta == null)
            return NotFound("Venta no encontrada.");

        var devoluciones = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Where(d => d.VentaId == ventaId)
            .OrderByDescending(d => d.FechaDevolucion)
            .ToListAsync();

        var resultado = new List<DevolucionVentaDto>();
        foreach (var devolucion in devoluciones)
        {
            string? autorizadoPor = null;
            if (devolucion.AutorizadoPorUsuarioId.HasValue)
            {
                var usuario = await _context.Usuarios.FindAsync(devolucion.AutorizadoPorUsuarioId.Value);
                autorizadoPor = usuario?.Email;
            }

            resultado.Add(MapDevolucionToDto(devolucion, venta.NumeroVenta, autorizadoPor));
        }

        return Ok(resultado);
    }

    /// <summary>
    /// Obtener detalle de una devolución específica.
    /// </summary>
    [HttpGet("devoluciones/{devolucionId:int}")]
    public async Task<ActionResult<DevolucionVentaDto>> ObtenerDevolucion(int devolucionId)
    {
        var devolucion = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
            .Include(d => d.Venta)
            .FirstOrDefaultAsync(d => d.Id == devolucionId);

        if (devolucion == null)
            return NotFound("Devolución no encontrada.");

        string? autorizadoPor = null;
        if (devolucion.AutorizadoPorUsuarioId.HasValue)
        {
            var usuario = await _context.Usuarios.FindAsync(devolucion.AutorizadoPorUsuarioId.Value);
            autorizadoPor = usuario?.Email;
        }

        return Ok(MapDevolucionToDto(devolucion, devolucion.Venta.NumeroVenta, autorizadoPor));
    }

    // ─── Mappers ───────────────────────────────────────

    private static VentaDto MapToDto(Venta v, string sucNombre, string cajaNombre, string? clienteNombre) =>
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
            }).ToList()
        );

    private static DevolucionVentaDto MapDevolucionToDto(
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
