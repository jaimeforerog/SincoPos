using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using POS.Application.DTOs;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo de Facturación Electrónica.
/// Verifica: configuración emisor (CRUD), listado de documentos y control de acceso.
/// </summary>
[Collection("POS-Auth")]
public class FacturacionTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string AdminEmail = "admin@sincopos.com";
    private const string SupervisorEmail = "supervisor@sincopos.com";
    private const string CajeroEmail = "cajero@sincopos.com";

    private int SucursalId => _factory.SucursalPPId;

    public FacturacionTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ─── Configuración Emisor ────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerConfiguracion_SinConfigurar_Devuelve404()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var idInexistente = 999_999;

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{idInexistente}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActualizarConfiguracion_ComoAdmin_Devuelve204()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var dto = BuildConfiguracionDto();

        var response = await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{SucursalId}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ObtenerConfiguracion_DespuesDeGuardar_DebeRetornarDatos()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var dto = BuildConfiguracionDto(nit: "800555777", razonSocial: "EMPRESA FACTURACION SAS");

        await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{SucursalId}", dto);

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{SucursalId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<ConfiguracionEmisorDto>(_jsonOptions);
        config.Should().NotBeNull();
        config!.Nit.Should().Be("800555777");
        config.RazonSocial.Should().Be("EMPRESA FACTURACION SAS");
        config.SucursalId.Should().Be(SucursalId);
    }

    [Fact]
    public async Task ObtenerConfiguracion_SinCertificado_TieneCertificadoDebeSer_False()
    {
        // Usar SucursalFIFOId para evitar interferencia con el test del certificado (que usa SucursalPPId)
        var sucursalSinCert = _factory.SucursalFIFOId;
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var dto = BuildConfiguracionDto(certificadoBase64: null);

        await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{sucursalSinCert}", dto);

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{sucursalSinCert}");
        var config = await response.Content.ReadFromJsonAsync<ConfiguracionEmisorDto>(_jsonOptions);

        config!.TieneCertificado.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerConfiguracion_ConCertificado_TieneCertificadoDebeSer_True()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        // Simular un certificado base64 (no tiene que ser válido para este test)
        var certFicticio = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        var dto = BuildConfiguracionDto(certificadoBase64: certFicticio);

        await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{SucursalId}", dto);

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{SucursalId}");
        var config = await response.Content.ReadFromJsonAsync<ConfiguracionEmisorDto>(_jsonOptions);

        config!.TieneCertificado.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarConfiguracion_Idempotente_PermiteMultiplesUpserts()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        // Primera actualización
        var dto1 = BuildConfiguracionDto(prefijo: "FV1");
        await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{SucursalId}", dto1);

        // Segunda actualización con datos diferentes
        var dto2 = BuildConfiguracionDto(prefijo: "FV2");
        var response2 = await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{SucursalId}", dto2);
        response2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // El último valor debe prevalecer
        var getResponse = await client.GetAsync($"/api/v1/Facturacion/configuracion/{SucursalId}");
        var config = await getResponse.Content.ReadFromJsonAsync<ConfiguracionEmisorDto>(_jsonOptions);
        config!.Prefijo.Should().Be("FV2");
    }

    // ─── Control de Acceso ───────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerConfiguracion_ComoSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{SucursalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ObtenerConfiguracion_ComoCajero_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{SucursalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActualizarConfiguracion_ComoSupervisor_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var dto = BuildConfiguracionDto();

        var response = await client.PutAsJsonAsync($"/api/v1/Facturacion/configuracion/{SucursalId}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ObtenerConfiguracion_SinAutenticacion_Devuelve401()
    {
        var client = _factory.CreateClient(); // sin header X-Test-User

        var response = await client.GetAsync($"/api/v1/Facturacion/configuracion/{SucursalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Listado de Documentos ───────────────────────────────────────────────

    [Fact]
    public async Task ListarDocumentos_ComoAdmin_Devuelve200_ConListaVacia()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync(
            $"/api/v1/Facturacion/documentos?sucursalId={SucursalId}&pageNumber=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resultado = await response.Content
            .ReadFromJsonAsync<PaginatedResult<DocumentoElectronicoDto>>(_jsonOptions);
        resultado.Should().NotBeNull();
        resultado!.Items.Should().NotBeNull();
        resultado.TotalCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ListarDocumentos_ComoSupervisor_Devuelve200()
    {
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        var response = await client.GetAsync("/api/v1/Facturacion/documentos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListarDocumentos_ComoCajero_Devuelve403()
    {
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        var response = await client.GetAsync("/api/v1/Facturacion/documentos");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ObtenerDocumento_IdInexistente_Devuelve404()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Facturacion/documentos/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DescargarXml_IdInexistente_Devuelve404()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.GetAsync("/api/v1/Facturacion/documentos/999999/xml");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reintentar_IdInexistente_Devuelve404()
    {
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        var response = await client.PostAsync("/api/v1/Facturacion/documentos/999999/reintentar", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Helper ─────────────────────────────────────────────────────────────

    private static ActualizarConfiguracionEmisorDto BuildConfiguracionDto(
        string nit = "900123456",
        string razonSocial = "EMPRESA TEST SAS",
        string prefijo = "FV",
        string? certificadoBase64 = null) => new(
        Nit: nit,
        DigitoVerificacion: "7",
        RazonSocial: razonSocial,
        NombreComercial: "Test Store",
        Direccion: "Calle 1 #2-3",
        CodigoMunicipio: "11001",
        CodigoDepartamento: "11",
        Telefono: "3001234567",
        Email: "test@empresa.com",
        CodigoCiiu: "4711",
        PerfilTributario: "REGIMEN_ORDINARIO",
        NumeroResolucion: "18764000001",
        FechaResolucion: new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        Prefijo: prefijo,
        NumeroDesde: 1,
        NumeroHasta: 100_000,
        FechaVigenciaDesde: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaVigenciaHasta: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Ambiente: "2",
        PinSoftware: "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c",
        IdSoftware: "3f211e78-dc16-4438-9c55-accfc38e1c10",
        CertificadoBase64: certificadoBase64,
        CertificadoPassword: null
    );
}
