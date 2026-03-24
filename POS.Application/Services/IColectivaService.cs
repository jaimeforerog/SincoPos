namespace POS.Application.Services;

/// <summary>
/// Capa 13 — Inteligencia colectiva.
/// Expone patrones locales de cross-selling y comparación cross-sucursal.
///
/// Nivel global (servicio central Sinco multi-tenant, bus de mensajes): 🔮 Futuro.
/// Modo degradado: cada sucursal opera con sus patrones locales independientemente.
/// </summary>
public interface IColectivaService
{
    /// <summary>
    /// Retorna los combos de productos más frecuentes en una sucursal.
    /// </summary>
    Task<List<ComboProductoDto>> ObtenerCombosAsync(int sucursalId, int top = 15);

    /// <summary>
    /// Compara la velocidad de los productos top entre las sucursales de una empresa.
    /// Permite identificar qué sucursal vende mejor cada producto.
    /// </summary>
    Task<PatronComparativoDto> CompararSucursalesAsync(int empresaId);

    /// <summary>
    /// Estado del módulo global: indica si hay conectividad con el servicio central Sinco.
    /// En la implementación actual siempre retorna modo local (sin servicio central).
    /// </summary>
    EstadoGlobalDto ObtenerEstadoGlobal();
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ComboProductoDto(
    string ProductoAId,
    string ProductoANombre,
    string ProductoBId,
    string ProductoBNombre,
    int VecesJuntos,
    double Frecuencia   // veces_juntos / total_ventas_sucursal (0–1)
);

public record PatronComparativoDto(
    List<string> Sucursales,                      // nombres de sucursales
    List<ProductoVelocidadComparativoDto> Items   // un ítem por producto top
);

public record ProductoVelocidadComparativoDto(
    string ProductoId,
    string NombreProducto,
    Dictionary<string, int> VelocidadPorSucursal  // sucursalNombre → unidades vendidas
);

public record EstadoGlobalDto(
    bool ServicioCentralDisponible,
    string Mensaje,
    DateTime? UltimaActualizacionGlobal
);
