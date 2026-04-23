using FluentAssertions;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services;

namespace POS.UnitTests.Domain;

public class TaxEngineTests
{
    private static readonly Guid ProdId = Guid.NewGuid();
    private readonly TaxEngine _engine = new();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static Impuesto Iva(decimal pct) => new()
    {
        Nombre = $"IVA {pct * 100:0}%", Tipo = TipoImpuesto.IVA,
        Porcentaje = pct, AplicaSobreBase = true
    };

    private static Impuesto Inc(decimal pct) => new()
    {
        Nombre = $"INC {pct * 100:0}%", Tipo = TipoImpuesto.INC,
        Porcentaje = pct, AplicaSobreBase = false
    };

    private static Impuesto Saludable(decimal pct) => new()
    {
        Nombre = "Saludable", Tipo = TipoImpuesto.Saludable,
        Porcentaje = pct, AplicaSobreBase = true
    };

    private static Impuesto Bolsa(decimal valorFijo) => new()
    {
        Nombre = "Bolsa", Tipo = TipoImpuesto.Bolsa, ValorFijo = valorFijo
    };

    private static RetencionRegla Retencion(
        TipoRetencion tipo, decimal pct, decimal baseMinUVT = 0,
        string vendedor = "REGIMEN_ORDINARIO", string comprador = "GRAN_CONTRIBUYENTE",
        string? municipio = null, int? conceptoId = null) => new()
    {
        Nombre = tipo.ToString(), Tipo = tipo, Porcentaje = pct,
        BaseMinUVT = baseMinUVT, PerfilVendedor = vendedor, PerfilComprador = comprador,
        CodigoMunicipio = municipio, ConceptoRetencionId = conceptoId, Activo = true
    };

    private static TramoBebidasAzucaradas Tramo(decimal? max, decimal valor) => new()
    {
        MaxGramosPor100ml = max, ValorPor100ml = valor,
        VigenciaDesde = DateOnly.FromDateTime(DateTime.Today), Activo = true
    };

    private TaxRequest Req(
        decimal precio, decimal cantidad,
        Impuesto? impuesto = null,
        string vendedor = "REGIMEN_ORDINARIO",
        string comprador = "GRAN_CONTRIBUYENTE",
        string municipio = "11001",
        decimal uvt = 47065m,
        List<RetencionRegla>? retenciones = null,
        List<TramoBebidasAzucaradas>? tramos = null,
        bool esUltraprocesado = false,
        decimal? gramosAzucar = null,
        int? conceptoRetencionId = null) =>
        new(ProdId, cantidad, precio,
            impuesto, esUltraprocesado, gramosAzucar,
            vendedor, comprador, municipio,
            conceptoRetencionId, uvt,
            retenciones ?? [],
            tramos ?? []);

    // ─── Sin impuesto ─────────────────────────────────────────────────────────

    [Fact]
    public void SinImpuesto_BaseEquivaleTotalNeto()
    {
        var r = _engine.Calcular(Req(1000, 2));

        r.BaseImponible.Should().Be(2000);
        r.TotalImpuestos.Should().Be(0);
        r.TotalNeto.Should().Be(2000);
        r.Impuestos.Should().BeEmpty();
    }

    // ─── IVA ──────────────────────────────────────────────────────────────────

    [Fact]
    public void IVA_19pct_CalculaCorrectamente()
    {
        var r = _engine.Calcular(Req(1000, 1, Iva(0.19m)));

        r.TotalImpuestos.Should().Be(190);
        r.TotalNeto.Should().Be(1190);
        r.Impuestos.Should().ContainSingle(i => i.Tipo == TipoImpuesto.IVA && i.Monto == 190);
    }

    [Fact]
    public void IVA_5pct_CalculaCorrectamente()
    {
        var r = _engine.Calcular(Req(2000, 3, Iva(0.05m)));

        r.BaseImponible.Should().Be(6000);
        r.TotalImpuestos.Should().Be(300);
        r.TotalNeto.Should().Be(6300);
    }

    [Fact]
    public void IVA_0pct_NoAgregaImpuesto()
    {
        var r = _engine.Calcular(Req(500, 1, Iva(0m)));

        r.TotalImpuestos.Should().Be(0);
    }

    [Theory]
    [InlineData(0.19, 100, 1, 19)]
    [InlineData(0.19, 50, 4, 38)]
    [InlineData(0.05, 1000, 2, 100)]
    public void IVA_Variados_CalculaCorrectamente(decimal pct, decimal precio, decimal cant, decimal esperado)
    {
        var r = _engine.Calcular(Req(precio, cant, Iva(pct)));

        r.TotalImpuestos.Should().Be(esperado);
    }

    // ─── INC monofásico ───────────────────────────────────────────────────────

    [Fact]
    public void INC_8pct_CalculaCorrectamente()
    {
        var r = _engine.Calcular(Req(1000, 1, Inc(0.08m)));

        r.TotalImpuestos.Should().Be(80);
        r.Impuestos.Should().ContainSingle(i => i.Tipo == TipoImpuesto.INC);
    }

    [Fact]
    public void INC_NoSeSumaConIVA_SoloUnoAplica()
    {
        // INC tiene AplicaSobreBase=false → IVA requiere AplicaSobreBase=true
        // Si se envía INC, no debe aparecer IVA
        var r = _engine.Calcular(Req(500, 2, Inc(0.08m)));

        r.Impuestos.Should().AllSatisfy(i => i.Tipo.Should().Be(TipoImpuesto.INC));
    }

    // ─── Impuesto Saludable ───────────────────────────────────────────────────

    [Fact]
    public void Saludable_Ultraprocesado_CalculaSobreBase()
    {
        var r = _engine.Calcular(Req(1000, 2, Saludable(0.10m), esUltraprocesado: true));

        r.TotalImpuestos.Should().Be(200);
        r.Impuestos.Should().ContainSingle(i => i.Tipo == TipoImpuesto.Saludable && i.Monto == 200);
    }

    [Fact]
    public void Saludable_SinFlagUltraprocesado_NoAplica()
    {
        var r = _engine.Calcular(Req(1000, 2, Saludable(0.10m), esUltraprocesado: false));

        r.TotalImpuestos.Should().Be(0);
    }

    [Fact]
    public void BebidasAzucaradas_PrimerTramo_AplicaTarifaBaja()
    {
        var tramos = new List<TramoBebidasAzucaradas>
        {
            Tramo(6m, 18m),   // hasta 6 g/100ml → $18
            Tramo(null, 35m), // más de 6 g/100ml → $35
        };

        // 4 g/100ml → primer tramo ($18) × 2 unidades = $36
        var r = _engine.Calcular(Req(100, 2, gramosAzucar: 4m, tramos: tramos));

        r.TotalImpuestos.Should().Be(36);
        r.Impuestos.Should().ContainSingle(i => i.Tipo == TipoImpuesto.Saludable && i.ValorFijo == 18m);
    }

    [Fact]
    public void BebidasAzucaradas_SegundoTramo_AplicaTarifaAlta()
    {
        var tramos = new List<TramoBebidasAzucaradas>
        {
            Tramo(6m, 18m),
            Tramo(null, 35m),
        };

        // 10 g/100ml → segundo tramo ($35) × 3 unidades = $105
        var r = _engine.Calcular(Req(100, 3, gramosAzucar: 10m, tramos: tramos));

        r.TotalImpuestos.Should().Be(105);
        r.Impuestos.Should().ContainSingle(i => i.ValorFijo == 35m);
    }

    [Fact]
    public void BebidasAzucaradas_SinGramos_NoAplica()
    {
        var tramos = new List<TramoBebidasAzucaradas> { Tramo(6m, 18m) };

        var r = _engine.Calcular(Req(100, 1, gramosAzucar: null, tramos: tramos));

        r.TotalImpuestos.Should().Be(0);
    }

    // ─── Impuesto a la Bolsa ──────────────────────────────────────────────────

    [Fact]
    public void Bolsa_ValorFijo_MultiplicitadPorCantidad()
    {
        var r = _engine.Calcular(Req(100, 5, Bolsa(66m)));

        r.TotalImpuestos.Should().Be(330);
        r.Impuestos.Should().ContainSingle(i => i.Tipo == TipoImpuesto.Bolsa && i.Monto == 330);
    }

    // ─── Retenciones ─────────────────────────────────────────────────────────

    [Fact]
    public void Retencion_PerfilMatch_AplicaRetencion()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m, baseMinUVT: 0);
        var r = _engine.Calcular(Req(10000, 1, retenciones: [regla]));

        r.TotalRetenciones.Should().Be(250);
        r.Retenciones.Should().ContainSingle(rt => rt.Tipo == TipoRetencion.ReteFuente);
    }

    [Fact]
    public void Retencion_BaseImponibleBajoUmbralUVT_NoAplica()
    {
        // 4 UVT × $47065 = $188.260. Venta de $1000 → no retiene
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m, baseMinUVT: 4);
        var r = _engine.Calcular(Req(1000, 1, uvt: 47065m, retenciones: [regla]));

        r.TotalRetenciones.Should().Be(0);
        r.Retenciones.Should().BeEmpty();
    }

    [Fact]
    public void Retencion_BaseImponibleExactoUmbral_SiAplica()
    {
        // Umbral = 4 × $47065 = $188.260. Venta exactamente = $188.260 → aplica
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m, baseMinUVT: 4);
        var r = _engine.Calcular(Req(188260m, 1, uvt: 47065m, retenciones: [regla]));

        r.TotalRetenciones.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Retencion_PerfilVendedorNoCoincide_NoAplica()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m,
            vendedor: "REGIMEN_ORDINARIO");

        // vendedor enviado es diferente
        var r = _engine.Calcular(Req(50000, 1,
            vendedor: "REGIMEN_SIMPLE",
            retenciones: [regla]));

        r.TotalRetenciones.Should().Be(0);
    }

    [Fact]
    public void Retencion_PerfilCompradorNoCoincide_NoAplica()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m,
            comprador: "GRAN_CONTRIBUYENTE");

        var r = _engine.Calcular(Req(50000, 1,
            comprador: "REGIMEN_COMUN",
            retenciones: [regla]));

        r.TotalRetenciones.Should().Be(0);
    }

    [Fact]
    public void Retencion_RegimenSimpleVendedor_ExentoReteFuente()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m,
            vendedor: "REGIMEN_SIMPLE", comprador: "GRAN_CONTRIBUYENTE");

        // vendedor=REGIMEN_SIMPLE → exento aunque la regla lo incluya
        var r = _engine.Calcular(Req(50000, 1,
            vendedor: "REGIMEN_SIMPLE",
            comprador: "GRAN_CONTRIBUYENTE",
            retenciones: [regla]));

        r.TotalRetenciones.Should().Be(0,
            "REGIMEN_SIMPLE está exento de retención en la fuente");
    }

    [Fact]
    public void ReteICA_MismoMunicipio_Aplica()
    {
        var regla = Retencion(TipoRetencion.ReteICA, 0.005m, municipio: "11001");
        var r = _engine.Calcular(Req(100000, 1,
            municipio: "11001", retenciones: [regla]));

        r.Retenciones.Should().ContainSingle(rt => rt.Tipo == TipoRetencion.ReteICA);
    }

    [Fact]
    public void ReteICA_DiferenteMunicipio_NoAplica()
    {
        var regla = Retencion(TipoRetencion.ReteICA, 0.005m, municipio: "11001");
        var r = _engine.Calcular(Req(100000, 1,
            municipio: "05001", retenciones: [regla]));

        r.Retenciones.Should().BeEmpty();
    }

    [Fact]
    public void ReteICA_SinMunicipio_AplicaEnCualquierMunicipio()
    {
        var regla = Retencion(TipoRetencion.ReteICA, 0.005m, municipio: null);
        var r = _engine.Calcular(Req(100000, 1,
            municipio: "05001", retenciones: [regla]));

        r.Retenciones.Should().ContainSingle(rt => rt.Tipo == TipoRetencion.ReteICA);
    }

    [Fact]
    public void ReteFuente_ConceptoRetencionDistinto_NoAplica()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m, conceptoId: 1);

        // El producto tiene concepto 2 → no coincide con la regla
        var r = _engine.Calcular(Req(50000, 1,
            conceptoRetencionId: 2, retenciones: [regla]));

        r.TotalRetenciones.Should().Be(0);
    }

    [Fact]
    public void ReteFuente_ConceptoRetencionCoincide_Aplica()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m, conceptoId: 5);
        var r = _engine.Calcular(Req(50000, 1,
            conceptoRetencionId: 5, retenciones: [regla]));

        r.TotalRetenciones.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MultipleRetenciones_SeAcumulan()
    {
        var retenciones = new List<RetencionRegla>
        {
            Retencion(TipoRetencion.ReteFuente, 0.025m),
            Retencion(TipoRetencion.ReteICA, 0.005m),
        };

        var r = _engine.Calcular(Req(100000, 1, retenciones: retenciones));

        r.Retenciones.Should().HaveCount(2);
        r.TotalRetenciones.Should().Be(3000); // 2500 + 500
    }

    [Fact]
    public void RetencionInactiva_NoAplica()
    {
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m);
        regla.Activo = false;

        var r = _engine.Calcular(Req(50000, 1, retenciones: [regla]));

        r.TotalRetenciones.Should().Be(0);
    }

    // ─── TotalNeto ────────────────────────────────────────────────────────────

    [Fact]
    public void TotalNeto_BaseImpuestosRetenciones_FormulaCorrecta()
    {
        // base=10000, IVA 19%=1900, ReteFuente 2.5%=250
        // neto = 10000 + 1900 - 250 = 11650
        var regla = Retencion(TipoRetencion.ReteFuente, 0.025m);
        var r = _engine.Calcular(Req(10000, 1, Iva(0.19m), retenciones: [regla]));

        r.TotalNeto.Should().Be(11650);
    }

    // ─── Flag factura electrónica ─────────────────────────────────────────────

    [Fact]
    public void RequiereFactura_TotalMayor5UVT_EsTrue()
    {
        // 5 × $47065 = $235.325. Venta de $300.000 → requiere factura
        var r = _engine.Calcular(Req(300000, 1, uvt: 47065m));

        r.RequiereFacturaElectronica.Should().BeTrue();
    }

    [Fact]
    public void RequiereFactura_TotalMenor5UVT_EsFalse()
    {
        // Venta de $1000 → no requiere factura
        var r = _engine.Calcular(Req(1000, 1, uvt: 47065m));

        r.RequiereFacturaElectronica.Should().BeFalse();
    }

    [Fact]
    public void RequiereFactura_ExactamenteFrontera5UVT_EsFalse()
    {
        // Exactamente 5 × $47065 = $235325 → no supera → false
        var r = _engine.Calcular(Req(235325m, 1, uvt: 47065m));

        r.RequiereFacturaElectronica.Should().BeFalse();
    }
}
