using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Api.Controllers;

/// <summary>
/// Capa 15 — Orquestación contextual.
/// Procesa ventas a través del pipeline auditable y expone métricas de rendimiento.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class OrquestadorController : ControllerBase
{
    private readonly ISaleOrchestrator _orchestrator;
    private readonly IPipelineMetricsService _metrics;

    public OrquestadorController(
        ISaleOrchestrator orchestrator,
        IPipelineMetricsService metrics)
    {
        _orchestrator = orchestrator;
        _metrics      = metrics;
    }

    /// <summary>
    /// Procesa una venta a través del pipeline orquestado.
    /// Retorna la venta creada junto con el trace de latencias por paso.
    /// </summary>
    [HttpPost("venta")]
    [Authorize(Roles = "cajero,supervisor,admin")]
    public async Task<IActionResult> ProcesarVenta([FromBody] CrearVentaDto dto)
    {
        var result = await _orchestrator.ProcesarVentaAsync(dto);

        if (!result.Exitoso)
        {
            // Determinar código HTTP según el tipo de error
            var statusCode = result.Error?.Contains("no encontrada") == true ||
                             result.Error?.Contains("inválido") == true
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status422UnprocessableEntity;

            return StatusCode(statusCode, new ProblemDetails
            {
                Title  = "Error en el pipeline de venta",
                Detail = result.Error,
                Status = statusCode,
                Extensions = { ["pipeline"] = result.Pipeline }
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Resumen estadístico de las últimas N ejecuciones del pipeline
    /// (en memoria — se reinicia con el servidor).
    /// </summary>
    [HttpGet("metricas")]
    [Authorize(Roles = "supervisor,admin")]
    public IActionResult GetMetricas() =>
        Ok(_metrics.ObtenerResumen());

    /// <summary>
    /// Ejecuciones recientes del pipeline con detalle por paso.
    /// </summary>
    [HttpGet("ejecuciones")]
    [Authorize(Roles = "supervisor,admin")]
    public IActionResult GetEjecuciones([FromQuery] int take = 20) =>
        Ok(_metrics.ObtenerRecientes(take));
}
