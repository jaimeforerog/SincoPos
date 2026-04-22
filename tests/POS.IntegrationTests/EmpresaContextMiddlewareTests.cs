using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para EmpresaContextMiddleware.
/// Verifica las tres rutas de resolución de empresa:
///   1) Header X-Empresa-Id validado via JOIN usuario_sucursales
///   2) Header X-Empresa-Id validado via fallback admin (rol='admin' + empresa activa)
///   3) Sin header → primera empresa de las sucursales del usuario (fallback DB)
///   4) Usuario desconocido → EmpresaId null → sin filtro (todos los datos visibles)
/// </summary>
[Collection("POS")]
public class EmpresaContextMiddlewareTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // ExternalId fijo del supervisor (ver TestAuthHandler)
    private const string SupervisorExternalId  = "00000000-0000-0000-0000-000000000002";
    private const string SupervisorEmail      = "supervisor@sincopos.com";

    public EmpresaContextMiddlewareTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Empresa empresa, Sucursal sucursal)> CrearEmpresaConSucursalAsync(
        AppDbContext ctx, string nombre, string nitSuffix)
    {
        var empresa = new Empresa
        {
            Nombre = nombre,
            Nit    = $"900-MW-{nitSuffix}",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Empresas.Add(empresa);
        await ctx.SaveChangesAsync();

        var sucursal = new Sucursal
        {
            Nombre     = $"Suc-MW-{nombre}",
            EmpresaId  = empresa.Id,
            Activo     = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor  = "test"
        };
        ctx.Sucursales.Add(sucursal);
        await ctx.SaveChangesAsync();

        return (empresa, sucursal);
    }

    /// <summary>Crea (o reutiliza) el usuario supervisor en la DB de tests.</summary>
    private async Task<Usuario> ObtenerOCrearSupervisorAsync(AppDbContext ctx)
    {
        var usuario = await ctx.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ExternalId == SupervisorExternalId);

        if (usuario != null) return usuario;

        usuario = new Usuario
        {
            ExternalId      = SupervisorExternalId,
            Email           = SupervisorEmail,
            NombreCompleto  = "Supervisor Test Middleware",
            Rol             = "supervisor",
            SucursalDefaultId = _factory.SucursalPPId,
            Activo          = true,
            FechaCreacion   = DateTime.UtcNow
        };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        return usuario;
    }

    /// <summary>Elimina todas las asignaciones del supervisor para garantizar aislamiento.</summary>
    private async Task LimpiarUsuarioSucursalesSupervisorAsync(AppDbContext ctx, int supervisorId)
    {
        var asignaciones = await ctx.UsuarioSucursales
            .Where(us => us.UsuarioId == supervisorId)
            .ToListAsync();
        ctx.UsuarioSucursales.RemoveRange(asignaciones);
        await ctx.SaveChangesAsync();
    }

    private HttpClient CrearClienteSupervisor()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", SupervisorEmail);
        return client;
    }

    // ─── Test 1: Header validado via usuario_sucursales ───────────────────────

    [Fact]
    public async Task Middleware_HeaderEmpresaId_Validado_Via_UsuarioSucursales_AplicaFiltro()
    {
        // Arrange: dos empresas con una sucursal cada una
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaA, sucursalA) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaA", "MWA");
        var (empresaB, sucursalB) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaB", "MWB");

        // Asignar supervisor a la sucursal de empresa A
        var supervisor = await ObtenerOCrearSupervisorAsync(ctx);
        await LimpiarUsuarioSucursalesSupervisorAsync(ctx, supervisor.Id);

        ctx.UsuarioSucursales.Add(new UsuarioSucursal
        {
            UsuarioId  = supervisor.Id,
            SucursalId = sucursalA.Id,
            FechaAsignacion = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Act: solicitar con X-Empresa-Id = empresa A
        var client = CrearClienteSupervisor();
        client.DefaultRequestHeaders.Add("X-Empresa-Id", empresaA.Id.ToString());

        var response = await client.GetAsync("/api/v1/Sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paginado = await response.Content.ReadFromJsonAsync<PaginatedResult<SucursalResumenDto>>(_json);
        var sucursales = paginado?.Items;
        sucursales.Should().NotBeNull();

        // Assert: solo aparece la sucursal de empresa A
        sucursales!.Should().Contain(s => s.Id == sucursalA.Id,
            "la sucursal asignada al supervisor debe ser visible");
        sucursales.Should().NotContain(s => s.Id == sucursalB.Id,
            "la sucursal de empresa B no debe filtrarse con el contexto de empresa A");
    }

    // ─── Test 2: Header validado via fallback admin ────────────────────────────

    [Fact]
    public async Task Middleware_HeaderEmpresaId_Fallback_Admin_SinSucursalAsignada_AplicaFiltro()
    {
        // Arrange: dos empresas; el admin NO tiene usuario_sucursales para ninguna
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaC, sucursalC) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaC", "MWC");
        var (empresaD, sucursalD) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaD", "MWD");

        // Act: admin envía X-Empresa-Id = C (admin no tiene usuario_sucursales para esa empresa)
        // → middleware usa fallback admin: verifica rol='admin' + empresa activa
        var client = _factory.CreateClient(); // admin por defecto
        client.DefaultRequestHeaders.Add("X-Empresa-Id", empresaC.Id.ToString());

        var response = await client.GetAsync("/api/v1/Sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paginado = await response.Content.ReadFromJsonAsync<PaginatedResult<SucursalResumenDto>>(_json);
        var sucursales = paginado?.Items;
        sucursales.Should().NotBeNull();

        // Assert: sucursal C visible, sucursal D NO
        sucursales!.Should().Contain(s => s.Id == sucursalC.Id,
            "el admin debe acceder a empresa C vía fallback admin");
        sucursales.Should().NotContain(s => s.Id == sucursalD.Id,
            "empresa D no debe aparecer cuando el contexto es empresa C");
    }

    // ─── Test 3: Sin header → fallback primera empresa de usuario_sucursales ──

    [Fact]
    public async Task Middleware_SinHeader_FallbackPrimeraEmpresaDeSucursalesUsuario()
    {
        // Arrange: supervisor con UNA sola sucursal asignada (empresa F)
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaF, sucursalF) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaF", "MWF");
        var (_, sucursalG) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaG", "MWG");

        var supervisor = await ObtenerOCrearSupervisorAsync(ctx);
        // Limpiar y asignar SOLO sucursal F al supervisor
        await LimpiarUsuarioSucursalesSupervisorAsync(ctx, supervisor.Id);
        ctx.UsuarioSucursales.Add(new UsuarioSucursal
        {
            UsuarioId   = supervisor.Id,
            SucursalId  = sucursalF.Id,
            FechaAsignacion = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Act: supervisor hace una petición SIN header X-Empresa-Id
        // → middleware usa el fallback DB: primera empresa de usuario_sucursales
        var client = CrearClienteSupervisor();
        // SIN X-Empresa-Id

        var response = await client.GetAsync("/api/v1/Sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paginado = await response.Content.ReadFromJsonAsync<PaginatedResult<SucursalResumenDto>>(_json);
        var sucursales = paginado?.Items;
        sucursales.Should().NotBeNull();

        // Assert: empresa F resuelta automáticamente → sucursal F visible, sucursal G NO
        sucursales!.Should().Contain(s => s.Id == sucursalF.Id,
            "el fallback debe resolver empresa F desde usuario_sucursales");
        sucursales.Should().NotContain(s => s.Id == sucursalG.Id,
            "empresa G no debe aparecer cuando el contexto auto-resuelto es empresa F");
    }

    // ─── Test 4: Usuario desconocido → EmpresaId null → sin filtro ───────────

    [Fact]
    public async Task Middleware_UsuarioDesconocido_EmpresaIdNull_NoFiltroAplicado()
    {
        // Arrange: insertar al menos una sucursal con empresa para tener datos
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, sucursalH) = await CrearEmpresaConSucursalAsync(ctx, "MWEmpresaH", "MWH");

        // Act: usuario que NO existe en la DB → middleware no puede resolver empresa
        // → EmpresaId queda null → filtro global deshabilitado → todos los datos visibles
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "desconocido_mw@noexiste.com");

        var response = await client.GetAsync("/api/v1/Sucursales");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paginado = await response.Content.ReadFromJsonAsync<PaginatedResult<SucursalResumenDto>>(_json);
        var sucursales = paginado?.Items;
        sucursales.Should().NotBeNull();

        // Assert: sucursal H es visible porque no hay filtro de empresa aplicado
        sucursales!.Should().Contain(s => s.Id == sucursalH.Id,
            "sin empresa context (EmpresaId=null), todos los datos son visibles");
    }

    // ─── Test 5: Empresa inactiva en header → no se aplica el contexto ────────

    [Fact]
    public async Task Middleware_EmpresaInactiva_EnHeader_NoSetEmpresaId()
    {
        // Arrange: empresa inactiva
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var empresaInactiva = new Empresa
        {
            Nombre = "MWEmpresaInactiva",
            Nit    = "900-MW-INACT",
            Activo = false, // inactiva
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Empresas.Add(empresaInactiva);
        await ctx.SaveChangesAsync();
        // IgnoreQueryFilters necesario porque Activo=false queda filtrado por el soft-delete global,
        // impidiendo que EF Core resuelva el Id generado de la empresa inactiva.
        var empresaInactivaId = await ctx.Empresas.IgnoreQueryFilters()
            .Where(e => e.Nit == "900-MW-INACT")
            .Select(e => e.Id)
            .FirstAsync();

        var sucursalOtraEmpresa = new Sucursal
        {
            Nombre    = "Suc-MW-OtraEmpresa",
            EmpresaId = empresaInactivaId, // empresa inactiva — visible cuando EmpresaId del provider es null
            Activo    = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor = "test"
        };
        ctx.Sucursales.Add(sucursalOtraEmpresa);
        await ctx.SaveChangesAsync();

        // Act: admin envía X-Empresa-Id = empresa inactiva
        // → admin fallback falla (empresa no está activa) → EmpresaId null
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Empresa-Id", empresaInactiva.Id.ToString());

        var response = await client.GetAsync("/api/v1/Sucursales");

        // Assert: respuesta exitosa (no error) aunque la empresa sea inactiva;
        // el middleware hace fallback silencioso → EmpresaId null → datos visibles
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paginado = await response.Content.ReadFromJsonAsync<PaginatedResult<SucursalResumenDto>>(_json);
        var sucursales = paginado?.Items;
        sucursales.Should().NotBeNull();
    }

    // ─── DTO local para deserializar la respuesta de sucursales ──────────────
    private record SucursalResumenDto(int Id, string Nombre);
}
