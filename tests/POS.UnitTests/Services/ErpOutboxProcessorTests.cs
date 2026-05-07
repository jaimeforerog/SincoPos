using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;
using POS.Infrastructure.Services.Erp;

namespace POS.UnitTests.Services;

/// <summary>
/// Tests del processor del Outbox: cubre los caminos críticos del patrón Outbox
/// (éxito, retry transitorio, descarte tras max-reintentos, JSON inválido,
/// tipo no soportado) sin levantar el host de Azure Functions.
/// </summary>
public sealed class ErpOutboxProcessorTests : IDisposable
{
    private const int MaxReintentos = 3;
    private const int SucursalId = 1;

    private readonly AppDbContext _db;
    private readonly IErpClient _erpClient;
    private readonly ErpOutboxProcessor _sut;

    public ErpOutboxProcessorTests()
    {
        var empresaProvider = Substitute.For<ICurrentEmpresaProvider>();
        empresaProvider.EmpresaId.Returns((int?)null);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(
            options,
            Substitute.For<IHttpContextAccessor>(),
            empresaProvider,
            Substitute.For<ILogger<AppDbContext>>());

        _erpClient = Substitute.For<IErpClient>();

        var erpOpts = Options.Create(new ErpSincoOptions { MaxReintentos = MaxReintentos });

        _sut = new ErpOutboxProcessor(
            _db, _erpClient, Substitute.For<ILogger<ErpOutboxProcessor>>(), erpOpts);
    }

    [Fact]
    public async Task ProcesarLote_SinMensajes_RetornaListaVacia()
    {
        var result = await _sut.ProcesarLoteAsync();

        result.Should().BeEmpty();
        await _erpClient.DidNotReceiveWithAnyArgs().ContabilizarVentaAsync(default!);
    }

    [Fact]
    public async Task ProcesarLote_VentaCompletadaExitosa_MarcaProcesadoYActualizaVenta()
    {
        var venta = SeedVenta();
        SeedMensajeVenta("VentaCompletada", venta.Id, venta.NumeroVenta);

        _erpClient.ContabilizarVentaAsync(Arg.Any<VentaErpPayload>())
            .Returns(new ErpResponse(true, "ERP-12345", null));

        var notifications = await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Estado.Should().Be(EstadoOutbox.Procesado);
        mensaje.UltimoError.Should().BeNull();
        mensaje.FechaProcesamiento.Should().NotBeNull();
        mensaje.Intentos.Should().Be(1);

        var ventaActualizada = await _db.Ventas.FirstAsync();
        ventaActualizada.SincronizadoErp.Should().BeTrue();
        ventaActualizada.ErpReferencia.Should().Be("ERP-12345");
        ventaActualizada.ErrorSincronizacion.Should().BeNull();

        notifications.Should().ContainSingle()
            .Which.Notificacion.Tipo.Should().Be("erp_sincronizado");
    }

    [Fact]
    public async Task ProcesarLote_VentaFallaConIntentosBajoLimite_MarcaErrorParaRetry()
    {
        var venta = SeedVenta();
        SeedMensajeVenta("VentaCompletada", venta.Id, venta.NumeroVenta, intentos: 0);

        _erpClient.ContabilizarVentaAsync(Arg.Any<VentaErpPayload>())
            .Returns(new ErpResponse(false, null, "Conexión rechazada"));

        var notifications = await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Estado.Should().Be(EstadoOutbox.Error);
        mensaje.UltimoError.Should().Be("Conexión rechazada");
        mensaje.Intentos.Should().Be(1);
        mensaje.FechaProcesamiento.Should().BeNull();

        var ventaActualizada = await _db.Ventas.FirstAsync();
        ventaActualizada.SincronizadoErp.Should().BeFalse();
        ventaActualizada.ErrorSincronizacion.Should().Be("Conexión rechazada");

        notifications.Should().ContainSingle()
            .Which.Notificacion.Tipo.Should().Be("erp_error");
    }

    [Fact]
    public async Task ProcesarLote_VentaFallaSuperandoMaxReintentos_MarcaDescartado()
    {
        var venta = SeedVenta();
        // Intentos = MaxReintentos - 1; tras este intento (++) llega al límite
        SeedMensajeVenta("VentaCompletada", venta.Id, venta.NumeroVenta, intentos: MaxReintentos - 1);

        _erpClient.ContabilizarVentaAsync(Arg.Any<VentaErpPayload>())
            .Returns(new ErpResponse(false, null, "ERP down"));

        await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Intentos.Should().Be(MaxReintentos);
        mensaje.Estado.Should().Be(EstadoOutbox.Descartado);
    }

    [Fact]
    public async Task ProcesarLote_PayloadVentaInvalido_MarcaError()
    {
        SeedMensajeRaw("VentaCompletada", entidadId: 999, payload: "not-json-{{{");

        await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Estado.Should().Be(EstadoOutbox.Error);
        mensaje.UltimoError.Should().Contain("JSON inválido");

        await _erpClient.DidNotReceiveWithAnyArgs().ContabilizarVentaAsync(default!);
    }

    [Fact]
    public async Task ProcesarLote_TipoDocumentoNoSoportado_MarcaError()
    {
        SeedMensajeRaw("DocumentoExotico", entidadId: 1, payload: "{}");

        await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Estado.Should().Be(EstadoOutbox.Error);
        mensaje.UltimoError.Should().Contain("no soportado");
    }

    [Fact]
    public async Task ProcesarLote_CompraRecibidaExitosa_MarcaProcesadoYActualizaOrden()
    {
        var orden = SeedOrdenCompra();
        SeedMensajeCompra("CompraRecibida", orden.Id, orden.NumeroOrden);

        _erpClient.ContabilizarCompraAsync(Arg.Any<CompraErpPayload>())
            .Returns(new ErpResponse(true, "ERP-COMPRA-99", null));

        var notifications = await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Estado.Should().Be(EstadoOutbox.Procesado);

        var ordenActualizada = await _db.OrdenesCompra.FirstAsync();
        ordenActualizada.SincronizadoErp.Should().BeTrue();
        ordenActualizada.ErpReferencia.Should().Be("ERP-COMPRA-99");

        notifications.Should().ContainSingle()
            .Which.Notificacion.Tipo.Should().Be("erp_sincronizado");
    }

    [Fact]
    public async Task ProcesarLote_AnulacionVentaExitosa_NotificaComoAnulacion()
    {
        var venta = SeedVenta();
        SeedMensajeVenta("AnulacionVenta", venta.Id, venta.NumeroVenta);

        _erpClient.ContabilizarVentaAsync(Arg.Any<VentaErpPayload>())
            .Returns(new ErpResponse(true, "ERP-ANU-1", null));

        var notifications = await _sut.ProcesarLoteAsync();

        var mensaje = await _db.ErpOutboxMessages.FirstAsync();
        mensaje.Estado.Should().Be(EstadoOutbox.Procesado);

        notifications.Should().ContainSingle()
            .Which.Notificacion.Titulo.Should().Be("Anulación contabilizada");
    }

    // ── Helpers de seeding ─────────────────────────────────────────────────────

    private Venta SeedVenta()
    {
        var venta = new Venta
        {
            NumeroVenta = "V-000001",
            EmpresaId = 1,
            SucursalId = SucursalId,
            CajaId = 1,
            Total = 50_000m,
            Estado = EstadoVenta.Completada,
            MetodoPago = MetodoPago.Efectivo,
        };
        _db.Ventas.Add(venta);
        _db.SaveChanges();
        return venta;
    }

    private OrdenCompra SeedOrdenCompra()
    {
        var orden = new OrdenCompra
        {
            NumeroOrden = "OC-000001",
            EmpresaId = 1,
            SucursalId = SucursalId,
            ProveedorId = 1,
            Total = 100_000m,
        };
        _db.OrdenesCompra.Add(orden);
        _db.SaveChanges();
        return orden;
    }

    private void SeedMensajeVenta(string tipo, int entidadId, string numeroVenta, int intentos = 0)
    {
        var payload = new VentaErpPayload(
            NumeroVenta: numeroVenta,
            NitCliente: null,
            MetodoPago: "Efectivo",
            FechaVenta: DateTime.UtcNow,
            SucursalId: SucursalId,
            Asientos: [new AsientoContableErp("4135", "CC1", "Credito", 50_000m, "test")],
            TotalOriginalDocumento: 50_000m);

        SeedMensajeRaw(tipo, entidadId, JsonSerializer.Serialize(payload), intentos);
    }

    private void SeedMensajeCompra(string tipo, int entidadId, string numeroOrden, int intentos = 0)
    {
        var payload = new CompraErpPayload(
            NumeroOrden: numeroOrden,
            NitProveedor: "900123456",
            FormaPago: "Contado",
            FechaVencimientoErp: DateTime.UtcNow.AddDays(30),
            FechaRecepcion: DateTime.UtcNow,
            SucursalId: SucursalId,
            Asientos: [new AsientoContableErp("1435", "CC1", "Debito", 100_000m, "test")],
            TotalOriginalDocumento: 100_000m);

        SeedMensajeRaw(tipo, entidadId, JsonSerializer.Serialize(payload), intentos);
    }

    private void SeedMensajeRaw(string tipo, int entidadId, string payload, int intentos = 0)
    {
        _db.ErpOutboxMessages.Add(new ErpOutboxMessage
        {
            TipoDocumento = tipo,
            EntidadId = entidadId,
            Payload = payload,
            Intentos = intentos,
            Estado = EstadoOutbox.Pendiente,
            FechaCreacion = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
