using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CategoriasController> _logger;
    private const int NivelMaximo = 3; // Máximo 3 niveles de profundidad

    public CategoriasController(AppDbContext context, ILogger<CategoriasController> logger)
    {
        _context = context;
        _logger = logger;
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
            return BadRequest(new { errors });
        }

        // Verificar nombre único
        var existeNombre = await _context.Categorias
            .AnyAsync(c => c.Nombre == dto.Nombre);

        if (existeNombre)
            return Conflict(new { error = "Ya existe una categoría con ese nombre." });

        // Verificar categoría padre si se especifica
        Categoria? categoriaPadre = null;
        int nivel = 0;
        string rutaCompleta = dto.Nombre;

        if (dto.CategoriaPadreId.HasValue)
        {
            categoriaPadre = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Id == dto.CategoriaPadreId.Value);

            if (categoriaPadre == null)
                return BadRequest(new { error = "La categoría padre especificada no existe." });

            if (categoriaPadre.Nivel >= NivelMaximo - 1)
                return BadRequest(new { error = $"No se pueden crear subcategorías con más de {NivelMaximo} niveles de profundidad." });

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
            .Include(c => c.CategoriaPadre)
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return NotFound(new { error = $"Categoría {id} no encontrada." });

        return Ok(MapearACategoriaDto(categoria));
    }

    /// <summary>
    /// Listar categorías (plano)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CategoriaDto>>> ObtenerCategorias(
        [FromQuery] bool incluirInactivas = false,
        [FromQuery] int? categoriaPadreId = null)
    {
        var query = _context.Categorias
            .Include(c => c.CategoriaPadre)
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .AsQueryable();

        if (!incluirInactivas)
            query = query.Where(c => c.Activo);

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
    public async Task<ActionResult<List<CategoriaArbolDto>>> ObtenerArbol([FromQuery] bool incluirInactivas = false)
    {
        var todasLasCategorias = await _context.Categorias
            .Include(c => c.Productos)
            .Where(c => incluirInactivas || c.Activo)
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
            return NotFound(new { error = $"Categoría {id} no encontrada." });

        var subCategorias = await _context.Categorias
            .Include(c => c.CategoriaPadre)
            .Include(c => c.SubCategorias)
            .Include(c => c.Productos)
            .Where(c => c.CategoriaPadreId == id && (incluirInactivas || c.Activo))
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
            return BadRequest(new { errors });
        }

        var categoria = await _context.Categorias
            .Include(c => c.CategoriaPadre)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return NotFound(new { error = $"Categoría {id} no encontrada." });

        // Verificar nombre único (excluyendo la misma categoría)
        var existeNombre = await _context.Categorias
            .AnyAsync(c => c.Nombre == dto.Nombre && c.Id != id);

        if (existeNombre)
            return Conflict(new { error = "Ya existe otra categoría con ese nombre." });

        // Si está cambiando la categoría padre, validar
        if (dto.CategoriaPadreId != categoria.CategoriaPadreId)
        {
            if (dto.CategoriaPadreId.HasValue)
            {
                // No puede ser padre de sí misma
                if (dto.CategoriaPadreId.Value == id)
                    return BadRequest(new { error = "Una categoría no puede ser padre de sí misma." });

                // Verificar que no sea una subcategoría (evitar ciclos)
                if (await EsSubCategoria(id, dto.CategoriaPadreId.Value))
                    return BadRequest(new { error = "No se puede mover una categoría a una de sus subcategorías (crearía un ciclo)." });

                var nuevoPadre = await _context.Categorias.FindAsync(dto.CategoriaPadreId.Value);
                if (nuevoPadre == null)
                    return BadRequest(new { error = "La categoría padre especificada no existe." });

                if (nuevoPadre.Nivel >= NivelMaximo - 1)
                    return BadRequest(new { error = $"No se pueden crear subcategorías con más de {NivelMaximo} niveles de profundidad." });
            }

            // Actualizar categoría padre y recalcular jerarquía
            await ActualizarJerarquia(categoria, dto.CategoriaPadreId);
        }

        categoria.Nombre = dto.Nombre;
        categoria.Descripcion = dto.Descripcion;

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
            return NotFound(new { error = "Categoría no encontrada." });

        // Validaciones
        if (dto.NuevaCategoriaPadreId.HasValue)
        {
            if (dto.NuevaCategoriaPadreId.Value == dto.CategoriaId)
                return BadRequest(new { error = "Una categoría no puede ser padre de sí misma." });

            if (await EsSubCategoria(dto.CategoriaId, dto.NuevaCategoriaPadreId.Value))
                return BadRequest(new { error = "No se puede mover una categoría a una de sus subcategorías." });

            var nuevoPadre = await _context.Categorias.FindAsync(dto.NuevaCategoriaPadreId.Value);
            if (nuevoPadre == null)
                return BadRequest(new { error = "La categoría padre especificada no existe." });

            if (nuevoPadre.Nivel >= NivelMaximo - 1)
                return BadRequest(new { error = $"No se pueden crear subcategorías con más de {NivelMaximo} niveles." });
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
            return NotFound(new { error = $"Categoría {id} no encontrada." });

        if (categoria.SubCategorias.Any())
            return BadRequest(new { error = "No se puede eliminar una categoría que tiene subcategorías. Elimine las subcategorías primero." });

        if (categoria.Productos.Any())
            return BadRequest(new { error = "No se puede eliminar una categoría que tiene productos asociados." });

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
            CantidadProductos: categoria.Productos?.Count ?? 0
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
        // Obtener todas las subcategorías directas
        var subCategorias = await _context.Categorias
            .Where(c => c.CategoriaPadreId == categoriaId)
            .ToListAsync();

        foreach (var sub in subCategorias)
        {
            // Si encontramos la categoría buscada, es una subcategoría
            if (sub.Id == posibleSubCategoriaId) return true;

            // Buscar recursivamente en las subcategorías
            if (await EsSubCategoria(sub.Id, posibleSubCategoriaId)) return true;
        }

        return false;
    }

    private async Task ActualizarJerarquia(Categoria categoria, int? nuevoPadreId)
    {
        categoria.CategoriaPadreId = nuevoPadreId;

        if (nuevoPadreId.HasValue)
        {
            var padre = await _context.Categorias.FindAsync(nuevoPadreId.Value);
            categoria.Nivel = padre!.Nivel + 1;
            categoria.RutaCompleta = $"{padre.RutaCompleta} > {categoria.Nombre}";
        }
        else
        {
            categoria.Nivel = 0;
            categoria.RutaCompleta = categoria.Nombre;
        }

        // Actualizar recursivamente todas las subcategorías
        var subCategorias = await _context.Categorias
            .Where(c => c.CategoriaPadreId == categoria.Id)
            .ToListAsync();

        foreach (var sub in subCategorias)
        {
            await ActualizarJerarquia(sub, categoria.Id);
        }
    }

    #endregion
}
