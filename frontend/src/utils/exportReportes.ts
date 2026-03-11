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

/** Crea un workbook con una sola hoja: bloque de resumen + fila vacía + tabla de detalle */
function hojaUnica(
  nombre: string,
  resumen: (string | number | null)[][],
  encabezados: string[],
  filas: (string | number | null)[][],
): XLSX.WorkBook {
  const wb = XLSX.utils.book_new();
  const data: (string | number | null)[][] = [
    ...resumen,
    [],
    encabezados,
    ...filas,
  ];
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(data), nombre);
  return wb;
}

// ── Reporte de Ventas ──────────────────────────────────────────────────────────
export function exportarReporteVentas(
  reporte: ReporteVentasDTO,
  fechaDesde: string,
  fechaHasta: string,
) {
  const resumen = [
    ['Reporte de Ventas'],
    ['Período', `${fechaDesde} — ${fechaHasta}`],
    [],
    ['Total Ventas', moneda(reporte.totalVentas)],
    ['Cantidad Ventas', reporte.cantidadVentas],
    ['Ticket Promedio', moneda(reporte.ticketPromedio)],
    ['Costo Total', moneda(reporte.costoTotal)],
    ['Utilidad Total', moneda(reporte.utilidadTotal)],
    ['Margen Promedio %', Number(reporte.margenPromedio.toFixed(2))],
    [],
    ['Ventas por Método de Pago'],
    ['Método', 'Cantidad', 'Total'],
    ...reporte.ventasPorMetodoPago.map((m) => [m.metodo, m.cantidad, moneda(m.total)]),
  ];

  const encabezados = ['Fecha', 'Cantidad Ventas', 'Total Ventas', 'Costo Total', 'Utilidad', 'Margen %'];
  const filas = reporte.ventasPorDia.map((d) => {
    const margen = d.total > 0 ? (d.utilidad / d.total) * 100 : 0;
    return [
      d.fecha.substring(0, 10),
      d.cantidad,
      moneda(d.total),
      moneda(d.costoTotal),
      moneda(d.utilidad),
      Number(margen.toFixed(2)),
    ] as (string | number | null)[];
  });

  const wb = hojaUnica('Ventas por Día', resumen, encabezados, filas);
  descargar(wb, `reporte-ventas-${fechaDesde}-${fechaHasta}`);
}

// ── Reporte de Inventario ──────────────────────────────────────────────────────
export function exportarReporteInventario(
  reporte: ReporteInventarioValorizadoDTO,
  productos: ProductoValorizadoDTO[],
) {
  const resumen = [
    ['Reporte de Inventario Valorizado'],
    ['Generado', new Date().toLocaleDateString('es-CO')],
    [],
    ['Total Productos', reporte.totalProductos],
    ['Total Unidades', reporte.totalUnidades],
    ['Costo Total', moneda(reporte.totalCosto)],
    ['Valor Venta', moneda(reporte.totalVenta)],
    ['Utilidad Potencial', moneda(reporte.utilidadPotencial)],
  ];

  const encabezados = [
    'Código', 'Producto', 'Categoría', 'Sucursal', 'Stock',
    'Costo Unitario', 'Costo Total', 'Precio Venta', 'Valor Venta', 'Utilidad', 'Margen %',
  ];
  const filas = productos.map((p) => [
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
  ] as (string | number | null)[]);

  const wb = hojaUnica('Inventario', resumen, encabezados, filas);
  descargar(wb, `inventario-valorizado-${new Date().toISOString().substring(0, 10)}`);
}

// ── Reporte de Caja ────────────────────────────────────────────────────────────
export function exportarReporteCaja(reporte: ReporteCajaDTO) {
  const resumen = [
    ['Reporte de Caja'],
    ['Caja', reporte.nombreCaja],
    ['Sucursal', reporte.nombreSucursal],
    ['Apertura', reporte.fechaApertura.substring(0, 19).replace('T', ' ')],
    ['Cierre', reporte.fechaCierre ? reporte.fechaCierre.substring(0, 19).replace('T', ' ') : 'Abierta'],
    [],
    ['Monto Apertura', moneda(reporte.montoApertura)],
    ['Ventas Efectivo', moneda(reporte.totalVentasEfectivo)],
    ['Ventas Tarjeta', moneda(reporte.totalVentasTarjeta)],
    ['Ventas Transferencia', moneda(reporte.totalVentasTransferencia)],
    ['Total Ventas', moneda(reporte.totalVentas)],
    ['Diferencia', moneda(reporte.diferenciaEsperado ?? 0)],
  ];

  const encabezados = ['Nº Venta', 'Fecha', 'Método Pago', 'Cliente', 'Total', 'Costo', 'Utilidad', 'Margen %'];
  const filas = reporte.ventas.map((v) => {
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
    ] as (string | number | null)[];
  });

  const wb = hojaUnica('Ventas', resumen, encabezados, filas);
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
    ['Total Entradas', entradas],
    ['Total Salidas', salidas],
    ['Saldo Final', reporte.saldoFinal],
    ['Costo Promedio Vigente', moneda(reporte.costoPromedioVigente)],
  ];

  const encabezados = [
    'Fecha', 'Movimiento', 'Documento', 'Entrada', 'Salida',
    'Saldo', 'Costo Unit.', 'Costo Total', 'Observaciones',
  ];
  const filas = reporte.movimientos.map((m) => [
    m.fecha.substring(0, 19).replace('T', ' '),
    kardexTipoLabel[m.tipoMovimiento] ?? m.tipoMovimiento,
    m.referencia,
    m.entrada || 0,
    m.salida || 0,
    m.saldoAcumulado,
    moneda(m.costoUnitario),
    moneda(m.costoTotalMovimiento),
    m.observaciones,
  ] as (string | number | null)[]);

  const wb = hojaUnica('Movimientos', resumen, encabezados, filas);
  descargar(wb, `kardex-${reporte.codigoBarras}-${reporte.fechaDesde.substring(0, 10)}-${reporte.fechaHasta.substring(0, 10)}`);
}
