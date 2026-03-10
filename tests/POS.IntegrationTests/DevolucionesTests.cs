using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Devoluciones Parciales.
/// Verifica: devoluciones simples, múltiples devoluciones, validaciones de negocio,
/// restauración de inventario, y límites de tiempo.
/// </summary>
[Collection("POS")]
public class DevolucionesTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private int SucPP => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;
    private int TerceroId => _factory.TerceroTestId;

    public DevolucionesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProductoTest(string codigo, decimal precioVenta = 1000m, decimal precioCosto = 500m)
    {
        var dto = new
        {
            codigoBarras = codigo,
            nombre = $"Producto {codigo}",
            descripcion = "Test devoluciones",
            categoriaId = CatId,
            precioVenta,
            precioCosto
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task RegistrarEntradaInventario(Guid productoId, int sucursalId,
        decimal cantidad, decimal costoUnitario)
    {
        var dto = new
        {
            productoId,
            sucursalId,
            cantidad,
            costoUnitario,
            terceroId = TerceroId,
            referencia = $"FC-DEV-{Guid.NewGuid():N}"[..20],
            observaciones = "Entrada para test de devoluciones"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearYAbrirCaja(int sucursalId, string nombre, decimal montoApertura = 100_000m)
    {
        // Crear caja
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/Cajas", new
        {
            nombre,
            sucursalId
        });
        crearResponse.EnsureSuccessStatusCode();
        var caja = await crearResponse.Content.ReadFromJsonAsync<CajaDto>(_jsonOptions);
        var cajaId = caja!.Id;

        // Abrir caja
        var abrirResponse = await _client.PostAsJsonAsync($"/api/v1/Cajas/{cajaId}/abrir", new
        {
            montoApertura
        });
        abrirResponse.EnsureSuccessStatusCode();

        return cajaId;
    }

    private async Task<StockDto?> ObtenerStock(Guid productoId, int sucursalId)
    {
        var response = await _client.GetFromJsonAsync<List<StockDto>>(
            $"/api/v1/Inventario?productoId={productoId}&sucursalId={sucursalId}",
            _jsonOptions);
        return response?.FirstOrDefault();
    }

    private async Task<VentaDto> CrearVentaTest(
        int sucursalId,
        int cajaId,
        List<(Guid ProductoId, decimal Cantidad)> lineas,
        decimal montoPagado)
    {
        var dto = new
        {
            sucursalId,
            cajaId,
            clienteId = (int?)null,
            metodoPago = 0, // Efectivo
            montoPagado,
            observaciones = "Test devolución",
            lineas = lineas.Select(l => new
            {
                productoId = l.ProductoId,
                cantidad = l.Cantidad,
                precioUnitario = (decimal?)null,
                descuento = 0m
            }).ToList()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Ventas", dto);
        response.EnsureSuccessStatusCode();
        var venta = await response.Content.ReadFromJsonAsync<VentaDto>(_jsonOptions);
        return venta!;
    }

    // ═══════════════════════════════════════════════════════
    //  DEVOLUCIÓN PARCIAL SIMPLE
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionParcial_Simple_RestaurarStockCorrectamente()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-001-{Guid.NewGuid():N}"[..15], 1000m, 500m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 500m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev1-{Guid.NewGuid():N}"[..20]);

        // Verificar stock inicial
        var stockInicial = await ObtenerStock(producto, SucPP);
        stockInicial.Should().NotBeNull();
        stockInicial!.Cantidad.Should().Be(50);

        // Crear venta de 10 unidades
        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 10) }, 20_000m);

        // Verificar stock después de venta
        var stockDespuesVenta = await ObtenerStock(producto, SucPP);
        stockDespuesVenta!.Cantidad.Should().Be(40);

        // Obtener info de caja antes de devolución
        var cajaAntesDevolucion = await _client.GetFromJsonAsync<CajaDto>($"/api/v1/Cajas/{caja}", _jsonOptions);

        // Act: Devolver 3 unidades
        var devolucionDto = new
        {
            motivo = "Producto defectuoso",
            lineas = new[]
            {
                new { productoId = producto, cantidad = 3m }
            }
        };

        var responseDevolucion = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial",
            devolucionDto);

        // Assert
        responseDevolucion.StatusCode.Should().Be(HttpStatusCode.OK);
        var devolucion = await responseDevolucion.Content.ReadFromJsonAsync<DevolucionVentaDto>(_jsonOptions);

        devolucion.Should().NotBeNull();
        devolucion!.NumeroDevolucion.Should().StartWith("DEV-");
        devolucion.VentaId.Should().Be(venta.Id);
        devolucion.Motivo.Should().Be("Producto defectuoso");
        devolucion.Detalles.Should().HaveCount(1);
        devolucion.Detalles[0].CantidadDevuelta.Should().Be(3);
        devolucion.TotalDevuelto.Should().Be(3000); // 3 * 1000

        // Verificar stock restaurado
        var stockFinal = await ObtenerStock(producto, SucPP);
        stockFinal!.Cantidad.Should().Be(43); // 40 + 3

        // Verificar ajuste de caja
        var cajaDespuesDevolucion = await _client.GetFromJsonAsync<CajaDto>($"/api/v1/Cajas/{caja}", _jsonOptions);
        cajaDespuesDevolucion!.MontoActual.Should()
            .Be(cajaAntesDevolucion!.MontoActual - 3000); // Se restó el monto devuelto
    }

    // ═══════════════════════════════════════════════════════
    //  MÚLTIPLES DEVOLUCIONES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionParcial_MultiplesDevoluciones_NoExcederCantidad()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-002-{Guid.NewGuid():N}"[..15], 2000m, 1000m);
        await RegistrarEntradaInventario(producto, SucPP, 100, 1000m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev2-{Guid.NewGuid():N}"[..20]);

        // Vender 10 unidades
        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 10) }, 30_000m);

        // Act & Assert

        // Primera devolución: 4 unidades (OK)
        var devolucion1 = new
        {
            motivo = "Primera devolución",
            lineas = new[] { new { productoId = producto, cantidad = 4m } }
        };
        var response1 = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucion1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Segunda devolución: 5 unidades (OK - total 9)
        var devolucion2 = new
        {
            motivo = "Segunda devolución",
            lineas = new[] { new { productoId = producto, cantidad = 5m } }
        };
        var response2 = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucion2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Tercera devolución: 2 unidades (FALLA - solo queda 1)
        var devolucion3 = new
        {
            motivo = "Tercera devolución (excede)",
            lineas = new[] { new { productoId = producto, cantidad = 2m } }
        };
        var response3 = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucion3);
        response3.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response3.Content.ReadAsStringAsync();
        error.Should().Contain("No se puede devolver");
        error.Should().Contain("Ya devuelto: 9");

        // Verificar que solo se pueden devolver las devoluciones exitosas
        var devolucionesResponse = await _client.GetFromJsonAsync<List<DevolucionVentaDto>>(
            $"/api/v1/Ventas/{venta.Id}/devoluciones", _jsonOptions);
        devolucionesResponse.Should().HaveCount(2);
        devolucionesResponse!.Sum(d => d.Detalles[0].CantidadDevuelta).Should().Be(9);
    }

    // ═══════════════════════════════════════════════════════
    //  VALIDACIONES DE NEGOCIO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionParcial_VentaAnulada_Rechaza()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-003-{Guid.NewGuid():N}"[..15], 1500m, 750m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 750m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev3-{Guid.NewGuid():N}"[..20]);

        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 5) }, 10_000m);

        // Anular la venta
        var anularResponse = await _client.PostAsync(
            $"/api/v1/Ventas/{venta.Id}/anular?motivo=Test", null);
        anularResponse.EnsureSuccessStatusCode();

        // Act: Intentar devolver producto de venta anulada
        var devolucionDto = new
        {
            motivo = "Intentando devolver venta anulada",
            lineas = new[] { new { productoId = producto, cantidad = 2m } }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("completadas");
    }

    [Fact]
    public async Task DevolucionParcial_ProductoNoEnVenta_Rechaza()
    {
        // Arrange
        var producto1 = await CrearProductoTest($"DEV-004A-{Guid.NewGuid():N}"[..15], 1000m, 500m);
        var producto2 = await CrearProductoTest($"DEV-004B-{Guid.NewGuid():N}"[..15], 1000m, 500m);

        await RegistrarEntradaInventario(producto1, SucPP, 50, 500m);
        await RegistrarEntradaInventario(producto2, SucPP, 50, 500m);

        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev4-{Guid.NewGuid():N}"[..20]);

        // Vender solo producto1
        var venta = await CrearVentaTest(SucPP, caja, new() { (producto1, 5) }, 10_000m);

        // Act: Intentar devolver producto2 (no está en la venta)
        var devolucionDto = new
        {
            motivo = "Producto incorrecto",
            lineas = new[] { new { productoId = producto2, cantidad = 2m } }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("no está en la venta original");
    }

    [Fact]
    public async Task DevolucionParcial_CantidadExcedida_Rechaza()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-005-{Guid.NewGuid():N}"[..15], 1000m, 500m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 500m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev5-{Guid.NewGuid():N}"[..20]);

        // Vender 5 unidades
        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 5) }, 10_000m);

        // Act: Intentar devolver 6 unidades (más de lo vendido)
        var devolucionDto = new
        {
            motivo = "Cantidad excedida",
            lineas = new[] { new { productoId = producto, cantidad = 6m } }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("No se puede devolver");
        error.Should().Contain("Disponible: 5");
    }

    [Fact]
    public async Task DevolucionParcial_MotivoVacio_Rechaza()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-006-{Guid.NewGuid():N}"[..15], 1000m, 500m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 500m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev6-{Guid.NewGuid():N}"[..20]);

        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 5) }, 10_000m);

        // Act: Intentar devolver sin motivo
        var devolucionDto = new
        {
            motivo = "",
            lineas = new[] { new { productoId = producto, cantidad = 2m } }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("motivo");
    }

    // ═══════════════════════════════════════════════════════
    //  CONSULTAS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ObtenerDevolucionesPorVenta_DevuelveListaCompleta()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-007-{Guid.NewGuid():N}"[..15], 1000m, 500m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 500m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev7-{Guid.NewGuid():N}"[..20]);

        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 10) }, 15_000m);

        // Crear dos devoluciones
        var dev1 = new
        {
            motivo = "Primera devolución",
            lineas = new[] { new { productoId = producto, cantidad = 2m } }
        };
        await _client.PostAsJsonAsync($"/api/v1/Ventas/{venta.Id}/devolucion-parcial", dev1);

        var dev2 = new
        {
            motivo = "Segunda devolución",
            lineas = new[] { new { productoId = producto, cantidad = 3m } }
        };
        await _client.PostAsJsonAsync($"/api/v1/Ventas/{venta.Id}/devolucion-parcial", dev2);

        // Act
        var response = await _client.GetFromJsonAsync<List<DevolucionVentaDto>>(
            $"/api/v1/Ventas/{venta.Id}/devoluciones", _jsonOptions);

        // Assert
        response.Should().NotBeNull();
        response.Should().HaveCount(2);
        response!.Sum(d => d.TotalDevuelto).Should().Be(5000); // 2000 + 3000
    }

    [Fact]
    public async Task ObtenerDevolucion_PorId_DevuelveDetalle()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-008-{Guid.NewGuid():N}"[..15], 1500m, 750m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 750m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev8-{Guid.NewGuid():N}"[..20]);

        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 8) }, 15_000m);

        var devolucionDto = new
        {
            motivo = "Test consulta individual",
            lineas = new[] { new { productoId = producto, cantidad = 3m } }
        };

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);
        var devolucionCreada = await createResponse.Content.ReadFromJsonAsync<DevolucionVentaDto>(_jsonOptions);

        // Act
        var response = await _client.GetFromJsonAsync<DevolucionVentaDto>(
            $"/api/v1/Ventas/devoluciones/{devolucionCreada!.Id}", _jsonOptions);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().Be(devolucionCreada.Id);
        response.NumeroDevolucion.Should().Be(devolucionCreada.NumeroDevolucion);
        response.Motivo.Should().Be("Test consulta individual");
        response.TotalDevuelto.Should().Be(4500); // 3 * 1500
        response.Detalles.Should().HaveCount(1);
        response.Detalles[0].CantidadDevuelta.Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════
    //  VERIFICACIÓN DE COSTOS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionParcial_UsaCostoOriginalVenta()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-009-{Guid.NewGuid():N}"[..15], 2000m, 800m);
        await RegistrarEntradaInventario(producto, SucPP, 50, 800m); // Costo inicial 800
        var caja = await CrearYAbrirCaja(SucPP, $"CajaDev9-{Guid.NewGuid():N}"[..20]);

        // Vender con costo 800
        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 10) }, 25_000m);

        // Cambiar el costo en inventario (nueva entrada con costo diferente)
        await RegistrarEntradaInventario(producto, SucPP, 20, 1200m); // Nuevo costo 1200

        // Act: Devolver producto
        var devolucionDto = new
        {
            motivo = "Test costo original",
            lineas = new[] { new { productoId = producto, cantidad = 3m } }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var devolucion = await response.Content.ReadFromJsonAsync<DevolucionVentaDto>(_jsonOptions);

        // Verificar que el detalle registró el costo original (800), no el nuevo (1200)
        devolucion.Should().NotBeNull();
        devolucion!.Detalles[0].CostoUnitario.Should().Be(800m);
    }

    // ═══════════════════════════════════════════════════════
    //  NOTA CRÉDITO CONTABLE — INTEGRACIÓN ERP
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionParcial_GeneraNotaCreditoContableYOutbox()
    {
        // Arrange
        var producto = await CrearProductoTest($"DEV-NC-001-{Guid.NewGuid():N}"[..15], 2000m, 1000m);
        await RegistrarEntradaInventario(producto, SucPP, 100, 1000m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaNC1-{Guid.NewGuid():N}"[..20]);

        var venta = await CrearVentaTest(SucPP, caja, new() { (producto, 20) }, 50_000m);

        // Act: Devolver 5 unidades
        var devolucionDto = new
        {
            motivo = "Productos defectuosos",
            lineas = new[] { new { productoId = producto, cantidad = 5m } }
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var devolucion = await response.Content.ReadFromJsonAsync<DevolucionVentaDto>(_jsonOptions);
        devolucion.Should().NotBeNull();

        // Assert - Verificar DocumentoContable (Nota Crédito)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        var docs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.DocumentosContables
                .Include(d => d.Detalles)
                .Where(d => d.TipoDocumento == "NotaCredito"
                         && d.NumeroSoporte == devolucion!.NumeroDevolucion));

        docs.Should().HaveCount(1, "Debe crear un DocumentoContable tipo NotaCredito");
        var doc = docs.Single();
        doc.Detalles.Should().NotBeEmpty();
        doc.TotalDebito.Should().Be(doc.TotalCredito, "Partida doble: Débitos = Créditos");

        // Verificar OutboxMessage
        var outbox = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.ErpOutboxMessages
                .Where(m => m.TipoDocumento == "NotaCreditoVenta"
                         && m.EntidadId == devolucion!.Id));

        outbox.Should().NotBeNull("Debe encolar un mensaje outbox para la nota crédito");
        outbox!.Estado.Should().Be(POS.Infrastructure.Data.Entities.EstadoOutbox.Pendiente);

        // Verificar payload de asientos
        var payload = JsonSerializer.Deserialize<POS.Application.DTOs.CompraErpPayload>(
            outbox.Payload, _jsonOptions);
        payload.Should().NotBeNull();
        payload!.NumeroOrden.Should().Be(devolucion!.NumeroDevolucion);

        // Débitos: Reversión Ingreso + Reversión IVA
        var debitos = payload.Asientos.Where(a => a.Naturaleza == "Debito").ToList();
        debitos.Should().NotBeEmpty("Debe tener débitos de reversión de ingresos");

        // Crédito: Reembolso caja
        var creditos = payload.Asientos.Where(a => a.Naturaleza == "Credito").ToList();
        creditos.Should().ContainSingle("Debe tener 1 crédito de reembolso a caja");

        // Total devuelto: 5 × 2000 = $10,000
        payload.TotalOriginalDocumento.Should().Be(10000m);
    }

    [Fact]
    public async Task DevolucionParcial_ConIVA_AsientosNotaCreditoCuadran()
    {
        // Arrange - Producto con IVA 19%
        var productoId = await CrearProductoTest($"DEV-NC-IVA-{Guid.NewGuid():N}"[..15], 5000m, 2500m);
        await RegistrarEntradaInventario(productoId, SucPP, 50, 2500m);
        var caja = await CrearYAbrirCaja(SucPP, $"CajaNCIVA-{Guid.NewGuid():N}"[..20]);

        // Venta con IVA (el TaxEngine calcula IVA automáticamente si el producto tiene impuesto)
        var venta = await CrearVentaTest(SucPP, caja, new() { (productoId, 10) }, 100_000m);

        // Act: Devolver 4 unidades
        var devolucionDto = new
        {
            motivo = "Cliente insatisfecho",
            lineas = new[] { new { productoId, cantidad = 4m } }
        };
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{venta.Id}/devolucion-parcial", devolucionDto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var devolucion = await response.Content.ReadFromJsonAsync<DevolucionVentaDto>(_jsonOptions);

        // Assert - Partida doble en el payload ERP
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        var outbox = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(
            db.ErpOutboxMessages
                .Where(m => m.TipoDocumento == "NotaCreditoVenta"
                         && m.EntidadId == devolucion!.Id));

        var payload = JsonSerializer.Deserialize<POS.Application.DTOs.CompraErpPayload>(
            outbox.Payload, _jsonOptions)!;

        var totalDebitos = payload.Asientos.Where(a => a.Naturaleza == "Debito").Sum(a => a.Valor);
        var totalCreditos = payload.Asientos.Where(a => a.Naturaleza == "Credito").Sum(a => a.Valor);

        totalDebitos.Should().Be(totalCreditos,
            "La partida doble exige que Débitos = Créditos en la nota crédito");

        // Los asientos deben incluir al menos el centro de costo de la sucursal
        payload.Asientos.Should().OnlyContain(
            a => !string.IsNullOrEmpty(a.CentroCosto),
            "Todos los asientos deben tener centro de costo");
    }
}
