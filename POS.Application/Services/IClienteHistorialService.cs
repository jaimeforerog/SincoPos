using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Capa 4 — Dependencias inteligentes.
/// Acceso al historial de compras acumulado de un cliente (Marten projection).
/// </summary>
public interface IClienteHistorialService
{
    /// <summary>
    /// Retorna el historial de compras del cliente, o null si no tiene ventas registradas.
    /// </summary>
    Task<ClienteHistorialDto?> ObtenerHistorialAsync(int clienteId);
}
