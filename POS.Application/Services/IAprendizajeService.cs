using POS.Domain.Aggregates;

namespace POS.Application.Services;

/// <summary>
/// Capa 9 — Aprendizaje continuo.
/// Expone los patrones aprendidos por cajero y por tienda.
/// </summary>
public interface IAprendizajeService
{
    /// <summary>
    /// Retorna el patrón del cajero autenticado.
    /// Incluye velocidad de productos, horas pico y días activos.
    /// </summary>
    Task<CashierPattern?> ObtenerPatronCajero(string externalUserId);

    /// <summary>
    /// Retorna el patrón de la tienda (sucursal).
    /// Incluye top productos, horas pico y velocidad agregada.
    /// Uso: Capa 14 — Radar de Negocio.
    /// </summary>
    Task<StorePattern?> ObtenerPatronTienda(int sucursalId);
}
