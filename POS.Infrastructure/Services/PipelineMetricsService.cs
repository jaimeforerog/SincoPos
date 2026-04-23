using POS.Application.Services;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace POS.Infrastructure.Services;

/// <summary>
/// Buffer circular en memoria de las últimas N ejecuciones del pipeline.
/// Singleton — los datos se pierden al reiniciar el proceso (por diseño,
/// es monitoreo en tiempo real, no histórico permanente).
/// Además emite métricas OTLP via System.Diagnostics.Metrics (integración OpenTelemetry).
/// </summary>
public sealed class PipelineMetricsService : IPipelineMetricsService
{
    public const string MeterName = "sincopos.pipeline";

    private static readonly Meter _meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> _ventasProcesadas =
        _meter.CreateCounter<long>("pos.ventas.procesadas", unit: "ventas",
            description: "Total de ventas procesadas por el pipeline");
    private static readonly Histogram<double> _ventasDuracion =
        _meter.CreateHistogram<double>("pos.ventas.duracion_ms", unit: "ms",
            description: "Duración del pipeline de venta end-to-end");

    private const int MaxBuffer = 100;
    private readonly ConcurrentQueue<EjecucionResumenDto> _buffer = new();

    public void Registrar(OrchestratorResult result)
    {
        // Emitir métricas OTLP (exportadas a Grafana/Jaeger/Azure Monitor via AddOtlpExporter)
        _ventasProcesadas.Add(1, new KeyValuePair<string, object?>("exitoso", result.Exitoso));
        _ventasDuracion.Record(result.TotalMs, new KeyValuePair<string, object?>("exitoso", result.Exitoso));

        var entry = new EjecucionResumenDto(
            DateTime.UtcNow,
            result.TotalMs,
            result.Exitoso,
            result.Error,
            result.Pipeline);

        _buffer.Enqueue(entry);

        // Mantener solo las últimas MaxBuffer
        while (_buffer.Count > MaxBuffer)
            _buffer.TryDequeue(out _);
    }

    public PipelineMetricsSummary ObtenerResumen()
    {
        var all = _buffer.ToList();
        if (all.Count == 0)
            return new PipelineMetricsSummary(0, 0, 0, 0, 0, 0, 0, []);

        var exitosas = all.Count(e => e.Exitoso);
        var latencias = all.Select(e => e.TotalMs).ToList();

        // Métricas por paso
        var pasos = all
            .SelectMany(e => e.Pasos)
            .GroupBy(p => p.Nombre)
            .Select(g => new PasoMetricaDto(
                g.Key,
                (long)g.Average(p => p.Ms),
                g.Max(p => p.Ms),
                g.Count(p => p.Exitoso) * 100.0 / g.Count()))
            .ToList();

        return new PipelineMetricsSummary(
            all.Count,
            exitosas,
            all.Count - exitosas,
            exitosas * 100.0 / all.Count,
            (long)latencias.Average(),
            latencias.Max(),
            latencias.Min(),
            pasos);
    }

    public List<EjecucionResumenDto> ObtenerRecientes(int take = 20) =>
        _buffer.Reverse().Take(take).ToList();
}
