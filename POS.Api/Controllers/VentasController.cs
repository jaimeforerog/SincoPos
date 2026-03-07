using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Validators;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class VentasController : ControllerBase
{
    private readonly IVentaService _ventaService;
    private readonly AppDbContext _context;
    private readonly ILogger<VentasController> _logger;

    public VentasController(
        IVentaService ventaService,
        AppDbContext context,
        ILogger<VentasController> logger)
    {
        _ventaService = ventaService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Crear una venta completa.
    /// Valida caja abierta, resuelve precios (cascada sucursal → producto → margen),
    /// aplica TaxEngine, consume stock via Event Sourcing y registra la venta.
    /// </summary>
    /// <remarks>
    /// El campo <c>precioUnitario</c> en cada línea es opcional. Si se omite (null),
    /// el sistema resuelve el precio configurado para la sucursal.<br/>
    /// <c>metodoPago</c>: 0 = Efectivo, 1 = Tarjeta, 2 = Transferencia.<br/>
    /// <c>montoPagado</c> debe ser &gt;= total de la venta.
    /// </remarks>
    /// <response code="200">Venta creada exitosamente con su VentaDto completo.</response>
    /// <response code="400">Caja cerrada, stock insuficiente, precio inválido u otro error de negocio.</response>
    [HttpPost]
    [Authorize(Policy = "Cajero")]
    [ProducesResponseType(typeof(VentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VentaDto>> CrearVenta([FromBody] CrearVentaDto dto)
    {
        var validResult = await new CrearVentaValidator().ValidateAsync(dto);
        if (!validResult.IsValid)
            return BadRequest(validResult.Errors.Select(e => e.ErrorMessage));

        var (venta, error) = await _ventaService.CrearVentaAsync(dto);
        return error != null ? BadRequest(new { error }) : Ok(venta);
    }

    /// <summary>
    /// Obtener detalle de una venta por ID.
    /// </summary>
    /// <response code="200">VentaDto con líneas de detalle.</response>
    /// <response code="404">Venta no encontrada.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VentaDto>> ObtenerVenta(int id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Sucursal)
            .Include(v => v.Caja)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (venta == null) return NotFound();

        return Ok(VentaService.MapToDto(venta, venta.Sucursal.Nombre, venta.Caja.Nombre, venta.Cliente?.Nombre));
    }

    /// <summary>
    /// Listar ventas con filtros opcionales.
    /// </summary>
    /// <param name="sucursalId">Filtrar por sucursal. Null = todas las sucursales.</param>
    /// <param name="cajaId">Filtrar por caja específica.</param>
    /// <param name="desde">Fecha de inicio del período (UTC).</param>
    /// <param name="hasta">Fecha fin del período (UTC).</param>
    /// <param name="estado">0 = Completada, 1 = Anulada, 2 = Devuelta. Null = todos.</param>
    /// <param name="limite">Máximo de resultados. Default 50.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<VentaDto>), StatusCodes.Status200OK)]
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
            VentaService.MapToDto(v, v.Sucursal.Nombre, v.Caja.Nombre, v.Cliente?.Nombre)));
    }

    /// <summary>
    /// Anular una venta. Revierte el stock consumido via Event Sourcing y descuenta de la caja.
    /// </summary>
    /// <param name="id">ID de la venta a anular.</param>
    /// <param name="motivo">Motivo opcional de la anulación (queda en el log de auditoría).</param>
    /// <response code="200">Venta anulada. Stock revertido.</response>
    /// <response code="404">Venta no encontrada.</response>
    /// <response code="400">Venta ya anulada u otro error de negocio.</response>
    [HttpPost("{id:int}/anular")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AnularVenta(int id, [FromQuery] string? motivo = null)
    {
        var (success, error) = await _ventaService.AnularVentaAsync(id, motivo);
        if (!success)
        {
            if (error == "NOT_FOUND") return NotFound();
            return BadRequest(new { error });
        }
        return Ok(new { mensaje = "Venta anulada", stockRevertido = true });
    }

    /// <summary>
    /// Resumen agregado de ventas: totales, costo, ganancia y margen por período/sucursal.
    /// Solo incluye ventas en estado Completada.
    /// </summary>
    [HttpGet("resumen")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(ResumenVentaDto), StatusCodes.Status200OK)]
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
    /// Reintegra el stock vía Event Sourcing y ajusta el monto de la caja.
    /// Se permiten múltiples devoluciones sobre la misma venta hasta agotar las cantidades originales.
    /// </summary>
    /// <response code="200">Devolución registrada con DevolucionVentaDto completo.</response>
    /// <response code="404">Venta no encontrada.</response>
    /// <response code="400">Cantidades superan lo vendido, línea ya devuelta u otro error.</response>
    [HttpPost("{ventaId:int}/devolucion-parcial")]
    [Authorize(Policy = "Supervisor")]
    [ProducesResponseType(typeof(DevolucionVentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DevolucionVentaDto>> CrearDevolucionParcial(
        int ventaId,
        [FromBody] CrearDevolucionParcialDto dto)
    {
        var validResult = await new CrearDevolucionParcialValidator().ValidateAsync(dto);
        if (!validResult.IsValid)
            return BadRequest(validResult.Errors.Select(e => e.ErrorMessage));

        var emailUsuario = User.FindFirst("email")?.Value ?? User.Identity?.Name;

        var (devolucion, error) = await _ventaService.CrearDevolucionParcialAsync(ventaId, dto, emailUsuario);
        if (devolucion == null)
        {
            if (error == "NOT_FOUND") return NotFound("Venta no encontrada.");
            return BadRequest(new { error });
        }
        return Ok(devolucion);
    }

    /// <summary>
    /// Obtener todas las devoluciones de una venta específica.
    /// </summary>
    /// <response code="200">Lista de devoluciones (puede ser vacía si no hay devoluciones).</response>
    /// <response code="404">Venta no encontrada.</response>
    [HttpGet("{ventaId:int}/devoluciones")]
    [ProducesResponseType(typeof(List<DevolucionVentaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

            resultado.Add(VentaService.MapDevolucionToDto(devolucion, venta.NumeroVenta, autorizadoPor));
        }

        return Ok(resultado);
    }

    /// <summary>
    /// Obtener detalle de una devolución específica.
    /// </summary>
    /// <response code="200">DevolucionVentaDto con líneas y datos de autorización.</response>
    /// <response code="404">Devolución no encontrada.</response>
    [HttpGet("devoluciones/{devolucionId:int}")]
    [ProducesResponseType(typeof(DevolucionVentaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        return Ok(VentaService.MapDevolucionToDto(devolucion, devolucion.Venta.NumeroVenta, autorizadoPor));
    }
}
