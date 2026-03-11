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

    // ── Bebidas Azucaradas ────────────────────────────────────────────────────

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
}
