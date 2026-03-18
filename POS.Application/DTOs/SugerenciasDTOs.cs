namespace POS.Application.DTOs;

// ─── Capa 10 — Explicabilidad ───────────────────────────────────────────────

/// <summary>
/// Toda acción automática sugerida por el sistema incluye:
/// - Reason: "por qué" la sugerencia es relevante ahora
/// - DataSource: fuente de datos que respalda la recomendación
/// - Confidence: 0–1 según cantidad de datos históricos disponibles
/// - CanOverride: el usuario siempre puede rechazar la sugerencia
/// </summary>
public record AutomaticActionDto(
    string   TipoAccion,        // "Reabastecimiento"
    Guid?    ProductoId,
    string   NombreProducto,
    string   Description,       // Acción concreta sugerida
    string   Reason,            // "Stock a X uds/día se agota en N días"
    string   DataSource,        // "Basado en N ventas en M días de actividad"
    double   Confidence,        // 0.0 – 1.0
    bool     CanOverride,
    decimal? CantidadSugerida,
    decimal? DiasRestantes      // Días de stock con el ritmo actual
);
