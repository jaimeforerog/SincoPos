import { http, HttpResponse } from 'msw';

const BASE = 'http://localhost:5086/api/v1';

export const handlers = [
  // Productos
  http.get(`${BASE}/productos`, () =>
    HttpResponse.json([
      {
        id: 'p1',
        codigoBarras: '1234567890',
        nombre: 'Coca-Cola 350ml',
        precioVenta: 3500,
        precioCosto: 2000,
        porcentajeImpuesto: 0.19,
        impuestoPorcentaje: 0.19,
        activo: true,
        categoriaId: 1,
        esAlimentoUltraprocesado: false,
        unidadMedida: '94',
        fechaCreacion: '2026-01-01T00:00:00Z',
      },
    ])
  ),

  // Cajas — lista general
  http.get(`${BASE}/cajas`, () => HttpResponse.json([])),

  // Cajas abiertas del usuario
  http.get(`${BASE}/cajas/mis-abiertas`, () => HttpResponse.json([])),

  // Precios lote
  http.get(`${BASE}/precios/resolver-lote`, () => HttpResponse.json([])),

  // Inventario stock
  http.get(`${BASE}/inventario/stock`, () => HttpResponse.json([])),

  // Terceros (clientes)
  http.get(`${BASE}/terceros`, () => HttpResponse.json([])),

  // Ventas
  http.post(`${BASE}/ventas`, () =>
    HttpResponse.json({ id: 1, numero: 'V-001', total: 4165 }, { status: 201 })
  ),
];
