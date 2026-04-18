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

    public int EmpresaTestId { get; private set; }
    public int CategoriaTestId { get; private set; }
    public int SucursalPPId { get; private set; }
    public int SucursalFIFOId { get; private set; }
    public int SucursalLIFOId { get; private set; }
    /// <summary>
    /// Sucursal con PerfilTributario="GRAN_CONTRIBUYENTE" para tests de retenciones.
    /// Las reglas seeded matchean PerfilVendedor=REGIMEN_ORDINARIO + PerfilComprador=GRAN_CONTRIBUYENTE.
    /// </summary>
    public int SucursalRetencionId { get; private set; }
    public int TerceroTestId { get; private set; }
    public int ConceptoComprasId { get; private set; }
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

        // Reemplazar JWT por TestAuthHandler (sin proveedor de identidad real en CI/tests)
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.RemoveAll<IAuthenticationHandlerProvider>();

            services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    options => { options.DefaultEmail = "admin@sincopos.com"; });

            // Reemplazar el proveedor de identidad de WorkOS por un Mock para que los tests
            // de integración no intenten salir a internet con API Keys reales
            services.RemoveAll<POS.Application.Services.IIdentityProviderService>();
            services.AddSingleton<POS.Application.Services.IIdentityProviderService, MockIdentityProviderService>();

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
            TRUNCATE TABLE public.erp_outbox_messages RESTART IDENTITY CASCADE;
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

        // Seed: Empresa de prueba (requerida por FK_sucursales_Empresas_EmpresaId)
        var empresa = new POS.Infrastructure.Data.Entities.Empresa
        {
            Nombre = "Empresa Test",
            Nit = "900-TEST-001",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();
        EmpresaTestId = empresa.Id;

        // Seed: Categoria
        var categoria = new POS.Infrastructure.Data.Entities.Categoria
        {
            Nombre = "Categoria Test",
            Descripcion = "Para pruebas",
            EmpresaId = empresa.Id,
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
            CentroCosto = "CC-TEST-01",
            EmpresaId = empresa.Id,
            Activo = true
        };
        var sucFIFO = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = "Suc PEPS",
            MetodoCosteo = POS.Infrastructure.Data.Entities.MetodoCosteo.PEPS,
            EmpresaId = empresa.Id,
            Activo = true
        };
        var sucLIFO = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = "Suc UEPS",
            MetodoCosteo = POS.Infrastructure.Data.Entities.MetodoCosteo.UEPS,
            EmpresaId = empresa.Id,
            Activo = true
        };
        var sucRetencion = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = "Suc Retencion (Gran Contribuyente)",
            MetodoCosteo = POS.Infrastructure.Data.Entities.MetodoCosteo.PromedioPonderado,
            CentroCosto = "CC-RTE-01",
            EmpresaId = empresa.Id,
            PerfilTributario = "GRAN_CONTRIBUYENTE",
            CodigoMunicipio = "11001", // Bogotá (para ReteICA)
            ValorUVT = 47065m,
            Activo = true
        };
        context.Sucursales.AddRange(sucPP, sucFIFO, sucLIFO, sucRetencion);
        await context.SaveChangesAsync();
        SucursalPPId = sucPP.Id;
        SucursalFIFOId = sucFIFO.Id;
        SucursalLIFOId = sucLIFO.Id;
        SucursalRetencionId = sucRetencion.Id;

        // Seed: Usuarios de prueba (ExternalIds sincronizados con TestAuthHandler)
        var usuarioAdmin = new POS.Infrastructure.Data.Entities.Usuario
        {
            ExternalId = "00000000-0000-0000-0000-000000000001",
            Email = "admin@sincopos.com",
            NombreCompleto = "Admin Test",
            Rol = "admin",
            SucursalDefaultId = sucPP.Id,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        var usuarioSupervisor = new POS.Infrastructure.Data.Entities.Usuario
        {
            ExternalId = "00000000-0000-0000-0000-000000000002",
            Email = "supervisor@sincopos.com",
            NombreCompleto = "Supervisor Test",
            Rol = "supervisor",
            SucursalDefaultId = sucPP.Id,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        var usuarioCajero = new POS.Infrastructure.Data.Entities.Usuario
        {
            ExternalId = "00000000-0000-0000-0000-000000000003",
            Email = "cajero@sincopos.com",
            NombreCompleto = "Cajero Test",
            Rol = "cajero",
            SucursalDefaultId = sucPP.Id,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        context.Usuarios.AddRange(usuarioAdmin, usuarioSupervisor, usuarioCajero);
        await context.SaveChangesAsync();
        UsuarioTestId = usuarioAdmin.Id;

        // Asignar todos los usuarios a la sucursal principal (middleware resuelve empresa de aquí)
        context.UsuarioSucursales.AddRange(
            new POS.Infrastructure.Data.Entities.UsuarioSucursal { UsuarioId = usuarioAdmin.Id,      SucursalId = sucPP.Id, FechaAsignacion = DateTime.UtcNow },
            new POS.Infrastructure.Data.Entities.UsuarioSucursal { UsuarioId = usuarioSupervisor.Id, SucursalId = sucPP.Id, FechaAsignacion = DateTime.UtcNow },
            new POS.Infrastructure.Data.Entities.UsuarioSucursal { UsuarioId = usuarioCajero.Id,     SucursalId = sucPP.Id, FechaAsignacion = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Seed: Tercero proveedor
        var tercero = new POS.Infrastructure.Data.Entities.Tercero
        {
            Nombre = "Proveedor Test",
            Identificacion = "TEST-NIT-001",
            TipoTercero = POS.Infrastructure.Data.Entities.TipoTercero.Proveedor,
            EmpresaId = empresa.Id,
            Activo = true
        };
        context.Terceros.Add(tercero);

        // RE-SEED IMPUESTOS Y RETENCIONES (Como fue truncado, inicializamos los básicos)
        var fecha = DateTime.UtcNow;
        var exento = new POS.Infrastructure.Data.Entities.Impuesto { Nombre = "Exento", Tipo = POS.Infrastructure.Data.Entities.TipoImpuesto.IVA, Porcentaje = 0m, CodigoCuentaContable = "2408", AplicaSobreBase = true, CodigoPais = "CO", EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        var iva5 = new POS.Infrastructure.Data.Entities.Impuesto { Nombre = "IVA 5%", Tipo = POS.Infrastructure.Data.Entities.TipoImpuesto.IVA, Porcentaje = 0.05m, CodigoCuentaContable = "2408", AplicaSobreBase = true, CodigoPais = "CO", EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        var iva19 = new POS.Infrastructure.Data.Entities.Impuesto { Nombre = "IVA 19%", Tipo = POS.Infrastructure.Data.Entities.TipoImpuesto.IVA, Porcentaje = 0.19m, CodigoCuentaContable = "2408", AplicaSobreBase = true, CodigoPais = "CO", EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        context.Impuestos.AddRange(exento, iva5, iva19);

        var concepto1 = new POS.Infrastructure.Data.Entities.ConceptoRetencion { Nombre = "Honorarios", CodigoDian = "2301", PorcentajeSugerido = 11m, EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        var concepto2 = new POS.Infrastructure.Data.Entities.ConceptoRetencion { Nombre = "Compras", CodigoDian = "2307", PorcentajeSugerido = 2.5m, EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        context.ConceptosRetencion.AddRange(concepto1, concepto2);

        var retefuente = new POS.Infrastructure.Data.Entities.RetencionRegla { Nombre = "ReteFuente Compras 2.5%", Tipo = POS.Infrastructure.Data.Entities.TipoRetencion.ReteFuente, Porcentaje = 0.025m, BaseMinUVT = 4m, PerfilVendedor = "REGIMEN_ORDINARIO", PerfilComprador = "GRAN_CONTRIBUYENTE", CodigoCuentaContable = "1355", ConceptoRetencion = concepto2, EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        var reteica = new POS.Infrastructure.Data.Entities.RetencionRegla { Nombre = "ReteICA Bogotá 0.966%", Tipo = POS.Infrastructure.Data.Entities.TipoRetencion.ReteICA, Porcentaje = 0.00966m, BaseMinUVT = 0m, CodigoMunicipio = "11001", PerfilVendedor = "REGIMEN_ORDINARIO", PerfilComprador = "GRAN_CONTRIBUYENTE", CodigoCuentaContable = "1356", EmpresaId = empresa.Id, Activo = true, FechaCreacion = fecha };
        context.RetencionesReglas.AddRange(retefuente, reteica);

        // Seed tramos bebidas azucaradas (Ley 2277/2022) — requeridos por TaxEngine
        if (!context.TramosBebidasAzucaradas.Any())
        {
            context.TramosBebidasAzucaradas.AddRange(
                new POS.Infrastructure.Data.Entities.TramoBebidasAzucaradas { MaxGramosPor100ml = 6m,   ValorPor100ml = 18m, VigenciaDesde = new DateOnly(2023, 1, 1), Activo = true },
                new POS.Infrastructure.Data.Entities.TramoBebidasAzucaradas { MaxGramosPor100ml = 10m,  ValorPor100ml = 35m, VigenciaDesde = new DateOnly(2023, 1, 1), Activo = true },
                new POS.Infrastructure.Data.Entities.TramoBebidasAzucaradas { MaxGramosPor100ml = null, ValorPor100ml = 55m, VigenciaDesde = new DateOnly(2023, 1, 1), Activo = true }
            );
        }

        await context.SaveChangesAsync();
        TerceroTestId = tercero.Id;
        ConceptoComprasId = concepto2.Id;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
