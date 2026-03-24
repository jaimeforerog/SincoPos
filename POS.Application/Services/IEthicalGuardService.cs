using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IEthicalGuardService
{
    /// <summary>
    /// Evalúa todas las reglas activas sobre la venta propuesta.
    /// Devuelve error si alguna regla con Accion=Bloquear se dispara.
    /// </summary>
    Task<(bool permitido, string? error)> EvaluarVentaAsync(EvaluarVentaEticaDto dto);

    // ── CRUD reglas ────────────────────────────────────────────────────────
    Task<List<ReglaEticaDto>> ObtenerReglasAsync();
    Task<ReglaEticaDto?> ObtenerPorIdAsync(int id);
    Task<ReglaEticaDto> CrearAsync(CrearReglaEticaDto dto);
    Task<(ReglaEticaDto? result, string? error)> ActualizarAsync(int id, CrearReglaEticaDto dto);
    Task<bool> EliminarAsync(int id);

    // ── Historial de activaciones ──────────────────────────────────────────
    Task<List<ActivacionReglaEticaDto>> ObtenerActivacionesAsync(int? reglaId = null, int take = 50);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record EvaluarVentaEticaDto(
    int SucursalId,
    int? UsuarioId,
    decimal Subtotal,
    decimal DescuentoTotal,
    int NumeroLineas,
    List<LineaVentaEticaDto> Lineas
);

public record LineaVentaEticaDto(
    Guid ProductoId,
    decimal PrecioUnitario,
    decimal PrecioBase,
    decimal Descuento,
    decimal Cantidad
);

public record ReglaEticaDto(
    int Id,
    int? EmpresaId,
    string Nombre,
    string Contexto,
    string Condicion,
    decimal ValorLimite,
    string Accion,
    string? Mensaje,
    bool Activo,
    DateTime FechaCreacion
);

public record CrearReglaEticaDto(
    string Nombre,
    string Contexto,
    string Condicion,
    decimal ValorLimite,
    string Accion,
    string? Mensaje,
    bool Activo = true
);

public record ActivacionReglaEticaDto(
    int Id,
    int ReglaEticaId,
    string NombreRegla,
    int? VentaId,
    int? SucursalId,
    string? Detalle,
    string AccionTomada,
    DateTime FechaActivacion
);
