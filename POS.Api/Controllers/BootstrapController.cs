using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

/// <summary>
/// Endpoint de bootstrap para inicializar datos en producción (idempotente).
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
    /// Inicializa datos base (idempotente): sucursal, categorías, caja, usuario admin.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        var externalId = User.GetExternalId();
        var email = User.GetEmail() ?? "admin@sincopos.com";
        var nombre = User.GetNombreCompleto() ?? "Administrador";

        _logger.LogWarning("BOOTSTRAP: Ejecutado por {Email} (externalId={Id})", email, externalId);

        // 1. Sucursal (idempotente)
        var sucursal = await _db.Sucursales.FirstOrDefaultAsync();
        if (sucursal == null)
        {
            sucursal = new Sucursal
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
        }

        // 2. Categorías base (idempotente)
        if (!await _db.Set<Categoria>().AnyAsync())
        {
            var categorias = new[]
            {
                new Categoria { Nombre = "General", Descripcion = "Categoría general", MargenGanancia = 0.30m, Nivel = 0, RutaCompleta = "General", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
                new Categoria { Nombre = "Alimentos", Descripcion = "Productos alimenticios", MargenGanancia = 0.25m, Nivel = 0, RutaCompleta = "Alimentos", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
                new Categoria { Nombre = "Bebidas", Descripcion = "Bebidas y líquidos", MargenGanancia = 0.35m, Nivel = 0, RutaCompleta = "Bebidas", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
                new Categoria { Nombre = "Aseo", Descripcion = "Productos de aseo y limpieza", MargenGanancia = 0.30m, Nivel = 0, RutaCompleta = "Aseo", Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
            };
            _db.Set<Categoria>().AddRange(categorias);
            await _db.SaveChangesAsync();
        }

        // 3. Caja (idempotente)
        if (!await _db.Cajas.AnyAsync(c => c.SucursalId == sucursal.Id))
        {
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
        }

        // 4. Usuario admin (crear o promover a admin)
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

        // 5. Asignar sucursal al usuario (SQL directo — BD tiene columna fecha_asignacion NOT NULL)
        await _db.Database.ExecuteSqlRawAsync(
            @"INSERT INTO public.usuario_sucursales (usuario_id, sucursal_id, fecha_asignacion)
              VALUES ({0}, {1}, NOW())
              ON CONFLICT DO NOTHING",
            usuario.Id, sucursal.Id);

        _logger.LogWarning("BOOTSTRAP: Completado. Sucursal={SucId}, Usuario={UserId} (admin)", sucursal.Id, usuario.Id);

        return Ok(new
        {
            mensaje = "Bootstrap completado exitosamente",
            sucursalId = sucursal.Id,
            sucursalNombre = sucursal.Nombre,
            usuarioId = usuario.Id,
            usuarioEmail = usuario.Email,
            usuarioRol = usuario.Rol
        });
    }
}
