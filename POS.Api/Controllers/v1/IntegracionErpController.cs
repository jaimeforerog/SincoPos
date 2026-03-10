using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers.v1;

/// <summary>
/// Exposición de endpoints para control manual o consulta
/// del estado del Outbox y la integración contable hacia el ERP externo.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/integracion-erp")]
[Authorize(Policy = "Admin")]
public class IntegracionErpController : ControllerBase
{
    private readonly AppDbContext _context;

    public IntegracionErpController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Reactiva una orden de compra u otro documento descartado por el Outbox a "Pendiente" 
    /// para volver a forzar la sincronización hacia el ERP Sinco.
    /// </summary>
    [HttpPost("reintentar-outbox/{id}")]
    public async Task<IActionResult> ReintentarOutboxAsync(long id)
    {
        var mensaje = await _context.ErpOutboxMessages.FindAsync(id);
        
        if (mensaje == null)
            return NotFound(new { error = "Mensaje Outbox no encontrado" });

        if (mensaje.Estado == EstadoOutbox.Procesado)
            return BadRequest(new { error = "El mensaje ya se había procesado exitosamente." });

        mensaje.Estado = EstadoOutbox.Pendiente;
        mensaje.Intentos = 0;
        mensaje.UltimoError = null;

        if (mensaje.TipoDocumento == "CompraRecibida")
        {
            var orden = await _context.OrdenesCompra.FindAsync(mensaje.EntidadId);
            if (orden != null)
            {
                orden.SincronizadoErp = false;
                orden.ErrorSincronizacion = "Reintento forzado por administrador";
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "El mensaje ha sido encausado nuevamente al Outbox para sincronización.", outboxId = id });
    }

    /// <summary>
    /// Devuelve los últimos 50 mensajes de la bandeja de salida ERP 
    /// que hayan generado error o estén caídos.
    /// </summary>
    [HttpGet("outbox/errores")]
    public async Task<IActionResult> ObtenerErroresOutboxAsync()
    {
        var mensajes = await _context.ErpOutboxMessages
            .Where(m => m.Estado == EstadoOutbox.Error || m.Estado == EstadoOutbox.Descartado)
            .OrderByDescending(m => m.FechaCreacion)
            .Take(50)
            .Select(m => new {
                m.Id,
                m.TipoDocumento,
                m.EntidadId,
                m.FechaCreacion,
                m.FechaProcesamiento,
                m.Intentos,
                m.UltimoError,
                Estado = m.Estado.ToString()
            })
            .ToListAsync();

        return Ok(mensajes);
    }
}
