using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

// ──────────────────────────────────────────────────────────────────────────────
//  Interfaz del Motor de Impuestos Universal (Tax Engine)
//  Desacoplada para facilitar testing, extensión y actualización año a año.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Contrato del Tax Engine. Recibe el contexto fiscal de una línea de venta
/// y retorna el desglose completo de impuestos y retenciones.
/// </summary>
public interface ITaxEngine
{
    /// <summary>
    /// Calcula impuestos y retenciones para una línea de producto.
    /// Idempotente: mismas entradas → mismas salidas.
    /// </summary>
    TaxResult Calcular(TaxRequest request);
}

// ──────────────────────────────────────────────────────────────────────────────
//  DTOs de entrada / salida del motor
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Contexto fiscal completo de una línea de venta.
/// Corresponde al "header" + "item" del JSON de especificación.
/// </summary>
public record TaxRequest(
    // ── Ítem ──────────────────────────────────────────────────────────────────
    Guid ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario,

    // ── Perfil fiscal del producto ─────────────────────────────────────────────
    Impuesto? Impuesto,               // null = exento
    bool EsAlimentoUltraprocesado,
    decimal? GramosAzucarPor100ml,

    // ── Perfiles tributarios ───────────────────────────────────────────────────
    string PerfilVendedor,            // "REGIMEN_ORDINARIO" | "REGIMEN_SIMPLE"
    string PerfilComprador,           // "GRAN_CONTRIBUYENTE" | "REGIMEN_COMUN" | ...
    string CodigoMunicipio,           // Código DANE (para ReteICA)

    // ── Parámetros DIAN ────────────────────────────────────────────────────────
    decimal ValorUVT,                 // Valor UVT vigente (ej. 47065 en 2026)
    List<RetencionRegla> ReglasRetencion  // Catálogo de reglas activas de la sucursal
);

/// <summary>
/// Resultado del motor: desglose completo listo para guardar y mostrar.
/// </summary>
public record TaxResult(
    decimal BaseImponible,
    List<ImpuestoAplicado> Impuestos,
    List<RetencionAplicada> Retenciones,
    decimal TotalImpuestos,
    decimal TotalRetenciones,
    /// <summary>(BaseImponible + TotalImpuestos) - TotalRetenciones</summary>
    decimal TotalNeto,
    /// <summary>true si Total > 5 UVT. Exige factura electrónica (DIAN).</summary>
    bool RequiereFacturaElectronica
);

public record ImpuestoAplicado(
    string Nombre,
    TipoImpuesto Tipo,
    decimal Porcentaje,
    decimal? ValorFijo,
    decimal Monto,
    string? CuentaContable              // GL Mapping
);

public record RetencionAplicada(
    string Nombre,
    TipoRetencion Tipo,
    decimal Porcentaje,
    decimal Monto,
    string? CuentaContable
);
