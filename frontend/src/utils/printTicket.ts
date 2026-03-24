import type { VentaDTO } from '@/types/api';

const fmt = (value: number) =>
  new Intl.NumberFormat('es-CO', {
    style: 'currency', currency: 'COP',
    minimumFractionDigits: 0, maximumFractionDigits: 0,
  }).format(value);

const fmtDate = (iso: string) =>
  new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

export function printTicket(venta: VentaDTO, cajeroNombre?: string) {
  const rows = venta.detalles.map((d) => `
    <tr>
      <td style="padding-right:4px">${d.nombreProducto}</td>
      <td style="text-align:right">${d.cantidad}</td>
      <td style="text-align:right">${fmt(d.precioUnitario)}</td>
      <td style="text-align:right">${fmt(d.subtotal)}</td>
    </tr>`).join('');

  const extras = [
    `<div style="display:flex;justify-content:space-between"><span>Subtotal</span><span>${fmt(venta.subtotal)}</span></div>`,
    venta.descuento > 0 ? `<div style="display:flex;justify-content:space-between"><span>Descuento</span><span>-${fmt(venta.descuento)}</span></div>` : '',
    venta.impuestos > 0 ? `<div style="display:flex;justify-content:space-between"><span>Impuestos</span><span>${fmt(venta.impuestos)}</span></div>` : '',
    `<hr/>`,
    `<div style="display:flex;justify-content:space-between;font-weight:bold;font-size:14px"><span>TOTAL</span><span>${fmt(venta.total)}</span></div>`,
    `<div style="display:flex;justify-content:space-between;margin-top:4px"><span>Pago (${venta.metodoPago})</span><span>${venta.montoPagado ? fmt(venta.montoPagado) : ''}</span></div>`,
    venta.cambio != null && venta.cambio > 0 ? `<div style="display:flex;justify-content:space-between"><span>Cambio</span><span>${fmt(venta.cambio)}</span></div>` : '',
  ].join('');

  const html = `<!DOCTYPE html><html><head><meta charset="utf-8">
    <style>body{font-family:monospace;font-size:12px;width:280px;margin:0 auto;padding:8px}</style>
  </head><body>
    <div style="text-align:center;margin-bottom:8px">
      <strong style="font-size:16px">SINCOPOS</strong><br/>
      <span>${venta.nombreSucursal}</span><br/>
      <span>Caja: ${venta.nombreCaja}</span><br/>
      ${cajeroNombre ? `<span>Cajero: ${cajeroNombre}</span><br/>` : ''}
      <span>${fmtDate(venta.fechaVenta)}</span><br/>
      <strong>#${venta.numeroVenta}</strong>
    </div>
    <hr/>
    ${venta.nombreCliente ? `<div style="margin-bottom:4px">Cliente: ${venta.nombreCliente}</div><hr/>` : ''}
    <table style="width:100%;border-collapse:collapse">
      <thead><tr>
        <th style="text-align:left">Producto</th>
        <th style="text-align:right">Cant</th>
        <th style="text-align:right">Precio</th>
        <th style="text-align:right">Total</th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>
    <hr/>
    ${extras}
    <hr/>
    <div style="text-align:center;margin-top:8px">¡Gracias por su compra!</div>
  </body></html>`;

  const win = window.open('', '_blank', 'width=320,height=600');
  if (!win) return;
  win.document.write(html);
  win.document.close();
  win.focus();
  win.print();
  win.close();
}
