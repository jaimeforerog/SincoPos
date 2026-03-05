using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

[Collection("POS")]
public class PreciosTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PreciosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Resolver Precio

    [Fact]
    public async Task ResolverPrecio_ConPrecioSucursal_DebeRetornarPrecioSucursal()
    {
        // Arrange: Crear producto
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto con Precio Sucursal",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Crear precio específico para la sucursal
        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto!.Id,
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 150m,
            PrecioMinimo: 120m,
            OrigenDato: "Manual");

        await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Act: Resolver precio
        var response = await _client.GetAsync($"/api/precios/resolver?productoId={producto.Id}&sucursalId={_factory.SucursalPPId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var precioResuelto = await response.Content.ReadFromJsonAsync<PrecioResueltoDto>();
        precioResuelto.Should().NotBeNull();
        precioResuelto!.PrecioVenta.Should().Be(150m);
        precioResuelto.PrecioMinimo.Should().Be(120m);
        precioResuelto.Origen.Should().Be("Sucursal");
        precioResuelto.OrigenDato.Should().Be("Manual");
    }

    [Fact]
    public async Task ResolverPrecio_SinPrecioSucursal_DebeRetornarPrecioProducto()
    {
        // Arrange: Crear producto sin precio de sucursal
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto sin Precio Sucursal",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 200m,
            PrecioCosto: 80m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Act: Resolver precio (sin crear precio de sucursal)
        var response = await _client.GetAsync($"/api/precios/resolver?productoId={producto!.Id}&sucursalId={_factory.SucursalPPId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var precioResuelto = await response.Content.ReadFromJsonAsync<PrecioResueltoDto>();
        precioResuelto.Should().NotBeNull();
        precioResuelto!.PrecioVenta.Should().Be(200m);
        precioResuelto.Origen.Should().Be("Producto");
    }

    [Fact]
    public async Task ResolverPrecio_ProductoInexistente_DebeRetornar400()
    {
        // Act
        var response = await _client.GetAsync($"/api/precios/resolver?productoId={Guid.NewGuid()}&sucursalId={_factory.SucursalPPId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Crear/Actualizar Precio

    [Fact]
    public async Task CrearPrecioSucursal_ConDatosValidos_DebeCrear()
    {
        // Arrange: Crear producto
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto para Precio Nuevo",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Act: Crear precio
        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto!.Id,
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 130m,
            PrecioMinimo: 110m,
            OrigenDato: "Manual");

        var response = await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var precioCreado = await response.Content.ReadFromJsonAsync<PrecioSucursalDto>();
        precioCreado.Should().NotBeNull();
        precioCreado!.Id.Should().BeGreaterThan(0);
        precioCreado.PrecioVenta.Should().Be(130m);
        precioCreado.PrecioMinimo.Should().Be(110m);
    }

    [Fact]
    public async Task CrearPrecioSucursal_ProductoInexistente_DebeRetornar400()
    {
        // Act
        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: Guid.NewGuid(),
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 100m,
            PrecioMinimo: null,
            OrigenDato: null);

        var response = await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Producto no encontrado");
    }

    [Fact]
    public async Task CrearPrecioSucursal_SucursalInexistente_DebeRetornar400()
    {
        // Arrange: Crear producto
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto Test",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Act: Usar sucursal inexistente
        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto!.Id,
            SucursalId: 99999,
            PrecioVenta: 100m,
            PrecioMinimo: null,
            OrigenDato: null);

        var response = await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Sucursal no encontrada");
    }

    [Fact]
    public async Task ActualizarPrecioExistente_DebeActualizar()
    {
        // Arrange: Crear producto y precio inicial
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto para Actualizar",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto!.Id,
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 100m,
            PrecioMinimo: 80m,
            OrigenDato: "Manual");

        await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Act: Actualizar precio
        var actualizarPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto.Id,
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 150m,
            PrecioMinimo: 120m,
            OrigenDato: "Manual");

        var response = await _client.PostAsJsonAsync("/api/precios", actualizarPrecioDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var precioActualizado = await response.Content.ReadFromJsonAsync<PrecioSucursalDto>();
        precioActualizado.Should().NotBeNull();
        precioActualizado!.PrecioVenta.Should().Be(150m);
        precioActualizado.PrecioMinimo.Should().Be(120m);
    }

    [Fact]
    public async Task CrearPrecio_ConOrigenDatoManual_DebeGuardarCorrectamente()
    {
        // Arrange
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto Manual",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Act
        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto!.Id,
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 100m,
            PrecioMinimo: null,
            OrigenDato: "Manual");

        await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Assert: Verificar que se guardó correctamente
        var responseResolver = await _client.GetAsync($"/api/precios/resolver?productoId={producto.Id}&sucursalId={_factory.SucursalPPId}");
        var precioResuelto = await responseResolver.Content.ReadFromJsonAsync<PrecioResueltoDto>();
        precioResuelto!.OrigenDato.Should().Be("Manual");
    }

    [Fact]
    public async Task CrearPrecio_ConOrigenDatoMigrado_DebeGuardarCorrectamente()
    {
        // Arrange
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto Migrado",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Act
        var crearPrecioDto = new CrearPrecioSucursalDto(
            ProductoId: producto!.Id,
            SucursalId: _factory.SucursalPPId,
            PrecioVenta: 100m,
            PrecioMinimo: null,
            OrigenDato: "Migrado");

        await _client.PostAsJsonAsync("/api/precios", crearPrecioDto);

        // Assert
        var responseResolver = await _client.GetAsync($"/api/precios/resolver?productoId={producto.Id}&sucursalId={_factory.SucursalPPId}");
        var precioResuelto = await responseResolver.Content.ReadFromJsonAsync<PrecioResueltoDto>();
        precioResuelto!.OrigenDato.Should().Be("Migrado");
    }

    #endregion

    #region Listar Precios

    [Fact]
    public async Task ListarPreciosProducto_DebeRetornarTodos()
    {
        // Arrange: Crear producto y precios en múltiples sucursales
        var crearProductoDto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto Multi-Sucursal",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var responseProducto = await _client.PostAsJsonAsync("/api/productos", crearProductoDto);
        var producto = await responseProducto.Content.ReadFromJsonAsync<ProductoDto>();

        // Crear precios en 2 sucursales
        await _client.PostAsJsonAsync("/api/precios", new CrearPrecioSucursalDto(
            producto!.Id, _factory.SucursalPPId, 100m, null, "Manual"));

        await _client.PostAsJsonAsync("/api/precios", new CrearPrecioSucursalDto(
            producto.Id, _factory.SucursalFIFOId, 110m, null, "Manual"));

        // Act
        var response = await _client.GetAsync($"/api/precios/producto/{producto.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var precios = await response.Content.ReadFromJsonAsync<List<PrecioSucursalDto>>();
        precios.Should().NotBeNull();
        precios!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ListarPreciosProducto_ProductoInexistente_DebeRetornar404()
    {
        // Act
        var response = await _client.GetAsync($"/api/precios/producto/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
