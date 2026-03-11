using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

[Collection("POS")]
public class ProductosTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProductosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CrearProducto_DeberiaRetornarProducto()
    {
        var dto = new CrearProductoDto(
            CodigoBarras: $"TEST{Guid.NewGuid():N}"[..20],
            Nombre: "Producto Test",
            Descripcion: "Descripcion test",
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 100m,
            PrecioCosto: 60m);

        var response = await _client.PostAsJsonAsync("/api/v1/productos", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var producto = await response.Content.ReadFromJsonAsync<ProductoDto>();
        producto.Should().NotBeNull();
        producto!.Id.Should().NotBeEmpty();
        producto.Nombre.Should().Be("Producto Test");
    }

    [Fact]
    public async Task CrearProducto_CodigoDuplicado_DeberiaRetornarConflict()
    {
        var codigo = $"DUP{Guid.NewGuid():N}"[..20];
        var dto = new CrearProductoDto(codigo, "Prod1", null, _factory.CategoriaTestId, 100m, 60m);

        await _client.PostAsJsonAsync("/api/v1/productos", dto);
        var response = await _client.PostAsJsonAsync("/api/v1/productos", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ObtenerProducto_DeberiaRetornarDatosCorrectos()
    {
        var crearDto = new CrearProductoDto(
            CodigoBarras: $"GET{Guid.NewGuid():N}"[..20],
            Nombre: "Producto para obtener",
            Descripcion: null,
            CategoriaId: _factory.CategoriaTestId,
            PrecioVenta: 50m,
            PrecioCosto: 30m);

        var crearResponse = await _client.PostAsJsonAsync("/api/v1/productos", crearDto);
        var created = await crearResponse.Content.ReadFromJsonAsync<ProductoDto>();

        var response = await _client.GetAsync($"/api/v1/productos/{created!.Id}");

        response.EnsureSuccessStatusCode();
        var producto = await response.Content.ReadFromJsonAsync<ProductoDto>();

        producto.Should().NotBeNull();
        producto!.Id.Should().Be(created.Id);
        producto.CodigoBarras.Should().Be(crearDto.CodigoBarras);
        producto.Nombre.Should().Be(crearDto.Nombre);
        producto.PrecioVenta.Should().Be(50m);
    }

    [Fact]
    public async Task ActualizarProducto_DeberiaCambiarDatos()
    {
        var crearDto = new CrearProductoDto(
            $"UPD{Guid.NewGuid():N}"[..20], "Original", null, _factory.CategoriaTestId, 100m, 60m);
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/productos", crearDto);
        var created = await crearResponse.Content.ReadFromJsonAsync<ProductoDto>();

        var actualizarDto = new ActualizarProductoDto("Actualizado", "Nueva desc", 120m, 70m);
        var response = await _client.PutAsJsonAsync($"/api/v1/productos/{created!.Id}", actualizarDto);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/productos/{created.Id}");
        var producto = await getResponse.Content.ReadFromJsonAsync<ProductoDto>();

        producto!.Nombre.Should().Be("Actualizado");
        producto.PrecioVenta.Should().Be(120m);
    }

    [Fact]
    public async Task DesactivarProducto_DeberiaMarcarInactivo()
    {
        var crearDto = new CrearProductoDto(
            $"DEL{Guid.NewGuid():N}"[..20], "Para desactivar", null, _factory.CategoriaTestId, 100m, 60m);
        var crearResponse = await _client.PostAsJsonAsync("/api/v1/productos", crearDto);
        var created = await crearResponse.Content.ReadFromJsonAsync<ProductoDto>();

        var response = await _client.DeleteAsync($"/api/v1/productos/{created!.Id}?motivo=test");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // No deberia aparecer en lista de activos
        var listResponse = await _client.GetAsync("/api/v1/productos");
        var paginado = await listResponse.Content.ReadFromJsonAsync<PaginatedResult<ProductoDto>>();
        paginado!.Items.Should().NotContain(p => p.Id == created.Id);

        // Deberia aparecer con incluirInactivos
        var allResponse = await _client.GetAsync("/api/v1/productos?incluirInactivos=true");
        var todoPaginado = await allResponse.Content.ReadFromJsonAsync<PaginatedResult<ProductoDto>>();
        todoPaginado!.Items.Should().Contain(p => p.Id == created.Id && !p.Activo);
    }

    [Fact]
    public async Task BuscarPorCodigoBarras_DeberiaRetornarProducto()
    {
        var codigo = $"BAR{Guid.NewGuid():N}"[..20];
        var crearDto = new CrearProductoDto(codigo, "Buscable", null, _factory.CategoriaTestId, 100m, 60m);
        await _client.PostAsJsonAsync("/api/v1/productos", crearDto);

        var response = await _client.GetAsync($"/api/v1/productos/codigo/{codigo}");

        response.EnsureSuccessStatusCode();
        var producto = await response.Content.ReadFromJsonAsync<ProductoDto>();
        producto!.CodigoBarras.Should().Be(codigo);
    }
}
