using System.Diagnostics;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

/// <summary>
/// Capa 15 — Orquestación contextual.
///
/// Pipeline de venta auditable con trazabilidad por paso:
///   1. PreValidación  — caja + lineas no vacías
///   2. ProcesarVenta  — VentaService (precios, impuestos, stock, event sourcing, ERP outbox, notificaciones)
///   3. RegistrarTrace — métricas en PipelineMetricsService
///
/// El paso 2 ya incluye internamente: EthicalGuard (Capa 12), TaxEngine, Marten events,
/// ERP outbox (Capa 6) y notificaciones SignalR (Capa 7).
/// El orquestador agrega la capa de visibilidad y trazabilidad sobre ese proceso.
///
/// Modo degradado: si el paso de ProcesarVenta falla, el trace se registra igual
/// con el error, permitiendo auditoría post-mortem.
/// </summary>
public sealed class SaleOrchestrator : ISaleOrchestrator
{
    private readonly IVentaService _ventaService;
    private readonly IPipelineMetricsService _metrics;

    public SaleOrchestrator(
        IVentaService ventaService,
        IPipelineMetricsService metrics)
    {
        _ventaService = ventaService;
        _metrics      = metrics;
    }

    public async Task<OrchestratorResult> ProcesarVentaAsync(CrearVentaDto dto)
    {
        var totalSw = Stopwatch.StartNew();
        var pasos   = new List<PipelineStepDto>();

        // ── Paso 1: PreValidación ─────────────────────────────────────────
        var paso1 = await EjecutarPaso("PreValidacion", () =>
        {
            if (dto.Lineas == null || dto.Lineas.Count == 0)
                throw new InvalidOperationException("La venta debe tener al menos una línea de producto.");
            if (dto.SucursalId <= 0)
                throw new InvalidOperationException("SucursalId inválido.");
            if (dto.CajaId <= 0)
                throw new InvalidOperationException("CajaId inválido.");
            return Task.CompletedTask;
        });
        pasos.Add(paso1);

        if (!paso1.Exitoso)
        {
            totalSw.Stop();
            var failResult = new OrchestratorResult(null, pasos, totalSw.ElapsedMilliseconds, false, paso1.Error);
            _metrics.Registrar(failResult);
            return failResult;
        }

        // ── Paso 2: Procesamiento principal ──────────────────────────────
        VentaDto? ventaDto = null;
        string?   ventaError = null;

        var paso2 = await EjecutarPaso("ProcesarVenta", async () =>
        {
            var (venta, error) = await _ventaService.CrearVentaAsync(dto);
            ventaDto   = venta;
            ventaError = error;

            if (error != null)
                throw new InvalidOperationException(error);
        });
        pasos.Add(paso2);

        totalSw.Stop();

        var exitoso = paso2.Exitoso && ventaDto != null;
        var error   = exitoso ? null : (ventaError ?? paso2.Error);

        var result = new OrchestratorResult(
            ventaDto, pasos, totalSw.ElapsedMilliseconds, exitoso, error);

        // ── Paso 3: Registro de métricas (fire-and-forget, nunca falla el pipeline) ──
        try { _metrics.Registrar(result); } catch { /* nunca propagar */ }

        return result;
    }

    // ── Helper ─────────────────────────────────────────────────────────────

    private static async Task<PipelineStepDto> EjecutarPaso(string nombre, Func<Task> accion)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await accion();
            sw.Stop();
            return new PipelineStepDto(nombre, sw.ElapsedMilliseconds, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PipelineStepDto(nombre, sw.ElapsedMilliseconds, false, ex.Message);
        }
    }
}
