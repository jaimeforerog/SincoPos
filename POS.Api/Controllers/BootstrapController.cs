using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

/// <summary>
/// Endpoints de bootstrap para inicializar datos en producción (idempotentes).
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
    /// Inicializa datos base (idempotente): empresa, sucursal, categorías, caja, usuario admin.
    /// Todos los registros quedan asignados a la empresa creada/existente.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed([FromBody] SeedRequestDto? dto = null)
    {
        var externalId = User.GetExternalId();
        var email = User.GetEmail() ?? "admin@sincopos.com";
        var nombre = User.GetNombreCompleto() ?? "Administrador";

        _logger.LogWarning("BOOTSTRAP: Ejecutado por {Email} (externalId={Id})", email, externalId);

        // 1. Empresa (idempotente) — base de todo el catálogo
        var empresa = await _db.Empresas.FirstOrDefaultAsync();
        if (empresa == null)
        {
            empresa = new Empresa
            {
                Nombre      = dto?.NombreEmpresa ?? "Mi Empresa",
                Nit         = dto?.Nit ?? "900000000-0",
                RazonSocial = dto?.RazonSocial,
                Activo      = true,
                FechaCreacion = DateTime.UtcNow,
                CreadoPor   = email
            };
            _db.Empresas.Add(empresa);
            await _db.SaveChangesAsync();
            _logger.LogWarning("BOOTSTRAP: Empresa '{Nombre}' creada (Id={Id})", empresa.Nombre, empresa.Id);
        }

        // 2. Sucursal vinculada a la empresa (idempotente)
        var sucursal = await _db.Sucursales
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EmpresaId == empresa.Id);

        if (sucursal == null)
        {
            sucursal = new Sucursal
            {
                Nombre        = "Sucursal Principal",
                Direccion     = "Dirección pendiente",
                Ciudad        = "Bogotá",
                CodigoPais    = "CO",
                NombrePais    = "Colombia",
                Telefono      = "",
                Email         = email,
                MetodoCosteo  = MetodoCosteo.PromedioPonderado,
                PerfilTributario = "REGIMEN_ORDINARIO",
                EmpresaId     = empresa.Id,   // ← vinculada a la empresa
                Activo        = true,
                FechaCreacion = DateTime.UtcNow,
                CreadoPor     = email
            };
            _db.Sucursales.Add(sucursal);
            await _db.SaveChangesAsync();
        }

        // 3. Categorías base vinculadas a la empresa (idempotente)
        var hayCategoriasEmpresa = await _db.Categorias
            .IgnoreQueryFilters()
            .AnyAsync(c => c.EmpresaId == empresa.Id);

        if (!hayCategoriasEmpresa)
        {
            var categorias = new[]
            {
                new Categoria { Nombre = "General",    Descripcion = "Categoría general",              MargenGanancia = 0.30m, Nivel = 0, RutaCompleta = "General",    EmpresaId = empresa.Id, Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
                new Categoria { Nombre = "Alimentos",  Descripcion = "Productos alimenticios",          MargenGanancia = 0.25m, Nivel = 0, RutaCompleta = "Alimentos",  EmpresaId = empresa.Id, Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
                new Categoria { Nombre = "Bebidas",    Descripcion = "Bebidas y líquidos",              MargenGanancia = 0.35m, Nivel = 0, RutaCompleta = "Bebidas",    EmpresaId = empresa.Id, Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
                new Categoria { Nombre = "Aseo",       Descripcion = "Productos de aseo y limpieza",   MargenGanancia = 0.30m, Nivel = 0, RutaCompleta = "Aseo",       EmpresaId = empresa.Id, Activo = true, FechaCreacion = DateTime.UtcNow, CreadoPor = email },
            };
            _db.Set<Categoria>().AddRange(categorias);
            await _db.SaveChangesAsync();
        }

        // 4. Caja principal (idempotente)
        var hayCaja = await _db.Cajas
            .IgnoreQueryFilters()
            .AnyAsync(c => c.SucursalId == sucursal.Id);

        if (!hayCaja)
        {
            var caja = new Caja
            {
                Nombre      = "Caja Principal",
                SucursalId  = sucursal.Id,
                EmpresaId   = empresa.Id,      // ← vinculada a la empresa
                Estado      = EstadoCaja.Cerrada,
                MontoApertura = 0,
                MontoActual   = 0,
                Activo        = true,
                FechaCreacion = DateTime.UtcNow,
                CreadoPor     = email
            };
            _db.Cajas.Add(caja);
            await _db.SaveChangesAsync();
        }

        // 5. Usuario admin (crear o promover a admin)
        var usuario = await _db.Set<Usuario>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.KeycloakId == externalId);

        if (usuario == null)
        {
            usuario = new Usuario
            {
                KeycloakId        = externalId!,
                Email             = email,
                NombreCompleto    = nombre,
                Rol               = Roles.Admin,
                SucursalDefaultId = sucursal.Id,
                UltimoAcceso      = DateTime.UtcNow,
                Activo            = true,
                FechaCreacion     = DateTime.UtcNow
            };
            _db.Set<Usuario>().Add(usuario);
        }
        else
        {
            usuario.Rol               = Roles.Admin;
            usuario.SucursalDefaultId = sucursal.Id;
            usuario.FechaModificacion = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        // 6. Asignar sucursal al usuario
        var yaAsignada = await _db.Set<UsuarioSucursal>()
            .AnyAsync(us => us.UsuarioId == usuario.Id && us.SucursalId == sucursal.Id);
        if (!yaAsignada)
        {
            _db.Set<UsuarioSucursal>().Add(new UsuarioSucursal
            {
                UsuarioId  = usuario.Id,
                SucursalId = sucursal.Id
            });
            await _db.SaveChangesAsync();
        }

        _logger.LogWarning(
            "BOOTSTRAP: Completado. Empresa={EmpId}, Sucursal={SucId}, Usuario={UserId} (admin)",
            empresa.Id, sucursal.Id, usuario.Id);

        return Ok(new
        {
            mensaje        = "Bootstrap completado exitosamente",
            empresaId      = empresa.Id,
            empresaNombre  = empresa.Nombre,
            sucursalId     = sucursal.Id,
            sucursalNombre = sucursal.Nombre,
            usuarioId      = usuario.Id,
            usuarioEmail   = usuario.Email,
            usuarioRol     = usuario.Rol
        });
    }

    /// <summary>
    /// Migra todos los registros de catálogo con EmpresaId = NULL asignándolos a la empresa indicada.
    /// Útil para datos legados creados antes del soporte multi-empresa.
    /// Idempotente: solo afecta registros con EmpresaId IS NULL.
    /// Requiere rol Admin.
    /// </summary>
    [HttpPost("migrar-catalogo/{empresaId:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> MigrarCatalogo(int empresaId)
    {
        var empresa = await _db.Empresas.FindAsync(empresaId);
        if (empresa == null)
            return Problem(detail: $"Empresa {empresaId} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        var email = User.GetEmail() ?? "sistema";
        _logger.LogWarning(
            "MIGRAR-CATALOGO: Iniciado por {Email} para empresa {EmpresaId} ({Nombre})",
            email, empresaId, empresa.Nombre);

        var ahora = DateTime.UtcNow;
        var conteos = new Dictionary<string, int>();

        // Sucursales sin empresa
        var sucursales = await _db.Sucursales
            .IgnoreQueryFilters()
            .Where(s => s.EmpresaId == null)
            .ToListAsync();
        sucursales.ForEach(s => { s.EmpresaId = empresaId; s.FechaModificacion = ahora; s.ModificadoPor = email; });
        conteos["Sucursales"] = sucursales.Count;

        // Cajas sin empresa
        var cajas = await _db.Cajas
            .IgnoreQueryFilters()
            .Where(c => c.EmpresaId == null)
            .ToListAsync();
        cajas.ForEach(c => { c.EmpresaId = empresaId; c.FechaModificacion = ahora; c.ModificadoPor = email; });
        conteos["Cajas"] = cajas.Count;

        // Categorías sin empresa
        var categorias = await _db.Categorias
            .IgnoreQueryFilters()
            .Where(c => c.EmpresaId == null)
            .ToListAsync();
        categorias.ForEach(c => { c.EmpresaId = empresaId; c.FechaModificacion = ahora; c.ModificadoPor = email; });
        conteos["Categorias"] = categorias.Count;

        // Productos sin empresa
        var productos = await _db.Productos
            .IgnoreQueryFilters()
            .Where(p => p.EmpresaId == null)
            .ToListAsync();
        productos.ForEach(p => { p.EmpresaId = empresaId; p.FechaModificacion = ahora; p.ModificadoPor = email; });
        conteos["Productos"] = productos.Count;

        // Terceros sin empresa
        var terceros = await _db.Terceros
            .IgnoreQueryFilters()
            .Where(t => t.EmpresaId == null)
            .ToListAsync();
        terceros.ForEach(t => { t.EmpresaId = empresaId; t.FechaModificacion = ahora; t.ModificadoPor = email; });
        conteos["Terceros"] = terceros.Count;

        // Impuestos sin empresa
        var impuestos = await _db.Impuestos
            .IgnoreQueryFilters()
            .Where(i => i.EmpresaId == null)
            .ToListAsync();
        impuestos.ForEach(i => { i.EmpresaId = empresaId; i.FechaModificacion = ahora; i.ModificadoPor = email; });
        conteos["Impuestos"] = impuestos.Count;

        // Conceptos de retención sin empresa
        var conceptos = await _db.ConceptosRetencion
            .IgnoreQueryFilters()
            .Where(c => c.EmpresaId == null)
            .ToListAsync();
        conceptos.ForEach(c => { c.EmpresaId = empresaId; c.FechaModificacion = ahora; c.ModificadoPor = email; });
        conteos["ConceptosRetencion"] = conceptos.Count;

        // Reglas de retención sin empresa
        var retenciones = await _db.RetencionesReglas
            .IgnoreQueryFilters()
            .Where(r => r.EmpresaId == null)
            .ToListAsync();
        retenciones.ForEach(r => { r.EmpresaId = empresaId; r.FechaModificacion = ahora; r.ModificadoPor = email; });
        conteos["RetencionesReglas"] = retenciones.Count;

        var total = conteos.Values.Sum();
        await _db.SaveChangesAsync();

        _logger.LogWarning(
            "MIGRAR-CATALOGO: Completado. {Total} registros migrados a empresa {EmpresaId}. Detalle: {@Conteos}",
            total, empresaId, conteos);

        return Ok(new
        {
            mensaje    = $"Migración completada: {total} registros asignados a empresa '{empresa.Nombre}'",
            empresaId,
            empresaNombre = empresa.Nombre,
            totalMigrados = total,
            detalle       = conteos
        });
    }
}

public record SeedRequestDto(
    string? NombreEmpresa,
    string? Nit,
    string? RazonSocial
);
