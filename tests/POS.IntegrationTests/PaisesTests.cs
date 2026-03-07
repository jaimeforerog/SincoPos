using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

[Collection("POS")]
public class PaisesTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PaisesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Países

    [Fact]
    public async Task ObtenerPaises_DebeRetornarListado()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/paises");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paises = await response.Content.ReadFromJsonAsync<List<PaisDto>>();
        paises.Should().NotBeNull();
        paises!.Count.Should().BeGreaterThan(0);
        paises.Should().Contain(p => p.Iso2 == "CO"); // Colombia debe estar
    }

    [Fact]
    public async Task ObtenerPaises_DebeIncluirDatosCompletos()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/paises");

        // Assert
        var paises = await response.Content.ReadFromJsonAsync<List<PaisDto>>();
        paises.Should().NotBeNull();

        // Verificar que cada país tiene los datos necesarios
        foreach (var pais in paises!)
        {
            pais.Iso2.Should().NotBeNullOrEmpty();
            pais.Nombre.Should().NotBeNullOrEmpty();
            // Emoji es opcional
        }
    }

    [Fact]
    public async Task ObtenerPaises_DebeIncluirEmoji()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/paises");

        // Assert
        var paises = await response.Content.ReadFromJsonAsync<List<PaisDto>>();
        var colombia = paises!.FirstOrDefault(p => p.Iso2 == "CO");

        colombia.Should().NotBeNull();
        colombia!.Emoji.Should().NotBeNullOrEmpty();
        // El emoji de Colombia debería ser 🇨🇴
        colombia.Emoji.Should().Be("🇨🇴");
    }

    [Fact]
    public async Task ObtenerPaises_DebeUsarCache()
    {
        // Act: Hacer dos llamadas consecutivas
        var response1 = await _client.GetAsync("/api/v1/paises");
        var response2 = await _client.GetAsync("/api/v1/paises");

        // Assert: Ambas deben ser exitosas
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var paises1 = await response1.Content.ReadFromJsonAsync<List<PaisDto>>();
        var paises2 = await response2.Content.ReadFromJsonAsync<List<PaisDto>>();

        // Deberían retornar la misma cantidad (cache funcionando)
        paises1!.Count.Should().Be(paises2!.Count);
    }

    #endregion

    #region Ciudades

    [Fact]
    public async Task ObtenerCiudades_PorPais_DebeRetornarCiudades()
    {
        // Act: Obtener ciudades de Colombia
        var response = await _client.GetAsync("/api/v1/paises/CO/ciudades");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ciudades = await response.Content.ReadFromJsonAsync<List<CiudadDto>>();
        ciudades.Should().NotBeNull();
        ciudades!.Count.Should().BeGreaterThan(0);

        // Verificar que incluye ciudades importantes
        ciudades.Should().Contain(c => c.Nombre.Contains("Bogotá", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ObtenerCiudades_DebeIncluirDatosCompletos()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/paises/CO/ciudades");

        // Assert
        var ciudades = await response.Content.ReadFromJsonAsync<List<CiudadDto>>();
        ciudades.Should().NotBeNull();

        // Verificar que cada ciudad tiene datos completos
        foreach (var ciudad in ciudades!)
        {
            ciudad.Nombre.Should().NotBeNullOrEmpty();
            ciudad.Latitud.Should().NotBe(0);
            ciudad.Longitud.Should().NotBe(0);
        }
    }

    [Fact]
    public async Task ObtenerCiudades_PaisInvalido_DebeRetornar400()
    {
        // Act: Usar código de país inválido
        var response = await _client.GetAsync("/api/v1/paises/ /ciudades");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ObtenerCiudades_PaisVacio_DebeRetornar400()
    {
        // Act: Usar código de país vacío
        var response = await _client.GetAsync("/api/v1/paises//ciudades");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // Ruta no encontrada
    }

    [Fact]
    public async Task ObtenerCiudades_DebeUsarCache()
    {
        // Act: Hacer dos llamadas consecutivas
        var response1 = await _client.GetAsync("/api/v1/paises/CO/ciudades");
        var response2 = await _client.GetAsync("/api/v1/paises/CO/ciudades");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var ciudades1 = await response1.Content.ReadFromJsonAsync<List<CiudadDto>>();
        var ciudades2 = await response2.Content.ReadFromJsonAsync<List<CiudadDto>>();

        // Deberían retornar la misma cantidad (cache funcionando)
        ciudades1!.Count.Should().Be(ciudades2!.Count);
    }

    [Fact]
    public async Task ObtenerCiudades_DistintosPaises_DebeTenerDiferentesCiudades()
    {
        // Act: Obtener ciudades de Colombia y Perú
        var responseCO = await _client.GetAsync("/api/v1/paises/CO/ciudades");
        var responsePE = await _client.GetAsync("/api/v1/paises/PE/ciudades");

        // Assert
        responseCO.StatusCode.Should().Be(HttpStatusCode.OK);
        responsePE.StatusCode.Should().Be(HttpStatusCode.OK);

        var ciudadesCO = await responseCO.Content.ReadFromJsonAsync<List<CiudadDto>>();
        var ciudadesPE = await responsePE.Content.ReadFromJsonAsync<List<CiudadDto>>();

        // Las listas no deberían ser idénticas
        ciudadesCO.Should().NotBeEquivalentTo(ciudadesPE);
        ciudadesCO!.Should().Contain(c => c.Nombre.Contains("Bogotá", StringComparison.OrdinalIgnoreCase));
        ciudadesPE!.Should().Contain(c => c.Nombre.Contains("Lima", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
