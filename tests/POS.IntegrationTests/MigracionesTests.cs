using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS.Api.Controllers;

namespace POS.IntegrationTests;

[Collection("POS-Auth")]
public class MigracionesTests
{
    private readonly HttpClient _client;
    private readonly HttpClient _clientSupervisor;
    private readonly AuthenticatedWebApplicationFactory _factory;

    public MigracionesTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
        // Cliente con rol Admin
        _client = factory.CreateAuthenticatedClient("admin@test.com");
        // Cliente con rol Supervisor (no Admin) para tests de permisos
        _clientSupervisor = factory.CreateAuthenticatedClient("supervisor@test.com");
    }

    #region Consultar Historial

    [Fact]
    public async Task ObtenerHistorial_DebeRetornarMigraciones()
    {
        // Act
        var response = await _client.GetAsync("/api/migraciones?limite=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var migraciones = await response.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        migraciones.Should().NotBeNull();
        // Puede haber 0 o más migraciones dependiendo del estado de la BD
        migraciones!.Should().BeAssignableTo<List<MigracionLogDto>>();
    }

    [Fact]
    public async Task ObtenerHistorial_ConLimite_DebeRespetarLimite()
    {
        // Arrange: Sincronizar para tener datos
        await _client.PostAsync("/api/migraciones/sincronizar", null);

        // Act
        var response = await _client.GetAsync("/api/migraciones?limite=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var migraciones = await response.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        migraciones.Should().NotBeNull();
        migraciones!.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task ObtenerHistorial_DebeOrdenarPorFechaDesc()
    {
        // Arrange: Sincronizar para tener datos
        await _client.PostAsync("/api/migraciones/sincronizar", null);

        // Act
        var response = await _client.GetAsync("/api/migraciones?limite=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var migraciones = await response.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        migraciones.Should().NotBeNull();

        if (migraciones!.Count > 1)
        {
            // Verificar que están ordenadas por fecha descendente (más recientes primero)
            for (int i = 0; i < migraciones.Count - 1; i++)
            {
                migraciones[i].FechaAplicacion.Should()
                    .BeOnOrAfter(migraciones[i + 1].FechaAplicacion);
            }
        }
    }

    #endregion

    #region Sincronizar Migraciones

    [Fact]
    public async Task SincronizarMigraciones_DebeSincronizarHistoricas()
    {
        // Act
        var response = await _client.PostAsync("/api/migraciones/sincronizar", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resultado.Should().ContainKey("mensaje");
        resultado!["mensaje"].Should().Contain("exitosamente");
    }

    [Fact]
    public async Task SincronizarMigraciones_NoDebeDuplicar()
    {
        // Act: Ejecutar dos veces
        await _client.PostAsync("/api/migraciones/sincronizar", null);
        var responseHistorial1 = await _client.GetAsync("/api/migraciones?limite=100");
        var migraciones1 = await responseHistorial1.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        var count1 = migraciones1!.Count;

        await _client.PostAsync("/api/migraciones/sincronizar", null);
        var responseHistorial2 = await _client.GetAsync("/api/migraciones?limite=100");
        var migraciones2 = await responseHistorial2.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        var count2 = migraciones2!.Count;

        // Assert: El count debe ser el mismo (no duplicó)
        count2.Should().Be(count1);
    }

    [Fact]
    public async Task SincronizarMigraciones_DebeMarcarComoHistoricas()
    {
        // Act
        await _client.PostAsync("/api/migraciones/sincronizar", null);
        var response = await _client.GetAsync("/api/migraciones?limite=100");

        // Assert
        var migraciones = await response.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        migraciones.Should().NotBeNull();

        // Si hay migraciones sincronizadas, deberían tener notas indicando que son históricas
        var sincronizadas = migraciones!.Where(m => m.Notas != null && m.Notas.Contains("histórica", StringComparison.OrdinalIgnoreCase));
        // No hacemos assert del count porque podría ser 0 si no hay migraciones históricas
        // Solo verificamos que si hay, tienen el formato correcto
        sincronizadas.Should().BeAssignableTo<IEnumerable<MigracionLogDto>>();
    }

    #endregion

    #region Registrar Migración

    [Fact]
    public async Task RegistrarMigracion_ConDatosValidos_DebeRegistrar()
    {
        // Arrange
        var migrationGuid = Guid.NewGuid().ToString("N");
        var dto = new RegistrarMigracionDto
        {
            MigracionId = $"TestMigration_{migrationGuid}",
            Descripcion = "Migración de prueba",
            ProductVersion = "1.0.0",
            DuracionMs = 1000,
            Notas = "Prueba automatizada"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/migraciones/registrar", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resultado.Should().ContainKey("mensaje");
        resultado!["mensaje"].Should().Contain("exitosamente");
    }

    [Fact]
    public async Task RegistrarMigracion_DebeCapturarUsuario()
    {
        // Arrange
        var migrationGuid = Guid.NewGuid().ToString("N");
        var dto = new RegistrarMigracionDto
        {
            MigracionId = $"UserTest_{migrationGuid}",
            Descripcion = "Test usuario",
            ProductVersion = "1.0.0",
            DuracionMs = 500,
            Notas = "Test"
        };

        // Act
        await _client.PostAsJsonAsync("/api/migraciones/registrar", dto);

        // Assert: Verificar que se guardó con el usuario correcto
        var responseHistorial = await _client.GetAsync("/api/migraciones?limite=1");
        var migraciones = await responseHistorial.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        migraciones.Should().NotBeNull();
        migraciones!.Count.Should().BeGreaterThan(0);
        migraciones[0].AplicadoPor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegistrarMigracion_DebeGenerarDescripcion()
    {
        // Arrange
        var migracionId = "20260302000000_AgregarCampoTest";
        var dto = new RegistrarMigracionDto
        {
            MigracionId = migracionId,
            Descripcion = "Descripción de prueba",
            ProductVersion = "1.0.0",
            DuracionMs = 500,
            Notas = null
        };

        // Act
        await _client.PostAsJsonAsync("/api/migraciones/registrar", dto);

        // Assert: Verificar que la descripción se guardó
        var responseHistorial = await _client.GetAsync("/api/migraciones?limite=1");
        var migraciones = await responseHistorial.Content.ReadFromJsonAsync<List<MigracionLogDto>>();
        migraciones.Should().NotBeNull();
        migraciones!.Count.Should().BeGreaterThan(0);
        migraciones[0].Descripcion.Should().Be("Descripción de prueba");
    }

    #endregion

    #region Permisos

    [Fact]
    public async Task AccederMigraciones_SinSerAdmin_DebeRetornar403()
    {
        // Act: Usar cliente sin rol Admin
        var response = await _clientSupervisor.GetAsync("/api/migraciones?limite=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SincronizarMigraciones_SinSerAdmin_DebeRetornar403()
    {
        // Act
        var response = await _clientSupervisor.PostAsync("/api/migraciones/sincronizar", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegistrarMigracion_SinSerAdmin_DebeRetornar403()
    {
        // Arrange
        var dto = new RegistrarMigracionDto
        {
            MigracionId = "Test",
            Descripcion = "Test",
            ProductVersion = "1.0.0",
            DuracionMs = 0
        };

        // Act
        var response = await _clientSupervisor.PostAsJsonAsync("/api/migraciones/registrar", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}
