using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementación del Motor de Impuestos Universal.
///
/// Orden de cálculo (cascada):
///   1. Base imponible = PrecioUnitario × Cantidad
///   2. IVA (si aplica) — acumulativo sobre la base
///   3. INC (si aplica) — monofásico, no acumulable con IVA
///   4. Impuesto Saludable — ultraprocesados o bebidas azucaradas
///   5. Impuesto a la Bolsa — valor fijo × cantidad
///   6. Retenciones — evaluación de matriz Vendedor/Comprador + umbral UVT
///   7. Flag REQUIRES_ELECTRONIC_INVOICE — total > 5 UVT
///
/// Diseño: inyección de dependencias, sin estado interno → idempotente.
/// </summary>
public sealed class TaxEngine : ITaxEngine
{

    public TaxResult Calcular(TaxRequest req)
    {
        var impuestosAplicados = new List<ImpuestoAplicado>();
        var retenciones = new List<RetencionAplicada>();

        decimal baseImponible = Math.Round(req.PrecioUnitario * req.Cantidad, 2);
        decimal totalImpuestos = 0m;

        // ── 1. IVA ─────────────────────────────────────────────────────────────
        if (req.Impuesto is { Tipo: TipoImpuesto.IVA, AplicaSobreBase: true })
        {
            var monto = Math.Round(baseImponible * req.Impuesto.Porcentaje, 2);
            impuestosAplicados.Add(new ImpuestoAplicado(
                req.Impuesto.Nombre,
                TipoImpuesto.IVA,
                req.Impuesto.Porcentaje,
                ValorFijo: null,
                monto,
                req.Impuesto.CodigoCuentaContable));
            totalImpuestos += monto;
        }

        // ── 2. INC (monofásico) ────────────────────────────────────────────────
        // El INC no se acumula con IVA. Si el producto tiene INC, no tiene IVA.
        if (req.Impuesto is { Tipo: TipoImpuesto.INC, AplicaSobreBase: false })
        {
            var monto = Math.Round(baseImponible * req.Impuesto.Porcentaje, 2);
            impuestosAplicados.Add(new ImpuestoAplicado(
                req.Impuesto.Nombre,
                TipoImpuesto.INC,
                req.Impuesto.Porcentaje,
                ValorFijo: null,
                monto,
                req.Impuesto.CodigoCuentaContable));
            totalImpuestos += monto;
        }

        // ── 3. Impuesto Saludable ──────────────────────────────────────────────

        // 3a. Ultraprocesados — porcentaje sobre base (tarifa Ley 2277/2022)
        if (req.EsAlimentoUltraprocesado && req.Impuesto is { Tipo: TipoImpuesto.Saludable })
        {
            var monto = Math.Round(baseImponible * req.Impuesto.Porcentaje, 2);
            impuestosAplicados.Add(new ImpuestoAplicado(
                req.Impuesto.Nombre,
                TipoImpuesto.Saludable,
                req.Impuesto.Porcentaje,
                ValorFijo: null,
                monto,
                req.Impuesto.CodigoCuentaContable));
            totalImpuestos += monto;
        }

        // 3b. Bebidas azucaradas — tabla de tramos g/100ml × volumen
        if (req.GramosAzucarPor100ml.HasValue && req.GramosAzucarPor100ml.Value > 0
            && req.TramosBebidasAzucaradas.Count > 0)
        {
            var tarifa = ObtenerTarifaBebidaAzucarada(req.GramosAzucarPor100ml.Value, req.TramosBebidasAzucaradas);
            // Se asume que PrecioUnitario equivale a 100ml para el cálculo;
            // en producción el volumen vendría del sku del producto.
            var monto = Math.Round(tarifa * req.Cantidad, 2);
            impuestosAplicados.Add(new ImpuestoAplicado(
                $"Bebida Azucarada ({req.GramosAzucarPor100ml:N1} g/100ml)",
                TipoImpuesto.Saludable,
                Porcentaje: 0m,
                ValorFijo: tarifa,
                monto,
                CuentaContable: "2425"));
            totalImpuestos += monto;
        }

        // ── 4. Impuesto a la Bolsa (valor fijo × unidad) ───────────────────────
        if (req.Impuesto is { Tipo: TipoImpuesto.Bolsa } && req.Impuesto.ValorFijo.HasValue)
        {
            var monto = Math.Round(req.Impuesto.ValorFijo.Value * req.Cantidad, 2);
            impuestosAplicados.Add(new ImpuestoAplicado(
                req.Impuesto.Nombre,
                TipoImpuesto.Bolsa,
                Porcentaje: 0m,
                req.Impuesto.ValorFijo,
                monto,
                req.Impuesto.CodigoCuentaContable));
            totalImpuestos += monto;
        }

        // ── 5. Retenciones ─────────────────────────────────────────────────────
        decimal totalRetenciones = 0m;

        // Régimen Simple está exento de retención en la fuente de renta
        if (req.PerfilVendedor != "REGIMEN_SIMPLE")
        {
            foreach (var regla in req.ReglasRetencion.Where(r => r.Activo))
            {
                if (!AplicaRetencion(regla, req, baseImponible))
                    continue;

                var monto = Math.Round(baseImponible * regla.Porcentaje, 2);
                retenciones.Add(new RetencionAplicada(
                    regla.Nombre,
                    regla.Tipo,
                    regla.Porcentaje,
                    monto,
                    regla.CodigoCuentaContable));
                totalRetenciones += monto;
            }
        }

        // ── 6. Totales ─────────────────────────────────────────────────────────
        decimal totalNeto = baseImponible + totalImpuestos - totalRetenciones;

        // ── 7. Flag factura electrónica (> 5 UVT) ─────────────────────────────
        bool requiereFactura = totalNeto > (5m * req.ValorUVT);

        return new TaxResult(
            baseImponible,
            impuestosAplicados,
            retenciones,
            Math.Round(totalImpuestos, 2),
            Math.Round(totalRetenciones, 2),
            Math.Round(totalNeto, 2),
            requiereFactura
        );
    }

    // ── Helpers privados ───────────────────────────────────────────────────────

    /// <summary>
    /// Evalúa si una regla de retención aplica para el contexto de la transacción.
    /// Criterios: cruce de perfiles + umbral UVT + municipio.
    /// </summary>
    private static bool AplicaRetencion(RetencionRegla regla, TaxRequest req, decimal baseImponible)
    {
        // 1. Cruce de perfiles Vendedor/Comprador
        if (!regla.PerfilVendedor.Equals(req.PerfilVendedor, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!regla.PerfilComprador.Equals(req.PerfilComprador, StringComparison.OrdinalIgnoreCase))
            return false;

        // 2. Validar umbral mínimo en UVT
        var baseMinPesos = regla.BaseMinUVT * req.ValorUVT;
        if (baseImponible < baseMinPesos)
            return false;

        // 3. ReteFuente: filtrar por concepto de retención del producto
        if (regla.Tipo == TipoRetencion.ReteFuente &&
            regla.ConceptoRetencionId.HasValue &&
            regla.ConceptoRetencionId != req.ConceptoRetencionId)
            return false;

        // 4. ReteICA: validar coincidencia de municipio
        if (regla.Tipo == TipoRetencion.ReteICA)
        {
            if (string.IsNullOrEmpty(regla.CodigoMunicipio))
                return true; // Sin municipio = aplica a todos
            if (!regla.CodigoMunicipio.Equals(req.CodigoMunicipio, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static decimal ObtenerTarifaBebidaAzucarada(
        decimal gramosPor100ml, List<TramoBebidasAzucaradas> tramos)
    {
        foreach (var t in tramos.OrderBy(t => t.MaxGramosPor100ml ?? decimal.MaxValue))
        {
            if (!t.MaxGramosPor100ml.HasValue || gramosPor100ml <= t.MaxGramosPor100ml)
                return t.ValorPor100ml;
        }
        return tramos.MaxBy(t => t.MaxGramosPor100ml ?? decimal.MaxValue)!.ValorPor100ml;
    }
}
