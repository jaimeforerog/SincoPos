using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

/// <summary>
/// Endpoint de bootstrap para inicializar datos en producción.
/// Se auto-deshabilita si ya existen datos.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class BootstrapController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<BootstrapController> _logger;

    public BootstrapController(AppDbContext db, ILogger<BootstrapController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Inicializa datos base: sucursal, categoría, caja, y asigna rol admin al usuario actual.
    /// Solo funciona si NO existen sucursales en la BD (primera ejecución).
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        var existenSucursales = await _db.Sucursales.AnyAsync();
        if (existenSucursales)
            return BadRequest(new { error = "Ya existen datos. Bootstrap no es necesario." });

        var externalId = User.GetExternalId();
        var email = User.GetEmail() ?? "admin@sincopos.com";
        var nombre = User.GetNombreCompleto() ?? "Administrador";

        _logger.LogWarning("BOOTSTRAP: Inicializando datos de producción por {Email}", email);

        // 1. Sucursal
        var sucursal = new Sucursal
        {
            Nombre = "Sucursal Principal",
            Direccion = "Dirección pendiente",
            Ciudad = "Bogotá",
            CodigoPais = "CO",
            NombrePais = "Colombia",
            Telefono = "",
            Email = email,
            MetodoCosteo = MetodoCosteo.PromedioPonderado,
            PerfilTributario = "REGIMEN_ORDINARIO",
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor = email
        };
        _db.Sucursales.Add(sucursal);
        await _db.SaveChangesAsync();

        // 2. Categorías base
        var categorias = new[]
        {
            new Categoria { Nombre = "General", Descripcion = "Categoría general", MargenGanancia = 0.30m, Nivel = 0, RutaCompleta = "General", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
            new Categoria { Nombre = "Alimentos", Descripcion = "Productos alimenticios", MargenGanancia = 0.25m, Nivel = 0, RutaCompleta = "Alimentos", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
            new Categoria { Nombre = "Bebidas", Descripcion = "Bebidas y líquidos", MargenGanancia = 0.35m, Nivel = 0, RutaCompleta = "Bebidas", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
            new Categoria { Nombre = "Aseo", Descripcion = "Productos de aseo y limpieza", MargenGanancia = 0.30m, Nivel = 0, RutaCompleta = "Aseo", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
        };
        _db.Set<Categoria>().AddRange(categorias);
        await _db.SaveChangesAsync();

        // 3. Caja
        var caja = new Caja
        {
            Nombre = "Caja Principal",
            SucursalId = sucursal.Id,
            Estado = EstadoCaja.Cerrada,
            MontoApertura = 0,
            MontoActual = 0,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            CreadoPor = email
        };
        _db.Cajas.Add(caja);
        await _db.SaveChangesAsync();

        // 4. Usuario admin
        var usuario = await _db.Set<Usuario>()
            .FirstOrDefaultAsync(u => u.KeycloakId == externalId);

        if (usuario == null)
        {
            usuario = new Usuario
            {
                KeycloakId = externalId!,
                Email = email,
                NombreCompleto = nombre,
                Rol = Roles.Admin,
                SucursalDefaultId = sucursal.Id,
                UltimoAcceso = DateTime.UtcNow,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            _db.Set<Usuario>().Add(usuario);
        }
        else
        {
            usuario.Rol = Roles.Admin;
            usuario.SucursalDefaultId = sucursal.Id;
            usuario.FechaModificacion = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        // 5. Asignar sucursal al usuario
        var yaAsignada = await _db.Set<UsuarioSucursal>()
            .AnyAsync(us => us.UsuarioId == usuario.Id && us.SucursalId == sucursal.Id);
        if (!yaAsignada)
        {
            _db.Set<UsuarioSucursal>().Add(new UsuarioSucursal
            {
                UsuarioId = usuario.Id,
                SucursalId = sucursal.Id
            });
            await _db.SaveChangesAsync();
        }

        _logger.LogWarning("BOOTSTRAP: Completado. Sucursal={SucId}, Usuario={UserId} (admin)", sucursal.Id, usuario.Id);

        return Ok(new
        {
            mensaje = "Bootstrap completado exitosamente",
            sucursal = new { sucursal.Id, sucursal.Nombre },
            categorias = categorias.Length,
            caja = new { caja.Id, caja.Nombre },
            usuario = new { usuario.Id, usuario.Email, usuario.Rol }
        });
    }
}
