namespace POS.Domain.Events.Venta;

public record VentaItemLine(Guid ProductoId, string NombreProducto, decimal Cantidad, decimal PrecioUnitario);

/// <summary>
/// Evento emitido por VentaService cuando una venta se completa exitosamente.
/// Alimenta la Capa 4 (ClienteHistorialProjection) para historial de cliente.
/// Alimenta la Capa 5 (UserBehaviorProjection) para anticipación de productos.
/// Alimenta la Capa 9 (CashierPatternProjection, StorePatternProjection).
/// Alimenta la Capa 14 (BusinessRiskProjection) para radar de negocio.
/// </summary>
public record VentaCompletadaEvent(
    string              ExternalUserId, // oid (Entra / WorkOS) — identifica al cajero
    int                 SucursalId,
    int                 CajaId,
    int                 HoraDelDia,     // 0-23 — para anticipación por franja horaria
    int                 DiaSemana,      // 0-6 (DayOfWeek) — para anticipación por día
    List<VentaItemLine> Items,
    decimal             Total,          // Capa 14: ingresos en tiempo real por sucursal
    int?                ClienteId       // Capa 4: historial de cliente (null = venta anónima)
);
