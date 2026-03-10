using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Infrastructure.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Data;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Compras.
/// Verifica: creación, aprobación, recepción (completa/parcial), rechazo, cancelación,
/// integración con inventario, lotes y múltiples proveedores.
/// </summary>
[Collection("POS")]
public class ComprasTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // IDs de sucursales y categorías
    private int SucPP => _factory.SucursalPPId;
    private int SucFIFO => _factory.SucursalFIFOId;
    private int CatId => _factory.CategoriaTestId;
    private int TerceroId => _factory.TerceroTestId;

    public ComprasTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────

    private async Task<Guid> CrearProductoTest(string codigo, decimal precioVenta = 1000m)
    {
        var dto = new
        {
            codigoBarras = codigo,
            nombre = $"Producto {codigo}",
            descripcion = "Test compras",
            categoriaId = CatId,
            precioVenta,
            precioCosto = precioVenta * 0.6m
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductoDto>(_jsonOptions);
        return result!.Id;
    }

    private async Task<int> CrearProveedorTest(string nombre)
    {
        var dto = new
        {
            tipoIdentificacion = "NIT",
            identificacion = $"NIT-{Guid.NewGuid():N}"[..15],
            nombre,
            tipoTercero = "Proveedor",
            telefono = "3001234567",
            email = $"{nombre.ToLower().Replace(" ", "")}@test.com",
            direccion = "Calle Test 123",
            ciudad = (string?)null
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Terceros", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return result.GetProperty("id").GetInt32();
    }

    private async Task<JsonElement> CrearOrdenCompra(
        int sucursalId,
        int proveedorId,
        List<(Guid productoId, decimal cantidad, decimal precioUnitario, decimal impuesto)> lineas,
        DateTime? fechaEntrega = null,
        string? observaciones = null)
    {
        var dto = new
        {
            sucursalId,
            proveedorId,
            fechaEntregaEsperada = fechaEntrega,
            observaciones,
            lineas = lineas.Select(l => new
            {
                productoId = l.productoId,
                cantidad = l.cantidad,
                precioUnitario = l.precioUnitario,
                porcentajeImpuesto = l.impuesto
            }).ToList()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Compras", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Crear orden debería ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<OrdenCompraDto?> ObtenerOrdenCompra(int ordenId)
    {
        return await _client.GetFromJsonAsync<OrdenCompraDto>(
            $"/api/v1/Compras/{ordenId}",
            _jsonOptions);
    }

    private async Task<JsonElement> AprobarOrdenCompra(int ordenId, string? observaciones = null)
    {
        var dto = new { observaciones };
        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/aprobar", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Aprobar orden debería ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<JsonElement> RecibirOrdenCompra(
        int ordenId,
        List<(Guid productoId, decimal cantidadRecibida, string? observaciones)> lineas)
    {
        var dto = new
        {
            lineas = lineas.Select(l => new
            {
                productoId = l.productoId,
                cantidadRecibida = l.cantidadRecibida,
                observaciones = l.observaciones
            }).ToList()
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/recibir", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Recibir orden debería ser exitoso. Body: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    }

    private async Task<StockDto?> ObtenerStock(Guid productoId, int sucursalId)
    {
        var response = await _client.GetFromJsonAsync<List<StockDto>>(
            $"/api/v1/Inventario?productoId={productoId}&sucursalId={sucursalId}",
            _jsonOptions);
        return response?.FirstOrDefault();
    }


    // ═══════════════════════════════════════════════════════
    //  TESTS DE COMPRAS - FLUJO COMPLETO
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task OrdenCompra_FlujoCompleto_StockActualizado()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-001", 2000m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 001");

        var stockInicial = await ObtenerStock(productoId, SucPP);
        var cantidadInicial = stockInicial?.Cantidad ?? 0;

        // Act - Crear orden de compra
        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 100, 500m, 19)  // 100 unidades a $500 + 19% IVA
            },
            DateTime.UtcNow.AddDays(7),
            "Orden de prueba flujo completo");

        var ordenId = resultCrear.GetProperty("id").GetInt32();

        // Verificar orden creada en estado Pendiente
        var ordenCreada = await ObtenerOrdenCompra(ordenId);
        ordenCreada.Should().NotBeNull();
        ordenCreada!.Estado.Should().Be("Pendiente");
        ordenCreada.NumeroOrden.Should().StartWith("OC-");
        ordenCreada.Subtotal.Should().Be(50000m);  // 100 × 500
        ordenCreada.Impuestos.Should().Be(9500m);   // 50000 × 0.19
        ordenCreada.Total.Should().Be(59500m);      // 50000 + 9500

        // Act - Aprobar orden
        await AprobarOrdenCompra(ordenId, "Aprobado por gerencia");

        var ordenAprobada = await ObtenerOrdenCompra(ordenId);
        ordenAprobada!.Estado.Should().Be("Aprobada");
        ordenAprobada.FechaAprobacion.Should().NotBeNull();

        // Act - Recibir mercancía completa
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)>
            {
                (productoId, 100, "Producto en buen estado")
            });

        // Assert - Verificar orden recibida
        var ordenRecibida = await ObtenerOrdenCompra(ordenId);
        ordenRecibida!.Estado.Should().Be("RecibidaCompleta");
        ordenRecibida.FechaRecepcion.Should().NotBeNull();
        ordenRecibida.Detalles[0].CantidadRecibida.Should().Be(100);

        // Assert - Verificar stock actualizado
        var stockFinal = await ObtenerStock(productoId, SucPP);
        stockFinal.Should().NotBeNull();
        stockFinal!.Cantidad.Should().Be(cantidadInicial + 100);

        // Verificar costo promedio actualizado con la compra
        stockFinal.CostoPromedio.Should().BeGreaterThan(0, "El costo promedio debe actualizarse");
    }

    [Fact]
    public async Task OrdenCompra_RecepcionParcial_EstadoCorrecto()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-002", 1500m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 002");

        var stockInicial = await ObtenerStock(productoId, SucPP);
        var cantidadInicial = stockInicial?.Cantidad ?? 0;

        // Act - Crear y aprobar orden de 200 unidades
        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 200, 400m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);

        // Act - Recibir solo 120 unidades (recepción parcial)
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)>
            {
                (productoId, 120, "Primera entrega parcial")
            });

        // Assert - Estado debe ser RecibidaParcial
        var ordenParcial = await ObtenerOrdenCompra(ordenId);
        ordenParcial!.Estado.Should().Be("RecibidaParcial");
        ordenParcial.Detalles[0].CantidadSolicitada.Should().Be(200);
        ordenParcial.Detalles[0].CantidadRecibida.Should().Be(120);

        // Verificar stock aumentó 120
        var stockDespuesPrimera = await ObtenerStock(productoId, SucPP);
        stockDespuesPrimera!.Cantidad.Should().Be(cantidadInicial + 120);

        // Act - Recibir las 80 unidades restantes
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)>
            {
                (productoId, 80, "Segunda entrega - completa")
            });

        // Assert - Ahora debe estar RecibidaCompleta
        var ordenCompleta = await ObtenerOrdenCompra(ordenId);
        ordenCompleta!.Estado.Should().Be("RecibidaCompleta");
        ordenCompleta.Detalles[0].CantidadRecibida.Should().Be(200);

        // Verificar stock total
        var stockFinal = await ObtenerStock(productoId, SucPP);
        stockFinal!.Cantidad.Should().Be(cantidadInicial + 200);
    }

    [Fact]
    public async Task OrdenCompra_RecepcionExcedida_Rechaza()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-003", 1000m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 003");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 50, 300m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);

        // Act & Assert - Intentar recibir más de lo solicitado
        var dto = new
        {
            lineas = new[]
            {
                new { productoId, cantidadRecibida = 70m, observaciones = (string?)null }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/recibir", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "No debe permitir recibir más de lo pendiente");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("pendiente", "El mensaje debe indicar la cantidad pendiente");
    }

    [Fact]
    public async Task OrdenCompra_Aprobar_SoloEstadoPendiente()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-004", 800m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 004");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 30, 200m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();

        // Aprobar por primera vez → OK
        await AprobarOrdenCompra(ordenId);

        // Act & Assert - Intentar aprobar de nuevo
        var dto = new { observaciones = "Segundo intento" };
        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/aprobar", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Solo se pueden aprobar órdenes en estado Pendiente");
    }

    [Fact]
    public async Task OrdenCompra_Rechazar_EstadoCorrecto()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-005", 1200m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 005");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 100, 350m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();

        // Act - Rechazar orden
        var dtoRechazo = new { motivoRechazo = "Precios no competitivos" };
        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/rechazar", dtoRechazo);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var ordenRechazada = await ObtenerOrdenCompra(ordenId);
        ordenRechazada!.Estado.Should().Be("Rechazada");
        ordenRechazada.MotivoRechazo.Should().Contain("no competitivos");
    }

    [Fact]
    public async Task OrdenCompra_Cancelar_EstadoPendiente()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-006", 900m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 006");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 50, 250m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();

        // Act - Cancelar orden en estado Pendiente
        var dtoCancelar = new { motivo = "Cambio de proveedor" };
        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/cancelar", dtoCancelar);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var ordenCancelada = await ObtenerOrdenCompra(ordenId);
        ordenCancelada!.Estado.Should().Be("Cancelada");
        ordenCancelada.MotivoRechazo.Should().Contain("proveedor");
    }

    [Fact]
    public async Task OrdenCompra_CancelarConRecepciones_Rechaza()
    {
        // Arrange - Orden aprobada con recepción parcial
        var productoId = await CrearProductoTest("COMPRA-007", 1100m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 007");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 100, 300m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);

        // Recibir parcialmente
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)>
            {
                (productoId, 50, null)
            });

        // Act & Assert - Intentar cancelar con recepciones
        var dtoCancelar = new { motivo = "Intento de cancelación" };
        var response = await _client.PostAsJsonAsync($"/api/v1/Compras/{ordenId}/cancelar", dtoCancelar);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "No debe permitir cancelar órdenes con recepciones");
    }

    [Fact]
    public async Task OrdenCompra_VariosProductos_TodosSeReciben()
    {
        // Arrange - Orden con 3 productos diferentes
        var producto1Id = await CrearProductoTest("COMPRA-008A", 1000m);
        var producto2Id = await CrearProductoTest("COMPRA-008B", 1500m);
        var producto3Id = await CrearProductoTest("COMPRA-008C", 2000m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 008");

        var stock1Inicial = (await ObtenerStock(producto1Id, SucPP))?.Cantidad ?? 0;
        var stock2Inicial = (await ObtenerStock(producto2Id, SucPP))?.Cantidad ?? 0;
        var stock3Inicial = (await ObtenerStock(producto3Id, SucPP))?.Cantidad ?? 0;

        // Act - Crear orden con 3 productos
        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (producto1Id, 50, 400m, 0),
                (producto2Id, 75, 600m, 19),
                (producto3Id, 100, 800m, 0)
            });

        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);

        // Recibir todos los productos
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)>
            {
                (producto1Id, 50, null),
                (producto2Id, 75, null),
                (producto3Id, 100, null)
            });

        // Assert - Verificar todos los stocks
        var stock1Final = await ObtenerStock(producto1Id, SucPP);
        stock1Final!.Cantidad.Should().Be(stock1Inicial + 50);

        var stock2Final = await ObtenerStock(producto2Id, SucPP);
        stock2Final!.Cantidad.Should().Be(stock2Inicial + 75);

        var stock3Final = await ObtenerStock(producto3Id, SucPP);
        stock3Final!.Cantidad.Should().Be(stock3Inicial + 100);

        // Verificar orden completa
        var ordenFinal = await ObtenerOrdenCompra(ordenId);
        ordenFinal!.Estado.Should().Be("RecibidaCompleta");
        ordenFinal.Detalles.Should().HaveCount(3);
    }

    [Fact]
    public async Task OrdenCompra_MultipleProveedores_StockCorrectoPorProveedor()
    {
        // Arrange - Mismo producto, diferentes proveedores
        var productoId = await CrearProductoTest("COMPRA-009", 1800m);
        var proveedor1Id = await CrearProveedorTest("Proveedor A");
        var proveedor2Id = await CrearProveedorTest("Proveedor B");

        var stockInicial = (await ObtenerStock(productoId, SucPP))?.Cantidad ?? 0;

        // Act - Crear y recibir orden del Proveedor A (50 unidades a $300)
        var resultado1 = await CrearOrdenCompra(
            SucPP,
            proveedor1Id,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 50, 300m, 0)
            });
        var orden1Id = resultado1.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(orden1Id);
        await RecibirOrdenCompra(orden1Id,
            new List<(Guid, decimal, string?)> { (productoId, 50, null) });

        // Act - Crear y recibir orden del Proveedor B (100 unidades a $250)
        var resultado2 = await CrearOrdenCompra(
            SucPP,
            proveedor2Id,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 100, 250m, 0)
            });
        var orden2Id = resultado2.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(orden2Id);
        await RecibirOrdenCompra(orden2Id,
            new List<(Guid, decimal, string?)> { (productoId, 100, null) });

        // Assert - Stock total debe ser 150 unidades
        var stockFinal = await ObtenerStock(productoId, SucPP);
        stockFinal!.Cantidad.Should().Be(stockInicial + 150);

        // Verificar que ambas órdenes están completas
        var orden1Final = await ObtenerOrdenCompra(orden1Id);
        orden1Final!.Estado.Should().Be("RecibidaCompleta");

        var orden2Final = await ObtenerOrdenCompra(orden2Id);
        orden2Final!.Estado.Should().Be("RecibidaCompleta");

        // Verificar costo promedio ponderado: (50*300 + 100*250) / 150 = 266.67
        stockFinal.CostoPromedio.Should().BeApproximately(266.67m, 0.01m,
            "El costo promedio debe reflejar las compras de ambos proveedores");
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS DE VALIDACIONES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task OrdenCompra_SinLineas_Rechaza()
    {
        // Arrange
        var proveedorId = await CrearProveedorTest("Proveedor Test Validación");

        // Act & Assert - Orden sin líneas
        var dto = new
        {
            sucursalId = SucPP,
            proveedorId,
            fechaEntregaEsperada = (DateTime?)null,
            observaciones = "Sin productos",
            lineas = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Compras", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("línea", "Debe indicar que se requiere al menos una línea");
    }

    [Fact]
    public async Task OrdenCompra_ProveedorInexistente_Rechaza()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-VAL-001", 1000m);

        // Act & Assert - Proveedor que no existe
        var dto = new
        {
            sucursalId = SucPP,
            proveedorId = 99999,  // ID inexistente
            fechaEntregaEsperada = (DateTime?)null,
            observaciones = (string?)null,
            lineas = new[]
            {
                new { productoId, cantidad = 10m, precioUnitario = 500m, porcentajeImpuesto = 0m }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Compras", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Proveedor", "Debe indicar que el proveedor no existe");
    }

    [Fact]
    public async Task OrdenCompra_PrecioNegativo_Rechaza()
    {
        // Arrange
        var productoId = await CrearProductoTest("COMPRA-VAL-002", 1000m);
        var proveedorId = await CrearProveedorTest("Proveedor Validación");

        // Act & Assert - Precio unitario negativo
        var dto = new
        {
            sucursalId = SucPP,
            proveedorId,
            fechaEntregaEsperada = (DateTime?)null,
            observaciones = (string?)null,
            lineas = new[]
            {
                new { productoId, cantidad = 10m, precioUnitario = -100m, porcentajeImpuesto = 0m }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Compras", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "No debe permitir precios negativos");
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS DE CONSULTAS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ListarOrdenes_FiltrosPorSucursal()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/Compras?sucursalId={SucPP}&limite=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ordenes = await response.Content.ReadFromJsonAsync<List<OrdenCompraDto>>(_jsonOptions);

        // Assert
        ordenes.Should().NotBeNull();
        ordenes!.Should().OnlyContain(o => o.SucursalId == SucPP);
    }

    [Fact]
    public async Task ListarOrdenes_FiltrosPorEstado()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/Compras?estado=Aprobada&limite=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ordenes = await response.Content.ReadFromJsonAsync<List<OrdenCompraDto>>(_jsonOptions);

        // Assert
        ordenes.Should().NotBeNull();
        if (ordenes!.Any())
        {
            ordenes.Should().OnlyContain(o => o.Estado == "Aprobada");
        }
    }

    [Fact]
    public async Task ObtenerOrdenPorId_RetornaDetalleCompleto()
    {
        // Arrange - Crear una orden
        var productoId = await CrearProductoTest("COMPRA-010", 3000m);
        var proveedorId = await CrearProveedorTest("Proveedor Test 010");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 25, 1200m, 19)
            },
            DateTime.UtcNow.AddDays(10),
            "Orden para test de consulta");

        var ordenId = resultCrear.GetProperty("id").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/v1/Compras/{ordenId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orden = await response.Content.ReadFromJsonAsync<OrdenCompraDto>(_jsonOptions);

        // Assert
        orden.Should().NotBeNull();
        orden!.Id.Should().Be(ordenId);
        orden.NumeroOrden.Should().StartWith("OC-");
        orden.Estado.Should().Be("Pendiente");
        orden.Detalles.Should().HaveCount(1);
        orden.Detalles[0].CantidadSolicitada.Should().Be(25);
        orden.Detalles[0].PrecioUnitario.Should().Be(1200m);
        orden.FechaEntregaEsperada.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS DE INTEGRACIÓN ERP OUTBOX, DOCUMENTOS Y RETENCIONES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task OrdenCompra_AlRecibir_EscribeDocumentoContableYErpOutbox()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-COMPRA-001", 5000m);
        var proveedorId = await CrearProveedorTest("Proveedor ERP 1");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 10, 3000m, 19) // Total Bruto: 30000 + 5700 = 35700
            });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        var numOrden = resultCrear.GetProperty("numeroOrden").GetString()!;

        await AprobarOrdenCompra(ordenId);

        // Act - Recibimos la orden entera
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)>
            {
                (productoId, 10, "Ok")
            });

        // Assert - DB Check
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Verificar que se creó el DocumentoContable físico
        var docs = await db.DocumentosContables
            .Include(d => d.Detalles)
            .Where(d => d.TipoDocumento == "RecepcionCompra" && d.NumeroSoporte.StartsWith(numOrden))
            .ToListAsync();

        docs.Should().HaveCount(1);
        var doc = docs.Single();
        doc.Detalles.Should().NotBeEmpty();

        // 2. Verificar que se coló el mensaje al Outbox
        var outboxMessages = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == ordenId && m.TipoDocumento == "CompraRecibida")
            .ToListAsync();

        outboxMessages.Should().HaveCount(1);
        var msg = outboxMessages.Single();
        msg.Estado.Should().Be(EstadoOutbox.Pendiente);

        // 3. Verificar la carga útil (Payload JSON)
        var payload = JsonSerializer.Deserialize<CompraErpPayload>(msg.Payload, _jsonOptions);
        payload.Should().NotBeNull();
        payload!.Asientos.Should().NotBeEmpty();
        payload.NumeroOrden.Should().Be(numOrden);
        
        // Verifica que todos los asientos contengan el Centro de Costo (herencia de la Sucursal)
        payload.Asientos.All(a => !string.IsNullOrEmpty(a.CentroCosto)).Should().BeTrue("El centro de costo debe inyectarse en el ERP");
    }

    [Fact]
    public async Task OrdenCompra_RecepcionMultiple_ObsoletaOutboxesPrevios()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-COMPRA-002", 5000m);
        var proveedorId = await CrearProveedorTest("Proveedor ERP 2");

        var resultCrear = await CrearOrdenCompra(
            SucPP,
            proveedorId,
            new List<(Guid, decimal, decimal, decimal)>
            {
                (productoId, 100, 3000m, 0)
            });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);

        // Act - Recepción #1 (Parcial 50 und)
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)> { (productoId, 50, "Primera Mitad") });

        // Act - Recepción #2 (Parcial 50 und - Completa la orden)
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)> { (productoId, 50, "Segunda Mitad") });

        // Assert - DB Check
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var outboxLíneas = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == ordenId && m.TipoDocumento == "CompraRecibida")
            .OrderBy(m => m.Id)
            .ToListAsync();

        outboxLíneas.Should().HaveCount(2, "Hubo dos recepciones para la misma orden de compra");

        // El primer payload que se quedó estancado (pendiente) antes de procesarse debe ser invalidado porque llega un registro que la supersede.
        // Dado que en las pruebas el Worker del ERP no está encendido consumiendo la DB (BackgroundService en tests no se inyecta o no procesa de inmediato todo), 
        // asumimos que el primer registro sigue ahí
        var outbox1 = outboxLíneas.First();
        outbox1.Estado.Should().Be(EstadoOutbox.Descartado);
        outbox1.UltimoError.Should().Contain("Supersedido por recepción #2");

        var outbox2 = outboxLíneas.Last();
        outbox2.Estado.Should().Be(EstadoOutbox.Pendiente);
    }

    [Fact(Skip = "Requiere sucursal dedicada para perfiles tributarios custom — la sucursal compartida SucPP se resetea entre tests de la colección")]
    public async Task OrdenCompra_ConRetencion_GeneraAsientosAdicionalesEnERP()
    {
        // Arrange - Sembrar una regla de Retención y Producto con Concepto
        var proveedorIdRegla = await CrearProveedorTest("Proveedor Retencion");
        int conceptoId;

        // Scope 1: Seed retention rules
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var concepto = new ConceptoRetencion { CodigoDian = "RTE", Nombre = "ReteFuente Compras", PorcentajeSugerido = 2.5m };
            db.ConceptosRetencion.Add(concepto);
            await db.SaveChangesAsync();
            conceptoId = concepto.Id;

            var prov = await db.Terceros.FindAsync(proveedorIdRegla);
            var suc = await db.Sucursales.FindAsync(SucPP);

            prov!.PerfilTributario = "PRUEBA_VENDEDOR";
            suc!.PerfilTributario = "PRUEBA_COMPRADOR";
            suc.ValorUVT = 47000m;
            await db.SaveChangesAsync();

            db.RetencionesReglas.Add(new RetencionRegla
            {
                Nombre = "Regla General",
                Tipo = TipoRetencion.ReteFuente,
                Porcentaje = 0.025m,
                BaseMinUVT = 0,
                PerfilVendedor = "PRUEBA_VENDEDOR",
                PerfilComprador = "PRUEBA_COMPRADOR",
                CodigoCuentaContable = "236540",
                ConceptoRetencionId = conceptoId,
                Activo = true
            });
            await db.SaveChangesAsync();
        }

        // Create product via API (outside the seed scope)
        var dtoProd = new { codigoBarras = "PROD-RTE", nombre = "PROD RTE", categoriaId = CatId, precioVenta = 1000m, precioCosto = 1000m, conceptoRetencionId = conceptoId };
        var responseProd = await _client.PostAsJsonAsync("/api/v1/Productos", dtoProd);
        var prodIdStr = (await responseProd.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions)).GetProperty("id").GetString()!;
        var prodId = Guid.Parse(prodIdStr);

        // Act - Crear y Recibir
        var resultCrear = await CrearOrdenCompra(SucPP, proveedorIdRegla, new List<(Guid, decimal, decimal, decimal)> { (prodId, 10, 1000m, 0) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (prodId, 10, "Ok") });

        // Scope 2: Assert
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var outboxes = await db.ErpOutboxMessages.Where(m => m.EntidadId == ordenId).ToListAsync();
            var outbox = outboxes.First();
            var payload = JsonSerializer.Deserialize<CompraErpPayload>(outbox.Payload, _jsonOptions);

            var reteAsientos = payload!.Asientos.Where(a => a.Cuenta == "236540").ToList();
            reteAsientos.Should().ContainSingle("Debe existir 1 asiento de Retención en la fuente por el 2.5% de 10000");
            reteAsientos[0].Valor.Should().Be(250m);
            reteAsientos[0].Naturaleza.Should().Be("Credito");
        }
    }

    [Fact]
    public async Task OrdenCompra_AlRecibir_DtoExponeCamposErp()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-DTO-001", 2000m);
        var proveedorId = await CrearProveedorTest("Proveedor DTO ERP");

        var resultCrear = await CrearOrdenCompra(
            SucPP, proveedorId,
            new List<(Guid, decimal, decimal, decimal)> { (productoId, 10, 500m, 0) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();

        // Antes de recibir: campos ERP deben estar vacíos
        var ordenPendiente = await ObtenerOrdenCompra(ordenId);
        ordenPendiente!.SincronizadoErp.Should().BeFalse();
        ordenPendiente.ErpReferencia.Should().BeNull();
        ordenPendiente.ErrorSincronizacion.Should().BeNull();

        // Act
        await AprobarOrdenCompra(ordenId);
        await RecibirOrdenCompra(ordenId,
            new List<(Guid, decimal, string?)> { (productoId, 10, null) });

        // Assert - Después de recibir, SincronizadoErp sigue false (el Background Service
        // no ha procesado aún), pero los campos deben existir en el DTO
        var ordenRecibida = await ObtenerOrdenCompra(ordenId);
        ordenRecibida!.Estado.Should().Be("RecibidaCompleta");
        // Los campos existen en la respuesta (no null reference exception)
        ordenRecibida.SincronizadoErp.Should().BeFalse("El background service no ha procesado aún");
        ordenRecibida.FechaSincronizacionErp.Should().BeNull();
    }

    [Fact]
    public async Task OrdenCompra_ConIVA_AsientosCuadranDebitosConCreditos()
    {
        // Arrange - Compra con IVA 19% (sin retenciones) — valida partida doble
        var productoId = await CrearProductoTest("ERP-CUADRE-001", 5000m);
        var proveedorId = await CrearProveedorTest("Proveedor Cuadre");

        var resultCrear = await CrearOrdenCompra(SucPP, proveedorId,
            new List<(Guid, decimal, decimal, decimal)> { (productoId, 20, 1000m, 19) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 20, null) });

        // Assert - Verificar que Débitos = Créditos
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var msg = await db.ErpOutboxMessages.FirstAsync(m => m.EntidadId == ordenId);
        var payload = JsonSerializer.Deserialize<CompraErpPayload>(msg.Payload, _jsonOptions)!;

        var totalDebitos = payload.Asientos.Where(a => a.Naturaleza == "Debito").Sum(a => a.Valor);
        var totalCreditos = payload.Asientos.Where(a => a.Naturaleza == "Credito").Sum(a => a.Valor);

        totalDebitos.Should().Be(totalCreditos,
            "La partida doble exige que Débitos = Créditos en todo comprobante contable");

        // Base: 20 × 1000 = 20,000
        // IVA descontable (Débito): 20,000 × 0.19 = 3,800
        // CxP (Crédito): 20,000 + 3,800 = 23,800
        // Total Débitos: 20,000 + 3,800 = 23,800
        // Total Créditos: 23,800
        totalDebitos.Should().Be(23800m, "Inventario 20,000 + IVA 3,800");
    }

    [Fact]
    public async Task OrdenCompra_RecepcionParcial_DocumentosContablesTienenNumeracionSecuencial()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-NUMSEQ-001", 3000m);
        var proveedorId = await CrearProveedorTest("Proveedor NumSeq");

        var resultCrear = await CrearOrdenCompra(
            SucPP, proveedorId,
            new List<(Guid, decimal, decimal, decimal)> { (productoId, 100, 500m, 0) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        var numOrden = resultCrear.GetProperty("numeroOrden").GetString()!;
        await AprobarOrdenCompra(ordenId);

        // Act - 3 recepciones parciales
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 30, null) });
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 30, null) });
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 40, null) });

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var docs = await db.DocumentosContables
            .Where(d => d.TipoDocumento == "RecepcionCompra" && d.NumeroSoporte.StartsWith(numOrden))
            .OrderBy(d => d.FechaCausacion)
            .ToListAsync();

        docs.Should().HaveCount(3, "3 recepciones parciales = 3 documentos contables");
        docs[0].NumeroSoporte.Should().Be(numOrden, "Primera recepción usa el número de orden directo");
        docs[1].NumeroSoporte.Should().Be($"{numOrden}-R2", "Segunda recepción agrega sufijo -R2");
        docs[2].NumeroSoporte.Should().Be($"{numOrden}-R3", "Tercera recepción agrega sufijo -R3");

        // Outbox: solo el último debe estar pendiente
        var outboxes = await db.ErpOutboxMessages
            .Where(m => m.EntidadId == ordenId && m.TipoDocumento == "CompraRecibida")
            .OrderBy(m => m.Id)
            .ToListAsync();

        outboxes.Should().HaveCount(3);
        outboxes[0].Estado.Should().Be(EstadoOutbox.Descartado);
        outboxes[1].Estado.Should().Be(EstadoOutbox.Descartado);
        outboxes[2].Estado.Should().Be(EstadoOutbox.Pendiente, "Solo el último outbox queda activo");
    }

    [Fact]
    public async Task OrdenCompra_ConIVA_GeneraAsientoIVADescontableConCuentaImpuesto()
    {
        // Arrange
        var productoId = await CrearProductoTest("ERP-IVA-001", 5000m);
        var proveedorId = await CrearProveedorTest("Proveedor IVA Test");

        var resultCrear = await CrearOrdenCompra(
            SucPP, proveedorId,
            new List<(Guid, decimal, decimal, decimal)> { (productoId, 50, 2000m, 19) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);

        // Act
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 50, null) });

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var msg = await db.ErpOutboxMessages.FirstAsync(m => m.EntidadId == ordenId);
        var payload = JsonSerializer.Deserialize<CompraErpPayload>(msg.Payload, _jsonOptions)!;

        // Debe haber un asiento de IVA descontable (Débito) con cuenta 2408
        var ivaAsientos = payload.Asientos.Where(a => a.Naturaleza == "Debito" && a.Nota.Contains("Impuesto")).ToList();
        ivaAsientos.Should().NotBeEmpty("Debe generar asiento de IVA descontable");

        // IVA = 50 × 2000 × 0.19 = 19,000
        var totalIva = ivaAsientos.Sum(a => a.Valor);
        totalIva.Should().Be(19000m, "50 unidades × $2,000 × 19% = $19,000");
    }

    [Fact]
    public async Task IntegracionErp_ReintentarOutbox_ReactivaMensajeDescartado()
    {
        // Arrange - Crear y recibir orden para generar outbox
        var productoId = await CrearProductoTest("ERP-RETRY-001", 2000m);
        var proveedorId = await CrearProveedorTest("Proveedor Retry");

        var resultCrear = await CrearOrdenCompra(
            SucPP, proveedorId,
            new List<(Guid, decimal, decimal, decimal)> { (productoId, 10, 500m, 0) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 10, null) });

        // Marcar manualmente como Descartado para simular fallo
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await db.ErpOutboxMessages.FirstAsync(m => m.EntidadId == ordenId);
        outbox.Estado = EstadoOutbox.Descartado;
        outbox.Intentos = 5;
        outbox.UltimoError = "Simulación de fallo";
        await db.SaveChangesAsync();

        // Act - Reintentar via API
        var response = await _client.PostAsync($"/api/v1/integracion-erp/reintentar-outbox/{outbox.Id}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Outbox debe estar Pendiente con Intentos=0
        await db.Entry(outbox).ReloadAsync();
        outbox.Estado.Should().Be(EstadoOutbox.Pendiente);
        outbox.Intentos.Should().Be(0);
        outbox.UltimoError.Should().BeNull();
    }

    [Fact]
    public async Task IntegracionErp_ObtenerErroresOutbox_RetornaListaFiltrada()
    {
        // Arrange - Crear outbox con error
        var productoId = await CrearProductoTest("ERP-ERRLIST-001", 1000m);
        var proveedorId = await CrearProveedorTest("Proveedor ErrList");

        var resultCrear = await CrearOrdenCompra(
            SucPP, proveedorId,
            new List<(Guid, decimal, decimal, decimal)> { (productoId, 5, 200m, 0) });
        var ordenId = resultCrear.GetProperty("id").GetInt32();
        await AprobarOrdenCompra(ordenId);
        await RecibirOrdenCompra(ordenId, new List<(Guid, decimal, string?)> { (productoId, 5, null) });

        // Marcar como error
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await db.ErpOutboxMessages.FirstAsync(m => m.EntidadId == ordenId);
        outbox.Estado = EstadoOutbox.Error;
        outbox.UltimoError = "Connection refused";
        outbox.Intentos = 3;
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/integracion-erp/outbox/errores");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var errores = await response.Content.ReadFromJsonAsync<List<JsonElement>>(_jsonOptions);
        errores.Should().NotBeEmpty();

        // Verificar que nuestro outbox aparece en la lista
        var nuestroError = errores!.FirstOrDefault(e => e.GetProperty("entidadId").GetInt32() == ordenId);
        nuestroError.ValueKind.Should().NotBe(JsonValueKind.Undefined, "Nuestro outbox debe aparecer en errores");
        nuestroError.GetProperty("estado").GetString().Should().Be("Error");
        nuestroError.GetProperty("ultimoError").GetString().Should().Contain("Connection refused");
    }
}
