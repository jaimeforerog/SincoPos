using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        // Aplicar migraciones EF Core
        await context.Database.MigrateAsync();

        // Limpiar datos de tests anteriores
        await context.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE public.stock CASCADE;
            TRUNCATE TABLE public.lotes_inventario CASCADE;
            TRUNCATE TABLE public.productos CASCADE;
            TRUNCATE TABLE public.terceros CASCADE;
            TRUNCATE TABLE public.sucursales CASCADE;
            TRUNCATE TABLE public.categorias CASCADE;
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
