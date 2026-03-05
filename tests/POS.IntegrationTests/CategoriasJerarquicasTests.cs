using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

[Collection("POS-Auth-Sequential")]
public class CategoriasJerarquicasTests
{
    private readonly HttpClient _client;
    private readonly HttpClient _clientCajero;
    private readonly AuthenticatedWebApplicationFactory _factory;

    public CategoriasJerarquicasTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient("admin@test.com"); // Admin/Supervisor
        _clientCajero = factory.CreateAuthenticatedClient("cajero@test.com"); // Sin permisos
    }

    #region Crear Categorías

    [Fact]
    public async Task CrearCategoriaRaiz_ConDatosValidos_DebeCrear()
    {
        // Arrange
        var dto = new CrearCategoriaDto(
            Nombre: $"Alimentos-{Guid.NewGuid().ToString("N")[..8]}",
            Descripcion: "Todos los alimentos",
            CategoriaPadreId: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/categorias", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoria = await response.Content.ReadFromJsonAsync<CategoriaDto>();
        categoria.Should().NotBeNull();
        categoria!.Nombre.Should().Be(dto.Nombre);
        categoria.Nivel.Should().Be(0);
        categoria.RutaCompleta.Should().Be(dto.Nombre);
        categoria.CategoriaPadreId.Should().BeNull();
        categoria.NombrePadre.Should().BeNull();
    }

    [Fact]
    public async Task CrearSubCategoria_ConPadreValido_DebeCrear()
    {
        // Arrange: Crear categoría padre
        var dtoPadre = new CrearCategoriaDto(
            Nombre: $"Bebidas-{Guid.NewGuid().ToString("N")[..8]}",
            Descripcion: "Bebidas",
            CategoriaPadreId: null
        );
        var responsePadre = await _client.PostAsJsonAsync("/api/categorias", dtoPadre);
        var padre = await responsePadre.Content.ReadFromJsonAsync<CategoriaDto>();

        // Crear subcategoría
        var dtoHijo = new CrearCategoriaDto(
            Nombre: "Gaseosas",
            Descripcion: "Bebidas gaseosas",
            CategoriaPadreId: padre!.Id
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/categorias", dtoHijo);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoria = await response.Content.ReadFromJsonAsync<CategoriaDto>();
        categoria.Should().NotBeNull();
        categoria!.Nivel.Should().Be(1);
        categoria.CategoriaPadreId.Should().Be(padre.Id);
        categoria.NombrePadre.Should().Be(dtoPadre.Nombre);
        categoria.RutaCompleta.Should().Be($"{dtoPadre.Nombre} > Gaseosas");
    }

    [Fact]
    public async Task CrearCategoria_ConNombreDuplicado_DebeRetornar409()
    {
        // Arrange
        var nombre = $"Duplicado-{Guid.NewGuid().ToString("N")[..8]}";
        var dto1 = new CrearCategoriaDto(nombre, null, null);

        await _client.PostAsJsonAsync("/api/categorias", dto1);

        var dto2 = new CrearCategoriaDto(nombre, "Otra descripción", null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/categorias", dto2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CrearCategoria_ConPadreInexistente_DebeRetornar400()
    {
        // Arrange
        var dto = new CrearCategoriaDto(
            Nombre: "Test",
            Descripcion: null,
            CategoriaPadreId: 999999
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/categorias", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("no existe");
    }

    [Fact]
    public async Task CrearCategoria_ConMasDe3Niveles_DebeRetornar400()
    {
        // Arrange: Crear 3 niveles
        var dto1 = new CrearCategoriaDto($"Nivel0-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var res1 = await _client.PostAsJsonAsync("/api/categorias", dto1);
        var cat1 = await res1.Content.ReadFromJsonAsync<CategoriaDto>();

        var dto2 = new CrearCategoriaDto("Nivel1", null, cat1!.Id);
        var res2 = await _client.PostAsJsonAsync("/api/categorias", dto2);
        var cat2 = await res2.Content.ReadFromJsonAsync<CategoriaDto>();

        var dto3 = new CrearCategoriaDto("Nivel2", null, cat2!.Id);
        var res3 = await _client.PostAsJsonAsync("/api/categorias", dto3);
        var cat3 = await res3.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act: Intentar crear nivel 3 (4to nivel, no permitido)
        var dto4 = new CrearCategoriaDto("Nivel3", null, cat3!.Id);
        var response = await _client.PostAsJsonAsync("/api/categorias", dto4);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("3 niveles");
    }

    #endregion

    #region Obtener Categorías

    [Fact]
    public async Task ObtenerCategoria_PorId_DebeRetornarConDetalles()
    {
        // Arrange: Crear categoría
        var dto = new CrearCategoriaDto($"Test-{Guid.NewGuid().ToString("N")[..8]}", "Desc", null);
        var createResponse = await _client.PostAsJsonAsync("/api/categorias", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act
        var response = await _client.GetAsync($"/api/categorias/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categoria = await response.Content.ReadFromJsonAsync<CategoriaDto>();
        categoria.Should().NotBeNull();
        categoria!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task ObtenerArbol_DebeRetornarEstructuraJerarquica()
    {
        // Arrange: Crear estructura
        var dto1 = new CrearCategoriaDto($"Raiz-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var res1 = await _client.PostAsJsonAsync("/api/categorias", dto1);
        var raiz = await res1.Content.ReadFromJsonAsync<CategoriaDto>();

        var dto2 = new CrearCategoriaDto($"Hijo-{Guid.NewGuid().ToString("N")[..6]}", null, raiz!.Id);
        await _client.PostAsJsonAsync("/api/categorias", dto2);

        // Act
        var response = await _client.GetAsync("/api/categorias/arbol");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var arbol = await response.Content.ReadFromJsonAsync<List<CategoriaArbolDto>>();
        arbol.Should().NotBeNull();
        arbol!.Should().Contain(c => c.Id == raiz.Id);

        var nodoRaiz = arbol.First(c => c.Id == raiz.Id);
        nodoRaiz.SubCategorias.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ObtenerCategoriasRaiz_DebeRetornarSoloRaiz()
    {
        // Arrange
        var dtoRaiz = new CrearCategoriaDto($"Raiz-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resRaiz = await _client.PostAsJsonAsync("/api/categorias", dtoRaiz);
        var raiz = await resRaiz.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoHijo = new CrearCategoriaDto("Hijo", null, raiz!.Id);
        await _client.PostAsJsonAsync("/api/categorias", dtoHijo);

        // Act
        var response = await _client.GetAsync("/api/categorias/raiz");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categorias = await response.Content.ReadFromJsonAsync<List<CategoriaDto>>();
        categorias.Should().NotBeNull();
        categorias!.Should().Contain(c => c.Id == raiz.Id);
        categorias.Should().OnlyContain(c => c.Nivel == 0);
    }

    [Fact]
    public async Task ObtenerSubCategorias_DebeRetornarSoloHijos()
    {
        // Arrange
        var dtoPadre = new CrearCategoriaDto($"Padre-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre = await _client.PostAsJsonAsync("/api/categorias", dtoPadre);
        var padre = await resPadre.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoHijo1 = new CrearCategoriaDto($"Hijo1-{Guid.NewGuid().ToString("N")[..6]}", null, padre!.Id);
        await _client.PostAsJsonAsync("/api/categorias", dtoHijo1);

        var dtoHijo2 = new CrearCategoriaDto($"Hijo2-{Guid.NewGuid().ToString("N")[..6]}", null, padre.Id);
        await _client.PostAsJsonAsync("/api/categorias", dtoHijo2);

        // Act
        var response = await _client.GetAsync($"/api/categorias/{padre.Id}/subcategorias");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subcategorias = await response.Content.ReadFromJsonAsync<List<CategoriaDto>>();
        subcategorias.Should().NotBeNull();
        subcategorias!.Should().HaveCount(2);
        subcategorias.Should().OnlyContain(c => c.CategoriaPadreId == padre.Id);
    }

    #endregion

    #region Actualizar Categorías

    [Fact]
    public async Task ActualizarCategoria_ConDatosValidos_DebeActualizar()
    {
        // Arrange: Crear categoría
        var dtoCrear = new CrearCategoriaDto($"Original-{Guid.NewGuid().ToString("N")[..6]}", "Desc original", null);
        var createResponse = await _client.PostAsJsonAsync("/api/categorias", dtoCrear);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoActualizar = new ActualizarCategoriaDto(
            Nombre: $"Actualizado-{Guid.NewGuid().ToString("N")[..6]}",
            Descripcion: "Desc actualizada",
            CategoriaPadreId: null
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/categorias/{created!.Id}", dtoActualizar);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/categorias/{created.Id}");
        var actualizada = await getResponse.Content.ReadFromJsonAsync<CategoriaDto>();
        actualizada!.Nombre.Should().Be(dtoActualizar.Nombre);
        actualizada.Descripcion.Should().Be(dtoActualizar.Descripcion);
    }

    [Fact]
    public async Task ActualizarCategoria_CambiarPadre_DebeActualizarJerarquia()
    {
        // Arrange: Crear 2 raíz y 1 hijo
        var dtoPadre1 = new CrearCategoriaDto($"Padre1-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre1 = await _client.PostAsJsonAsync("/api/categorias", dtoPadre1);
        var padre1 = await resPadre1.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoPadre2 = new CrearCategoriaDto($"Padre2-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre2 = await _client.PostAsJsonAsync("/api/categorias", dtoPadre2);
        var padre2 = await resPadre2.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoHijo = new CrearCategoriaDto($"Hijo-{Guid.NewGuid().ToString("N")[..6]}", null, padre1!.Id);
        var resHijo = await _client.PostAsJsonAsync("/api/categorias", dtoHijo);
        var hijo = await resHijo.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act: Mover hijo de padre1 a padre2
        var dtoActualizar = new ActualizarCategoriaDto(hijo!.Nombre, hijo.Descripcion, padre2!.Id);
        var response = await _client.PutAsJsonAsync($"/api/categorias/{hijo.Id}", dtoActualizar);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/categorias/{hijo.Id}");
        var actualizada = await getResponse.Content.ReadFromJsonAsync<CategoriaDto>();
        actualizada!.CategoriaPadreId.Should().Be(padre2.Id);
        actualizada.NombrePadre.Should().Be(dtoPadre2.Nombre);
    }

    [Fact]
    public async Task ActualizarCategoria_PadreASiMisma_DebeRetornar400()
    {
        // Arrange
        var dto = new CrearCategoriaDto($"Test-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var createResponse = await _client.PostAsJsonAsync("/api/categorias", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoActualizar = new ActualizarCategoriaDto(
            created!.Nombre,
            created.Descripcion,
            CategoriaPadreId: created.Id // Padre a sí misma
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/categorias/{created.Id}", dtoActualizar);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("sí misma");
    }

    #endregion

    #region Mover Categorías

    [Fact]
    public async Task MoverCategoria_AOtroPadre_DebeMover()
    {
        // Arrange
        var dtoPadre1 = new CrearCategoriaDto($"P1-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre1 = await _client.PostAsJsonAsync("/api/categorias", dtoPadre1);
        var padre1 = await resPadre1.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoPadre2 = new CrearCategoriaDto($"P2-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre2 = await _client.PostAsJsonAsync("/api/categorias", dtoPadre2);
        var padre2 = await resPadre2.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoHijo = new CrearCategoriaDto($"Hijo-{Guid.NewGuid().ToString("N")[..6]}", null, padre1!.Id);
        var resHijo = await _client.PostAsJsonAsync("/api/categorias", dtoHijo);
        var hijo = await resHijo.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act: Mover de padre1 a padre2
        var dtoMover = new MoverCategoriaDto(hijo!.Id, padre2!.Id);
        var response = await _client.PostAsJsonAsync("/api/categorias/mover", dtoMover);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/categorias/{hijo.Id}");
        var movida = await getResponse.Content.ReadFromJsonAsync<CategoriaDto>();
        movida!.CategoriaPadreId.Should().Be(padre2.Id);
    }

    [Fact]
    public async Task MoverCategoria_ASubcategoria_DebeRetornar400()
    {
        // Arrange: Padre > Hijo
        var dtoPadre = new CrearCategoriaDto($"Padre-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre = await _client.PostAsJsonAsync("/api/categorias", dtoPadre);
        resPadre.StatusCode.Should().Be(HttpStatusCode.Created);
        var padre = await resPadre.Content.ReadFromJsonAsync<CategoriaDto>();
        padre.Should().NotBeNull();

        var dtoHijo = new CrearCategoriaDto($"Hijo-{Guid.NewGuid().ToString("N")[..6]}", null, padre!.Id);
        var resHijo = await _client.PostAsJsonAsync("/api/categorias", dtoHijo);
        resHijo.StatusCode.Should().Be(HttpStatusCode.Created);
        var hijo = await resHijo.Content.ReadFromJsonAsync<CategoriaDto>();
        hijo.Should().NotBeNull();
        hijo!.CategoriaPadreId.Should().Be(padre.Id); // Verificar que se creó correctamente

        // Act: Intentar mover padre dentro de hijo (crearía ciclo)
        var dtoMover = new MoverCategoriaDto(padre.Id, hijo.Id);
        var response = await _client.PostAsJsonAsync("/api/categorias/mover", dtoMover);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("subcategorías");
    }

    #endregion

    #region Eliminar Categorías

    [Fact]
    public async Task EliminarCategoria_SinHijosNiProductos_DebeEliminar()
    {
        // Arrange
        var dto = new CrearCategoriaDto($"PorEliminar-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var createResponse = await _client.PostAsJsonAsync("/api/categorias", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/categorias/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/categorias/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EliminarCategoria_ConSubCategorias_DebeRetornar400()
    {
        // Arrange: Padre con hijo
        var dtoPadre = new CrearCategoriaDto($"PadreConHijos-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var resPadre = await _client.PostAsJsonAsync("/api/categorias", dtoPadre);
        var padre = await resPadre.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoHijo = new CrearCategoriaDto("Hijo", null, padre!.Id);
        await _client.PostAsJsonAsync("/api/categorias", dtoHijo);

        // Act: Intentar eliminar padre
        var response = await _client.DeleteAsync($"/api/categorias/{padre.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("subcategorías");
    }

    #endregion

    #region Permisos

    [Fact]
    public async Task CrearCategoria_SinSerSupervisor_DebeRetornar403()
    {
        // Arrange
        var dto = new CrearCategoriaDto("Test", null, null);

        // Act
        var response = await _clientCajero.PostAsJsonAsync("/api/categorias", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActualizarCategoria_SinSerSupervisor_DebeRetornar403()
    {
        // Arrange: Crear con admin
        var dtoCrear = new CrearCategoriaDto($"Test-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var createResponse = await _client.PostAsJsonAsync("/api/categorias", dtoCrear);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        var dtoActualizar = new ActualizarCategoriaDto("Actualizado", null, null);

        // Act
        var response = await _clientCajero.PutAsJsonAsync($"/api/categorias/{created!.Id}", dtoActualizar);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EliminarCategoria_SinSerAdmin_DebeRetornar403()
    {
        // Arrange
        var dto = new CrearCategoriaDto($"Test-{Guid.NewGuid().ToString("N")[..6]}", null, null);
        var createResponse = await _client.PostAsJsonAsync("/api/categorias", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act
        var response = await _clientCajero.DeleteAsync($"/api/categorias/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}
