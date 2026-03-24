using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Capa 15 — Orquestación contextual.
/// Pipeline auditable que unifica la lógica de venta con trazabilidad por paso.
///
/// Métrica de éxito: Latencia total (intención → confirmación) &lt; 500ms.
/// Cada paso del pipeline es rastreable individualmente.
///
/// Modo degradado: si un paso post-persistencia falla, el sistema registra el estado
/// parcial y retorna la venta exitosa con el paso marcado como fallido en el trace.
/// </summary>
public interface ISaleOrchestrator
{
    Task<OrchestratorResult> ProcesarVentaAsync(CrearVentaDto dto);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record PipelineStepDto(
    string  Nombre,
    long    Ms,
    bool    Exitoso,
    string? Error = null
);

public record OrchestratorResult(
    VentaDto?            Venta,
    List<PipelineStepDto> Pipeline,
    long                 TotalMs,
    bool                 Exitoso,
    string?              Error
);

/// <summary>
/// Métricas agregadas de ejecuciones del pipeline (últimas N).
/// </summary>
public interface IPipelineMetricsService
{
    void Registrar(OrchestratorResult result);
    PipelineMetricsSummary ObtenerResumen();
    List<EjecucionResumenDto> ObtenerRecientes(int take = 20);
}

public record PipelineMetricsSummary(
    int    TotalEjecuciones,
    int    Exitosas,
    int    Fallidas,
    double TasaExitoPorc,
    long   LatenciaPromedioMs,
    long   LatenciaMaximaMs,
    long   LatenciaMinimaMs,
    List<PasoMetricaDto> Pasos
);

public record PasoMetricaDto(
    string Nombre,
    long   PromedioMs,
    long   MaxMs,
    double TasaExitoPorc
);

public record EjecucionResumenDto(
    DateTime             Timestamp,
    long                 TotalMs,
    bool                 Exitoso,
    string?              Error,
    List<PipelineStepDto> Pasos
);
