import * as XLSX from 'xlsx';
import type {
  ReporteVentasDTO,
  ReporteInventarioValorizadoDTO,
  ProductoValorizadoDTO,
  ReporteCajaDTO,
  ReporteKardexDTO,
} from '@/types/api';

function descargar(wb: XLSX.WorkBook, filename: string) {
  XLSX.writeFile(wb, `${filename}.xlsx`);
}

function moneda(v: number) {
  return Math.round(v);
}

// ── Reporte de Ventas ──────────────────────────────────────────────────────────
export function exportarReporteVentas(
  reporte: ReporteVentasDTO,
  fechaDesde: string,
  fechaHasta: string,
) {
  const wb = XLSX.utils.book_new();

  // Hoja 1: Resumen
  const resumen = [
    ['Reporte de Ventas'],
    ['Período', `${fechaDesde} — ${fechaHasta}`],
    [],
    ['Métrica', 'Valor'],
    ['Total Ventas', moneda(reporte.totalVentas)],
    ['Cantidad Ventas', reporte.cantidadVentas],
    ['Ticket Promedio', moneda(reporte.ticketPromedio)],
    ['Costo Total', moneda(reporte.costoTotal)],
    ['Utilidad Total', moneda(reporte.utilidadTotal)],
    ['Margen Promedio %', Number(reporte.margenPromedio.toFixed(2))],
    [],
    ['Método de Pago', 'Cantidad', 'Total'],
    ...reporte.ventasPorMetodoPago.map((m) => [m.metodo, m.cantidad, moneda(m.total)]),
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(resumen), 'Resumen');

  // Hoja 2: Detalle por Día
  const detalle = [
    ['Fecha', 'Cantidad Ventas', 'Total Ventas', 'Costo Total', 'Utilidad', 'Margen %'],
    ...reporte.ventasPorDia.map((d) => {
      const margen = d.total > 0 ? (d.utilidad / d.total) * 100 : 0;
      return [
        d.fecha.substring(0, 10),
        d.cantidad,
        moneda(d.total),
        moneda(d.costoTotal),
        moneda(d.utilidad),
        Number(margen.toFixed(2)),
      ];
    }),
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(detalle), 'Por Día');

  descargar(wb, `reporte-ventas-${fechaDesde}-${fechaHasta}`);
}

// ── Reporte de Inventario ──────────────────────────────────────────────────────
export function exportarReporteInventario(
  reporte: ReporteInventarioValorizadoDTO,
  productos: ProductoValorizadoDTO[],
) {
  const wb = XLSX.utils.book_new();

  // Hoja 1: Resumen
  const resumen = [
    ['Reporte de Inventario Valorizado'],
    [],
    ['Métrica', 'Valor'],
    ['Total Productos', reporte.totalProductos],
    ['Total Unidades', reporte.totalUnidades],
    ['Costo Total', moneda(reporte.totalCosto)],
    ['Valor Venta', moneda(reporte.totalVenta)],
    ['Utilidad Potencial', moneda(reporte.utilidadPotencial)],
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(resumen), 'Resumen');

  // Hoja 2: Productos
  const detalle = [
    [
      'Código',
      'Producto',
      'Categoría',
      'Sucursal',
      'Stock',
      'Costo Unitario',
      'Costo Total',
      'Precio Venta',
      'Valor Venta',
      'Utilidad',
      'Margen %',
    ],
    ...productos.map((p) => [
      p.codigoBarras,
      p.nombre,
      p.categoria ?? '',
      p.nombreSucursal,
      p.cantidad,
      moneda(p.costoPromedio),
      moneda(p.costoTotal),
      moneda(p.precioVenta),
      moneda(p.valorVenta),
      moneda(p.utilidadPotencial),
      Number(p.margenPorcentaje.toFixed(2)),
    ]),
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(detalle), 'Productos');

  const fecha = new Date().toISOString().substring(0, 10);
  descargar(wb, `inventario-valorizado-${fecha}`);
}

// ── Reporte de Caja ────────────────────────────────────────────────────────────
export function exportarReporteCaja(reporte: ReporteCajaDTO) {
  const wb = XLSX.utils.book_new();

  // Hoja 1: Resumen
  const resumen = [
    ['Reporte de Caja'],
    ['Caja', reporte.nombreCaja],
    ['Sucursal', reporte.nombreSucursal],
    ['Apertura', reporte.fechaApertura.substring(0, 19).replace('T', ' ')],
    ['Cierre', reporte.fechaCierre ? reporte.fechaCierre.substring(0, 19).replace('T', ' ') : 'Abierta'],
    [],
    ['Concepto', 'Monto'],
    ['Monto Apertura', moneda(reporte.montoApertura)],
    ['Ventas Efectivo', moneda(reporte.totalVentasEfectivo)],
    ['Ventas Tarjeta', moneda(reporte.totalVentasTarjeta)],
    ['Ventas Transferencia', moneda(reporte.totalVentasTransferencia)],
    ['Total Ventas', moneda(reporte.totalVentas)],
    ['Diferencia', moneda(reporte.diferenciaEsperado ?? 0)],
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(resumen), 'Resumen');

  // Hoja 2: Ventas
  const detalle = [
    ['Nº Venta', 'Fecha', 'Método Pago', 'Cliente', 'Total', 'Costo', 'Utilidad', 'Margen %'],
    ...reporte.ventas.map((v) => {
      const margen = v.total > 0 ? (v.utilidad / v.total) * 100 : 0;
      return [
        v.numeroVenta,
        v.fechaVenta.substring(0, 19).replace('T', ' '),
        v.metodoPago,
        v.cliente ?? '',
        moneda(v.total),
        moneda(v.costoTotal),
        moneda(v.utilidad),
        Number(margen.toFixed(2)),
      ];
    }),
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(detalle), 'Ventas');

  descargar(wb, `reporte-caja-${reporte.nombreCaja}-${new Date().toISOString().substring(0, 10)}`);
}

// ── Reporte Kardex ─────────────────────────────────────────────────────────────
const kardexTipoLabel: Record<string, string> = {
  EntradaCompra: 'Compra',
  SalidaVenta: 'Venta',
  DevolucionCompra: 'Dev. Compra',
  Ajuste: 'Ajuste',
  TrasladoEntrada: 'Ent. Traslado',
  TrasladoSalida: 'Sal. Traslado',
};

export function exportarReporteKardex(reporte: ReporteKardexDTO) {
  const wb = XLSX.utils.book_new();

  // Hoja 1: Resumen
  const entradas = reporte.movimientos.reduce((a, m) => a + m.entrada, 0);
  const salidas = reporte.movimientos.reduce((a, m) => a + m.salida, 0);
  const resumen = [
    ['Kardex de Inventario'],
    ['Producto', reporte.nombre],
    ['Código', reporte.codigoBarras],
    ['Sucursal', reporte.nombreSucursal],
    ['Período', `${reporte.fechaDesde.substring(0, 10)} — ${reporte.fechaHasta.substring(0, 10)}`],
    [],
    ['Saldo Inicial', reporte.saldoInicial],
    ['Entradas', entradas],
    ['Salidas', salidas],
    ['Saldo Final', reporte.saldoFinal],
    ['Costo Promedio Vigente', moneda(reporte.costoPromedioVigente)],
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(resumen), 'Resumen');

  // Hoja 2: Movimientos
  const detalle = [
    ['Fecha', 'Movimiento', 'Documento', 'Entrada', 'Salida', 'Saldo', 'Costo Unit.', 'Costo Total', 'Observaciones'],
    ...reporte.movimientos.map((m) => [
      m.fecha.substring(0, 19).replace('T', ' '),
      kardexTipoLabel[m.tipoMovimiento] ?? m.tipoMovimiento,
      m.referencia,
      m.entrada || 0,
      m.salida || 0,
      m.saldoAcumulado,
      moneda(m.costoUnitario),
      moneda(m.costoTotalMovimiento),
      m.observaciones,
    ]),
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(detalle), 'Movimientos');

  descargar(wb, `kardex-${reporte.codigoBarras}-${reporte.fechaDesde.substring(0, 10)}-${reporte.fechaHasta.substring(0, 10)}`);
}
