using FluentAssertions;
using POS.Domain.Aggregates;

namespace POS.UnitTests.Domain;

public class InventarioAggregateTests
{
    private static readonly Guid ProductoId = Guid.NewGuid();
    private const int SucursalId = 1;

    // ── GenerarStreamId ───────────────────────────────────────────────────────

    [Fact]
    public void GenerarStreamId_MismosInputs_RetornaMismoGuid()
    {
        var id1 = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);
        var id2 = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);

        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerarStreamId_DiferentesSucursales_RetornaGuidsDistintos()
    {
        var id1 = InventarioAggregate.GenerarStreamId(ProductoId, 1);
        var id2 = InventarioAggregate.GenerarStreamId(ProductoId, 2);

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GenerarStreamId_DiferentesProductos_RetornaGuidsDistintos()
    {
        var id1 = InventarioAggregate.GenerarStreamId(Guid.NewGuid(), SucursalId);
        var id2 = InventarioAggregate.GenerarStreamId(Guid.NewGuid(), SucursalId);

        id1.Should().NotBe(id2);
    }

    // ── RegistrarEntrada (estático) ───────────────────────────────────────────

    [Fact]
    public void RegistrarEntrada_CantidadValida_CreaAggregateConStockYCosto()
    {
        var streamId = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);

        var (agg, evt) = InventarioAggregate.RegistrarEntrada(
            streamId, ProductoId, SucursalId,
            cantidad: 10, costoUnitario: 500,
            porcentajeImpuesto: 0, montoImpuesto: 0,
            terceroId: null, nombreTercero: null,
            referencia: "OC-001", observaciones: null,
            usuarioId: 1, sucursalUsuarioId: null);

        agg.Cantidad.Should().Be(10);
        agg.CostoPromedio.Should().Be(500);
        agg.Lotes.Should().HaveCount(1);
        evt.CostoTotal.Should().Be(5000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegistrarEntrada_CantidadInvalida_LanzaExcepcion(decimal cantidad)
    {
        var streamId = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);

        var act = () => InventarioAggregate.RegistrarEntrada(
            streamId, ProductoId, SucursalId,
            cantidad, costoUnitario: 100,
            0, 0, null, null, null, null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cantidad*");
    }

    [Fact]
    public void RegistrarEntrada_CostoNegativo_LanzaExcepcion()
    {
        var streamId = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);

        var act = () => InventarioAggregate.RegistrarEntrada(
            streamId, ProductoId, SucursalId,
            cantidad: 5, costoUnitario: -1,
            0, 0, null, null, null, null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*costo*");
    }

    // ── AgregarEntrada (costo promedio ponderado) ─────────────────────────────

    [Fact]
    public void AgregarEntrada_VariasEntradas_CalculaCostoPromedioPonderado()
    {
        var streamId = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);
        var (agg, _) = InventarioAggregate.RegistrarEntrada(
            streamId, ProductoId, SucursalId,
            cantidad: 10, costoUnitario: 100,
            0, 0, null, null, null, null, null, null);

        // Segunda entrada: 10 unidades a $200
        agg.AgregarEntrada(10, 200, null, null, null, null, null);

        // Promedio ponderado: (10*100 + 10*200) / 20 = 150
        agg.Cantidad.Should().Be(20);
        agg.CostoPromedio.Should().Be(150);
        agg.Lotes.Should().HaveCount(2);
    }

    [Fact]
    public void AgregarEntrada_CantidadCero_LanzaExcepcion()
    {
        var streamId = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);
        var (agg, _) = InventarioAggregate.RegistrarEntrada(
            streamId, ProductoId, SucursalId,
            10, 100, 0, 0, null, null, null, null, null, null);

        var act = () => agg.AgregarEntrada(0, 100, null, null, null, null, null);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── RegistrarDevolucion ───────────────────────────────────────────────────

    [Fact]
    public void RegistrarDevolucion_StockSuficiente_ReduceCantidad()
    {
        var (agg, _) = CrearAggregateConStock(10, 500);

        agg.RegistrarDevolucion(3, terceroId: 1, "Proveedor", "REF", null, null);

        agg.Cantidad.Should().Be(7);
    }

    [Fact]
    public void RegistrarDevolucion_StockInsuficiente_LanzaExcepcion()
    {
        var (agg, _) = CrearAggregateConStock(5, 500);

        var act = () => agg.RegistrarDevolucion(10, 1, "Proveedor", null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Stock insuficiente*");
    }

    // ── RegistrarAjuste ───────────────────────────────────────────────────────

    [Fact]
    public void RegistrarAjuste_AumentaStock_EsPositivoYDiferenciaCorrecta()
    {
        var (agg, _) = CrearAggregateConStock(10, 100);

        var evt = agg.RegistrarAjuste(15, null, null);

        agg.Cantidad.Should().Be(15);
        evt.Diferencia.Should().Be(5);
        evt.EsPositivo.Should().BeTrue();
    }

    [Fact]
    public void RegistrarAjuste_ReduceStock_EsNegativoYDiferenciaCorrecta()
    {
        var (agg, _) = CrearAggregateConStock(10, 100);

        var evt = agg.RegistrarAjuste(6, null, null);

        agg.Cantidad.Should().Be(6);
        evt.Diferencia.Should().Be(-4);
        evt.EsPositivo.Should().BeFalse();
    }

    [Fact]
    public void RegistrarAjuste_CantidadNegativa_LanzaExcepcion()
    {
        var (agg, _) = CrearAggregateConStock(10, 100);

        var act = () => agg.RegistrarAjuste(-1, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*negativa*");
    }

    // ── RegistrarSalidaVenta ──────────────────────────────────────────────────

    [Fact]
    public void RegistrarSalidaVenta_StockSuficiente_ReduceCantidadYUsaCostoPromedio()
    {
        var (agg, _) = CrearAggregateConStock(10, 300);

        var evt = agg.RegistrarSalidaVenta(4, precioVenta: 500, 0, 0, "V-001", null);

        agg.Cantidad.Should().Be(6);
        evt.CostoUnitario.Should().Be(300);
        evt.CostoTotal.Should().Be(1200);
    }

    [Fact]
    public void RegistrarSalidaVenta_StockInsuficiente_LanzaExcepcion()
    {
        var (agg, _) = CrearAggregateConStock(3, 100);

        var act = () => agg.RegistrarSalidaVenta(5, 200, 0, 0, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Stock insuficiente*");
    }

    // ── RegistrarSalidaTraslado ───────────────────────────────────────────────

    [Fact]
    public void RegistrarSalidaTraslado_StockSuficiente_ReduceCantidad()
    {
        var (agg, _) = CrearAggregateConStock(20, 200);

        agg.RegistrarSalidaTraslado(5, 200, sucursalDestinoId: 2, "TRL-001", null, null);

        agg.Cantidad.Should().Be(15);
    }

    [Fact]
    public void RegistrarSalidaTraslado_StockInsuficiente_LanzaExcepcion()
    {
        var (agg, _) = CrearAggregateConStock(3, 200);

        var act = () => agg.RegistrarSalidaTraslado(10, 200, 2, "TRL-001", null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Stock insuficiente*");
    }

    // ── RegistrarEntradaTraslado ──────────────────────────────────────────────

    [Fact]
    public void RegistrarEntradaTraslado_RecalculaCostoPromedioYSuma()
    {
        var (agg, _) = CrearAggregateConStock(10, 100);

        // Traslado entrante: 10 unidades a $200
        agg.RegistrarEntradaTraslado(10, 200, sucursalOrigenId: 2, "TRL-001", null, null);

        // Promedio: (10*100 + 10*200) / 20 = 150
        agg.Cantidad.Should().Be(20);
        agg.CostoPromedio.Should().Be(150);
    }

    // ── ActualizarStockMinimo ─────────────────────────────────────────────────

    [Fact]
    public void ActualizarStockMinimo_ValorValido_ActualizaStockMinimo()
    {
        var (agg, _) = CrearAggregateConStock(10, 100);

        agg.ActualizarStockMinimo(5, null);

        agg.StockMinimo.Should().Be(5);
    }

    [Fact]
    public void ActualizarStockMinimo_ValorNegativo_LanzaExcepcion()
    {
        var (agg, _) = CrearAggregateConStock(10, 100);

        var act = () => agg.ActualizarStockMinimo(-1, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*negativo*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (InventarioAggregate, object) CrearAggregateConStock(decimal cantidad, decimal costo)
    {
        var streamId = InventarioAggregate.GenerarStreamId(ProductoId, SucursalId);
        var (agg, evt) = InventarioAggregate.RegistrarEntrada(
            streamId, ProductoId, SucursalId,
            cantidad, costo, 0, 0, null, null, null, null, null, null);
        return (agg, evt);
    }
}
