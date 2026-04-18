using FluentAssertions;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.UnitTests.Domain;

public class BusinessRadarTests
{
    private static readonly DateTime Timestamp = new(2026, 4, 18, 10, 30, 0, DateTimeKind.Utc);

    private static VentaCompletadaEvent BuildEvt(decimal total = 1000, List<VentaItemLine>? items = null) =>
        new("ext-1", SucursalId: 1, CajaId: 1,
            HoraDelDia: Timestamp.Hour, DiaSemana: (int)Timestamp.DayOfWeek,
            items ?? [new VentaItemLine(Guid.NewGuid(), "Prod A", 2, 500)],
            total, ClienteId: null);

    // ── Ingresos por fecha ────────────────────────────────────────────────────

    [Fact]
    public void Apply_PrimeraVenta_RegistraIngresoYConteoDelDia()
    {
        var radar = new BusinessRadar();

        radar.Apply(BuildEvt(total: 500), Timestamp);

        var fecha = Timestamp.ToString("yyyy-MM-dd");
        radar.IngresosPorFecha[fecha].Should().Be(500);
        radar.VentasPorFecha[fecha].Should().Be(1);
    }

    [Fact]
    public void Apply_VariasVentas_AcumulaIngresosPorFecha()
    {
        var radar = new BusinessRadar();

        radar.Apply(BuildEvt(total: 300), Timestamp);
        radar.Apply(BuildEvt(total: 700), Timestamp);

        var fecha = Timestamp.ToString("yyyy-MM-dd");
        radar.IngresosPorFecha[fecha].Should().Be(1000);
        radar.VentasPorFecha[fecha].Should().Be(2);
    }

    // ── Ingresos por fecha-hora ───────────────────────────────────────────────

    [Fact]
    public void Apply_RegistraIngresosPorFechaHora()
    {
        var radar = new BusinessRadar();

        radar.Apply(BuildEvt(total: 400), Timestamp);
        radar.Apply(BuildEvt(total: 600), Timestamp);

        var key = $"{Timestamp:yyyy-MM-dd}:{Timestamp.Hour:D2}";
        radar.IngresosPorFechaHora[key].Should().Be(1000);
    }

    [Fact]
    public void Apply_DiferentesHoras_RegistraSeparado()
    {
        var radar = new BusinessRadar();
        var ts1 = Timestamp;
        var ts2 = Timestamp.AddHours(2);

        var evt1 = new VentaCompletadaEvent("ext-1", 1, 1, ts1.Hour, 1, [new(Guid.NewGuid(), "A", 1, 100)], 100, null);
        var evt2 = new VentaCompletadaEvent("ext-1", 1, 1, ts2.Hour, 1, [new(Guid.NewGuid(), "B", 1, 200)], 200, null);

        radar.Apply(evt1, ts1);
        radar.Apply(evt2, ts2);

        var key1 = $"{ts1:yyyy-MM-dd}:{ts1.Hour:D2}";
        var key2 = $"{ts2:yyyy-MM-dd}:{ts2.Hour:D2}";
        radar.IngresosPorFechaHora[key1].Should().Be(100);
        radar.IngresosPorFechaHora[key2].Should().Be(200);
    }

    // ── Velocidad de productos ────────────────────────────────────────────────

    [Fact]
    public void Apply_ProductoVelocidad_AcumulaUnidadesVendidasTodasLasFechas()
    {
        var productoId = Guid.NewGuid();
        var radar = new BusinessRadar();
        var items = new List<VentaItemLine> { new(productoId, "Prod X", 5, 100) };

        radar.Apply(BuildEvt(items: items), Timestamp);
        radar.Apply(BuildEvt(items: items), Timestamp.AddDays(1));

        radar.ProductoVelocidad[productoId.ToString()].Should().Be(10);
    }

    [Fact]
    public void Apply_VariosProductos_AcumulaIndependientemente()
    {
        var prod1 = Guid.NewGuid();
        var prod2 = Guid.NewGuid();
        var radar = new BusinessRadar();

        radar.Apply(BuildEvt(items: [new(prod1, "A", 3, 100)]), Timestamp);
        radar.Apply(BuildEvt(items: [new(prod2, "B", 7, 100)]), Timestamp);

        radar.ProductoVelocidad[prod1.ToString()].Should().Be(3);
        radar.ProductoVelocidad[prod2.ToString()].Should().Be(7);
    }

    // ── UltimaActualizacion ───────────────────────────────────────────────────

    [Fact]
    public void Apply_ActualizaUltimaActualizacion()
    {
        var radar = new BusinessRadar();
        var ts = new DateTime(2026, 4, 18, 15, 0, 0, DateTimeKind.Utc);

        radar.Apply(BuildEvt(), ts);

        radar.UltimaActualizacion.Should().Be(ts);
    }

    // ── Diferentes días ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_DiferentesDias_MantieneConteosPorFechaSeparados()
    {
        var radar = new BusinessRadar();
        var hoy = Timestamp;
        var ayer = Timestamp.AddDays(-1);

        radar.Apply(BuildEvt(total: 1000), hoy);
        radar.Apply(BuildEvt(total: 500), ayer);

        radar.IngresosPorFecha[hoy.ToString("yyyy-MM-dd")].Should().Be(1000);
        radar.IngresosPorFecha[ayer.ToString("yyyy-MM-dd")].Should().Be(500);
        radar.IngresosPorFecha.Should().HaveCount(2);
    }
}
