using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ImpuestosController : ControllerBase
{
    private readonly AppDbContext _context;

    public ImpuestosController(AppDbContext context) => _context = context;

    // ─── Impuestos ────────────────────────────────────────────────────────────

    /// <summary>
    /// Listar impuestos activos. Filtra opcionalmente por código de país (ej. "CO").
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "Catalogo5m", VaryByQueryKeys = ["pais"])]
    [ProducesResponseType(typeof(IEnumerable<ImpuestoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ImpuestoDto>>> GetImpuestos(
        [FromQuery] string? pais = null)
    {
        var query = _context.Impuestos.Where(i => i.Activo);
        if (!string.IsNullOrEmpty(pais))
            query = query.Where(i => i.CodigoPais == pais);

        var impuestos = await query
            .OrderBy(i => i.Tipo).ThenBy(i => i.Nombre)
            .Select(i => new ImpuestoDto(
                i.Id, i.Nombre, i.Tipo.ToString(), i.Porcentaje,
                i.ValorFijo, i.CodigoCuentaContable, i.AplicaSobreBase,
                i.CodigoPais, i.Descripcion))
            .ToListAsync();

        return Ok(impuestos);
    }

    /// <summary>
    /// Obtener un impuesto por ID.
    /// </summary>
    /// <response code="200">ImpuestoDto con todos sus campos.</response>
    /// <response code="404">Impuesto no encontrado o inactivo.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ImpuestoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImpuestoDto>> GetImpuesto(int id)
    {
        var i = await _context.Impuestos.FindAsync(id);
        if (i == null || !i.Activo) return NotFound("Impuesto no encontrado.");
        return Ok(new ImpuestoDto(
            i.Id, i.Nombre, i.Tipo.ToString(), i.Porcentaje,
            i.ValorFijo, i.CodigoCuentaContable, i.AplicaSobreBase,
            i.CodigoPais, i.Descripcion));
    }

    /// <summary>
    /// Catálogo de tipos de impuesto disponibles (para selects del frontend).
    /// Tipos: IVA, INC, Saludable, Bolsa.
    /// </summary>
    [HttpGet("tipos")]
    [OutputCache(PolicyName = "Catalogo1h")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<object>> GetTipos() =>
        Ok(Enum.GetValues<TipoImpuesto>()
            .Select(t => new { valor = (int)t, nombre = t.ToString() }));

    /// <summary>
    /// Crear un nuevo impuesto.
    /// </summary>
    /// <remarks>
    /// <c>Tipo</c> acepta: "IVA" (default), "INC", "Saludable", "Bolsa".<br/>
    /// <c>Porcentaje</c> como decimal entre 0 y 0.9999 (ej. 0.19 = 19%).<br/>
    /// <c>AplicaSobreBase</c> = true aplica el impuesto sobre el precio base; false sobre el subtotal ya impositado.
    /// </remarks>
    /// <response code="201">Impuesto creado.</response>
    /// <response code="400">Validación fallida (nombre vacío, porcentaje fuera de rango).</response>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(ImpuestoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImpuestoDto>> CrearImpuesto([FromBody] CrearImpuestoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("Nombre requerido.");
        if (dto.Porcentaje < 0 || dto.Porcentaje >= 1)
            return BadRequest("Porcentaje debe estar entre 0 y 0.9999.");

        var impuesto = new Impuesto
        {
            Nombre = dto.Nombre,
            Tipo = Enum.TryParse<TipoImpuesto>(dto.Tipo, out var t) ? t : TipoImpuesto.IVA,
            Porcentaje = dto.Porcentaje,
            ValorFijo = dto.ValorFijo,
            CodigoCuentaContable = dto.CodigoCuentaContable,
            AplicaSobreBase = dto.AplicaSobreBase,
            CodigoPais = dto.CodigoPais ?? "CO",
            Descripcion = dto.Descripcion,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Impuestos.Add(impuesto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetImpuesto), new { id = impuesto.Id },
            new ImpuestoDto(impuesto.Id, impuesto.Nombre, impuesto.Tipo.ToString(),
                impuesto.Porcentaje, impuesto.ValorFijo, impuesto.CodigoCuentaContable,
                impuesto.AplicaSobreBase, impuesto.CodigoPais, impuesto.Descripcion));
    }

    /// <summary>
    /// Actualizar campos de un impuesto existente. Solo se actualizan los campos no nulos del body.
    /// </summary>
    /// <response code="204">Actualizado exitosamente.</response>
    /// <response code="404">Impuesto no encontrado o inactivo.</response>
    /// <response code="400">Porcentaje fuera de rango.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> EditarImpuesto(int id, [FromBody] EditarImpuestoDto dto)
    {
        var impuesto = await _context.Impuestos.FindAsync(id);
        if (impuesto == null || !impuesto.Activo)
            return NotFound("Impuesto no encontrado.");

        if (!string.IsNullOrWhiteSpace(dto.Nombre)) impuesto.Nombre = dto.Nombre;
        if (dto.Porcentaje.HasValue)
        {
            if (dto.Porcentaje < 0 || dto.Porcentaje >= 1)
                return BadRequest("Porcentaje debe estar entre 0 y 0.9999.");
            impuesto.Porcentaje = dto.Porcentaje.Value;
        }
        if (dto.ValorFijo.HasValue) impuesto.ValorFijo = dto.ValorFijo;
        if (!string.IsNullOrEmpty(dto.CodigoCuentaContable))
            impuesto.CodigoCuentaContable = dto.CodigoCuentaContable;
        if (!string.IsNullOrEmpty(dto.Descripcion))
            impuesto.Descripcion = dto.Descripcion;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Desactivar (soft delete) un impuesto. Falla si está asignado a productos activos.
    /// </summary>
    /// <response code="204">Desactivado exitosamente.</response>
    /// <response code="404">Impuesto no encontrado.</response>
    /// <response code="400">Impuesto en uso por productos activos.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DesactivarImpuesto(int id)
    {
        var impuesto = await _context.Impuestos.FindAsync(id);
        if (impuesto == null || !impuesto.Activo) return NotFound("Impuesto no encontrado.");

        var enUso = await _context.Productos.AnyAsync(p => p.ImpuestoId == id && p.Activo);
        if (enUso) return BadRequest("El impuesto está asignado a productos activos. Desasígnelo primero.");

        impuesto.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ─── Retenciones ──────────────────────────────────────────────────────────

    /// <summary>
    /// Listar todas las reglas de retención activas e inactivas.
    /// Tipos: ReteFuente, ReteICA, ReteIVA.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/Retenciones")]
    [ProducesResponseType(typeof(IEnumerable<RetencionReglaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RetencionReglaDto>>> GetRetenciones() =>
        Ok(await _context.RetencionesReglas
            .OrderBy(r => r.Tipo).ThenBy(r => r.Nombre)
            .Select(r => new RetencionReglaDto(
                r.Id, r.Nombre, r.Tipo.ToString(), r.Porcentaje,
                r.BaseMinUVT, r.CodigoMunicipio, r.PerfilVendedor,
                r.PerfilComprador, r.CodigoCuentaContable, r.Activo))
            .ToListAsync());

    /// <summary>
    /// Crear una regla de retención.
    /// </summary>
    /// <remarks>
    /// <c>Tipo</c>: "ReteFuente", "ReteICA" o "ReteIVA".<br/>
    /// <c>Porcentaje</c> como decimal (ej. 0.035 = 3.5%).<br/>
    /// <c>BaseMinUVT</c>: base mínima en UVT para que aplique la retención (0 = siempre aplica).
    /// </remarks>
    /// <response code="201">Regla de retención creada.</response>
    /// <response code="400">Nombre vacío o porcentaje inválido.</response>
    [HttpPost("/api/v{version:apiVersion}/Retenciones")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CrearRetencion([FromBody] CrearRetencionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) return BadRequest("Nombre requerido.");
        if (dto.Porcentaje <= 0 || dto.Porcentaje >= 1) return BadRequest("Porcentaje inválido.");

        var regla = new RetencionRegla
        {
            Nombre = dto.Nombre,
            Tipo = Enum.TryParse<TipoRetencion>(dto.Tipo, out var t) ? t : TipoRetencion.ReteFuente,
            Porcentaje = dto.Porcentaje,
            BaseMinUVT = dto.BaseMinUVT,
            CodigoMunicipio = dto.CodigoMunicipio,
            PerfilVendedor = dto.PerfilVendedor,
            PerfilComprador = dto.PerfilComprador,
            CodigoCuentaContable = dto.CodigoCuentaContable,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.RetencionesReglas.Add(regla);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRetenciones), new { id = regla.Id }, new { regla.Id });
    }

    /// <summary>Actualizar una regla de retención existente (reemplaza todos los campos).</summary>
    /// <response code="204">Actualizado exitosamente.</response>
    /// <response code="404">Regla no encontrada.</response>
    [HttpPut("/api/v{version:apiVersion}/Retenciones/{id:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> EditarRetencion(int id, [FromBody] CrearRetencionDto dto)
    {
        var regla = await _context.RetencionesReglas.FindAsync(id);
        if (regla == null) return NotFound();

        regla.Nombre = dto.Nombre;
        regla.Porcentaje = dto.Porcentaje;
        regla.BaseMinUVT = dto.BaseMinUVT;
        regla.CodigoMunicipio = dto.CodigoMunicipio;
        regla.PerfilVendedor = dto.PerfilVendedor;
        regla.PerfilComprador = dto.PerfilComprador;
        regla.CodigoCuentaContable = dto.CodigoCuentaContable;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Desactivar (soft delete) una regla de retención.</summary>
    /// <response code="204">Desactivado exitosamente.</response>
    /// <response code="404">Regla no encontrada.</response>
    [HttpDelete("/api/v{version:apiVersion}/Retenciones/{id:int}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DesactivarRetencion(int id)
    {
        var regla = await _context.RetencionesReglas.FindAsync(id);
        if (regla == null) return NotFound();
        regla.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record ImpuestoDto(
    int Id, string Nombre, string Tipo, decimal Porcentaje,
    decimal? ValorFijo, string? CodigoCuentaContable,
    bool AplicaSobreBase, string CodigoPais, string? Descripcion);

public record CrearImpuestoDto(
    string Nombre,
    decimal Porcentaje,
    string? Tipo = "IVA",           // "IVA" | "INC" | "Saludable" | "Bolsa"
    decimal? ValorFijo = null,
    string? CodigoCuentaContable = null,
    bool AplicaSobreBase = true,
    string? CodigoPais = "CO",
    string? Descripcion = null);

public record EditarImpuestoDto(
    string? Nombre,
    decimal? Porcentaje,
    decimal? ValorFijo,
    string? CodigoCuentaContable,
    string? Descripcion);

public record RetencionReglaDto(
    int Id, string Nombre, string Tipo, decimal Porcentaje,
    decimal BaseMinUVT, string? CodigoMunicipio,
    string PerfilVendedor, string PerfilComprador,
    string? CodigoCuentaContable, bool Activo);

public record CrearRetencionDto(
    string Nombre,
    string Tipo,           // "ReteFuente" | "ReteICA" | "ReteIVA"
    decimal Porcentaje,
    decimal BaseMinUVT,
    string? CodigoMunicipio,
    string PerfilVendedor,
    string PerfilComprador,
    string? CodigoCuentaContable);
