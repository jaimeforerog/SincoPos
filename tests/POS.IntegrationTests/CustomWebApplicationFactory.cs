using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace POS.IntegrationTests;

/// <summary>
/// xUnit Collection que comparte una sola instancia del factory entre test classes.
/// </summary>
[CollectionDefinition("POS")]
public class PosCollection : ICollectionFixture<CustomWebApplicationFactory> { }

/// <summary>
/// Factory de integracion con PostgreSQL local (pos_test).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=pos_test;Username=postgres;Password=postgrade;Include Error Detail=true";

    public int CategoriaTestId { get; private set; }
    public int SucursalPPId { get; private set; }
    public int SucursalFIFOId { get; private set; }
    public int SucursalLIFOId { get; private set; }
    public int TerceroTestId { get; private set; }
    public int UsuarioTestId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = TestConnectionString
            });
        });

        builder.UseEnvironment("Development");

        // Reemplazar JWT por TestAuthHandler (sin Keycloak en CI/tests)
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.RemoveAll<IAuthenticationHandlerProvider>();

            services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    options => { options.DefaultEmail = "admin@sincopos.com"; });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName)
                    .Build();
                options.AddPolicy("Admin", policy => policy
                    .RequireAuthenticatedUser().RequireRole("admin")
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));
                options.AddPolicy("Supervisor", policy => policy
                    .RequireAuthenticatedUser().RequireRole("supervisor", "admin")
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));
                options.AddPolicy("Cajero", policy => policy
                    .RequireAuthenticatedUser().RequireRole("cajero", "supervisor", "admin")
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));
                options.AddPolicy("Vendedor", policy => policy
                    .RequireAuthenticatedUser().RequireRole("cajero", "supervisor", "admin", "vendedor")
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        // Recrear schema desde el modelo actual (sin depender de migraciones con Designer)
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Limpiar datos de tests anteriores y reiniciar secuencias de IDs
        await context.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE public.stock RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.lotes_inventario RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.productos RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.terceros RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.usuarios RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.sucursales RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.categorias RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.conceptos_retencion RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.configuracion_emisor RESTART IDENTITY CASCADE;
            TRUNCATE TABLE public.documentos_electronicos RESTART IDENTITY CASCADE;
        ");

        // Limpiar Marten event store
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE events.mt_events CASCADE;
                TRUNCATE TABLE events.mt_streams CASCADE;
            ");
        }
        catch { /* Marten tables may not exist yet */ }

        // Seed: Categoria
        var categoria = new POS.Infrastructure.Data.Entities.Categoria
        {
            Nombre = "Categoria Test",
            Descripcion = "Para pruebas",
            Activo = true
        };
        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();
        CategoriaTestId = categoria.Id;

        // Seed: 4 sucursales con metodos de costeo distintos
        var sucPP = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = "Suc PromedioPonderado",
            MetodoCosteo = POS.Infrastructure.Data.Entities.MetodoCosteo.PromedioPonderado,
            Activo = true
        };
        var sucFIFO = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = "Suc PEPS",
            MetodoCosteo = POS.Infrastructure.Data.Entities.MetodoCosteo.PEPS,
            Activo = true
        };
        var sucLIFO = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = "Suc UEPS",
            MetodoCosteo = POS.Infrastructure.Data.Entities.MetodoCosteo.UEPS,
            Activo = true
        };
        context.Sucursales.AddRange(sucPP, sucFIFO, sucLIFO);
        await context.SaveChangesAsync();
        SucursalPPId = sucPP.Id;
        SucursalFIFOId = sucFIFO.Id;
        SucursalLIFOId = sucLIFO.Id;

        // Seed: Usuario admin de prueba
        var usuarioAdmin = new POS.Infrastructure.Data.Entities.Usuario
        {
            KeycloakId = "test-keycloak-admin-001",
            Email = "admin@sincopos.com",
            NombreCompleto = "Admin Test",
            Rol = "admin",
            SucursalDefaultId = sucPP.Id,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        context.Usuarios.Add(usuarioAdmin);
        await context.SaveChangesAsync();
        UsuarioTestId = usuarioAdmin.Id;

        // Seed: Tercero proveedor
        var tercero = new POS.Infrastructure.Data.Entities.Tercero
        {
            Nombre = "Proveedor Test",
            Identificacion = "TEST-NIT-001",
            TipoTercero = POS.Infrastructure.Data.Entities.TipoTercero.Proveedor,
            Activo = true
        };
        context.Terceros.Add(tercero);
        await context.SaveChangesAsync();
        TerceroTestId = tercero.Id;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
