using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.IntegrationTests;

/// <summary>
/// Tests unitarios del TaxEngine. Sin BD, sin WebApplicationFactory.
/// Cubre casos borde: umbral UVT, IVA excluido, reglas de retención.
/// </summary>
public class TaxEngineUnitTests
{
    private readonly TaxEngine _engine = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Impuesto IvaImpuesto(decimal porcentaje = 0.19m) => new()
    {
        Nombre = $"IVA {porcentaje * 100:0.##}%",
        Tipo = TipoImpuesto.IVA,
        Porcentaje = porcentaje,
        AplicaSobreBase = true,
        CodigoCuentaContable = "2408"
    };

    private static RetencionRegla ReteFuenteRegla(
        decimal baseMinUVT = 4m,
        decimal porcentaje = 0.025m,
        int? conceptoId = null) => new()
    {
        Nombre = "ReteFuente General",
        Tipo = TipoRetencion.ReteFuente,
        Porcentaje = porcentaje,
        BaseMinUVT = baseMinUVT,
        PerfilVendedor = "REGIMEN_ORDINARIO",
        PerfilComprador = "REGIMEN_COMUN",
        Activo = true,
        ConceptoRetencionId = conceptoId
    };

    private static TaxRequest BaseRequest(
        decimal precioUnitario = 100_000m,
        decimal cantidad = 1m,
        Impuesto? impuesto = null,
        decimal valorUVT = 47_065m,
        List<RetencionRegla>? reglas = null) => new(
            ProductoId: Guid.NewGuid(),
            Cantidad: cantidad,
            PrecioUnitario: precioUnitario,
            Impuesto: impuesto,
            EsAlimentoUltraprocesado: false,
            GramosAzucarPor100ml: null,
            PerfilVendedor: "REGIMEN_ORDINARIO",
            PerfilComprador: "REGIMEN_COMUN",
            CodigoMunicipio: "11001",
            ConceptoRetencionId: null,
            ValorUVT: valorUVT,
            ReglasRetencion: reglas ?? new List<RetencionRegla>()
        );

    // ── IVA ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ConIva19_RetornaTotalImpuestoCorrecto()
    {
        var req = BaseRequest(precioUnitario: 100_000m, impuesto: IvaImpuesto());

        var result = _engine.Calcular(req);

        Assert.Equal(100_000m, result.BaseImponible);
        Assert.Equal(19_000m, result.TotalImpuestos);
        Assert.Single(result.Impuestos);
        Assert.Equal(TipoImpuesto.IVA, result.Impuestos[0].Tipo);
    }

    [Fact]
    public void Calcular_SinImpuesto_NoGeneraImpuesto()
    {
        // Producto exento (Impuesto = null)
        var req = BaseRequest(precioUnitario: 50_000m, impuesto: null);

        var result = _engine.Calcular(req);

        Assert.Equal(0m, result.TotalImpuestos);
        Assert.Empty(result.Impuestos);
    }

    // ── Retenciones ───────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_BaseByEncimaDeUmbralUVT_AplicaRetención()
    {
        // 5 UVT a $47.065 = $235.325. Compramos $300.000 → debe retener
        var regla = ReteFuenteRegla(baseMinUVT: 4m, porcentaje: 0.025m);
        var req = BaseRequest(precioUnitario: 300_000m, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Single(result.Retenciones);
        Assert.Equal(7_500m, result.TotalRetenciones); // 300.000 × 2.5%
    }

    [Fact]
    public void Calcular_BaseBajoUmbralUVT_NoAplicaRetención()
    {
        // Umbral 4 UVT × 47.065 = $188.260. Compramos $100.000 → no retiene
        var regla = ReteFuenteRegla(baseMinUVT: 4m, porcentaje: 0.025m);
        var req = BaseRequest(precioUnitario: 100_000m, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Empty(result.Retenciones);
        Assert.Equal(0m, result.TotalRetenciones);
    }

    [Fact]
    public void Calcular_RegimenSimple_NoAplicaRetención()
    {
        // Régimen Simple está exento de ReteFuente
        var regla = ReteFuenteRegla(baseMinUVT: 0m, porcentaje: 0.025m);
        var req = BaseRequest(precioUnitario: 1_000_000m, reglas: new List<RetencionRegla> { regla }) with
        {
            PerfilVendedor = "REGIMEN_SIMPLE"
        };

        var result = _engine.Calcular(req);

        Assert.Empty(result.Retenciones);
    }

    [Fact]
    public void Calcular_ConceptoRetencionDistinto_NoAplicaRetención()
    {
        // La regla aplica SOLO a conceptoId=5, el producto tiene conceptoId=7 → no retiene
        var regla = ReteFuenteRegla(baseMinUVT: 0m, porcentaje: 0.025m, conceptoId: 5);
        var req = BaseRequest(precioUnitario: 500_000m, reglas: new List<RetencionRegla> { regla }) with
        {
            ConceptoRetencionId = 7
        };

        var result = _engine.Calcular(req);

        Assert.Empty(result.Retenciones);
    }

    // ── Factura Electrónica ───────────────────────────────────────────────────

    [Fact]
    public void Calcular_TotalEncimaDe5UVT_RequiereFacturaElectronica()
    {
        // 5 UVT × 47.065 = $235.325. Precio $250.000 → requiere factura
        var req = BaseRequest(precioUnitario: 250_000m);

        var result = _engine.Calcular(req);

        Assert.True(result.RequiereFacturaElectronica);
    }

    [Fact]
    public void Calcular_TotalBajoDe5UVT_NoRequiereFacturaElectronica()
    {
        // Precio $10.000 → no requiere factura
        var req = BaseRequest(precioUnitario: 10_000m);

        var result = _engine.Calcular(req);

        Assert.False(result.RequiereFacturaElectronica);
    }

    // ── INC ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ConINC_NoAcumulaConIVA()
    {
        // INC es monofásico, no hay IVA en el mismo producto
        var impuesto = new Impuesto
        {
            Nombre = "INC 8%",
            Tipo = TipoImpuesto.INC,
            Porcentaje = 0.08m,
            AplicaSobreBase = false,
            CodigoCuentaContable = "2411"
        };
        var req = BaseRequest(precioUnitario: 100_000m, impuesto: impuesto);

        var result = _engine.Calcular(req);

        Assert.Single(result.Impuestos);
        Assert.Equal(TipoImpuesto.INC, result.Impuestos[0].Tipo);
        Assert.Equal(8_000m, result.TotalImpuestos);
    }

    // ── IVA tasas alternativas ────────────────────────────────────────────────

    [Fact]
    public void Calcular_ConIva5_RetornaMontoCorrectoBienesPrimeraNecesidad()
    {
        // IVA 5% aplica a bienes de primera necesidad (art. 468-1 ET)
        var req = BaseRequest(precioUnitario: 200_000m, impuesto: IvaImpuesto(0.05m));

        var result = _engine.Calcular(req);

        Assert.Equal(10_000m, result.TotalImpuestos);
        Assert.Equal(TipoImpuesto.IVA, result.Impuestos[0].Tipo);
    }

    [Fact]
    public void Calcular_ConCantidadMayorQueUno_BaseEsPrecioXCantidad()
    {
        // base = 15.000 × 4 = 60.000; IVA 19% = 11.400
        var req = BaseRequest(precioUnitario: 15_000m, cantidad: 4m, impuesto: IvaImpuesto());

        var result = _engine.Calcular(req);

        Assert.Equal(60_000m, result.BaseImponible);
        Assert.Equal(11_400m, result.TotalImpuestos);
    }

    // ── Impuesto Saludable — Ultraprocesados ──────────────────────────────────

    [Fact]
    public void Calcular_UltraprocesadoConImpuestoSaludable_AplicaPorcentajeSobreBase()
    {
        var impuesto = new Impuesto
        {
            Nombre = "Saludable Ultraprocesado 10%",
            Tipo = TipoImpuesto.Saludable,
            Porcentaje = 0.10m,
            AplicaSobreBase = true,
            CodigoCuentaContable = "2420"
        };
        var req = new TaxRequest(
            ProductoId: Guid.NewGuid(),
            Cantidad: 2m,
            PrecioUnitario: 50_000m,
            Impuesto: impuesto,
            EsAlimentoUltraprocesado: true,
            GramosAzucarPor100ml: null,
            PerfilVendedor: "REGIMEN_ORDINARIO",
            PerfilComprador: "REGIMEN_COMUN",
            CodigoMunicipio: "11001",
            ConceptoRetencionId: null,
            ValorUVT: 47_065m,
            ReglasRetencion: new List<RetencionRegla>());

        var result = _engine.Calcular(req);

        // base = 100.000; saludable 10% = 10.000
        Assert.Equal(100_000m, result.BaseImponible);
        Assert.Equal(10_000m, result.TotalImpuestos);
        Assert.Single(result.Impuestos);
        Assert.Equal(TipoImpuesto.Saludable, result.Impuestos[0].Tipo);
    }

    [Fact]
    public void Calcular_NoEsUltraprocesado_NoAplicaImpuestoSaludable()
    {
        var impuesto = new Impuesto
        {
            Nombre = "Saludable 10%",
            Tipo = TipoImpuesto.Saludable,
            Porcentaje = 0.10m,
            AplicaSobreBase = true,
            CodigoCuentaContable = "2420"
        };
        // EsAlimentoUltraprocesado = false por defecto en BaseRequest
        var req = BaseRequest(precioUnitario: 50_000m, impuesto: impuesto);

        var result = _engine.Calcular(req);

        Assert.Equal(0m, result.TotalImpuestos);
        Assert.Empty(result.Impuestos);
    }

    // ── Bebidas Azucaradas — tramos Ley 2277/2022 ────────────────────────────

    [Theory]
    [InlineData(3.0,  18.0)]  // ≤ 6 g/100ml → $18 por 100ml
    [InlineData(6.0,  18.0)]  // exactamente 6 g → tramo 1
    [InlineData(7.0,  35.0)]  // > 6 y ≤ 10 → $35
    [InlineData(10.0, 35.0)]  // exactamente 10 g → tramo 2
    [InlineData(11.0, 55.0)]  // > 10 g → $55
    [InlineData(25.0, 55.0)]  // muy alto → tramo 3
    public void Calcular_BebidaAzucarada_AplicaTarifaDeTramoCorrector(
        double gramosPor100ml, double tarifaEsperada)
    {
        var req = new TaxRequest(
            ProductoId: Guid.NewGuid(),
            Cantidad: 3m,
            PrecioUnitario: 2_000m,
            Impuesto: null,
            EsAlimentoUltraprocesado: false,
            GramosAzucarPor100ml: (decimal)gramosPor100ml,
            PerfilVendedor: "REGIMEN_ORDINARIO",
            PerfilComprador: "REGIMEN_COMUN",
            CodigoMunicipio: "11001",
            ConceptoRetencionId: null,
            ValorUVT: 47_065m,
            ReglasRetencion: new List<RetencionRegla>());

        var result = _engine.Calcular(req);

        // Monto = tarifa × cantidad (3 unidades)
        Assert.Equal((decimal)tarifaEsperada * 3m, result.TotalImpuestos);
        Assert.Single(result.Impuestos);
        Assert.Equal(TipoImpuesto.Saludable, result.Impuestos[0].Tipo);
    }

    [Fact]
    public void Calcular_GramosAzucarCeroONull_NoAplicaImpuestoBebida()
    {
        var req = new TaxRequest(
            ProductoId: Guid.NewGuid(),
            Cantidad: 1m,
            PrecioUnitario: 3_000m,
            Impuesto: null,
            EsAlimentoUltraprocesado: false,
            GramosAzucarPor100ml: 0m,
            PerfilVendedor: "REGIMEN_ORDINARIO",
            PerfilComprador: "REGIMEN_COMUN",
            CodigoMunicipio: "11001",
            ConceptoRetencionId: null,
            ValorUVT: 47_065m,
            ReglasRetencion: new List<RetencionRegla>());

        var result = _engine.Calcular(req);

        Assert.Equal(0m, result.TotalImpuestos);
        Assert.Empty(result.Impuestos);
    }

    // ── Impuesto a la Bolsa ───────────────────────────────────────────────────

    [Fact]
    public void Calcular_ImpuestoBolsa_AplicaValorFijoPorUnidad()
    {
        // Bolsa plástica: $66 por unidad (tarifa 2024)
        var impuesto = new Impuesto
        {
            Nombre = "Bolsa Plástica",
            Tipo = TipoImpuesto.Bolsa,
            Porcentaje = 0m,
            ValorFijo = 66m,
            AplicaSobreBase = false,
            CodigoCuentaContable = "2430"
        };
        var req = BaseRequest(precioUnitario: 5_000m, cantidad: 5m, impuesto: impuesto);

        var result = _engine.Calcular(req);

        // Monto = 66 × 5 unidades = 330
        Assert.Equal(330m, result.TotalImpuestos);
        Assert.Single(result.Impuestos);
        Assert.Equal(TipoImpuesto.Bolsa, result.Impuestos[0].Tipo);
        Assert.Equal(66m, result.Impuestos[0].ValorFijo);
    }

    // ── Exportaciones ────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_Exportacion_AplicaTarifaCero()
    {
        // Exportación de servicios/bienes: IVA 0% (exento pero registrado)
        var impuesto = new Impuesto
        {
            Nombre = "Exportación",
            Tipo = TipoImpuesto.IVA,
            Porcentaje = 0m,
            AplicaSobreBase = true,
            CodigoCuentaContable = "240801"
        };
        var req = BaseRequest(precioUnitario: 100_000m, impuesto: impuesto);

        var result = _engine.Calcular(req);

        Assert.Equal(100_000m, result.BaseImponible);
        Assert.Equal(0m, result.TotalImpuestos);
        Assert.Single(result.Impuestos);
        Assert.Equal(0m, result.Impuestos[0].Monto);
    }

    // ── ReteICA territorial ───────────────────────────────────────────────────

    [Fact]
    public void Calcular_ReteICA_SinMunicipio_AplicaATodos()
    {
        // Regla sin CodigoMunicipio → aplica a cualquier municipio
        var regla = new RetencionRegla
        {
            Nombre = "ReteICA Bogotá",
            Tipo = TipoRetencion.ReteICA,
            Porcentaje = 0.0069m, // 6.9 por mil
            BaseMinUVT = 0m,
            PerfilVendedor = "REGIMEN_ORDINARIO",
            PerfilComprador = "REGIMEN_COMUN",
            CodigoMunicipio = null, // sin municipio = todos
            Activo = true
        };
        var req = BaseRequest(precioUnitario: 500_000m, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Single(result.Retenciones);
        Assert.Equal(3_450m, result.TotalRetenciones); // 500.000 × 0.0069
    }

    [Fact]
    public void Calcular_ReteICA_MunicipioCoincide_AplicaRetencion()
    {
        var regla = new RetencionRegla
        {
            Nombre = "ReteICA Bogotá",
            Tipo = TipoRetencion.ReteICA,
            Porcentaje = 0.0069m,
            BaseMinUVT = 0m,
            PerfilVendedor = "REGIMEN_ORDINARIO",
            PerfilComprador = "REGIMEN_COMUN",
            CodigoMunicipio = "11001", // Bogotá
            Activo = true
        };
        // CodigoMunicipio de la transacción también es "11001"
        var req = BaseRequest(precioUnitario: 500_000m, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Single(result.Retenciones);
        Assert.Equal(TipoRetencion.ReteICA, result.Retenciones[0].Tipo);
    }

    [Fact]
    public void Calcular_ReteICA_MunicipioDistinto_NoAplicaRetencion()
    {
        var regla = new RetencionRegla
        {
            Nombre = "ReteICA Medellín",
            Tipo = TipoRetencion.ReteICA,
            Porcentaje = 0.005m,
            BaseMinUVT = 0m,
            PerfilVendedor = "REGIMEN_ORDINARIO",
            PerfilComprador = "REGIMEN_COMUN",
            CodigoMunicipio = "05001", // Medellín
            Activo = true
        };
        // Transacción es en "11001" (Bogotá) → no retiene
        var req = BaseRequest(precioUnitario: 500_000m, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Empty(result.Retenciones);
    }

    // ── Reglas de retención — casos edge ─────────────────────────────────────

    [Fact]
    public void Calcular_ReglaInactiva_NoAplicaRetencion()
    {
        var regla = new RetencionRegla
        {
            Nombre = "ReteFuente Inactiva",
            Tipo = TipoRetencion.ReteFuente,
            Porcentaje = 0.025m,
            BaseMinUVT = 0m,
            PerfilVendedor = "REGIMEN_ORDINARIO",
            PerfilComprador = "REGIMEN_COMUN",
            Activo = false // inactiva — no debe aplicar
        };
        var req = BaseRequest(precioUnitario: 1_000_000m, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Empty(result.Retenciones);
    }

    [Fact]
    public void Calcular_PerfilCompradorDistinto_NoAplicaRetencion()
    {
        // La regla exige PerfilComprador = "REGIMEN_COMUN",
        // pero la transacción es con un comprador "PERSONA_NATURAL"
        var regla = ReteFuenteRegla(baseMinUVT: 0m, porcentaje: 0.025m);
        var req = BaseRequest(precioUnitario: 500_000m, reglas: new List<RetencionRegla> { regla }) with
        {
            PerfilComprador = "PERSONA_NATURAL"
        };

        var result = _engine.Calcular(req);

        Assert.Empty(result.Retenciones);
    }

    [Fact]
    public void Calcular_ConceptoRetencionNull_AplicaATodosLosConceptos()
    {
        // Regla sin ConceptoRetencionId (null) → aplica sin importar el concepto del producto
        var regla = ReteFuenteRegla(baseMinUVT: 0m, porcentaje: 0.025m, conceptoId: null);
        var req = BaseRequest(precioUnitario: 500_000m, reglas: new List<RetencionRegla> { regla }) with
        {
            ConceptoRetencionId = 99 // cualquier concepto
        };

        var result = _engine.Calcular(req);

        Assert.Single(result.Retenciones);
    }

    [Fact]
    public void Calcular_UmbralExactoUVT_AplicaRetencionEnLimite()
    {
        // Umbral 4 UVT × 47.065 = $188.260. Compramos exactamente $188.260 → debe retener
        var umbralPesos = 4m * 47_065m; // 188.260
        var regla = ReteFuenteRegla(baseMinUVT: 4m, porcentaje: 0.025m);
        var req = BaseRequest(precioUnitario: umbralPesos, reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Single(result.Retenciones);
        Assert.Equal(Math.Round(umbralPesos * 0.025m, 2), result.TotalRetenciones);
    }

    // ── Múltiples retenciones simultáneas ────────────────────────────────────

    [Fact]
    public void Calcular_ReteFuenteYReteICA_AmbosAplicanSimultaneamente()
    {
        var reteFuente = ReteFuenteRegla(baseMinUVT: 0m, porcentaje: 0.025m);
        var reteIca = new RetencionRegla
        {
            Nombre = "ReteICA",
            Tipo = TipoRetencion.ReteICA,
            Porcentaje = 0.0069m,
            BaseMinUVT = 0m,
            PerfilVendedor = "REGIMEN_ORDINARIO",
            PerfilComprador = "REGIMEN_COMUN",
            CodigoMunicipio = null,
            Activo = true
        };
        var req = BaseRequest(
            precioUnitario: 400_000m,
            reglas: new List<RetencionRegla> { reteFuente, reteIca });

        var result = _engine.Calcular(req);

        Assert.Equal(2, result.Retenciones.Count);
        var retencionFuente = 400_000m * 0.025m;  // 10.000
        var retencionIca    = 400_000m * 0.0069m; // 2.760
        Assert.Equal(Math.Round(retencionFuente + retencionIca, 2), result.TotalRetenciones);
    }

    // ── totalNeto ─────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_TotalNeto_EsBaseImponibleMasImpuestosMenosRetenciones()
    {
        // base = 300.000; IVA 19% = 57.000; ReteFuente 2.5% = 7.500
        // neto = 300.000 + 57.000 - 7.500 = 349.500
        var regla = ReteFuenteRegla(baseMinUVT: 0m, porcentaje: 0.025m);
        var req = BaseRequest(
            precioUnitario: 300_000m,
            impuesto: IvaImpuesto(),
            reglas: new List<RetencionRegla> { regla });

        var result = _engine.Calcular(req);

        Assert.Equal(300_000m, result.BaseImponible);
        Assert.Equal(57_000m, result.TotalImpuestos);
        Assert.Equal(7_500m, result.TotalRetenciones);
        Assert.Equal(349_500m, result.TotalNeto);
    }
}
