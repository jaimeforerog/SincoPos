namespace POS.Application.DTOs;

// ─── Capa 3 — Repetición cero: contexto de turno ───────────────────────────

/// <summary>
/// Cliente que compró recientemente en la sucursal.
/// Permite al cajero seleccionar clientes habituales sin buscarlos.
/// </summary>
public record ClienteRecienteDto(
    int      Id,
    string   Nombre,
    string?  Identificacion,
    DateTime UltimaVenta
);

/// <summary>
/// Resumen de una orden de compra pendiente de recibir.
/// Alerta al cajero sobre entregas esperadas antes de iniciar el turno.
/// </summary>
public record OrdenPendienteResumenDto(
    int       Id,
    string    NumeroOrden,
    string    NombreProveedor,
    DateTime  FechaOrden,
    DateTime? FechaEntregaEsperada,
    decimal   Total,
    int       ItemsCount
);

/// <summary>
/// Contexto precargado al abrir turno (Capa 3 — Repetición cero).
/// Combina clientes recientes de la sucursal y órdenes pendientes de recibir
/// para que el cajero no tenga que buscar ni confirmar datos ya conocidos.
/// </summary>
public record TurnContextDto(
    List<ClienteRecienteDto>       ClientesRecientes,
    List<OrdenPendienteResumenDto> OrdenesPendientes
);
