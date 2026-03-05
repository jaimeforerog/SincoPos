namespace POS.Application.DTOs;

public record CrearProductoDto(
    string CodigoBarras,
    string Nombre,
    string? Descripcion,
    int CategoriaId,
    decimal PrecioVenta,
    decimal PrecioCosto,
    int? ImpuestoId = null,                     // null = exento
    bool EsAlimentoUltraprocesado = false,
    decimal? GramosAzucarPor100ml = null,
    string UnidadMedida = "94"                  // 94 = Unidad DIAN (default)
);

public record ActualizarProductoDto(
    string Nombre,
    string? Descripcion,
    decimal PrecioVenta,
    decimal PrecioCosto,
    int? ImpuestoId = null,
    bool EsAlimentoUltraprocesado = false,
    decimal? GramosAzucarPor100ml = null,
    string UnidadMedida = "94"
);

public record ProductoDto(
    Guid Id,
    string CodigoBarras,
    string Nombre,
    string? Descripcion,
    int CategoriaId,
    decimal PrecioVenta,
    decimal PrecioCosto,
    bool Activo,
    DateTime FechaCreacion,
    // ── Tax Engine ────────────────────────────────────────────────────────────
    int? ImpuestoId,
    string? NombreImpuesto,        // "IVA 19%"
    string? TipoImpuesto,          // "IVA" | "INC" | "Saludable" | "Bolsa"
    decimal? PorcentajeImpuesto,   // 0.19 — el frontend usa esto para la estimación del carrito
    bool EsAlimentoUltraprocesado,
    decimal? GramosAzucarPor100ml,
    string UnidadMedida = "94"
);
