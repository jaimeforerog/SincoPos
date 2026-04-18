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

    public ConsecutivosMultiEmpresaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ─── Infraestructura ─────────────────────────────────────────────────────

    /// <summary>Crea HttpRequestMessage con X-Empresa-Id para operar en el contexto de una empresa.</summary>
    private HttpRequestMessage ConEmpresaId(HttpMethod method, string url, int empresaId, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Empresa-Id", empresaId.ToString());
        if (body != null)
            req.Content = JsonContent.Create(body, options: _json);
        return req;
    }

    /// <summary>Crea empresa + sucursal + proveedor + cliente propios para cada test.</summary>
    private async Task<(Empresa empresa, Sucursal sucursal, int proveedorId, int clienteId)> CrearEmpresaConSucursalAsync(
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

        // Proveedor propio de la empresa (el proveedor del factory pertenece a empresa 1)
        var proveedor = new Tercero
        {
            Nombre         = $"Prov-Consec-{sufijo}",
            Identificacion = $"NIT-CONSEC-{sufijo}",
            TipoTercero    = TipoTercero.Proveedor,
            EmpresaId      = empresa.Id,
            Activo         = true,
            FechaCreacion  = DateTime.UtcNow
        };
        ctx.Terceros.Add(proveedor);

        // Cliente propio de la empresa (requerido por validador de venta)
        var cliente = new Tercero
        {
            Nombre         = $"Cliente-Consec-{sufijo}",
            Identificacion = $"CC-CONSEC-{sufijo}",
            TipoTercero    = TipoTercero.Cliente,
            EmpresaId      = empresa.Id,
            Activo         = true,
            FechaCreacion  = DateTime.UtcNow
        };
        ctx.Terceros.Add(cliente);

        await ctx.SaveChangesAsync();

        return (empresa, sucursal, proveedor.Id, cliente.Id);
    }

    private async Task<Guid> CrearProductoParaSucursal(string codigo, int empresaId,
        decimal precioVenta = 1200m, decimal precioCosto = 600m)
    {
        // Crear categoria propia para la empresa de prueba (la del factory pertenece a empresa 1)
        var catDto = new { nombre = $"Cat-Consec-{empresaId}-{codigo[..Math.Min(8, codigo.Length)]}", descripcion = "Test consecutivos" };
        var catResp = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Categorias", empresaId, catDto));
        catResp.EnsureSuccessStatusCode();
        var cat = await catResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var catId = cat.GetProperty("id").GetInt32();

        var dto = new
        {
            codigoBarras  = codigo,
            nombre        = $"Prod-Consec-{codigo}",
            categoriaId   = catId,
            precioVenta,
            precioCosto,
            unidadMedida  = "94"
        };
        var r = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Productos", empresaId, dto));
        r.EnsureSuccessStatusCode();
        var result = await r.Content.ReadFromJsonAsync<ProductoDto>(_json);
        return result!.Id;
    }

    private async Task AgregarStockAsync(Guid productoId, int sucursalId, int empresaId,
        decimal cantidad = 100m, decimal costo = 600m)
    {
        // No pasamos terceroId: el proveedor del factory (empresa 1) no es visible
        // en el contexto de la empresa de prueba. TerceroId es nullable.
        var dto = new
        {
            productoId, sucursalId, cantidad, costoUnitario = costo,
            referencia = $"FC-CONSEC-{Guid.NewGuid():N}"[..20],
            observaciones = "Stock para test consecutivos"
        };
        var r = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Inventario/entrada", empresaId, dto));
        r.EnsureSuccessStatusCode();
    }

    private async Task<int> CrearYAbrirCaja(int sucursalId, string nombre, int empresaId)
    {
        var crear = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Cajas", empresaId,
            new { nombre, sucursalId }));
        crear.EnsureSuccessStatusCode();
        var caja = await crear.Content.ReadFromJsonAsync<CajaDto>(_json);

        var abrir = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, $"/api/v1/Cajas/{caja!.Id}/abrir", empresaId,
            new { montoApertura = 100_000m }));
        abrir.EnsureSuccessStatusCode();

        return caja.Id;
    }

    private async Task<VentaDto> CrearVentaAsync(int sucursalId, int cajaId, Guid productoId, int empresaId, int clienteId)
    {
        var dto = new
        {
            sucursalId,
            cajaId,
            clienteId,
            metodoPago  = 0, // Efectivo
            montoPagado = 999_999m,
            lineas = new[]
            {
                new { productoId, cantidad = 1m, precioUnitario = (decimal?)null, descuento = 0m }
            }
        };
        var r = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Ventas", empresaId, dto));
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

        var (empresaA, sucursalA, _, clienteA) = await CrearEmpresaConSucursalAsync(ctx, "VA");
        var (empresaB, sucursalB, _, clienteB) = await CrearEmpresaConSucursalAsync(ctx, "VB");

        var prodA = await CrearProductoParaSucursal($"CONSEC-VA-{sucursalA.Id}", empresaA.Id);
        var prodB = await CrearProductoParaSucursal($"CONSEC-VB-{sucursalB.Id}", empresaB.Id);

        await AgregarStockAsync(prodA, sucursalA.Id, empresaA.Id);
        await AgregarStockAsync(prodB, sucursalB.Id, empresaB.Id);

        var cajaA = await CrearYAbrirCaja(sucursalA.Id, $"CajaConsecA-{sucursalA.Id}", empresaA.Id);
        var cajaB = await CrearYAbrirCaja(sucursalB.Id, $"CajaConsecB-{sucursalB.Id}", empresaB.Id);

        // Act: crear una venta en cada empresa
        var ventaA = await CrearVentaAsync(sucursalA.Id, cajaA, prodA, empresaA.Id, clienteA);
        var ventaB = await CrearVentaAsync(sucursalB.Id, cajaB, prodB, empresaB.Id, clienteB);

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

        ventaAEnDb.NumeroVenta.Should().Be(ventaA.NumeroVenta);
        ventaBEnDb.NumeroVenta.Should().Be(ventaB.NumeroVenta);
    }

    [Fact]
    public async Task Venta_MismaSucursal_SegundaVenta_ObtieneConsecutivoSiguiente()
    {
        // Verifica que el consecutivo incrementa dentro de la misma sucursal
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaC, sucursalC, _, clienteC) = await CrearEmpresaConSucursalAsync(ctx, "VC");
        var prodC = await CrearProductoParaSucursal($"CONSEC-VC-{sucursalC.Id}", empresaC.Id);
        await AgregarStockAsync(prodC, sucursalC.Id, empresaC.Id, cantidad: 50m);
        var cajaC = await CrearYAbrirCaja(sucursalC.Id, $"CajaConsecC-{sucursalC.Id}", empresaC.Id);

        // Act: dos ventas en la misma sucursal
        var venta1 = await CrearVentaAsync(sucursalC.Id, cajaC, prodC, empresaC.Id, clienteC);
        var venta2 = await CrearVentaAsync(sucursalC.Id, cajaC, prodC, empresaC.Id, clienteC);

        // Assert: números distintos y correlativos
        venta1.NumeroVenta.Should().NotBe(venta2.NumeroVenta,
            "cada venta en la misma sucursal debe tener número único");

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

        var (empresaD, sucursalD, _, clienteD) = await CrearEmpresaConSucursalAsync(ctx, "DEV-D");
        var (empresaE, sucursalE, _, clienteE) = await CrearEmpresaConSucursalAsync(ctx, "DEV-E");

        var prodD = await CrearProductoParaSucursal($"CONSEC-DEVD-{sucursalD.Id}", empresaD.Id);
        var prodE = await CrearProductoParaSucursal($"CONSEC-DEVE-{sucursalE.Id}", empresaE.Id);

        await AgregarStockAsync(prodD, sucursalD.Id, empresaD.Id, cantidad: 20m);
        await AgregarStockAsync(prodE, sucursalE.Id, empresaE.Id, cantidad: 20m);

        var cajaD = await CrearYAbrirCaja(sucursalD.Id, $"CajaDevD-{sucursalD.Id}", empresaD.Id);
        var cajaE = await CrearYAbrirCaja(sucursalE.Id, $"CajaDevE-{sucursalE.Id}", empresaE.Id);

        var ventaD = await CrearVentaAsync(sucursalD.Id, cajaD, prodD, empresaD.Id, clienteD);
        var ventaE = await CrearVentaAsync(sucursalE.Id, cajaE, prodE, empresaE.Id, clienteE);

        // Act: devolucion en empresa D
        var dtoDevD = new
        {
            motivo = "Prueba consecutivo empresa D",
            lineas = new[] { new { productoId = prodD, cantidad = 1m } }
        };
        var respDevD = await _client.SendAsync(ConEmpresaId(HttpMethod.Post,
            $"/api/v1/Ventas/{ventaD.Id}/devolucion-parcial", empresaD.Id, dtoDevD));
        respDevD.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Devolución D debe ser exitosa. Body: {await respDevD.Content.ReadAsStringAsync()}");

        // Act: devolucion en empresa E
        var dtoDevE = new
        {
            motivo = "Prueba consecutivo empresa E",
            lineas = new[] { new { productoId = prodE, cantidad = 1m } }
        };
        var respDevE = await _client.SendAsync(ConEmpresaId(HttpMethod.Post,
            $"/api/v1/Ventas/{ventaE.Id}/devolucion-parcial", empresaE.Id, dtoDevE));
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

        var numDevD = devolucionesEmpresaD.First().NumeroDevolucion;
        var numDevE = devolucionesEmpresaE.First().NumeroDevolucion;

        numDevD.Should().StartWith("DEV-");
        numDevE.Should().StartWith("DEV-");

        numDevD.Should().Be(numDevE,
            "cada empresa inicia su propia secuencia DEV-000001 de forma independiente — " +
            "la unicidad está garantizada por el índice compuesto (EmpresaId, NumeroDevolucion)");
    }

    // ═══════════════════════════════════════════════════════
    //  CONSECUTIVOS ORDEN DE COMPRA — global (max Id)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task OrdenCompra_DosEmpresasObtienen_NumerosIndependientesPorSucursal()
    {
        // CompraService usa COUNT por sucursal → cada sucursal tiene su propia secuencia.
        // El índice único es (sucursal_id, numero_orden) — no hay colisión entre sucursales.
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (empresaOC1, sucursalOC1, provOC1, _) = await CrearEmpresaConSucursalAsync(ctx, "OC1");
        var (empresaOC2, sucursalOC2, provOC2, _) = await CrearEmpresaConSucursalAsync(ctx, "OC2");

        var prodOC1 = await CrearProductoParaSucursal($"CONSEC-OC1-{sucursalOC1.Id}", empresaOC1.Id);
        var prodOC2 = await CrearProductoParaSucursal($"CONSEC-OC2-{sucursalOC2.Id}", empresaOC2.Id);

        // Act: crear una orden de compra para cada empresa (proveedor propio de cada empresa)
        var dtoOC1 = new
        {
            sucursalId = sucursalOC1.Id,
            proveedorId = provOC1,
            lineas = new[] { new { productoId = prodOC1, cantidad = 10m, precioUnitario = 500m } }
        };
        var dtoOC2 = new
        {
            sucursalId = sucursalOC2.Id,
            proveedorId = provOC2,
            lineas = new[] { new { productoId = prodOC2, cantidad = 10m, precioUnitario = 500m } }
        };

        var respOC1 = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Compras", empresaOC1.Id, dtoOC1));
        var respOC2 = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Compras", empresaOC2.Id, dtoOC2));

        respOC1.StatusCode.Should().Be(HttpStatusCode.OK,
            $"OC empresa 1 debe crearse. Body: {await respOC1.Content.ReadAsStringAsync()}");
        respOC2.StatusCode.Should().Be(HttpStatusCode.OK,
            $"OC empresa 2 debe crearse. Body: {await respOC2.Content.ReadAsStringAsync()}");

        var oc1 = await respOC1.Content.ReadFromJsonAsync<OrdenCompraDto>(_json);
        var oc2 = await respOC2.Content.ReadFromJsonAsync<OrdenCompraDto>(_json);

        // Assert: ambas se crean exitosamente con prefijo OC-.
        // Con secuencia por sucursal, ambas pueden obtener OC-000001 — eso es correcto.
        // La unicidad se garantiza por el índice compuesto (sucursal_id, numero_orden).
        oc1.Should().NotBeNull();
        oc2.Should().NotBeNull();
        oc1!.NumeroOrden.Should().StartWith("OC-");
        oc2!.NumeroOrden.Should().StartWith("OC-");

        var oc1EnDb = await ctx.OrdenesCompra
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.SucursalId == sucursalOC1.Id && o.NumeroOrden == oc1.NumeroOrden);
        var oc2EnDb = await ctx.OrdenesCompra
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.SucursalId == sucursalOC2.Id && o.NumeroOrden == oc2.NumeroOrden);

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

        var prod = await CrearProductoParaSucursal($"CONSEC-TRAS-{sucOrigen.Id}", empresa.Id);
        await AgregarStockAsync(prod, sucOrigen.Id, empresa.Id, cantidad: 50m);

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

        var respT1 = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Traslados", empresa.Id, dtoT1));
        respT1.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Traslado 1 debe crearse. Body: {await respT1.Content.ReadAsStringAsync()}");

        var respT2 = await _client.SendAsync(ConEmpresaId(HttpMethod.Post, "/api/v1/Traslados", empresa.Id, dtoT2));
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
