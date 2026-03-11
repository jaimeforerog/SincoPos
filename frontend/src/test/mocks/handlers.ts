import { http, HttpResponse } from 'msw';

export const handlers = [
  // Productos (Manejar con y sin query params mediante wildcard al final solo para esto)
  http.get('*/api/v1/productos*', () =>
    HttpResponse.json({
      items: [
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
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
      totalPages: 1,
    })
  ),

  // Cajas abiertas del usuario
  http.get('*/api/v1/cajas/mis-abiertas', () =>
    HttpResponse.json([
      { id: 1, nombre: 'Caja 01', sucursalId: 1, nombreSucursal: 'Principal', activa: true },
    ])
  ),

  // Cajas — lista general (usado en el diálogo con query params)
  http.get('*/api/v1/cajas', () =>
    HttpResponse.json([
      { id: 1, nombre: 'Caja 01', sucursalId: 1, nombreSucursal: 'Principal', activa: true },
    ])
  ),

  // Caja por ID
  http.get('*/api/v1/cajas/:id', ({ params }) => {
    const id = Number(params.id);
    if (isNaN(id)) return undefined; 
    
    return HttpResponse.json({
      id: id,
      nombre: `Caja ${id}`,
      sucursalId: 1,
      nombreSucursal: 'Principal',
      activa: true,
    });
  }),

  // Precios resolver
  http.get('*/api/v1/precios/resolver', () => 
    HttpResponse.json({ precioVenta: 3500, origen: 'Base' })
  ),
  
  http.get('*/api/v1/precios/resolver-lote', () => 
    HttpResponse.json([
      { productoId: 'p1', precioVenta: 3500 }
    ])
  ),

  // Inventario stock
  http.get('*/api/v1/inventario/stock', () => HttpResponse.json([
    { productoId: 'p1', sucursalId: 1, cantidad: 100, loteId: 1, numeroLote: 'LOTE001' }
  ])),
  
  http.get('*/api/v1/inventario', () => HttpResponse.json([])),

  // Terceros (clientes)
  http.get('*/api/v1/terceros', () =>
    HttpResponse.json({
      items: [
        { id: 1, nombre: 'Cliente General', numeroDocumento: '123', esCliente: true, activo: true }
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
      totalPages: 1,
    })
  ),

  // Ventas
  http.post('*/api/v1/ventas', () =>
    HttpResponse.json({ id: 1, numero: 'V-001', total: 4165 }, { status: 201 })
  ),

  // Sucursales
  http.get('*/api/v1/sucursales', () =>
    HttpResponse.json([
      { id: 1, nombre: 'Sucursal Principal', activa: true, codigoCajaPrefijo: 'SP' },
    ])
  ),

  // Impuestos
  http.get('*/api/v1/impuestos', () =>
    HttpResponse.json([
      { id: 1, nombre: 'IVA 19%', porcentaje: 0.19, activo: true },
      { id: 2, nombre: 'Exento', porcentaje: 0, activo: true },
    ])
  ),

  // Compras (Orden de compra)
  http.post('*/api/v1/compras', () =>
    HttpResponse.json({ id: 100, numeroOrden: 'OC-001' }, { status: 201 })
  ),
];
