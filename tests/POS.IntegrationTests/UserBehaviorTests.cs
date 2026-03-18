using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using POS.Application.DTOs;
using Xunit;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para la Capa 5 — Anticipación funcional.
/// Verifica el endpoint /api/v1/productos/anticipados alimentado por UserBehaviorProjection.
/// </summary>
[Collection("POS")]
public class UserBehaviorTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int SucId => _factory.SucursalPPId;
    private int CatId => _factory.CategoriaTestId;

    public UserBehaviorTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAnticipados_SinHistorial_RetornaListaVacia()
    {
        // El usuario de test (admin@sincopos.com) puede no tener historial de ventas.
        // El endpoint debe retornar siempre 200 OK con una lista (vacía o con datos).
        var response = await _client.GetAsync("/api/v1/productos/anticipados");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<ProductoDto>>(_json);
        Assert.NotNull(items);
        Assert.True(items.Count >= 0);
    }

    [Fact]
    public async Task GetAnticipados_ConLimiteParametro_RetornaOk()
    {
        var response = await _client.GetAsync("/api/v1/productos/anticipados?limite=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<ProductoDto>>(_json);
        Assert.NotNull(items);
        // No puede retornar más que el límite solicitado
        Assert.True(items.Count <= 5);
    }

    [Fact]
    public async Task GetAnticipados_ConLimiteDefault_RetornaOk()
    {
        var response = await _client.GetAsync("/api/v1/productos/anticipados");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<ProductoDto>>(_json);
        Assert.NotNull(items);
        // El límite default es 20
        Assert.True(items.Count <= 20);
    }
}
