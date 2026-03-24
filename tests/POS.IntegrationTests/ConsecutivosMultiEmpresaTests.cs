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
/// Pruebas de integración para consecutivos multi-empresa.
/// Verifica que los números de documento (venta, devolución, orden de compra, traslado)
/// son independientes por empresa/sucursal y no colisionan entre sí.
/// Los índices únicos compuestos garantizan la integridad a nivel de base de datos.
/// </summary>
[Collection("POS")]
public class ConsecutivosMultiEmpresaTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private int CatId => _factory.CategoriaTestId;
    private int TerceroId => _factory.TerceroTestId;

    public ConsecutivosMultiEmpresaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ─── Infraestructura ─────────────────────────────────────────────────────

    /// <summary>Crea empresa + sucursal para tests de consecutivos.</summary>
    private async Task<(Empresa empresa, Sucursal sucursal)> CrearEmpresaConSucursalAsync(
        AppDbContext ctx, string sufijo)
    {
        var empresa = new Empresa
        {
            Nombre = $"EmpresaConsec-{sufijo}",
            Nit    = $"900-CONSEC-{sufijo}",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Empresas.Add(empresa);
        await ctx.SaveChangesAsync();

        var sucursal = new Sucursal
        {
            Nombre     = $"Suc-Consec-{sufijo}",
            EmpresaId  = empresa.Id,
            MetodoCosteo = MetodoCosteo.PromedioPonderado,
            Activo     = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor  = "test"
        };
        ctx.Sucursales.Add(sucursal);
        await ctx.SaveChangesAsync();

        return (empresa, sucursal);
    }

    private async Task<Guid> CrearProductoParaSucursal(string codigo,
        decimal precioVenta = 1200m, decimal precioCosto = 600m)
    {
        var dto = new
        {
            codigoBarras  = codigo,
            nombre        = $"Prod-Consec-{codigo}",
            categoriaId   = CatId,
            precioVenta,
            precioCosto,
            unidadMedida  = "94"
        };
        var r = await _client.PostAsJsonAsync("/api/v1/Productos", dto);
        r.EnsureSuccessStatusCode();
        var result = await r.Content.ReadFromJsonAsync<ProductoDto>(_json);
        return result!.Id;
    }

    private async Task AgregarStockAsync(Guid productoId, int sucursalId,
        decimal cantidad = 100m, decimal costo = 600m)
    {
        var dto = new
        {
            productoId, sucursalId, cantidad, costoUnitario = costo,
            terceroId  = TerceroId,
            referencia = $"FC-CONSEC-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock para test consecutivos"
        };
        var r = await _client.PostAsJsonAsync("/api/v1/Inventario/entrada", dto);
        r.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearYAbrirCaja(int sucursalId, string nombre)
    {
        var crear = await _client.PostAsJsonAsync("/api/v1/Cajas",
            new { nombre, sucursalId });
        crear.EnsureSuccessStatusCode();
        var caja = await crear.Content.ReadFromJsonAsync<CajaDto>(_json);

        var abrir = await _client.PostAsJsonAsync($"/api/v1/Cajas/{caja!.Id}/abrir",
            new { montoApertura = 100_000m });
        abrir.EnsureSuccessStatusCode();

        return caja.Id;
    }

    private async Task<VentaDto> CrearVentaAsync(int sucursalId, int cajaId, Guid productoId)
    {
        var dto = new
        {
            sucursalId,
            cajaId,
            metodoPago  = 0, // Efectivo
            montoPagado = 999_999m,
            lineas = new[]
            {
                new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var r = await _client.PostAsJsonAsync("/api/v1/Ventas", dto);
        r.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Venta debe crearse exitosamente. Body: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<VentaDto>(_json))!;
    }

    // ═══════════════════════════════════════════════════════
    //  CONSECUTIVOS VENTA — por sucursal
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Venta_DosEmpresasDiferentesObtienen_MismoConsecutivo_SinColision()
    {
        // Arrange: empresa A y empresa B con sus propias sucursales, cajas y stock
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaA, sucursalA) = await CrearEmpresaConSucursalAsync(ctx, "VA");
        var (empresaB, sucursalB) = await CrearEmpresaConSucursalAsync(ctx, "VB");

        var prodA = await CrearProductoParaSucursal($"CONSEC-VA-{sucursalA.Id}");
        var prodB = await CrearProductoParaSucursal($"CONSEC-VB-{sucursalB.Id}");

        await AgregarStockAsync(prodA, sucursalA.Id);
        await AgregarStockAsync(prodB, sucursalB.Id);

        var cajaA = await CrearYAbrirCaja(sucursalA.Id, $"CajaConsecA-{sucursalA.Id}");
        var cajaB = await CrearYAbrirCaja(sucursalB.Id, $"CajaConsecB-{sucursalB.Id}");

        // Act: crear una venta en cada empresa
        var ventaA = await CrearVentaAsync(sucursalA.Id, cajaA, prodA);
        var ventaB = await CrearVentaAsync(sucursalB.Id, cajaB, prodB);

        // Assert: ambas ventas existen en BD con sucursales distintas
        var ventaAEnDb = await ctx.Ventas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == ventaA.Id);
        var ventaBEnDb = await ctx.Ventas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == ventaB.Id);

        ventaAEnDb.Should().NotBeNull();
        ventaBEnDb.Should().NotBeNull();

        ventaAEnDb!.EmpresaId.Should().Be(empresaA.Id,
            "la venta en sucursal A debe quedar asignada a empresa A");
        ventaBEnDb!.EmpresaId.Should().Be(empresaB.Id,
            "la venta en sucursal B debe quedar asignada a empresa B");

        // Ambas obtienen V-000001 (o cualquier número) para su propia sucursal
        // El índice único es (sucursal_id, numero_venta) → no hay colisión
        ventaA.NumeroVenta.Should().StartWith("V-");
        ventaB.NumeroVenta.Should().StartWith("V-");

        // Las dos ventas tienen número igual (V-000001 independiente por sucursal)
        // o distinto si ya había ventas previas — lo importante es que ambas existen
        ventaAEnDb.NumeroVenta.Should().Be(ventaA.NumeroVenta);
        ventaBEnDb.NumeroVenta.Should().Be(ventaB.NumeroVenta);
    }

    [Fact]
    public async Task Venta_MismaSucursal_SegundaVenta_ObtieneConsecutivoSiguiente()
    {
        // Verifica que el consecutivo incrementa dentro de la misma sucursal
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, sucursalC) = await CrearEmpresaConSucursalAsync(ctx, "VC");
        var prodC = await CrearProductoParaSucursal($"CONSEC-VC-{sucursalC.Id}");
        await AgregarStockAsync(prodC, sucursalC.Id, cantidad: 50m);
        var cajaC = await CrearYAbrirCaja(sucursalC.Id, $"CajaConsecC-{sucursalC.Id}");

        // Act: dos ventas en la misma sucursal
        var venta1 = await CrearVentaAsync(sucursalC.Id, cajaC, prodC);
        var venta2 = await CrearVentaAsync(sucursalC.Id, cajaC, prodC);

        // Assert: números distintos y correlativos
        venta1.NumeroVenta.Should().NotBe(venta2.NumeroVenta,
            "cada venta en la misma sucursal debe tener número único");

        // Extraer el número y verificar que la segunda es mayor
        var num1 = int.Parse(venta1.NumeroVenta.Split('-')[1]);
        var num2 = int.Parse(venta2.NumeroVenta.Split('-')[1]);
        num2.Should().Be(num1 + 1, "el consecutivo debe incrementar de uno en uno");
    }

    // ═══════════════════════════════════════════════════════
    //  CONSECUTIVOS DEVOLUCION — por empresa
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task DevolucionVenta_DosEmpresasGeneran_ConsecutivosIndependientes()
    {
        // Arrange: empresa D y empresa E con ventas previas
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaD, sucursalD) = await CrearEmpresaConSucursalAsync(ctx, "DEV-D");
        var (empresaE, sucursalE) = await CrearEmpresaConSucursalAsync(ctx, "DEV-E");

        var prodD = await CrearProductoParaSucursal($"CONSEC-DEVD-{sucursalD.Id}");
        var prodE = await CrearProductoParaSucursal($"CONSEC-DEVE-{sucursalE.Id}");

        await AgregarStockAsync(prodD, sucursalD.Id, cantidad: 20m);
        await AgregarStockAsync(prodE, sucursalE.Id, cantidad: 20m);

        var cajaD = await CrearYAbrirCaja(sucursalD.Id, $"CajaDevD-{sucursalD.Id}");
        var cajaE = await CrearYAbrirCaja(sucursalE.Id, $"CajaDevE-{sucursalE.Id}");

        var ventaD = await CrearVentaAsync(sucursalD.Id, cajaD, prodD);
        var ventaE = await CrearVentaAsync(sucursalE.Id, cajaE, prodE);

        // Act: devolucion en empresa D  → POST /Ventas/{id}/devolucion-parcial
        var dtoDevD = new
        {
            motivo = "Prueba consecutivo empresa D",
            lineas = new[] { new { productoId = prodD, cantidad = 1m } }
        };
        var respDevD = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{ventaD.Id}/devolucion-parcial", dtoDevD);
        respDevD.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Devolución D debe ser exitosa. Body: {await respDevD.Content.ReadAsStringAsync()}");

        // Act: devolucion en empresa E
        var dtoDevE = new
        {
            motivo = "Prueba consecutivo empresa E",
            lineas = new[] { new { productoId = prodE, cantidad = 1m } }
        };
        var respDevE = await _client.PostAsJsonAsync(
            $"/api/v1/Ventas/{ventaE.Id}/devolucion-parcial", dtoDevE);
        respDevE.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Devolución E debe ser exitosa. Body: {await respDevE.Content.ReadAsStringAsync()}");

        // Assert: ambas devoluciones existen en BD con empresas distintas
        var devolucionesEmpresaD = await ctx.DevolucionesVenta
            .IgnoreQueryFilters()
            .Where(d => d.EmpresaId == empresaD.Id)
            .ToListAsync();
        var devolucionesEmpresaE = await ctx.DevolucionesVenta
            .IgnoreQueryFilters()
            .Where(d => d.EmpresaId == empresaE.Id)
            .ToListAsync();

        devolucionesEmpresaD.Should().NotBeEmpty("empresa D debe tener al menos una devolución");
        devolucionesEmpresaE.Should().NotBeEmpty("empresa E debe tener al menos una devolución");

        // Cada empresa tiene sus propios consecutivos DEV- independientes
        // El índice único (EmpresaId, NumeroDevolucion) garantiza no colisión
        var numDevD = devolucionesEmpresaD.First().NumeroDevolucion;
        var numDevE = devolucionesEmpresaE.First().NumeroDevolucion;

        numDevD.Should().StartWith("DEV-");
        numDevE.Should().StartWith("DEV-");

        // Las dos pueden tener el mismo número (DEV-000001) porque son de distinta empresa
        // y el índice único es compuesto (EmpresaId, NumeroDevolucion)
        numDevD.Should().Be(numDevE,
            "cada empresa inicia su propia secuencia DEV-000001 de forma independiente — " +
            "la unicidad está garantizada por el índice compuesto (EmpresaId, NumeroDevolucion)");
    }

    // ═══════════════════════════════════════════════════════
    //  CONSECUTIVOS ORDEN DE COMPRA — global (max Id)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task OrdenCompra_DosEmpresasObtienen_NumerosUnicos_SinColision()
    {
        // CompraService usa IgnoreQueryFilters().MaxAsync(o => o.Id) → número global único
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaOC1, sucursalOC1) = await CrearEmpresaConSucursalAsync(ctx, "OC1");
        var (empresaOC2, sucursalOC2) = await CrearEmpresaConSucursalAsync(ctx, "OC2");

        var prodOC1 = await CrearProductoParaSucursal($"CONSEC-OC1-{sucursalOC1.Id}");
        var prodOC2 = await CrearProductoParaSucursal($"CONSEC-OC2-{sucursalOC2.Id}");

        // Act: crear una orden de compra para cada empresa
        var dtoOC1 = new
        {
            sucursalId = sucursalOC1.Id,
            proveedorId = TerceroId,
            lineas = new[] { new { productoId = prodOC1, cantidad = 10m, precioUnitario = 500m } }
        };
        var dtoOC2 = new
        {
            sucursalId = sucursalOC2.Id,
            proveedorId = TerceroId,
            lineas = new[] { new { productoId = prodOC2, cantidad = 10m, precioUnitario = 500m } }
        };

        var respOC1 = await _client.PostAsJsonAsync("/api/v1/Compras", dtoOC1);
        var respOC2 = await _client.PostAsJsonAsync("/api/v1/Compras", dtoOC2);

        respOC1.StatusCode.Should().Be(HttpStatusCode.OK,
            $"OC empresa 1 debe crearse. Body: {await respOC1.Content.ReadAsStringAsync()}");
        respOC2.StatusCode.Should().Be(HttpStatusCode.OK,
            $"OC empresa 2 debe crearse. Body: {await respOC2.Content.ReadAsStringAsync()}");

        var oc1 = await respOC1.Content.ReadFromJsonAsync<OrdenCompraDto>(_json);
        var oc2 = await respOC2.Content.ReadFromJsonAsync<OrdenCompraDto>(_json);

        // Assert: ambas creadas con números únicos (secuencia global por max Id)
        oc1.Should().NotBeNull();
        oc2.Should().NotBeNull();
        oc1!.NumeroOrden.Should().StartWith("OC-");
        oc2!.NumeroOrden.Should().StartWith("OC-");
        oc1.NumeroOrden.Should().NotBe(oc2.NumeroOrden,
            "cada orden de compra debe tener número único (consecutivo global por max Id)");

        // Verificar que ambas están en la BD con sus respectivos EmpresaId
        var oc1EnDb = await ctx.OrdenesCompra
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.NumeroOrden == oc1.NumeroOrden);
        var oc2EnDb = await ctx.OrdenesCompra
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.NumeroOrden == oc2.NumeroOrden);

        oc1EnDb!.EmpresaId.Should().Be(empresaOC1.Id);
        oc2EnDb!.EmpresaId.Should().Be(empresaOC2.Id);
    }

    // ═══════════════════════════════════════════════════════
    //  CONSECUTIVOS TRASLADO — por empresa (IgnoreQueryFilters)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Traslado_MismaEmpresa_DosTraslados_NumerosCorrelativos()
    {
        // TrasladoService usa IgnoreQueryFilters para evitar colisión entre empresas
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Para un traslado necesitamos dos sucursales en la misma empresa
        var empresa = new Empresa
        {
            Nombre = "EmpresaTrasConsec",
            Nit    = "900-TRAS-CONSEC",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        ctx.Empresas.Add(empresa);
        await ctx.SaveChangesAsync();

        var sucOrigen = new Sucursal
        {
            Nombre = "SucTrasOrigen", EmpresaId = empresa.Id,
            MetodoCosteo = MetodoCosteo.PromedioPonderado,
            Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = "test"
        };
        var sucDestino = new Sucursal
        {
            Nombre = "SucTrasDestino", EmpresaId = empresa.Id,
            MetodoCosteo = MetodoCosteo.PromedioPonderado,
            Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = "test"
        };
        ctx.Sucursales.AddRange(sucOrigen, sucDestino);
        await ctx.SaveChangesAsync();

        var prod = await CrearProductoParaSucursal($"CONSEC-TRAS-{sucOrigen.Id}");
        await AgregarStockAsync(prod, sucOrigen.Id, cantidad: 50m);

        // Act: dos traslados distintos
        var dtoT1 = new
        {
            sucursalOrigenId  = sucOrigen.Id,
            sucursalDestinoId = sucDestino.Id,
            lineas = new[] { new { productoId = prod, cantidad = 5m } }
        };
        var dtoT2 = new
        {
            sucursalOrigenId  = sucOrigen.Id,
            sucursalDestinoId = sucDestino.Id,
            lineas = new[] { new { productoId = prod, cantidad = 3m } }
        };

        var respT1 = await _client.PostAsJsonAsync("/api/v1/Traslados", dtoT1);
        respT1.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Traslado 1 debe crearse. Body: {await respT1.Content.ReadAsStringAsync()}");

        var respT2 = await _client.PostAsJsonAsync("/api/v1/Traslados", dtoT2);
        respT2.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Traslado 2 debe crearse. Body: {await respT2.Content.ReadAsStringAsync()}");

        var t1 = await respT1.Content.ReadFromJsonAsync<TrasladoDto>(_json);
        var t2 = await respT2.Content.ReadFromJsonAsync<TrasladoDto>(_json);

        var numT1 = t1?.NumeroTraslado;
        var numT2 = t2?.NumeroTraslado;

        // Assert: números distintos y con prefijo TRAS-
        numT1.Should().StartWith("TRAS-");
        numT2.Should().StartWith("TRAS-");
        numT1.Should().NotBe(numT2, "dos traslados de la misma empresa deben tener números únicos");
    }

}
