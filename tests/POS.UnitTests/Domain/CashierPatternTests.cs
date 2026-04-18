using FluentAssertions;
using POS.Domain.Aggregates;
using POS.Domain.Events.Venta;

namespace POS.UnitTests.Domain;

public class CashierPatternTests
{
    private static VentaCompletadaEvent BuildEvt(
        int horaDelDia = 10,
        int diaSemana = 1,
        decimal total = 1000,
        List<VentaItemLine>? items = null) =>
        new("ext-user-1", SucursalId: 1, CajaId: 1,
            horaDelDia, diaSemana,
            items ?? [new VentaItemLine(Guid.NewGuid(), "Prod A", 2, 500)],
            total, ClienteId: null);

    // ── HoraPicoMaxima / DiaMasActivo sin datos ───────────────────────────────

    [Fact]
    public void HoraPicoMaxima_SinDatos_RetornaMinusUno()
    {
        var pattern = new CashierPattern();
        pattern.HoraPicoMaxima.Should().Be(-1);
    }

    [Fact]
    public void DiaMasActivo_SinDatos_RetornaMinusUno()
    {
        var pattern = new CashierPattern();
        pattern.DiaMasActivo.Should().Be(-1);
    }

    // ── Apply acumula correctamente ───────────────────────────────────────────

    [Fact]
    public void Apply_PrimeraVenta_IncrementaTotalYRegistraHoraYDia()
    {
        var pattern = new CashierPattern();

        pattern.Apply(BuildEvt(horaDelDia: 14, diaSemana: 3));

        pattern.TotalVentas.Should().Be(1);
        pattern.HorasPico[14].Should().Be(1);
        pattern.DiasActivos[3].Should().Be(1);
    }

    [Fact]
    public void Apply_VariasVentas_AcumulaHorasPicoCorrectamente()
    {
        var pattern = new CashierPattern();

        pattern.Apply(BuildEvt(horaDelDia: 10));
        pattern.Apply(BuildEvt(horaDelDia: 10));
        pattern.Apply(BuildEvt(horaDelDia: 14));

        pattern.HorasPico[10].Should().Be(2);
        pattern.HorasPico[14].Should().Be(1);
        pattern.HoraPicoMaxima.Should().Be(10);
    }

    [Fact]
    public void Apply_ProductoVelocidad_AcumulaUnidadesVendidas()
    {
        var productoId = Guid.NewGuid();
        var pattern = new CashierPattern();
        var items = new List<VentaItemLine> { new(productoId, "Prod X", 3, 100) };

        pattern.Apply(BuildEvt(items: items));
        pattern.Apply(BuildEvt(items: items));

        pattern.ProductoVelocidad[productoId.ToString()].Should().Be(6);
    }

    [Fact]
    public void Apply_TopProductos_LimitadoA20YOrdenadoPorVelocidad()
    {
        var pattern = new CashierPattern();

        // 25 productos distintos, el primero con mayor velocidad
        var productoTop = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
            pattern.Apply(BuildEvt(items: [new(productoTop, "Top", 10, 100)]));

        for (var i = 0; i < 25; i++)
            pattern.Apply(BuildEvt(items: [new(Guid.NewGuid(), $"Prod {i}", 1, 100)]));

        pattern.TopProductos.Should().HaveCount(20);
        pattern.TopProductos[0].Should().Be(productoTop.ToString());
    }

    [Fact]
    public void Apply_DiaMasActivo_RetornaDiaConMasVentas()
    {
        var pattern = new CashierPattern();

        pattern.Apply(BuildEvt(diaSemana: 1));
        pattern.Apply(BuildEvt(diaSemana: 2));
        pattern.Apply(BuildEvt(diaSemana: 2));
        pattern.Apply(BuildEvt(diaSemana: 3));

        pattern.DiaMasActivo.Should().Be(2);
    }
}
