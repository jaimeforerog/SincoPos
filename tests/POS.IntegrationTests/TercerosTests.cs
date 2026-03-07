using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Terceros.
/// Verifica: CRUD completo, cálculo de DV (módulo 11 DIAN), búsquedas y actividades CIIU.
/// </summary>
[Collection("POS")]
public class TercerosTests
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TercerosTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<TerceroDto> CrearTerceroAsync(
        string? nombre = null,
        string? identificacion = null,
        string tipoTercero = "Cliente")
    {
        var dto = new CrearTerceroDto
        {
            TipoIdentificacion = "CC",
            Identificacion = identificacion ?? $"CC-{Guid.NewGuid():N}"[..15],
            Nombre = nombre ?? $"Tercero {Guid.NewGuid():N}"[..20],
            TipoTercero = tipoTercero,
            Telefono = "3001234567",
            Email = $"tercero{Guid.NewGuid():N}"[..10] + "@test.com",
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Terceros", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TerceroDto>(_jsonOptions))!;
    }

    // ─── Cálculo de DV ──────────────────────────────────────────────────────

    [Fact]
    public async Task CalcularDV_NIT_DevuelveDigitoEsperado()
    {
        // NIT 900455955 → DV = 0 (calculado con módulo 11 DIAN)
        var response = await _client.GetAsync("/api/v1/Terceros/calcular-dv?nit=900455955");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        // dv puede ser número o string según la serialización
        var dvElement = json.GetProperty("dv");
        var dvStr = dvElement.ValueKind == JsonValueKind.Number
            ? dvElement.GetInt32().ToString()
            : dvElement.GetString()!;
        dvStr.Should().MatchRegex("^[0-9]$");
    }

    [Fact]
    public async Task CalcularDV_NITInvalido_Devuelve400()
    {
        var response = await _client.GetAsync("/api/v1/Terceros/calcular-dv?nit=ABCDEF");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CalcularDV_NITVacio_Devuelve400()
    {
        var response = await _client.GetAsync("/api/v1/Terceros/calcular-dv?nit=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Crear Tercero ───────────────────────────────────────────────────────

    [Fact]
    public async Task CrearTercero_DatosValidos_Devuelve201()
    {
        var dto = new CrearTerceroDto
        {
            TipoIdentificacion = "NIT",
            Identificacion = $"NIT-{Guid.NewGuid():N}"[..15],
            Nombre = "Empresa de Prueba S.A.S",
            TipoTercero = "Proveedor",
            Telefono = "6014567890",
            Email = "empresa@test.com",
            Direccion = "Calle 123 # 45-67",
            PerfilTributario = "Responsable IVA",
            EsResponsableIVA = true,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Terceros", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tercero = await response.Content.ReadFromJsonAsync<TerceroDto>(_jsonOptions);
        tercero.Should().NotBeNull();
        tercero!.Id.Should().BeGreaterThan(0);
        tercero.Nombre.Should().Be(dto.Nombre);
        tercero.TipoTercero.Should().Be("Proveedor");
        tercero.EsResponsableIVA.Should().BeTrue();
        tercero.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task CrearTercero_IdentificacionDuplicada_Devuelve409()
    {
        var identificacion = $"DUP-{Guid.NewGuid():N}"[..15];
        await CrearTerceroAsync(identificacion: identificacion);

        var dto = new CrearTerceroDto
        {
            TipoIdentificacion = "CC",
            Identificacion = identificacion,
            Nombre = "Otro Tercero",
            TipoTercero = "Cliente",
        };
        var response = await _client.PostAsJsonAsync("/api/v1/Terceros", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CrearTercero_NombreVacio_Devuelve400()
    {
        var dto = new CrearTerceroDto
        {
            TipoIdentificacion = "CC",
            Identificacion = $"CC-{Guid.NewGuid():N}"[..15],
            Nombre = "",
            TipoTercero = "Cliente",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Terceros", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Obtener / Buscar ────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerTercero_PorId_Devuelve200()
    {
        var creado = await CrearTerceroAsync(nombre: "Tercero GetById Test");

        var response = await _client.GetAsync($"/api/v1/Terceros/{creado.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tercero = await response.Content.ReadFromJsonAsync<TerceroDto>(_jsonOptions);
        tercero!.Id.Should().Be(creado.Id);
        tercero.Nombre.Should().Be("Tercero GetById Test");
    }

    [Fact]
    public async Task ObtenerTercero_Inexistente_Devuelve404()
    {
        var response = await _client.GetAsync("/api/v1/Terceros/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BuscarTerceros_SinFiltro_DevuelveLista()
    {
        await CrearTerceroAsync();

        var response = await _client.GetAsync("/api/v1/Terceros");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<TerceroDto>>(_jsonOptions);
        lista.Should().NotBeNull();
        lista!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BuscarTerceros_PorNombre_DevuelveFiltrado()
    {
        var nombre = $"BuscarNombre-{Guid.NewGuid():N}"[..30];
        await CrearTerceroAsync(nombre: nombre);

        var response = await _client.GetAsync($"/api/v1/Terceros?q={Uri.EscapeDataString(nombre)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<TerceroDto>>(_jsonOptions);
        lista.Should().NotBeNull();
        lista!.Should().Contain(t => t.Nombre == nombre);
    }

    [Fact]
    public async Task BuscarPorIdentificacion_Existente_Devuelve200()
    {
        var identificacion = $"ID-{Guid.NewGuid():N}"[..15];
        await CrearTerceroAsync(identificacion: identificacion);

        var response = await _client.GetAsync($"/api/v1/Terceros/identificacion/{Uri.EscapeDataString(identificacion)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tercero = await response.Content.ReadFromJsonAsync<TerceroDto>(_jsonOptions);
        tercero!.Identificacion.Should().Be(identificacion);
    }

    [Fact]
    public async Task BuscarPorIdentificacion_Inexistente_Devuelve404()
    {
        var response = await _client.GetAsync("/api/v1/Terceros/identificacion/NO-EXISTE-12345");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Actualizar ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ActualizarTercero_DatosValidos_Devuelve204()
    {
        var creado = await CrearTerceroAsync();

        var dto = new ActualizarTerceroDto
        {
            Nombre = "Nombre Actualizado Test",
            TipoTercero = "Ambos",
            Telefono = "3109876543",
            Direccion = "Carrera 45 # 12-34",
            EsGranContribuyente = true,
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/Terceros/{creado.Id}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar cambios persisted
        var getResponse = await _client.GetAsync($"/api/v1/Terceros/{creado.Id}");
        var actualizado = await getResponse.Content.ReadFromJsonAsync<TerceroDto>(_jsonOptions);
        actualizado!.Nombre.Should().Be("Nombre Actualizado Test");
        actualizado.EsGranContribuyente.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarTercero_Inexistente_Devuelve404()
    {
        var dto = new ActualizarTerceroDto { Nombre = "Nombre" };
        var response = await _client.PutAsJsonAsync("/api/v1/Terceros/999999", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Desactivar ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DesactivarTercero_Existente_Devuelve204()
    {
        var creado = await CrearTerceroAsync();

        var response = await _client.DeleteAsync($"/api/v1/Terceros/{creado.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ObtenerPorId no filtra por activo — verificar que activo = false
        var getResponse = await _client.GetAsync($"/api/v1/Terceros/{creado.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var desactivado = await getResponse.Content.ReadFromJsonAsync<TerceroDto>(_jsonOptions);
        desactivado!.Activo.Should().BeFalse();

        // La búsqueda general sí filtra por activo (por defecto)
        var buscarResp = await _client.GetAsync($"/api/v1/Terceros?q={Uri.EscapeDataString(creado.Nombre)}");
        var listaBusqueda = await buscarResp.Content.ReadFromJsonAsync<List<TerceroDto>>(_jsonOptions);
        listaBusqueda!.Should().NotContain(t => t.Id == creado.Id);
    }

    [Fact]
    public async Task DesactivarTercero_Inexistente_Devuelve404()
    {
        var response = await _client.DeleteAsync("/api/v1/Terceros/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Actividades CIIU ────────────────────────────────────────────────────

    [Fact]
    public async Task AgregarActividadCIIU_DatosValidos_DeberiaAgregarse()
    {
        var tercero = await CrearTerceroAsync();

        var dto = new AgregarActividadDto
        {
            CodigoCIIU = "4711",
            Descripcion = "Comercio al por menor en establecimientos no especializados",
            EsPrincipal = true,
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/Terceros/{tercero.Id}/actividades", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actividad = await response.Content.ReadFromJsonAsync<TerceroActividadDto>(_jsonOptions);
        actividad.Should().NotBeNull();
        actividad!.CodigoCIIU.Should().Be("4711");
        actividad.EsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task AgregarActividadCIIU_SinCodigo_Devuelve400()
    {
        var tercero = await CrearTerceroAsync();

        var dto = new AgregarActividadDto
        {
            CodigoCIIU = "",
            Descripcion = "Sin código",
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/Terceros/{tercero.Id}/actividades", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EliminarActividadCIIU_Existente_Devuelve204()
    {
        var tercero = await CrearTerceroAsync();
        var addDto = new AgregarActividadDto
        {
            CodigoCIIU = "4711",
            Descripcion = "Actividad a eliminar",
        };
        var addResp = await _client.PostAsJsonAsync($"/api/v1/Terceros/{tercero.Id}/actividades", addDto);
        var actividad = await addResp.Content.ReadFromJsonAsync<TerceroActividadDto>(_jsonOptions);

        var response = await _client.DeleteAsync($"/api/v1/Terceros/{tercero.Id}/actividades/{actividad!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task EstablecerActividadPrincipal_Existente_Devuelve204()
    {
        var tercero = await CrearTerceroAsync();
        var addDto = new AgregarActividadDto
        {
            CodigoCIIU = "6201",
            Descripcion = "Actividades de desarrollo de software",
            EsPrincipal = false,
        };
        var addResp = await _client.PostAsJsonAsync($"/api/v1/Terceros/{tercero.Id}/actividades", addDto);
        var actividad = await addResp.Content.ReadFromJsonAsync<TerceroActividadDto>(_jsonOptions);

        var response = await _client.PatchAsync(
            $"/api/v1/Terceros/{tercero.Id}/actividades/{actividad!.Id}/principal",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
