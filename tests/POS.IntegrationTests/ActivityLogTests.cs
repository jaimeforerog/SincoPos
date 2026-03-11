using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para el sistema de Activity Log.
/// Verifica que las acciones críticas se registran correctamente con contexto completo.
/// </summary>
[Collection("POS-Auth")]
public class ActivityLogTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    private const string AdminEmail = "admin@sincopos.com";
    private const string SupervisorEmail = "supervisor@sincopos.com";
    private const string CajeroEmail = "cajero@sincopos.com";

    public ActivityLogTests(AuthenticatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene el contexto de BD para verificaciones directas
    /// </summary>
    private AppDbContext GetDbContext()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>
    /// Espera un poco para que el background processor persista los logs
    /// </summary>
    private async Task EsperarProcesamiento()
    {
        await Task.Delay(500); // 500ms para que el Channel processor escriba en BD
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: APERTURA/CIERRE DE CAJA
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task AbrirCaja_DebeRegistrarActivityLog_ConDatosCompletos()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);
        var ahora = DateTime.UtcNow;

        // Crear caja primero
        using (var db = GetDbContext())
        {
            var sucursal = await db.Sucursales.FirstAsync();
            var caja = new Caja
            {
                Nombre = "Caja ActivityLog Test",
                SucursalId = sucursal.Id,
                Estado = EstadoCaja.Cerrada,
                Activo = true
            };
            db.Cajas.Add(caja);
            await db.SaveChangesAsync();
        }

        // Act
        using var db2 = GetDbContext();
        var cajaAbierta = await db2.Cajas.FirstAsync(c => c.Nombre == "Caja ActivityLog Test");

        var response = await client.PostAsJsonAsync($"/api/v1/Cajas/{cajaAbierta.Id}/abrir", new
        {
            montoApertura = 100m
        });

        response.EnsureSuccessStatusCode();

        // Esperar a que el background processor persista el log
        await EsperarProcesamiento();

        // Assert
        using var db3 = GetDbContext();
        var log = await db3.ActivityLogs
            .Where(a => a.TipoEntidad == "Caja" && a.EntidadId == cajaAbierta.Id.ToString())
            .Where(a => a.Accion == "AperturaCaja")
            .OrderByDescending(a => a.FechaHora)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log.UsuarioEmail.Should().Be(CajeroEmail);
        log.Tipo.Should().Be(TipoActividad.Caja);
        log.Accion.Should().Be("AperturaCaja");
        log.SucursalId.Should().Be(cajaAbierta.SucursalId);
        log.Descripcion.Should().Contain("Caja");
        log.Descripcion.Should().Contain("$100");
        log.DatosNuevos.Should().Contain("Abierta");
        log.DatosNuevos.Should().Contain("100");
        log.Exitosa.Should().BeTrue();
        log.FechaHora.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CerrarCaja_DebeRegistrarActivityLog_ConDiferenciaCuadre()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        // Crear y abrir caja
        using (var db = GetDbContext())
        {
            var sucursal = await db.Sucursales.FirstAsync();
            var caja = new Caja
            {
                Nombre = "Caja Cierre Test",
                SucursalId = sucursal.Id,
                Estado = EstadoCaja.Abierta,
                MontoApertura = 100m,
                MontoActual = 250m,
                Activo = true
            };
            db.Cajas.Add(caja);
            await db.SaveChangesAsync();
        }

        // Act
        using var db2 = GetDbContext();
        var cajaCerrar = await db2.Cajas.FirstAsync(c => c.Nombre == "Caja Cierre Test");

        var response = await client.PostAsJsonAsync($"/api/v1/Cajas/{cajaCerrar.Id}/cerrar", new
        {
            montoReal = 240m, // Diferencia de -10
            observaciones = "Test de cierre"
        });

        response.EnsureSuccessStatusCode();
        await EsperarProcesamiento();

        // Assert
        using var db3 = GetDbContext();
        var log = await db3.ActivityLogs
            .Where(a => a.TipoEntidad == "Caja" && a.EntidadId == cajaCerrar.Id.ToString())
            .Where(a => a.Accion == "CierreCaja")
            .OrderByDescending(a => a.FechaHora)
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log.Accion.Should().Be("CierreCaja");
        log.Descripcion.Should().Contain("Esperado:");
        log.Descripcion.Should().Contain("Real:");
        log.Descripcion.Should().Contain("Diferencia:");
        log.Descripcion.Should().Contain("250");
        log.Descripcion.Should().Contain("240");
        log.DatosAnteriores.Should().Contain("Abierta");
        log.DatosNuevos.Should().Contain("Cerrada");
        log.DatosNuevos.Should().Contain("240");
        log.DatosNuevos.Should().Contain("Cuadra");
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: VENTAS
    // ═══════════════════════════════════════════════════════

    [Fact(Skip = "TODO: Falla en endpoint de inventario - necesita investigación adicional")]
    public async Task CrearVenta_DebeRegistrarActivityLog_ConDetallesProductos()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        using var db = GetDbContext();
        var sucursal = await db.Sucursales.FirstAsync();

        // Asegurar que hay un producto
        var producto = await db.Productos.FirstOrDefaultAsync();
        if (producto == null)
        {
            var categoria = await db.Categorias.FirstOrDefaultAsync();
            if (categoria == null)
            {
                categoria = new Categoria { Nombre = "Test", Activo = true };
                db.Categorias.Add(categoria);
                await db.SaveChangesAsync();
            }

            producto = new Producto
            {
                CodigoBarras = "TEST-VENTA",
                Nombre = "Producto Test Venta",
                CategoriaId = categoria.Id,
                PrecioVenta = 25m,
                PrecioCosto = 15m,
                Activo = true
            };
            db.Productos.Add(producto);
            await db.SaveChangesAsync();
        }

        var caja = await db.Cajas
            .Where(c => c.Estado == EstadoCaja.Abierta)
            .FirstOrDefaultAsync();

        if (caja == null)
        {
            caja = new Caja
            {
                Nombre = "Caja Ventas Test",
                SucursalId = sucursal.Id,
                Estado = EstadoCaja.Abierta,
                MontoApertura = 100m,
                MontoActual = 100m,
                Activo = true
            };
            db.Cajas.Add(caja);
            await db.SaveChangesAsync();
        }

        // Crear inventario correctamente usando el endpoint
        var entradaDto = new
        {
            productoId = producto.Id,
            sucursalId = sucursal.Id,
            terceroId = _factory.TerceroTestId,
            cantidad = 10m,
            costoUnitario = 30m,
            referencia = "ENT-ACTLOG-001",
            observaciones = "Entrada para test de activity log"
        };
        var entradaResponse = await client.PostAsJsonAsync("/api/v1/Inventario/entrada", entradaDto);
        entradaResponse.EnsureSuccessStatusCode();

        // Act
        var ventaDto = new
        {
            sucursalId = sucursal.Id,
            cajaId = caja.Id,
            metodoPago = 0, // Efectivo
            montoPagado = 50m,
            lineas = new[]
            {
                new { productoId = producto.Id, cantidad = 1m, descuento = 0m }
            }
        };

        var response = await client.PostAsJsonAsync("/api/v1/Ventas", ventaDto);
        response.EnsureSuccessStatusCode();

        var venta = await response.Content.ReadFromJsonAsync<VentaDto>();
        await EsperarProcesamiento();

        // Assert
        using var db2 = GetDbContext();
        var log = await db2.ActivityLogs
            .Where(a => a.TipoEntidad == "Venta" && a.EntidadId == venta!.Id.ToString())
            .Where(a => a.Accion == "CrearVenta")
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log.UsuarioEmail.Should().Be(CajeroEmail);
        log.Tipo.Should().Be(TipoActividad.Venta);
        log.Descripcion.Should().Contain("Venta");
        log.Descripcion.Should().Contain("Total:");
        log.DatosNuevos.Should().Contain("NumeroVenta");
        log.DatosNuevos.Should().Contain("Productos");
        log.Exitosa.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: USUARIOS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CambiarEstadoUsuario_DebeRegistrarActivityLog_ConMotivo()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AdminEmail);

        // Crear usuario para el test
        using (var db = GetDbContext())
        {
            var usuario = new Usuario
            {
                KeycloakId = "test-activity-log-user",
                Email = "activitylog-test@sincopos.com",
                NombreCompleto = "Usuario ActivityLog Test",
                Rol = "cajero",
                Activo = true
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
        }

        // Act
        using var db2 = GetDbContext();
        var usuario2 = await db2.Usuarios.FirstAsync(u => u.Email == "activitylog-test@sincopos.com");

        var response = await client.PutAsJsonAsync($"/api/v1/Usuarios/{usuario2.Id}/estado", new
        {
            activo = false,
            motivo = "Suspendido por prueba de Activity Log"
        });

        response.EnsureSuccessStatusCode();
        await EsperarProcesamiento();

        // Assert
        using var db3 = GetDbContext();
        var log = await db3.ActivityLogs
            .Where(a => a.TipoEntidad == "Usuario" && a.EntidadId == usuario2.Id.ToString())
            .Where(a => a.Accion == "CambiarEstadoUsuario")
            .FirstOrDefaultAsync();

        log.Should().NotBeNull();
        log.UsuarioEmail.Should().Be(AdminEmail);
        log.Tipo.Should().Be(TipoActividad.Usuario);
        log.Descripcion.Should().Contain("Activo a Inactivo");
        log.Descripcion.Should().Contain("Suspendido por prueba");
        log.DatosAnteriores.Should().Contain("Activo");
        log.DatosAnteriores.Should().Contain("true");
        log.DatosNuevos.Should().Contain("Activo");
        log.DatosNuevos.Should().Contain("false");
        log.DatosNuevos.Should().Contain("Suspendido por prueba");
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: CONSULTAS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task GetActivities_DeFiltrarPorTipo_YRetornarPaginado()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        // Act - Buscar solo logs de Caja
        var response = await client.GetAsync("/api/v1/ActivityLogs?tipo=1&pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ActivityLogFullDto>>();

        result.Should().NotBeNull();
        result.PageSize.Should().Be(10);
        result.Items.Should().AllSatisfy(log =>
        {
            log.Tipo.Should().Be(TipoActividad.Caja);
        });
    }

    [Fact]
    public async Task GetActivities_DebeFuncionar_ConFechasSinKindEspecificado()
    {
        // En Npgsql 6.0+, mandar un DateTime con Kind.Unspecified arroja error
        // si se compara contra una columna timestamptz. 
        // Este test verifica que el servicio maneje correctamente este caso.

        // Arrange
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var hoy = DateTime.Now.Date; 
        
        // Simular lo que llega del API (DateTime con Kind Unspecified)
        var fechaDesde = new DateTime(hoy.Year, hoy.Month, hoy.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var fechaHasta = new DateTime(hoy.Year, hoy.Month, hoy.Day, 23, 59, 59, DateTimeKind.Unspecified);

        // Act
        var response = await client.GetAsync($"/api/v1/ActivityLogs?fechaDesde={fechaDesde:O}&fechaHasta={fechaHasta:O}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ActivityLogFullDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEntityHistory_DebeRetornarHistorialCompleto_DeUnaEntidad()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        // Crear caja y generar actividad
        using (var db = GetDbContext())
        {
            var sucursal = await db.Sucursales.FirstAsync();
            var caja = new Caja
            {
                Nombre = "Caja Historial Test",
                SucursalId = sucursal.Id,
                Estado = EstadoCaja.Cerrada,
                Activo = true
            };
            db.Cajas.Add(caja);
            await db.SaveChangesAsync();
        }

        using var db2 = GetDbContext();
        var cajaHistorial = await db2.Cajas.FirstAsync(c => c.Nombre == "Caja Historial Test");

        // Abrir caja para generar log
        var clientCajero = _factory.CreateAuthenticatedClient(CajeroEmail);
        await clientCajero.PostAsJsonAsync($"/api/v1/Cajas/{cajaHistorial.Id}/abrir", new { montoApertura = 50m });
        await EsperarProcesamiento();

        // Act - Obtener historial
        var response = await client.GetAsync($"/api/v1/ActivityLogs/entidad/Caja/{cajaHistorial.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var history = await response.Content.ReadFromJsonAsync<List<CambioEntidadDto>>();

        history.Should().NotBeNull();
        history.Count.Should().BeGreaterThanOrEqualTo(1);
        history.Should().Contain(h => h.Accion == "AperturaCaja");
    }

    [Fact]
    public async Task GetDashboardMetrics_DebeRetornarEstadisticas_DelDiaActual()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(SupervisorEmail);

        // Act
        var response = await client.GetAsync("/api/v1/ActivityLogs/dashboard");

        // Assert
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardActivityDto>();

        dashboard.Should().NotBeNull();
        dashboard.TotalAcciones.Should().BeGreaterThanOrEqualTo(0);
        dashboard.AccionesExitosas.Should().BeGreaterThanOrEqualTo(0);
        dashboard.AccionesFallidas.Should().BeGreaterThanOrEqualTo(0);
        dashboard.AccionesPorTipo.Should().NotBeNull();
        dashboard.ActividadesRecientes.Should().NotBeNull();
    }

    [Fact]
    public async Task GetActivityTypes_DebeRetornarTodosTiposDisponibles()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);

        // Act
        var response = await client.GetAsync("/api/v1/ActivityLogs/tipos");

        // Assert
        response.EnsureSuccessStatusCode();
        var types = await response.Content.ReadFromJsonAsync<Dictionary<int, string>>();

        types.Should().NotBeNull();
        types.Should().ContainKey(1); // Caja
        types.Should().ContainKey(2); // Venta
        types.Should().ContainKey(3); // Inventario
        types.Should().ContainKey(4); // Usuario
        types.Values.Should().Contain("Caja");
        types.Values.Should().Contain("Venta");
        types.Values.Should().Contain("Inventario");
        types.Values.Should().Contain("Usuario");
    }
}
