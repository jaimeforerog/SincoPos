using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.IntegrationTests;

/// <summary>
/// Tests de integración para el sistema de auditoría automática.
/// Verifica que los campos CreadoPor, FechaCreacion, ModificadoPor y FechaModificacion
/// se registran correctamente en todas las entidades auditables.
/// </summary>
[Collection("POS-Auth")]
public class AuditoriaTests
{
    private readonly AuthenticatedWebApplicationFactory _factory;

    private const string AdminEmail = "admin@sincopos.com";
    private const string SupervisorEmail = "supervisor@sincopos.com";
    private const string CajeroEmail = "cajero@sincopos.com";

    public AuditoriaTests(AuthenticatedWebApplicationFactory factory)
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

    // ═══════════════════════════════════════════════════════
    //  TESTS: CREACIÓN DE ENTIDADES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CrearCategoria_DebeRegistrarCreadoPor_ConUsuarioAutenticado()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var ahora = DateTime.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Categorias", new
        {
            nombre = "Categoria Auditoria Test",
            descripcion = "Test de auditoría"
        });

        // Assert
        response.EnsureSuccessStatusCode();
        var categoria = await response.Content.ReadFromJsonAsync<CategoriaDto>();
        categoria.Should().NotBeNull();

        // Verificar en base de datos
        using var db = GetDbContext();
        var categoriaDb = await db.Categorias.FindAsync(categoria!.Id);

        categoriaDb.Should().NotBeNull();
        categoriaDb!.CreadoPor.Should().Be(AdminEmail);
        categoriaDb.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
        categoriaDb.ModificadoPor.Should().BeNullOrEmpty();
        categoriaDb.FechaModificacion.Should().BeNull();
    }

    [Fact]
    public async Task CrearImpuesto_DebeRegistrarCreadoPor_ConDiferenteUsuario()
    {
        // Arrange - Los impuestos solo pueden ser creados por Admin
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var ahora = DateTime.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Impuestos", new
        {
            nombre = "IVA Test Auditoria",
            porcentaje = 0.19m
        });

        // Assert
        response.EnsureSuccessStatusCode();

        // Deserializar como tipo anónimo
        var json = await response.Content.ReadAsStringAsync();
        dynamic? impuesto = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);

        using var db = GetDbContext();
        var impuestoDB = await db.Impuestos.FindAsync((int)impuesto!.GetProperty("id").GetInt32());

        impuestoDB.Should().NotBeNull();
        impuestoDB!.CreadoPor.Should().Be(AdminEmail);
        impuestoDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CrearTercero_DebeRegistrarCreadoPor()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(CajeroEmail);
        var ahora = DateTime.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Terceros", new
        {
            nombre = "Cliente Test Auditoria",
            tipoIdentificacion = "NIT",
            identificacion = "AUD-123456",
            tipoTercero = "Cliente"
        });

        // Assert
        response.EnsureSuccessStatusCode();
        var tercero = await response.Content.ReadFromJsonAsync<TerceroDto>();

        using var db = GetDbContext();
        var terceroDB = await db.Terceros.FindAsync(tercero!.Id);

        terceroDB.Should().NotBeNull();
        terceroDB!.CreadoPor.Should().Be(CajeroEmail);
        terceroDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CrearProducto_DebeRegistrarCreadoPor()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var ahora = DateTime.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = "AUD-PROD-001",
            nombre = "Producto Auditoria Test",
            descripcion = "Test de auditoría",
            categoriaId = _factory.CategoriaTestId,
            precioVenta = 100m,
            precioCosto = 50m
        });

        // Assert
        response.EnsureSuccessStatusCode();
        var producto = await response.Content.ReadFromJsonAsync<ProductoDto>();

        using var db = GetDbContext();
        var productoDB = await db.Productos.FindAsync(producto!.Id);

        productoDB.Should().NotBeNull();
        productoDB!.CreadoPor.Should().Be(AdminEmail);
        productoDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
        productoDB.ModificadoPor.Should().BeNullOrEmpty();
        productoDB.FechaModificacion.Should().BeNull();
    }

    [Fact]
    public async Task CrearSinAutenticacion_DebeUsarSistemaComoCreadoPor()
    {
        // Arrange - Cliente sin header de autenticación
        var client = _factory.CreateClient();

        // Act - Crear directamente en BD (simula migración o seed)
        using var db = GetDbContext();
        var categoria = new Categoria
        {
            Nombre = "Categoria Sin Auth",
            Descripcion = "Test sin autenticación",
            Activo = true
        };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();

        // Assert
        categoria.CreadoPor.Should().Be("sistema");
        categoria.FechaCreacion.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: MODIFICACIÓN DE ENTIDADES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ModificarCategoria_DebeRegistrarModificadoPor()
    {
        // Arrange - Crear como Admin
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var crearResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Categorias", new
        {
            nombre = "Categoria Para Modificar",
            descripcion = "Original"
        });
        var categoria = await crearResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        await Task.Delay(100); // Pequeña pausa para diferenciar timestamps

        // Act - Modificar como Supervisor
        var clientSupervisor = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var ahoraModificacion = DateTime.UtcNow;

        var modificarResponse = await clientSupervisor.PutAsJsonAsync(
            $"/api/v1/Categorias/{categoria!.Id}",
            new
            {
                nombre = "Categoria Modificada",
                descripcion = "Modificada por supervisor"
            });

        // Assert
        modificarResponse.EnsureSuccessStatusCode();

        using var db = GetDbContext();
        var categoriaDB = await db.Categorias.FindAsync(categoria.Id);

        categoriaDB.Should().NotBeNull();
        categoriaDB!.CreadoPor.Should().Be(AdminEmail, "el creador original no debe cambiar");
        categoriaDB.ModificadoPor.Should().Be(SupervisorEmail);
        categoriaDB.FechaModificacion.Should().NotBeNull();
        categoriaDB.FechaModificacion!.Value.Should().BeCloseTo(ahoraModificacion, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ModificarTercero_DebeRegistrarModificadoPor()
    {
        // Arrange - Crear tercero
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var crearResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Terceros", new
        {
            nombre = "Tercero Para Modificar",
            tipoIdentificacion = "CC",
            identificacion = "MOD-123",
            tipoTercero = "Cliente"
        });
        var tercero = await crearResponse.Content.ReadFromJsonAsync<TerceroDto>();

        await Task.Delay(100);

        // Act - Modificar como Supervisor
        var clientSupervisor = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var ahoraModificacion = DateTime.UtcNow;

        var modificarResponse = await clientSupervisor.PutAsJsonAsync(
            $"/api/v1/Terceros/{tercero!.Id}",
            new
            {
                nombre = "Tercero Modificado",
                tipoIdentificacion = "CC",
                identificacion = "MOD-123",
                tipoTercero = "Cliente",
                telefono = "123456789"
            });

        // Assert
        modificarResponse.EnsureSuccessStatusCode();

        using var db = GetDbContext();
        var terceroDB = await db.Terceros.FindAsync(tercero.Id);

        terceroDB.Should().NotBeNull();
        terceroDB!.CreadoPor.Should().Be(AdminEmail);
        terceroDB!.ModificadoPor.Should().Be(SupervisorEmail);
        terceroDB.FechaModificacion.Should().NotBeNull();
        terceroDB.FechaModificacion!.Value.Should().BeCloseTo(ahoraModificacion, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ModificarProducto_DebeRegistrarModificadoPor()
    {
        // Arrange - Crear producto
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var crearResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = "PROD-MOD-001",
            nombre = "Producto Para Modificar",
            descripcion = "Original",
            categoriaId = _factory.CategoriaTestId,
            precioVenta = 100m,
            precioCosto = 50m
        });
        var producto = await crearResponse.Content.ReadFromJsonAsync<ProductoDto>();

        await Task.Delay(100);

        // Act - Modificar como Supervisor
        var clientSupervisor = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var ahoraModificacion = DateTime.UtcNow;

        var modificarResponse = await clientSupervisor.PutAsJsonAsync(
            $"/api/v1/Productos/{producto!.Id}",
            new
            {
                codigoBarras = "PROD-MOD-001",
                nombre = "Producto Modificado",
                descripcion = "Modificado por supervisor",
                categoriaId = _factory.CategoriaTestId,
                precioVenta = 150m,
                precioCosto = 75m
            });

        // Assert
        modificarResponse.EnsureSuccessStatusCode();

        using var db = GetDbContext();
        var productoDB = await db.Productos.FindAsync(producto.Id);

        productoDB.Should().NotBeNull();
        productoDB!.CreadoPor.Should().Be(AdminEmail);
        productoDB.ModificadoPor.Should().Be(SupervisorEmail);
        productoDB.FechaModificacion.Should().NotBeNull();
        productoDB.FechaModificacion!.Value.Should().BeCloseTo(ahoraModificacion, TimeSpan.FromSeconds(5));
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: PROTECCIÓN DE CAMPOS DE CREACIÓN
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ModificarEntidad_NoPuedeModificarCamposDeCreacion()
    {
        // Arrange - Crear categoria
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var crearResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Categorias", new
        {
            nombre = "Categoria Proteccion Test",
            descripcion = "Test"
        });
        var categoria = await crearResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        using var db1 = GetDbContext();
        var categoriaOriginal = await db1.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria!.Id);
        var creadoPorOriginal = categoriaOriginal.CreadoPor;
        var fechaCreacionOriginal = categoriaOriginal.FechaCreacion;

        await Task.Delay(100);

        // Act - Intentar modificar directamente en BD los campos de creación
        using var db2 = GetDbContext();
        var categoriaDb = await db2.Categorias.FindAsync(categoria!.Id);
        categoriaDb!.Nombre = "Nombre Modificado";

        // Intentar modificar campos de creación (deberían ser ignorados)
        categoriaDb.CreadoPor = "hacker@evil.com";
        categoriaDb.FechaCreacion = DateTime.UtcNow.AddYears(-10);

        await db2.SaveChangesAsync();

        // Assert - Los campos de creación NO deben cambiar
        using var db3 = GetDbContext();
        var categoriaFinal = await db3.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria.Id);

        categoriaFinal.CreadoPor.Should().Be(creadoPorOriginal, "CreadoPor debe estar protegido");
        categoriaFinal.FechaCreacion.Should().BeCloseTo(fechaCreacionOriginal, TimeSpan.FromMilliseconds(1),
            "FechaCreacion debe estar protegida");
        categoriaFinal.ModificadoPor.Should().Be("sistema", "fue modificado sin autenticación");
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: MÚLTIPLES MODIFICACIONES
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task ModificarVariasVeces_RegistraUltimoModificadorYFecha()
    {
        // Arrange - Crear
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var crearResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Categorias", new
        {
            nombre = "Categoria Multi Modificacion",
            descripcion = "V1"
        });
        var categoria = await crearResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        // Act - Modificar múltiples veces con diferentes usuarios

        // Modificación 1 - Supervisor
        await Task.Delay(100);
        var clientSupervisor = _factory.CreateAuthenticatedClient(SupervisorEmail);
        await clientSupervisor.PutAsJsonAsync($"/api/v1/Categorias/{categoria!.Id}", new
        {
            nombre = "Categoria Multi Modificacion",
            descripcion = "V2 - Supervisor"
        });

        // Modificación 2 - Cajero (via API directa a BD)
        await Task.Delay(100);
        using (var db = GetDbContext())
        {
            var cat = await db.Categorias.FindAsync(categoria.Id);
            cat!.Descripcion = "V3 - Sistema";
            await db.SaveChangesAsync();
        }

        // Modificación 3 - Admin
        await Task.Delay(100);
        var ahoraFinal = DateTime.UtcNow;
        await clientAdmin.PutAsJsonAsync($"/api/v1/Categorias/{categoria.Id}", new
        {
            nombre = "Categoria Multi Modificacion",
            descripcion = "V4 - Admin final"
        });

        // Assert - Debe registrar el último modificador
        using var dbFinal = GetDbContext();
        var categoriaFinal = await dbFinal.Categorias.FindAsync(categoria.Id);

        categoriaFinal.Should().NotBeNull();
        categoriaFinal!.CreadoPor.Should().Be(AdminEmail, "creador original");
        categoriaFinal.ModificadoPor.Should().Be(AdminEmail, "último en modificar");
        categoriaFinal.FechaModificacion!.Value.Should().BeCloseTo(ahoraFinal, TimeSpan.FromSeconds(5));
        categoriaFinal.Descripcion.Should().Be("V4 - Admin final");
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: ENTIDADES ESPECÍFICAS
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task CrearSucursal_DebeRegistrarAuditoria()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var ahora = DateTime.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Sucursales", new
        {
            nombre = "Sucursal Auditoria Test",
            direccion = "Calle Test 123",
            telefono = "123456789",
            metodoCosteo = "PromedioPonderado"
        });

        // Assert
        response.EnsureSuccessStatusCode();
        var sucursal = await response.Content.ReadFromJsonAsync<SucursalDto>();

        using var db = GetDbContext();
        var sucursalDB = await db.Sucursales.FindAsync(sucursal!.Id);

        sucursalDB.Should().NotBeNull();
        sucursalDB!.CreadoPor.Should().Be(AdminEmail);
        sucursalDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CrearCaja_DebeRegistrarAuditoria()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(AdminEmail);
        var ahora = DateTime.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Cajas", new
        {
            nombre = "Caja Auditoria Test",
            sucursalId = _factory.SucursalPPId
        });

        // Assert
        response.EnsureSuccessStatusCode();
        var caja = await response.Content.ReadFromJsonAsync<CajaDto>();

        using var db = GetDbContext();
        var cajaDB = await db.Cajas.FindAsync(caja!.Id);

        cajaDB.Should().NotBeNull();
        cajaDB!.CreadoPor.Should().Be(AdminEmail);
        cajaDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
    }

    // ═══════════════════════════════════════════════════════
    //  TESTS: ESCENARIOS DE AUDITORÍA COMPLETA
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task AuditoriaCompleta_FlujoCategoriaDesdeCreacionHastaModificacion()
    {
        // Este test simula un flujo completo de auditoría en el ciclo de vida de una entidad

        var ahora = DateTime.UtcNow;

        // Paso 1: Admin crea la categoria
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var crearResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Categorias", new
        {
            nombre = "Categoria Ciclo Completo",
            descripcion = "Versión inicial"
        });
        crearResponse.EnsureSuccessStatusCode();
        var categoria = await crearResponse.Content.ReadFromJsonAsync<CategoriaDto>();

        // Verificar estado después de creación
        using (var db = GetDbContext())
        {
            var cat = await db.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria!.Id);
            cat.CreadoPor.Should().Be(AdminEmail);
            cat.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
            cat.ModificadoPor.Should().BeNullOrEmpty();
            cat.FechaModificacion.Should().BeNull();
        }

        await Task.Delay(200);

        // Paso 2: Supervisor modifica
        var ahoraModificacion = DateTime.UtcNow;
        var clientSupervisor = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var modificarResponse = await clientSupervisor.PutAsJsonAsync(
            $"/api/v1/Categorias/{categoria!.Id}",
            new
            {
                nombre = "Categoria Ciclo Completo MODIFICADA",
                descripcion = "Modificada por supervisor"
            });
        modificarResponse.EnsureSuccessStatusCode();

        // Verificar estado después de modificación
        using (var db = GetDbContext())
        {
            var cat = await db.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria.Id);
            cat.CreadoPor.Should().Be(AdminEmail, "el creador no cambia");
            cat.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));
            cat.ModificadoPor.Should().Be(SupervisorEmail);
            cat.FechaModificacion.Should().NotBeNull();
            cat.FechaModificacion!.Value.Should().BeCloseTo(ahoraModificacion, TimeSpan.FromSeconds(5));
            cat.FechaModificacion!.Value.Should().BeAfter(cat.FechaCreacion);
        }

        await Task.Delay(200);

        // Paso 3: Admin hace otra modificación
        var ahoraSegundaModificacion = DateTime.UtcNow;
        var modificar2Response = await clientAdmin.PutAsJsonAsync(
            $"/api/v1/Categorias/{categoria.Id}",
            new
            {
                nombre = "Categoria Ciclo Completo FINAL",
                descripcion = "Modificada nuevamente por admin"
            });
        modificar2Response.EnsureSuccessStatusCode();

        // Verificación final
        using (var db = GetDbContext())
        {
            var cat = await db.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria.Id);

            // El creador original nunca cambia
            cat.CreadoPor.Should().Be(AdminEmail);
            cat.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(5));

            // El modificador es el último en tocar la entidad
            cat.ModificadoPor.Should().Be(AdminEmail);
            cat.FechaModificacion!.Value.Should().BeCloseTo(ahoraSegundaModificacion, TimeSpan.FromSeconds(5));

            // Las fechas están en orden lógico
            cat.FechaModificacion!.Value.Should().BeAfter(cat.FechaCreacion);

            // Los cambios se aplicaron
            cat.Nombre.Should().Be("Categoria Ciclo Completo FINAL");
        }
    }

    [Fact]
    public async Task Auditoria_VariasEntidadesDiferentesUsuarios_TodosRegistranCorrectamente()
    {
        // Test que verifica que múltiples entidades y usuarios funcionan simultáneamente

        var ahora = DateTime.UtcNow;

        // Admin crea categoria y sucursal
        var clientAdmin = _factory.CreateAuthenticatedClient(AdminEmail);
        var categoriaResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Categorias", new
        {
            nombre = "Cat Multi Usuario",
            descripcion = "Test"
        });
        var sucursalResponse = await clientAdmin.PostAsJsonAsync("/api/v1/Sucursales", new
        {
            nombre = "Suc Multi Usuario",
            direccion = "Test 123",
            metodoCosteo = "PromedioPonderado"
        });

        // Supervisor crea producto
        var clientSupervisor = _factory.CreateAuthenticatedClient(SupervisorEmail);
        var productoResponse = await clientSupervisor.PostAsJsonAsync("/api/v1/Productos", new
        {
            codigoBarras = "MULTI-USER-001",
            nombre = "Prod Multi Usuario",
            categoriaId = _factory.CategoriaTestId,
            precioVenta = 100m,
            precioCosto = 50m
        });

        // Cajero crea tercero
        var clientCajero = _factory.CreateAuthenticatedClient(CajeroEmail);
        var terceroResponse = await clientCajero.PostAsJsonAsync("/api/v1/Terceros", new
        {
            nombre = "Tercero Multi Usuario",
            tipoIdentificacion = "NIT",
            identificacion = "MULTI-123",
            tipoTercero = "Cliente"
        });

        // Asegurar que todas las respuestas fueron exitosas
        categoriaResponse.EnsureSuccessStatusCode();
        sucursalResponse.EnsureSuccessStatusCode();
        productoResponse.EnsureSuccessStatusCode();
        terceroResponse.EnsureSuccessStatusCode();

        // Verificar que cada entidad registró el usuario correcto
        var categoria = await categoriaResponse.Content.ReadFromJsonAsync<CategoriaDto>();
        var sucursal = await sucursalResponse.Content.ReadFromJsonAsync<SucursalDto>();
        var producto = await productoResponse.Content.ReadFromJsonAsync<ProductoDto>();
        var tercero = await terceroResponse.Content.ReadFromJsonAsync<TerceroDto>();

        using var db = GetDbContext();

        var catDB = await db.Categorias.FindAsync(categoria!.Id);
        catDB!.CreadoPor.Should().Be(AdminEmail);
        catDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(10));

        var sucDB = await db.Sucursales.FindAsync(sucursal!.Id);
        sucDB!.CreadoPor.Should().Be(AdminEmail);
        sucDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(10));

        var prodDB = await db.Productos.FindAsync(producto!.Id);
        prodDB!.CreadoPor.Should().Be(SupervisorEmail);
        prodDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(10));

        var tercDB = await db.Terceros.FindAsync(tercero!.Id);
        tercDB!.CreadoPor.Should().Be(CajeroEmail);
        tercDB.FechaCreacion.Should().BeCloseTo(ahora, TimeSpan.FromSeconds(10));
    }
}
