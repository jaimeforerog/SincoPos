using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Aggregates;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public class KardexService : IKardexService
{
    private readonly AppDbContext _context;
    private readonly IActivityLogService _activityLogService;
    private readonly global::Marten.IDocumentSession _session;

    public KardexService(
        AppDbContext context,
        IActivityLogService activityLogService,
        global::Marten.IDocumentSession session)
    {
        _context = context;
        _activityLogService = activityLogService;
        _session = session;
    }

    public async Task<ReporteKardexDto> ObtenerKardexAsync(
        Guid productoId, int sucursalId, DateTime fechaDesde, DateTime fechaHasta)
    {
        var (fechaDesdeUtc, fechaHastaUtc) = NormalizarRangoUtc(fechaDesde, fechaHasta);

        var productoInfo = await _context.Productos
            .Where(p => p.Id == productoId)
            .Select(p => new { p.CodigoBarras, p.Nombre })
            .FirstOrDefaultAsync();

        var sucursalInfo = await _context.Sucursales
            .Where(s => s.Id == sucursalId)
            .Select(s => new { s.Nombre })
            .FirstOrDefaultAsync();

        if (productoInfo == null || sucursalInfo == null)
            throw new InvalidOperationException("Producto o Sucursal no encontrados.");

        var streamId = InventarioAggregate.GenerarStreamId(productoId, sucursalId);
        var eventosMarten = await _session.Events.FetchStreamAsync(streamId);

        decimal saldoAcumulado = 0;
        decimal saldoInicial = 0;
        decimal costoPromedioVigente = 0;
        var movimientos = new List<KardexMovimientoDto>();

        foreach (var martenEvent in eventosMarten.OrderBy(e => e.Timestamp))
        {
            if (martenEvent.Data is not BaseEvent evtData) continue;

            var evtTimestamp = evtData.Timestamp;

            if (evtTimestamp < fechaDesdeUtc)
            {
                AcumularSaldoAnterior(evtData, ref saldoInicial, ref costoPromedioVigente);
                saldoAcumulado = saldoInicial;
            }
            else if (evtTimestamp <= fechaHastaUtc)
            {
                var movimiento = ProcesarEvento(evtData, ref saldoAcumulado, ref costoPromedioVigente, evtTimestamp);
                if (movimiento != null)
                    movimientos.Add(movimiento);
            }
        }

        await _activityLogService.LogActivityAsync(new ActivityLogDto(
            Accion: "ConsultarKardex",
            Tipo: TipoActividad.Inventario,
            Descripcion: $"Kardex de {productoInfo.CodigoBarras} consultado. Saldo Final: {saldoAcumulado}",
            SucursalId: sucursalId,
            TipoEntidad: "Reporte",
            EntidadId: productoId.ToString(),
            EntidadNombre: "Kardex Inventario",
            DatosNuevos: new { productoId, sucursalId, fechaDesde, fechaHasta, saldoInicial, saldoFinal = saldoAcumulado }
        ));

        return new ReporteKardexDto(
            productoId,
            productoInfo.CodigoBarras,
            productoInfo.Nombre,
            sucursalId,
            sucursalInfo.Nombre,
            fechaDesdeUtc,
            fechaHastaUtc,
            saldoInicial,
            saldoAcumulado,
            costoPromedioVigente,
            movimientos
        );
    }

    private void AcumularSaldoAnterior(BaseEvent evtData, ref decimal saldo, ref decimal costoPromedio)
    {
        if (evtData is EntradaCompraRegistrada ec)
        {
            costoPromedio = RecalcularPromedio(saldo, costoPromedio, ec.Cantidad, ec.CostoUnitario);
            saldo += ec.Cantidad;
        }
        else if (evtData is SalidaVentaRegistrada sv) saldo -= sv.Cantidad;
        else if (evtData is DevolucionProveedorRegistrada dp) saldo -= dp.Cantidad;
        else if (evtData is AjusteInventarioRegistrado ai) { saldo = ai.CantidadNueva; costoPromedio = ai.CostoUnitario; }
        else if (evtData is TrasladoSalidaRegistrado ts) saldo -= ts.Cantidad;
        else if (evtData is TrasladoEntradaRegistrado te)
        {
            costoPromedio = RecalcularPromedio(saldo, costoPromedio, te.CantidadRecibida, te.CostoUnitario);
            saldo += te.CantidadRecibida;
        }
    }

    private KardexMovimientoDto? ProcesarEvento(
        BaseEvent evtData, ref decimal saldo, ref decimal costoPromedio, DateTimeOffset timestamp)
    {
        string tipo;
        string referencia;
        string observaciones = "";
        decimal entrada = 0, salida = 0, costoUnitario = 0, costoTotal = 0;

        switch (evtData)
        {
            case EntradaCompraRegistrada ec:
                tipo = "EntradaCompra";
                referencia = ec.Referencia ?? "";
                observaciones = ec.Observaciones ?? "";
                entrada = ec.Cantidad;
                costoUnitario = ec.CostoUnitario;
                costoTotal = ec.CostoTotal;
                costoPromedio = RecalcularPromedio(saldo, costoPromedio, ec.Cantidad, ec.CostoUnitario);
                saldo += ec.Cantidad;
                break;
            case SalidaVentaRegistrada sv:
                tipo = "SalidaVenta";
                referencia = sv.ReferenciaVenta ?? "";
                salida = sv.Cantidad;
                costoUnitario = sv.CostoUnitario;
                costoTotal = sv.CostoTotal;
                saldo -= sv.Cantidad;
                break;
            case DevolucionProveedorRegistrada dp:
                tipo = "DevolucionCompra";
                referencia = dp.Referencia ?? "";
                salida = dp.Cantidad;
                costoUnitario = dp.CostoUnitario;
                costoTotal = dp.CostoTotal;
                saldo -= dp.Cantidad;
                break;
            case AjusteInventarioRegistrado ai:
                tipo = "Ajuste";
                referencia = "Ajuste de Inventario";
                observaciones = ai.Observaciones ?? "";
                if (ai.EsPositivo) entrada = ai.Diferencia;
                else salida = Math.Abs(ai.Diferencia);
                costoUnitario = ai.CostoUnitario;
                costoTotal = ai.CostoTotal;
                saldo = ai.CantidadNueva;
                costoPromedio = ai.CostoUnitario;
                break;
            case TrasladoSalidaRegistrado ts:
                tipo = "TrasladoSalida";
                referencia = ts.NumeroTraslado ?? "";
                salida = ts.Cantidad;
                costoUnitario = ts.CostoUnitario;
                costoTotal = ts.CostoTotal;
                saldo -= ts.Cantidad;
                break;
            case TrasladoEntradaRegistrado te:
                tipo = "TrasladoEntrada";
                referencia = te.NumeroTraslado ?? "";
                entrada = te.CantidadRecibida;
                costoUnitario = te.CostoUnitario;
                costoTotal = te.CostoTotal;
                costoPromedio = RecalcularPromedio(saldo, costoPromedio, te.CantidadRecibida, te.CostoUnitario);
                saldo += te.CantidadRecibida;
                break;
            default:
                return null;
        }

        return new KardexMovimientoDto(
            timestamp.UtcDateTime, tipo, referencia, observaciones,
            entrada, salida, saldo, costoUnitario, costoTotal);
    }

    private static decimal RecalcularPromedio(decimal cantAnterior, decimal costoAnterior, decimal cantNueva, decimal costoNuevo)
    {
        var totalCant = cantAnterior + cantNueva;
        if (totalCant <= 0) return costoNuevo;
        return ((cantAnterior * costoAnterior) + (cantNueva * costoNuevo)) / totalCant;
    }

    private static (DateTime desde, DateTime hasta) NormalizarRangoUtc(DateTime fechaDesde, DateTime fechaHasta) =>
        (DateTime.SpecifyKind(fechaDesde.Date, DateTimeKind.Utc),
         DateTime.SpecifyKind(fechaHasta.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));
}
