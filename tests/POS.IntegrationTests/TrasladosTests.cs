using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Infrastructure.Data.Entities;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para traslados de inventario entre sucursales
/// </summary>
[Collection("POS")]
public class TrasladosTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // IDs de sucursales
    private int SucPP => _factory.SucursalPPId;
    private int SucFIFO => _factory.SucursalFIFOId;
    private int SucLIFO => _factory.SucursalLIFOId;
    private int CatId => _factory.CategoriaTestId;
    private int TerceroId => _factory.TerceroTestId;

    public TrasladosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProductoTest(string codigo)
    {
        var dto = new
        {
            codigoBarras = codigo,
            nombre = $"Producto {codigo}",
            descripcion = "Test traslados",
            categoriaId = CatId,
            precioVenta = 1000m,
            precioCosto = 500m
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task RegistrarEntrada(Guid productoId, int sucursalId,
        decimal cantidad, decimal costoUnitario, string referencia)
    {
        var dto = new
        {
            productoId,
            sucursalId,
            cantidad,
            costoUnitario,
            porcentajeImpuesto = 0m,
            terceroId = TerceroId,
            referencia,
            observaciones = $"Test entrada {referencia}"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Entrada {referencia} deberia ser exitosa");
    }

    private async Task<StockDto?> ObtenerStock(Guid productoId, int sucursalId)
    {
        var response = await _client.GetFromJsonAsync<List<StockDto>>(
            $"/api/v1/Inventario?productoId={productoId}&sucursalId={sucursalId}",
            _jsonOptions);
        return response?.FirstOrDefault();
    }

    private async Task<JsonElement> CrearTraslado(int sucursalOrigenId, int sucursalDestinoId,
        List<(Guid productoId, decimal cantidad)> lineas, string? observaciones = null)
    {
        var dto = new
        {
            sucursalOrigenId,
            sucursalDestinoId,
            observaciones,
            lineas = lineas.Select(l => new
            {
                productoId = l.productoId,
                cantidad = l.cantidad,
                observaciones = (string?)null
            }).ToList()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Traslados", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Crear traslado deberia ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<JsonElement> EnviarTraslado(int trasladoId)
    {
        var response = await _client.PostAsync($"/api/v1/Traslados/{trasladoId}/enviar", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Enviar traslado deberia ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<JsonElement> RecibirTraslado(int trasladoId,
        List<(Guid productoId, decimal cantidadRecibida)> lineas)
    {
        var dto = new
        {
            lineas = lineas.Select(l => new
            {
                productoId = l.productoId,
                cantidadRecibida = l.cantidadRecibida,
                observaciones = (string?)null
            }).ToList(),
            observaciones = (string?)null
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/Traslados/{trasladoId}/recibir", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Recibir traslado deberia ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<TrasladoDto?> ObtenerTraslado(int trasladoId)
    {
        return await _client.GetFromJsonAsync<TrasladoDto>(
            $"/api/v1/Traslados/{trasladoId}",
            _jsonOptions);
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS DE TRASLADOS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Traslado_Completo_StockTransferidoCorrectamente()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-001");
        await RegistrarEntrada(productoId, SucPP, 100, 50, "FC-TRAS-001");

        var stockOrigenInicial = await ObtenerStock(productoId, SucPP);
        stockOrigenInicial!.Cantidad.Should().Be(100);

        // Act - Crear traslado
        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 30) },
            "Traslado de prueba");

        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();

        // Verificar traslado creado en estado Pendiente
        var trasladoCreado = await ObtenerTraslado(trasladoId);
        trasladoCreado.Should().NotBeNull();
        trasladoCreado!.Estado.Should().Be("Pendiente");

        // Act - Enviar traslado
        await EnviarTraslado(trasladoId);

        // Verificar estado EnTransito y stock origen disminuido
        var trasladoEnviado = await ObtenerTraslado(trasladoId);
        trasladoEnviado!.Estado.Should().Be("EnTransito");

        var stockOrigenDespuesEnvio = await ObtenerStock(productoId, SucPP);
        stockOrigenDespuesEnvio!.Cantidad.Should().Be(70, "Origen debe tener 100 - 30 = 70");

        // Act - Recibir traslado
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 30) });

        // Verificar estado Recibido y stock destino aumentado
        var trasladoRecibido = await ObtenerTraslado(trasladoId);
        trasladoRecibido!.Estado.Should().Be("Recibido");

        var stockDestinoDespuesRecepcion = await ObtenerStock(productoId, SucFIFO);
        stockDestinoDespuesRecepcion!.Cantidad.Should().Be(30, "Destino debe tener 0 + 30 = 30");

        // Verificar stock origen no cambió
        var stockOrigenFinal = await ObtenerStock(productoId, SucPP);
        stockOrigenFinal!.Cantidad.Should().Be(70, "Origen debe mantenerse en 70");
    }

    [Fact]
    public async Task Traslado_StockInsuficiente_Rechaza()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-002");
        await RegistrarEntrada(productoId, SucPP, 10, 50, "FC-TRAS-002");

        // Act & Assert - Intentar crear traslado con más stock del disponible
        var dto = new
        {
            sucursalOrigenId = SucPP,
            sucursalDestinoId = SucFIFO,
            observaciones = "Traslado que debe fallar",
            lineas = new[]
            {
                new { productoId, cantidad = 20m, observaciones = (string?)null }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Traslados", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Debe rechazar traslado con stock insuficiente");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("insuficiente", "El mensaje debe indicar stock insuficiente");
    }

    [Fact]
    public async Task Traslado_SucursalesIguales_Rechaza()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-003");
        await RegistrarEntrada(productoId, SucPP, 100, 50, "FC-TRAS-003");

        // Act & Assert - Intentar crear traslado con origen == destino
        var dto = new
        {
            sucursalOrigenId = SucPP,
            sucursalDestinoId = SucPP,  // Misma sucursal
            observaciones = "Traslado que debe fallar",
            lineas = new[]
            {
                new { productoId, cantidad = 10m, observaciones = (string?)null }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Traslados", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Debe rechazar traslado con sucursales iguales");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("diferentes", "El mensaje debe indicar que las sucursales deben ser diferentes");
    }

    [Fact]
    public async Task Traslado_RecepcionParcial_PermiteDisminuir()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-004");
        await RegistrarEntrada(productoId, SucPP, 100, 50, "FC-TRAS-004");

        // Act - Crear y enviar traslado de 20 unidades
        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 20) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);

        // Verificar stock origen disminuyó
        var stockOrigen = await ObtenerStock(productoId, SucPP);
        stockOrigen!.Cantidad.Should().Be(80, "Origen: 100 - 20 = 80");

        // Act - Recibir solo 15 unidades (mercancía dañada)
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 15) });

        // Assert - Verificar cantidades
        var trasladoFinal = await ObtenerTraslado(trasladoId);
        trasladoFinal!.Detalles[0].CantidadSolicitada.Should().Be(20);
        trasladoFinal.Detalles[0].CantidadRecibida.Should().Be(15);

        var stockOrigenFinal = await ObtenerStock(productoId, SucPP);
        stockOrigenFinal!.Cantidad.Should().Be(80, "Origen: se quedó con -20 aunque solo llegaron 15");

        var stockDestinoFinal = await ObtenerStock(productoId, SucFIFO);
        stockDestinoFinal!.Cantidad.Should().Be(15, "Destino: solo recibió 15");
    }

    [Fact]
    public async Task Traslado_RecepcionExcedida_Rechaza()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-005");
        await RegistrarEntrada(productoId, SucPP, 100, 50, "FC-TRAS-005");

        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 10) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);

        // Act & Assert - Intentar recibir más de lo solicitado
        var dto = new
        {
            lineas = new[]
            {
                new { productoId, cantidadRecibida = 15m, observaciones = (string?)null }
            },
            observaciones = (string?)null
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/Traslados/{trasladoId}/recibir", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "No debe permitir recibir más de lo solicitado");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("exceder", "El mensaje debe indicar que no puede exceder la cantidad solicitada");
    }

    [Fact]
    public async Task Traslado_Cancelar_EstadoCorrecto()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-006");
        await RegistrarEntrada(productoId, SucPP, 100, 50, "FC-TRAS-006");

        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 10) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();

        var stockInicial = await ObtenerStock(productoId, SucPP);
        stockInicial!.Cantidad.Should().Be(100);

        // Act - Cancelar traslado
        var dtoCancel = new { motivo = "Cancelación de prueba" };
        var response = await _client.PostAsJsonAsync($"/api/v1/Traslados/{trasladoId}/cancelar", dtoCancel);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var trasladoCancelado = await ObtenerTraslado(trasladoId);
        trasladoCancelado!.Estado.Should().Be("Cancelado");

        // Stock no debe cambiar
        var stockFinal = await ObtenerStock(productoId, SucPP);
        stockFinal!.Cantidad.Should().Be(100, "Stock no debe cambiar al cancelar en Pendiente");
    }

    [Fact]
    public async Task Traslado_Rechazar_RevierteSalida()
    {
        // Arrange
        var productoId = await CrearProductoTest("TRAS-007");
        await RegistrarEntrada(productoId, SucPP, 100, 50, "FC-TRAS-007");

        // Act - Crear y enviar traslado
        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 30) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);

        var stockDespuesEnvio = await ObtenerStock(productoId, SucPP);
        stockDespuesEnvio!.Cantidad.Should().Be(70, "Después de enviar: 100 - 30 = 70");

        // Act - Rechazar traslado
        var dtoRechazo = new { motivoRechazo = "Mercancía dañada en tránsito" };
        var response = await _client.PostAsJsonAsync($"/api/v1/Traslados/{trasladoId}/rechazar", dtoRechazo);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Stock debe restaurarse
        var trasladoRechazado = await ObtenerTraslado(trasladoId);
        trasladoRechazado!.Estado.Should().Be("Rechazado");
        trasladoRechazado.MotivoRechazo.Should().Contain("dañada");

        var stockRestaurado = await ObtenerStock(productoId, SucPP);
        stockRestaurado!.Cantidad.Should().Be(100, "Stock debe restaurarse: 70 + 30 = 100");
    }

    [Fact]
    public async Task Traslado_CostoOriginal_SePreserva()
    {
        // Arrange - Crear producto con costo $500 en sucursal origen
        var productoId = await CrearProductoTest("TRAS-008");
        await RegistrarEntrada(productoId, SucPP, 100, 500, "FC-TRAS-008");

        var stockOrigen = await ObtenerStock(productoId, SucPP);
        stockOrigen!.CostoPromedio.Should().Be(500);

        // Act - Crear, enviar y recibir traslado
        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 25) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 25) });

        // Assert - Verificar que el costo se preservó en destino
        var traslado = await ObtenerTraslado(trasladoId);
        traslado!.Detalles[0].CostoUnitario.Should().Be(500, "Costo debe ser el del origen");

        var stockDestino = await ObtenerStock(productoId, SucFIFO);
        stockDestino.Should().NotBeNull();
        stockDestino!.Cantidad.Should().Be(25);
        stockDestino.CostoPromedio.Should().Be(500, "Costo promedio en destino debe ser $500");
    }

    [Fact]
    public async Task Traslado_MultipleProductos_TodosSeTransfieren()
    {
        // Arrange - Crear dos productos
        var producto1Id = await CrearProductoTest("TRAS-009A");
        var producto2Id = await CrearProductoTest("TRAS-009B");

        await RegistrarEntrada(producto1Id, SucPP, 100, 50, "FC-TRAS-009A");
        await RegistrarEntrada(producto2Id, SucPP, 200, 75, "FC-TRAS-009B");

        // Act - Crear traslado con ambos productos
        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)>
            {
                (producto1Id, 20),
                (producto2Id, 30)
            });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();

        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)>
            {
                (producto1Id, 20),
                (producto2Id, 30)
            });

        // Assert - Verificar ambos productos
        var stock1Origen = await ObtenerStock(producto1Id, SucPP);
        stock1Origen!.Cantidad.Should().Be(80);

        var stock1Destino = await ObtenerStock(producto1Id, SucFIFO);
        stock1Destino!.Cantidad.Should().Be(20);

        var stock2Origen = await ObtenerStock(producto2Id, SucPP);
        stock2Origen!.Cantidad.Should().Be(170);

        var stock2Destino = await ObtenerStock(producto2Id, SucFIFO);
        stock2Destino!.Cantidad.Should().Be(30);
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS DE TRASLADOS ENTRE DIFERENTES MÉTODOS DE COSTEO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Traslado_DesdeFIFO_APromedioPonderado_ConsumeLoteMasAntiguo()
    {
        // Arrange - Crear producto con múltiples lotes en sucursal FIFO
        var productoId = await CrearProductoTest("TRAS-010");

        // Entrada 1: 50 unidades a $100 (más antiguo)
        await RegistrarEntrada(productoId, SucFIFO, 50, 100, "FC-FIFO-001");

        // Entrada 2: 50 unidades a $200 (más reciente)
        await RegistrarEntrada(productoId, SucFIFO, 50, 200, "FC-FIFO-002");

        var stockOrigen = await ObtenerStock(productoId, SucFIFO);
        stockOrigen!.Cantidad.Should().Be(100);

        // Act - Trasladar 30 unidades a sucursal Promedio Ponderado
        var resultCrear = await CrearTraslado(SucFIFO, SucPP,
            new List<(Guid, decimal)> { (productoId, 30) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 30) });

        // Assert - FIFO debe consumir del lote más antiguo ($100)
        var traslado = await ObtenerTraslado(trasladoId);
        traslado!.Detalles[0].CostoUnitario.Should().Be(100,
            "FIFO debe consumir del lote más antiguo (costo $100)");

        // Destino (Promedio Ponderado) debe recibir con costo $100
        var stockDestino = await ObtenerStock(productoId, SucPP);
        stockDestino!.Cantidad.Should().Be(30);
        stockDestino.CostoPromedio.Should().Be(100,
            "Promedio Ponderado con una sola entrada debe ser $100");
    }

    [Fact]
    public async Task Traslado_DesdeLIFO_APromedioPonderado_ConsumeLoteMasReciente()
    {
        // Arrange - Crear producto con múltiples lotes en sucursal LIFO
        var productoId = await CrearProductoTest("TRAS-011");

        // Entrada 1: 50 unidades a $100 (más antiguo)
        await RegistrarEntrada(productoId, SucLIFO, 50, 100, "FC-LIFO-001");

        // Entrada 2: 50 unidades a $200 (más reciente)
        await RegistrarEntrada(productoId, SucLIFO, 50, 200, "FC-LIFO-002");

        var stockOrigen = await ObtenerStock(productoId, SucLIFO);
        stockOrigen!.Cantidad.Should().Be(100);

        // Act - Trasladar 30 unidades a sucursal Promedio Ponderado
        var resultCrear = await CrearTraslado(SucLIFO, SucPP,
            new List<(Guid, decimal)> { (productoId, 30) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 30) });

        // Assert - LIFO debe consumir del lote más reciente ($200)
        var traslado = await ObtenerTraslado(trasladoId);
        traslado!.Detalles[0].CostoUnitario.Should().Be(200,
            "LIFO debe consumir del lote más reciente (costo $200)");

        // Destino (Promedio Ponderado) debe recibir con costo $200
        var stockDestino = await ObtenerStock(productoId, SucPP);
        stockDestino!.Cantidad.Should().Be(30);
        stockDestino.CostoPromedio.Should().Be(200,
            "Promedio Ponderado con una sola entrada debe ser $200");
    }

    [Fact]
    public async Task Traslado_DesdePromedioPonderado_AFIFO_IntegraCostoCorrectamente()
    {
        // Arrange - Crear inventario en Promedio Ponderado con costo promedio $150
        var productoId = await CrearProductoTest("TRAS-012");

        await RegistrarEntrada(productoId, SucPP, 50, 100, "FC-PP-001");
        await RegistrarEntrada(productoId, SucPP, 50, 200, "FC-PP-002");
        // Promedio: (50*100 + 50*200) / 100 = 150

        var stockOrigen = await ObtenerStock(productoId, SucPP);
        stockOrigen!.CostoPromedio.Should().BeApproximately(150m, 0.01m);

        // Act - Trasladar 40 unidades a sucursal FIFO
        var resultCrear = await CrearTraslado(SucPP, SucFIFO,
            new List<(Guid, decimal)> { (productoId, 40) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 40) });

        // Assert - Debe transferir con costo promedio $150
        var traslado = await ObtenerTraslado(trasladoId);
        traslado!.Detalles[0].CostoUnitario.Should().BeApproximately(150m, 0.01m,
            "Debe transferir con el costo promedio de la sucursal origen");

        // Destino FIFO debe crear un nuevo lote con costo $150
        var stockDestino = await ObtenerStock(productoId, SucFIFO);
        stockDestino!.Cantidad.Should().Be(40);
        stockDestino.CostoPromedio.Should().BeApproximately(150m, 0.01m);
    }

    [Fact]
    public async Task Traslado_DesdeFIFO_ALIFO_ConMultiplesLotes_ConsumeLoteCorrectoPorMetodo()
    {
        // Arrange - Crear inventario en FIFO
        var productoId = await CrearProductoTest("TRAS-013");

        await RegistrarEntrada(productoId, SucFIFO, 30, 100, "FC-FIFO-001"); // Más antiguo
        await RegistrarEntrada(productoId, SucFIFO, 30, 150, "FC-FIFO-002"); // Medio
        await RegistrarEntrada(productoId, SucFIFO, 30, 200, "FC-FIFO-003"); // Más reciente

        // Act - Trasladar 50 unidades de FIFO a LIFO
        // FIFO debe consumir: 30 del primero ($100) + 20 del segundo ($150)
        var resultCrear = await CrearTraslado(SucFIFO, SucLIFO,
            new List<(Guid, decimal)> { (productoId, 50) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 50) });

        // Assert - Costo debe ser promedio ponderado de los lotes consumidos
        // (30*100 + 20*150) / 50 = 6000 / 50 = 120
        var traslado = await ObtenerTraslado(trasladoId);
        traslado!.Detalles[0].CostoUnitario.Should().BeApproximately(120m, 0.01m,
            "FIFO consume 30@$100 + 20@$150 = costo promedio $120");

        var stockDestino = await ObtenerStock(productoId, SucLIFO);
        stockDestino!.Cantidad.Should().Be(50);
        stockDestino.CostoPromedio.Should().BeApproximately(120m, 0.01m);
    }

    [Fact]
    public async Task Traslado_EntreMetodosDiferentes_MultiplesTrasladosAcumulan()
    {
        // Arrange - Producto en sucursal FIFO
        var productoId = await CrearProductoTest("TRAS-014");
        await RegistrarEntrada(productoId, SucFIFO, 100, 100, "FC-FIFO-001");

        // Producto YA existe en sucursal Promedio Ponderado con costo diferente
        await RegistrarEntrada(productoId, SucPP, 50, 200, "FC-PP-001");

        var stockDestinoInicial = await ObtenerStock(productoId, SucPP);
        stockDestinoInicial!.CostoPromedio.Should().Be(200);
        stockDestinoInicial.Cantidad.Should().Be(50);

        // Act - Trasladar 30 unidades de FIFO ($100) a Promedio Ponderado
        var resultCrear = await CrearTraslado(SucFIFO, SucPP,
            new List<(Guid, decimal)> { (productoId, 30) });
        var trasladoId = resultCrear.GetProperty("trasladoId").GetInt32();
        await EnviarTraslado(trasladoId);
        await RecibirTraslado(trasladoId,
            new List<(Guid, decimal)> { (productoId, 30) });

        // Assert - Promedio Ponderado debe recalcular correctamente
        // Stock anterior: 50 @ $200 = $10,000
        // Traslado: 30 @ $100 = $3,000
        // Nuevo promedio: $13,000 / 80 = $162.50
        var stockDestinoFinal = await ObtenerStock(productoId, SucPP);
        stockDestinoFinal!.Cantidad.Should().Be(80);
        stockDestinoFinal.CostoPromedio.Should().BeApproximately(162.50m, 0.01m,
            "Promedio Ponderado debe recalcular: (50*200 + 30*100) / 80 = 162.50");
    }
}
