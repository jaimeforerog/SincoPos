using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IPosContextoService
{
    /// <summary>
    /// Retorna el contexto de turno para una sucursal:
    /// - Los últimos 20 clientes distintos que compraron ahí (más recientes primero).
    /// - Las órdenes de compra pendientes de recibir (estado Pendiente/Aprobada/RecibidaParcial).
    /// </summary>
    Task<TurnContextDto> ObtenerContextoAsync(int sucursalId);
}
