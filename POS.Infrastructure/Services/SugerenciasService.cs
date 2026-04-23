using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public sealed class SugerenciasService : ISugerenciasService
{
    private readonly global::Marten.IDocumentSession _session;
    private readonly AppDbContext _context;

    // Umbral: sugerir si el stock se agota en menos de N días al ritmo actual
    private const int UmbralDias = 14;
    // Confianza mínima: requiere al menos 5 ventas para emitir sugerencias
    private const double ConfidenciaMinima = 0.1;
    // Confianza plena: 50 ventas = 1.0
    private const double VentasParaConfianzaPlena = 50.0;

    public SugerenciasService(global::Marten.IDocumentSession session, AppDbContext context)
    {
        _session = session;
        _context = context;
    }

    public async Task<List<AutomaticActionDto>> ObtenerSugerenciasReabastecimientoAsync(int sucursalId)
    {
        // ── Cargar patrones de Marten ─────────────────────────────────────────
        var storePattern  = await _session.LoadAsync<StorePattern>(sucursalId);
        var businessRadar = await _session.LoadAsync<BusinessRadar>(sucursalId);

        if (storePattern == null || storePattern.ProductoVelocidad.Count == 0)
            return [];

        // ── Confianza basada en volumen de datos ──────────────────────────────
        var confidence = Math.Min(1.0, storePattern.TotalVentas / VentasParaConfianzaPlena);
        if (confidence < ConfidenciaMinima) return [];

        // ── Días de actividad como denominador para velocidad diaria ──────────
        var diasConActividad = Math.Max(1, businessRadar?.IngresosPorFecha.Count ?? 1);

        // ── Cargar stock actual de productos con velocidad registrada ─────────
        var productoIds = storePattern.ProductoVelocidad.Keys
            .Select(k => Guid.TryParse(k, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        var stocks = await _context.Stock
            .Include(s => s.Producto)
            .Where(s => s.SucursalId == sucursalId
                     && productoIds.Contains(s.ProductoId)
                     && s.Producto.Activo)
            .ToDictionaryAsync(s => s.ProductoId);

        // ── Generar sugerencias ───────────────────────────────────────────────
        var sugerencias = new List<(AutomaticActionDto Dto, decimal DiasRestantes)>();

        foreach (var (key, totalUnidades) in storePattern.ProductoVelocidad)
        {
            if (!Guid.TryParse(key, out var productoId)) continue;
            if (!stocks.TryGetValue(productoId, out var stock)) continue;

            var velocidadDiaria = (double)totalUnidades / diasConActividad;
            if (velocidadDiaria < 0.01) continue; // Producto sin actividad real

            var diasRestantes = velocidadDiaria > 0
                ? stock.Cantidad / (decimal)velocidadDiaria
                : 9999m;

            if (diasRestantes >= UmbralDias) continue;

            var cantidadSugerida = Math.Ceiling(velocidadDiaria * 14); // 2 semanas
            var diasRestantesRound = Math.Round(diasRestantes, 1);

            var dto = new AutomaticActionDto(
                TipoAccion:       "Reabastecimiento",
                ProductoId:       productoId,
                NombreProducto:   stock.Producto.Nombre,
                Description:      $"Pedir {cantidadSugerida:0} unidades de {stock.Producto.Nombre}",
                Reason:           $"Stock actual ({stock.Cantidad:0} uds) a {velocidadDiaria:F1} uds/día se agota en {diasRestantesRound} días",
                DataSource:       $"Basado en {storePattern.TotalVentas} ventas en {diasConActividad} días de actividad",
                Confidence:       Math.Round(confidence, 2),
                CanOverride:      true,
                CantidadSugerida: (decimal)cantidadSugerida,
                DiasRestantes:    diasRestantesRound
            );

            sugerencias.Add((dto, diasRestantes));
        }

        // Ordenar: más urgentes primero (menos días restantes), luego por confianza
        return sugerencias
            .OrderBy(s => s.DiasRestantes)
            .ThenByDescending(s => s.Dto.Confidence)
            .Select(s => s.Dto)
            .ToList();
    }
}
