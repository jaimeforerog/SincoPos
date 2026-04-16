using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas de integración para el módulo Multi-Empresa.
/// Verifica: asignación de EmpresaId al crear entidades, filtros globales de tenant,
/// y compatibilidad hacia atrás con registros sin empresa (catálogo global).
/// </summary>
[Collection("POS")]
public class MultiEmpresaTests
{
    private readonly CustomWebApplicationFactory _factory;

    public MultiEmpresaTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ─── Productos ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Producto_Crear_AsignaEmpresaId_DelProvider()
    {
        // Arrange: scope con empresa = 5
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICurrentEmpresaProvider>();
        provider.EmpresaId = 5;

        var service = scope.ServiceProvider.GetRequiredService<IProductoService>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Con filtro estricto la categoria debe pertenecer a la misma empresa
        var categoriaEmpresa5 = new Categoria
        {
            Nombre = "Categoria Empresa 5 Test",
            Activo = true,
            EmpresaId = 5
        };
        context.Categorias.Add(categoriaEmpresa5);
        await context.SaveChangesAsync();

        var dto = new CrearProductoDto(
            CodigoBarras: "EMP-ME-PROD-001",
            Nombre: "Producto MultiEmpresa Test",
            Descripcion: null,
            CategoriaId: categoriaEmpresa5.Id,
            PrecioVenta: 1000m,
            PrecioCosto: 600m,
            ImpuestoId: null,
            EsAlimentoUltraprocesado: false,
            GramosAzucarPor100ml: null,
            UnidadMedida: "94",
            ConceptoRetencionId: null,
            ManejaLotes: false,
            DiasVidaUtil: null
        );

        // Act
        var (result, error) = await service.CrearAsync(dto);

        // Assert
        error.Should().BeNull();
        result.Should().NotBeNull();

        var productoEnDb = await context.Productos
            .IgnoreQueryFilters()
            .FirstAsync(p => p.CodigoBarras == "EMP-ME-PROD-001");
        productoEnDb.EmpresaId.Should().Be(5);
    }

    [Fact]
    public async Task Producto_FiltroGlobal_ExcluyeProductoDeOtraEmpresa()
    {
        // Arrange: insertar producto de empresa 1 y empresa 2 directamente en DB
        using var insertScope = _factory.Services.CreateScope();
        var insertCtx = insertScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var prodEmpresa1 = new Producto
        {
            Id = Guid.NewGuid(),
            CodigoBarras = "EMP-FILTER-E1",
            Nombre = "Prod Empresa 1",
            CategoriaId = _factory.CategoriaTestId,
            PrecioVenta = 100m,
            PrecioCosto = 60m,
            UnidadMedida = "94",
            EmpresaId = 1,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        var prodEmpresa2 = new Producto
        {
            Id = Guid.NewGuid(),
            CodigoBarras = "EMP-FILTER-E2",
            Nombre = "Prod Empresa 2",
            CategoriaId = _factory.CategoriaTestId,
            PrecioVenta = 100m,
            PrecioCosto = 60m,
            UnidadMedida = "94",
            EmpresaId = 2,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        insertCtx.Productos.AddRange(prodEmpresa1, prodEmpresa2);
        await insertCtx.SaveChangesAsync();

        // Act: scope con empresa = 1 → solo debe ver empresa 1
        using var queryScope = _factory.Services.CreateScope();
        var provider = queryScope.ServiceProvider.GetRequiredService<ICurrentEmpresaProvider>();
        provider.EmpresaId = 1;
        var queryCtx = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var productosVisibles = await queryCtx.Productos
            .Where(p => p.Activo && (p.CodigoBarras == "EMP-FILTER-E1" || p.CodigoBarras == "EMP-FILTER-E2"))
            .ToListAsync();

        // Assert
        productosVisibles.Should().ContainSingle(p => p.CodigoBarras == "EMP-FILTER-E1");
        productosVisibles.Should().NotContain(p => p.CodigoBarras == "EMP-FILTER-E2");
    }

    [Fact]
    public async Task Producto_FiltroEstricto_ExcluyeProductoDeOtraEmpresa_EnContextoEmpresa()
    {
        // Arrange: producto asignado a empresa 1 — invisible cuando el contexto es empresa 99
        using var insertScope = _factory.Services.CreateScope();
        var insertCtx = insertScope.ServiceProvider.GetRequiredService<AppDbContext>();

        insertCtx.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CodigoBarras = "EMP-GLOBAL-001",
            Nombre = "Producto Sin Empresa",
            CategoriaId = _factory.CategoriaTestId,
            PrecioVenta = 100m,
            PrecioCosto = 60m,
            UnidadMedida = "94",
            EmpresaId = 1,  // empresa distinta a 99 — filtro estricto la excluye
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        });
        await insertCtx.SaveChangesAsync();

        // Act: scope con empresa = 99 (filtro estricto activo)
        using var queryScope = _factory.Services.CreateScope();
        var provider = queryScope.ServiceProvider.GetRequiredService<ICurrentEmpresaProvider>();
        provider.EmpresaId = 99;
        var queryCtx = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var productosVisibles = await queryCtx.Productos
            .Where(p => p.CodigoBarras == "EMP-GLOBAL-001")
            .ToListAsync();

        // Assert: filtro estricto — productos de otra empresa NO son visibles en contexto de empresa 99
        // (comportamiento post-corrección multi-empresa 2026-03-23)
        productosVisibles.Should().BeEmpty(
            "con filtro estricto, un producto de empresa 1 no es visible " +
            "en el contexto de empresa 99");
    }

    [Fact]
    public async Task Producto_SinContextoEmpresa_VeTodosLosProductos()
    {
        // Arrange: producto de empresa 10
        using var insertScope = _factory.Services.CreateScope();
        var insertCtx = insertScope.ServiceProvider.GetRequiredService<AppDbContext>();

        insertCtx.Productos.Add(new Producto
        {
            Id = Guid.NewGuid(),
            CodigoBarras = "EMP-BYPASS-001",
            Nombre = "Producto Empresa 10 Bypass",
            CategoriaId = _factory.CategoriaTestId,
            PrecioVenta = 100m,
            PrecioCosto = 60m,
            UnidadMedida = "94",
            EmpresaId = 10,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        });
        await insertCtx.SaveChangesAsync();

        // Act: scope sin empresa (null) → debe ver todos
        using var queryScope = _factory.Services.CreateScope();
        var provider = queryScope.ServiceProvider.GetRequiredService<ICurrentEmpresaProvider>();
        provider.EmpresaId = null;  // sin contexto = modo administrador / background
        var queryCtx = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var productosVisibles = await queryCtx.Productos
            .Where(p => p.CodigoBarras == "EMP-BYPASS-001")
            .ToListAsync();

        // Assert: con EmpresaId=null el filtro se bypasea
        productosVisibles.Should().ContainSingle();
    }

    // ─── EmpresasController ────────────────────────────────────────────────────

    [Fact]
    public async Task Empresas_GetAll_RetornaListaVacia_CuandoNoHayEmpresas()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/v1/empresas");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var lista = await resp.Content.ReadFromJsonAsync<List<object>>();
        lista.Should().NotBeNull();
    }

    [Fact]
    public async Task Empresas_Crear_DevuelveEmpresaCreada()
    {
        var client = _factory.CreateClient();
        var body = new { nombre = "Empresa Test ME", nit = "900-TEST-ME-1", razonSocial = "Empresa Test ME S.A.S." };
        var resp = await client.PostAsJsonAsync("/api/v1/empresas", body);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var empresa = await resp.Content.ReadFromJsonAsync<POS.Application.DTOs.EmpresaDto>();
        empresa.Should().NotBeNull();
        empresa!.Nombre.Should().Be("Empresa Test ME");
        empresa.Nit.Should().Be("900-TEST-ME-1");
        empresa.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Empresas_Crear_Falla_SiNombreVacio()
    {
        var client = _factory.CreateClient();
        var body   = new { nombre = "", nit = "111" };
        var resp   = await client.PostAsJsonAsync("/api/v1/empresas", body);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Empresas_Actualizar_ModificaEmpresaExistente()
    {
        // Crear primero
        var client  = _factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/v1/empresas",
            new { nombre = "Empresa Actualizar", nit = "900-UPD-ME" }))
            .Content.ReadFromJsonAsync<POS.Application.DTOs.EmpresaDto>();

        // Actualizar
        var updateBody = new { nombre = "Empresa Actualizada OK", nit = "900-UPD-ME", razonSocial = "Actualizada", activo = true };
        var resp = await client.PutAsJsonAsync($"/api/v1/empresas/{created!.Id}", updateBody);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<POS.Application.DTOs.EmpresaDto>();
        updated!.Nombre.Should().Be("Empresa Actualizada OK");
        updated.RazonSocial.Should().Be("Actualizada");
    }

    [Fact]
    public async Task Empresas_GetById_Retorna404_CuandoNoExiste()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/v1/empresas/99999");
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ─── Terceros ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tercero_Crear_AsignaEmpresaId_DelProvider()
    {
        // Arrange: scope con empresa = 7
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICurrentEmpresaProvider>();
        provider.EmpresaId = 7;

        var service = scope.ServiceProvider.GetRequiredService<ITerceroService>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dto = new CrearTerceroDto
        {
            TipoIdentificacion = "NIT",
            Identificacion = "900-EMPRESA-ME-TEST",
            Nombre = "Tercero MultiEmpresa Test",
            TipoTercero = "Cliente",
        };

        // Act
        var (result, error) = await service.CrearAsync(dto);

        // Assert
        error.Should().BeNull();
        result.Should().NotBeNull();

        var terceroEnDb = await context.Terceros
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Identificacion == "900-EMPRESA-ME-TEST");
        terceroEnDb.EmpresaId.Should().Be(7);
    }

    // ─── Entidades Transaccionales ────────────────────────────────────────────

    // ─── Entidades Transaccionales ────────────────────────────────────────────

    /// <summary>Crea una Empresa + Sucursal vinculada para tests transaccionales.</summary>
    private async Task<(Empresa empresa, Sucursal sucursal)> CrearEmpresaConSucursalAsync(
        POS.Infrastructure.Data.AppDbContext ctx, string nombre, string nitSuffix)
    {
        var empresa = new Empresa
        {
            Nombre = nombre,
            Nit = $"900-TEST-{nitSuffix}",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Empresas.Add(empresa);
        await ctx.SaveChangesAsync();

        var sucursal = new POS.Infrastructure.Data.Entities.Sucursal
        {
            Nombre = $"Suc {nombre}",
            EmpresaId = empresa.Id,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor = "test"
        };
        ctx.Sucursales.Add(sucursal);
        await ctx.SaveChangesAsync();

        return (empresa, sucursal);
    }

    [Fact]
    public async Task Caja_AsignaEmpresaId_DesdeSucursal()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        var (empresa, sucursal) = await CrearEmpresaConSucursalAsync(ctx, "Empresa Caja Test", "CAJ-01");

        var caja = new POS.Infrastructure.Data.Entities.Caja
        {
            Nombre = "Caja-Trans-Test",
            EmpresaId = sucursal.EmpresaId,
            SucursalId = sucursal.Id,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor = "test"
        };
        ctx.Cajas.Add(caja);
        await ctx.SaveChangesAsync();

        var cajaEnDb = await ctx.Cajas.IgnoreQueryFilters().FirstAsync(c => c.Id == caja.Id);
        cajaEnDb.EmpresaId.Should().Be(empresa.Id);
    }

    [Fact]
    public async Task Caja_FiltroGlobal_ExcluyeCajaDeOtraEmpresa()
    {
        using var insertScope = _factory.Services.CreateScope();
        var insertCtx = insertScope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        var (emp1, suc1) = await CrearEmpresaConSucursalAsync(insertCtx, "Empresa Filtro Caja 1", "FC1");
        var (emp2, suc2) = await CrearEmpresaConSucursalAsync(insertCtx, "Empresa Filtro Caja 2", "FC2");

        insertCtx.Cajas.AddRange(
            new POS.Infrastructure.Data.Entities.Caja
            {
                Nombre = $"Caja-F-{emp1.Id}", EmpresaId = emp1.Id, SucursalId = suc1.Id,
                Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = "test"
            },
            new POS.Infrastructure.Data.Entities.Caja
            {
                Nombre = $"Caja-F-{emp2.Id}", EmpresaId = emp2.Id, SucursalId = suc2.Id,
                Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = "test"
            }
        );
        await insertCtx.SaveChangesAsync();

        using var queryScope = _factory.Services.CreateScope();
        var provider = queryScope.ServiceProvider.GetRequiredService<POS.Application.Services.ICurrentEmpresaProvider>();
        provider.EmpresaId = emp1.Id;
        var queryCtx = queryScope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        var cajas = await queryCtx.Cajas
            .Where(c => c.SucursalId == suc1.Id || c.SucursalId == suc2.Id)
            .ToListAsync();

        cajas.Should().ContainSingle(c => c.SucursalId == suc1.Id);
        cajas.Should().NotContain(c => c.SucursalId == suc2.Id);
    }

    [Fact]
    public async Task OrdenCompra_AsignaEmpresaId_DesdeSucursal()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<POS.Infrastructure.Data.AppDbContext>();

        var (empresa, sucursal) = await CrearEmpresaConSucursalAsync(ctx, "Empresa OC Test", "OC-01");

        var orden = new POS.Infrastructure.Data.Entities.OrdenCompra
        {
            NumeroOrden = $"OC-ME-TEST-{empresa.Id}",
            EmpresaId = empresa.Id,
            SucursalId = sucursal.Id,
            ProveedorId = _factory.TerceroTestId,
            Estado = POS.Infrastructure.Data.Entities.EstadoOrdenCompra.Pendiente,
            FechaOrden = DateTime.UtcNow,
            Subtotal = 1000m, Impuestos = 190m, Total = 1190m,
            Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = "test"
        };
        ctx.OrdenesCompra.Add(orden);
        await ctx.SaveChangesAsync();

        var ordenEnDb = await ctx.OrdenesCompra
            .IgnoreQueryFilters()
            .FirstAsync(o => o.NumeroOrden == orden.NumeroOrden);
        ordenEnDb.EmpresaId.Should().Be(empresa.Id);
    }
}
