using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CategoriasController> _logger;
    private readonly ICurrentEmpresaProvider _empresaProvider;
    private const int NivelMaximo = 3; // Máximo 3 niveles de profundidad

    public CategoriasController(AppDbContext context, ILogger<CategoriasController> logger, ICurrentEmpresaProvider empresaProvider)
    {
        _context = context;
        _logger = logger;
        _empresaProvider = empresaProvider;
    }

    /// <summary>
    /// Crear una nueva categoría
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult<CategoriaDto>> CrearCategoria(
        CrearCategoriaDto dto,
        [FromServices] IValidator<CrearCategoriaDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        // Verificar nombre único
        var existeNombre = await _context.Categorias
            .AnyAsync(c => c.Nombre == dto.Nombre);

        if (existeNombre)
            return Problem(detail: "Ya existe una categoría con ese nombre.", statusCode: StatusCodes.Status409Conflict);

        // Verificar categoría padre si se especifica
        Categoria? categoriaPadre = null;
        int nivel = 0;
        string rutaCompleta = dto.Nombre;

        if (dto.CategoriaPadreId.HasValue)
        {
            categoriaPadre = await _context.Categorias
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == dto.CategoriaPadreId.Value);

            if (categoriaPadre == null)
                return Problem(detail: "La categoría padre especificada no existe.", statusCode: StatusCodes.Status400BadRequest);

            if (categoriaPadre.Nivel >= NivelMaximo - 1)
                return Problem(detail: $"No se pueden crear subcategorías con más de {NivelMaximo} niveles de profundidad.", statusCode: StatusCodes.Status400BadRequest);

            nivel = categoriaPadre.Nivel + 1;
            rutaCompleta = $"{categoriaPadre.RutaCompleta} > {dto.Nombre}";
        }

        var categoria = new Categoria
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            CategoriaPadreId = dto.CategoriaPadreId,
            Nivel = nivel,
            RutaCompleta = rutaCompleta,
            MargenGanancia = dto.MargenGanancia,
            EmpresaId = _empresaProvider.EmpresaId,
            Activo = true
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoría creada. Id: {Id}, Nombre: {Nombre}, Nivel: {Nivel}",
            categoria.Id, categoria.Nombre, categoria.Nivel);

        // Recargar con navegación
        await _context.Entry(categoria).Reference(c => c.CategoriaPadre).LoadAsync();

        var result = MapearACategoriaDto(categoria);
        return CreatedAtAction(nameof(ObtenerCategoria), new { id = categoria.Id }, result);
    }

    /// <summary>
    /// Obtener una categoría por ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoriaDto>> ObtenerCategoria(int id)
    {
        var categoria = await _context.Categorias
            .IgnoreQueryFilters()
            .Include(c => c.CategoriaPadre)
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return Problem(detail: $"Categoría {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        return Ok(MapearACategoriaDto(categoria));
    }

    /// <summary>
    /// Listar categorías (plano)
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "Catalogo5m", VaryByQueryKeys = ["incluirInactivas", "categoriaPadreId"])]
    public async Task<ActionResult<List<CategoriaDto>>> ObtenerCategorias(
        [FromQuery] bool incluirInactivas = false,
        [FromQuery] int? categoriaPadreId = null)
    {
        var query = _context.Categorias.AsQueryable();

        if (incluirInactivas)
            query = query.IgnoreQueryFilters();

        query = query.Include(c => c.CategoriaPadre)
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos);

        // Filtrar por categoría padre (null = categorías raíz)
        if (categoriaPadreId.HasValue)
            query = query.Where(c => c.CategoriaPadreId == categoriaPadreId.Value);
        else if (categoriaPadreId == null) // No se especificó filtro, mostrar todas
        {
            // Sin filtro, mostrar todas
        }

        var categorias = await query
            .OrderBy(c => c.Nivel)
            .ThenBy(c => c.Nombre)
            .ToListAsync();

        return Ok(categorias.Select(MapearACategoriaDto).ToList());
    }

    /// <summary>
    /// Obtener árbol completo de categorías
    /// </summary>
    [HttpGet("arbol")]
    [OutputCache(PolicyName = "Catalogo5m", VaryByQueryKeys = ["incluirInactivas"])]
    public async Task<ActionResult<List<CategoriaArbolDto>>> ObtenerArbol([FromQuery] bool incluirInactivas = false)
    {
        var query = _context.Categorias.AsQueryable();
        if (incluirInactivas)
            query = query.IgnoreQueryFilters();

        var todasLasCategorias = await query
            .Include(c => c.Productos)
            .OrderBy(c => c.Nivel)
            .ThenBy(c => c.Nombre)
            .ToListAsync();

        // Construir árbol recursivamente
        var categoriasRaiz = todasLasCategorias
            .Where(c => c.CategoriaPadreId == null)
            .Select(c => ConstruirArbol(c, todasLasCategorias))
            .ToList();

        return Ok(categoriasRaiz);
    }

    /// <summary>
    /// Obtener solo categorías raíz
    /// </summary>
    [HttpGet("raiz")]
    public async Task<ActionResult<List<CategoriaDto>>> ObtenerCategoriasRaiz([FromQuery] bool incluirInactivas = false)
    {
        var categorias = await _context.Categorias
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .Where(c => c.CategoriaPadreId == null && (incluirInactivas || c.Activo))
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        return Ok(categorias.Select(MapearACategoriaDto).ToList());
    }

    /// <summary>
    /// Obtener subcategorías de una categoría
    /// </summary>
    [HttpGet("{id:int}/subcategorias")]
    public async Task<ActionResult<List<CategoriaDto>>> ObtenerSubCategorias(int id, [FromQuery] bool incluirInactivas = false)
    {
        var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == id);
        if (!categoriaExiste)
            return Problem(detail: $"Categoría {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);
        var query = _context.Categorias.AsQueryable();
        if (incluirInactivas)
            query = query.IgnoreQueryFilters();

        var subCategorias = await query
            .Include(c => c.CategoriaPadre)
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .Where(c => c.CategoriaPadreId == id)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        return Ok(subCategorias.Select(MapearACategoriaDto).ToList());
    }

    /// <summary>
    /// Actualizar una categoría
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> ActualizarCategoria(
        int id,
        ActualizarCategoriaDto dto,
        [FromServices] IValidator<ActualizarCategoriaDto> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            foreach (var (key, messages) in errors)
                foreach (var msg in messages)
                    ModelState.AddModelError(key, msg);
            return ValidationProblem();
        }

        var categoria = await _context.Categorias
            .Include(c => c.CategoriaPadre)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return Problem(detail: $"Categoría {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        // Verificar nombre único (excluyendo la misma categoría)
        var existeNombre = await _context.Categorias
            .AnyAsync(c => c.Nombre == dto.Nombre && c.Id != id);

        if (existeNombre)
            return Problem(detail: "Ya existe otra categoría con ese nombre.", statusCode: StatusCodes.Status409Conflict);

        // Si está cambiando la categoría padre, validar
        if (dto.CategoriaPadreId != categoria.CategoriaPadreId)
        {
            if (dto.CategoriaPadreId.HasValue)
            {
                // No puede ser padre de sí misma
                if (dto.CategoriaPadreId.Value == id)
                    return Problem(detail: "Una categoría no puede ser padre de sí misma.", statusCode: StatusCodes.Status400BadRequest);

                // Verificar que no sea una subcategoría (evitar ciclos)
                if (await EsSubCategoria(id, dto.CategoriaPadreId.Value))
                    return Problem(detail: "No se puede mover una categoría a una de sus subcategorías (crearía un ciclo).", statusCode: StatusCodes.Status400BadRequest);

                var nuevoPadre = await _context.Categorias.FindAsync(dto.CategoriaPadreId.Value);
                if (nuevoPadre == null)
                    return Problem(detail: "La categoría padre especificada no existe.", statusCode: StatusCodes.Status400BadRequest);

                if (nuevoPadre.Nivel >= NivelMaximo - 1)
                    return Problem(detail: $"No se pueden crear subcategorías con más de {NivelMaximo} niveles de profundidad.", statusCode: StatusCodes.Status400BadRequest);
            }

            // Actualizar categoría padre y recalcular jerarquía
            await ActualizarJerarquia(categoria, dto.CategoriaPadreId);
        }

        categoria.Nombre = dto.Nombre;
        categoria.Descripcion = dto.Descripcion;
        categoria.MargenGanancia = dto.MargenGanancia;

        // Si cambió el nombre, actualizar ruta completa
        if (categoria.CategoriaPadreId.HasValue)
        {
            var padre = await _context.Categorias.FindAsync(categoria.CategoriaPadreId.Value);
            categoria.RutaCompleta = $"{padre!.RutaCompleta} > {categoria.Nombre}";
        }
        else
        {
            categoria.RutaCompleta = categoria.Nombre;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoría actualizada. Id: {Id}, Nombre: {Nombre}", categoria.Id, categoria.Nombre);

        return Ok(new { mensaje = "Categoría actualizada exitosamente." });
    }

    /// <summary>
    /// Mover una categoría a otro padre
    /// </summary>
    [HttpPost("mover")]
    [Authorize(Policy = "Supervisor")]
    public async Task<ActionResult> MoverCategoria(MoverCategoriaDto dto)
    {
        var categoria = await _context.Categorias
            .Include(c => c.SubCategorias)
            .FirstOrDefaultAsync(c => c.Id == dto.CategoriaId);

        if (categoria == null)
            return Problem(detail: "Categoría no encontrada.", statusCode: StatusCodes.Status404NotFound);

        // Validaciones
        if (dto.NuevaCategoriaPadreId.HasValue)
        {
            if (dto.NuevaCategoriaPadreId.Value == dto.CategoriaId)
                return Problem(detail: "Una categoría no puede ser padre de sí misma.", statusCode: StatusCodes.Status400BadRequest);

            if (await EsSubCategoria(dto.CategoriaId, dto.NuevaCategoriaPadreId.Value))
                return Problem(detail: "No se puede mover una categoría a una de sus subcategorías.", statusCode: StatusCodes.Status400BadRequest);

            var nuevoPadre = await _context.Categorias.FindAsync(dto.NuevaCategoriaPadreId.Value);
            if (nuevoPadre == null)
                return Problem(detail: "La categoría padre especificada no existe.", statusCode: StatusCodes.Status400BadRequest);

            if (nuevoPadre.Nivel >= NivelMaximo - 1)
                return Problem(detail: $"No se pueden crear subcategorías con más de {NivelMaximo} niveles.", statusCode: StatusCodes.Status400BadRequest);
        }

        await ActualizarJerarquia(categoria, dto.NuevaCategoriaPadreId);
        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Categoría movida exitosamente." });
    }

    /// <summary>
    /// Eliminar una categoría
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> EliminarCategoria(int id)
    {
        var categoria = await _context.Categorias
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return Problem(detail: $"Categoría {id} no encontrada.", statusCode: StatusCodes.Status404NotFound);

        if (categoria.SubCategorias.Any())
            return Problem(detail: "No se puede eliminar una categoría que tiene subcategorías. Elimine las subcategorías primero.", statusCode: StatusCodes.Status400BadRequest);

        if (categoria.Productos.Any())
            return Problem(detail: "No se puede eliminar una categoría que tiene productos asociados.", statusCode: StatusCodes.Status400BadRequest);

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoría eliminada. Id: {Id}, Nombre: {Nombre}", id, categoria.Nombre);

        return Ok(new { mensaje = "Categoría eliminada exitosamente." });
    }

    #region Helpers

    private CategoriaDto MapearACategoriaDto(Categoria categoria)
    {
        return new CategoriaDto(
            Id: categoria.Id,
            Nombre: categoria.Nombre,
            Descripcion: categoria.Descripcion,
            Activa: categoria.Activo,
            CategoriaPadreId: categoria.CategoriaPadreId,
            NombrePadre: categoria.CategoriaPadre?.Nombre,
            Nivel: categoria.Nivel,
            RutaCompleta: categoria.RutaCompleta,
            CantidadSubCategorias: categoria.SubCategorias?.Count ?? 0,
            CantidadProductos: categoria.Productos?.Count ?? 0,
            MargenGanancia: categoria.MargenGanancia
        );
    }

    private CategoriaArbolDto ConstruirArbol(Categoria categoria, List<Categoria> todasLasCategorias)
    {
        var hijos = todasLasCategorias
            .Where(c => c.CategoriaPadreId == categoria.Id)
            .Select(c => ConstruirArbol(c, todasLasCategorias))
            .ToList();

        return new CategoriaArbolDto(
            Id: categoria.Id,
            Nombre: categoria.Nombre,
            Descripcion: categoria.Descripcion,
            Activa: categoria.Activo,
            CategoriaPadreId: categoria.CategoriaPadreId,
            Nivel: categoria.Nivel,
            RutaCompleta: categoria.RutaCompleta,
            CantidadProductos: categoria.Productos?.Count ?? 0,
            SubCategorias: hijos
        );
    }

    private async Task<bool> EsSubCategoria(int categoriaId, int posibleSubCategoriaId)
    {
        // Load entire tree in one query, then traverse in memory (avoids recursive N+1)
        var nodos = await _context.Categorias
            .Select(c => new { c.Id, c.CategoriaPadreId })
            .ToListAsync();

        var hijosPorPadre = nodos.ToLookup(c => c.CategoriaPadreId ?? -1, c => c.Id);
        return EsSubCategoriaEnMemoria(categoriaId, posibleSubCategoriaId, hijosPorPadre);
    }

    private static bool EsSubCategoriaEnMemoria(
        int categoriaId, int posibleSubCategoriaId,
        ILookup<int, int> hijosPorPadre)
    {
        foreach (var hijoId in hijosPorPadre[categoriaId])
        {
            if (hijoId == posibleSubCategoriaId) return true;
            if (EsSubCategoriaEnMemoria(hijoId, posibleSubCategoriaId, hijosPorPadre)) return true;
        }
        return false;
    }

    private async Task ActualizarJerarquia(Categoria categoria, int? nuevoPadreId)
    {
        // Load entire tree in one query, then update in memory (avoids recursive N+1)
        var todasLasCategorias = await _context.Categorias.ToListAsync();
        var padreDict = todasLasCategorias.ToDictionary(c => c.Id);
        ActualizarJerarquiaEnMemoria(categoria, nuevoPadreId, todasLasCategorias, padreDict);
    }

    private static void ActualizarJerarquiaEnMemoria(
        Categoria categoria, int? nuevoPadreId,
        List<Categoria> todas, Dictionary<int, Categoria> padreDict)
    {
        categoria.CategoriaPadreId = nuevoPadreId;

        if (nuevoPadreId.HasValue && padreDict.TryGetValue(nuevoPadreId.Value, out var padre))
        {
            categoria.Nivel = padre.Nivel + 1;
            categoria.RutaCompleta = $"{padre.RutaCompleta} > {categoria.Nombre}";
        }
        else
        {
            categoria.Nivel = 0;
            categoria.RutaCompleta = categoria.Nombre;
        }

        foreach (var sub in todas.Where(c => c.CategoriaPadreId == categoria.Id))
            ActualizarJerarquiaEnMemoria(sub, categoria.Id, todas, padreDict);
    }

    #endregion
}
