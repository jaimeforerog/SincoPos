import { describe, it, expect, beforeEach } from 'vitest';
import 'fake-indexeddb/auto';
import { openPosDb, resetDbInstance, setDbNameForTest } from '../posIdb';
import type { PosDb, StockCacheItem, PrecioCacheItem } from '../posIdb';
import type { ProductoDTO, CrearVentaDTO } from '@/types/api';
import {
  saveProductos,
  getProductos,
  getProductoById,
  savePrecios,
  getPreciosBySucursal,
  getPrecio,
  saveStock,
  getStockBySucursal,
  getStockProducto,
  enqueueVenta,
  getPendingVentas,
  getPendingCount,
  getFailedCount,
  markVentaSynced,
  markVentaFailed,
  markVentaPendingRetry,
  deleteVenta,
  getAllQueuedVentas,
  setCacheMeta,
  getCacheMeta,
} from '../posCache.service';

const makeProducto = (override?: Partial<ProductoDTO>): ProductoDTO => ({
  id: 'prod-1',
  codigoBarras: '7700001',
  nombre: 'Leche Entera',
  categoriaId: 1,
  precioCosto: 2500,
  precioVenta: 3500,
  activo: true,
  fechaCreacion: '2026-01-01T00:00:00Z',
  esAlimentoUltraprocesado: false,
  unidadMedida: '94',
  manejaLotes: false,
  ...override,
});

const makeVentaPayload = (override?: Partial<CrearVentaDTO>): CrearVentaDTO => ({
  sucursalId: 1,
  cajaId: 1,
  metodoPago: 0,
  montoPagado: 10000,
  lineas: [{ productoId: 'prod-1', cantidad: 2, descuento: 0 }],
  ...override,
});

describe('posCache.service', () => {
  let db: PosDb;
  let testCounter = 0;

  beforeEach(async () => {
    // Nombre único por test para evitar contaminación entre tests
    testCounter++;
    setDbNameForTest(`sincopos-test-${testCounter}`);
    resetDbInstance();
    db = await openPosDb();
  });

  // ─── Productos ───────────────────────────────────────────────────────────

  describe('productos', () => {
    it('saveProductos y getProductos roundtrip', async () => {
      const productos = [makeProducto(), makeProducto({ id: 'prod-2', nombre: 'Pan Integral' })];
      await saveProductos(db, productos);

      const result = await getProductos(db);
      expect(result).toHaveLength(2);
    });

    it('getProductos filtra por nombre (case-insensitive)', async () => {
      await saveProductos(db, [
        makeProducto({ id: 'p1', nombre: 'Leche Entera' }),
        makeProducto({ id: 'p2', nombre: 'Pan Integral' }),
      ]);

      const result = await getProductos(db, 'leche');
      expect(result).toHaveLength(1);
      expect(result[0].nombre).toBe('Leche Entera');
    });

    it('getProductos filtra por codigoBarras', async () => {
      await saveProductos(db, [
        makeProducto({ id: 'p1', codigoBarras: '1234567' }),
        makeProducto({ id: 'p2', codigoBarras: '9999999' }),
      ]);

      const result = await getProductos(db, '1234567');
      expect(result).toHaveLength(1);
    });

    it('getProductos sin query retorna todos', async () => {
      await saveProductos(db, [makeProducto(), makeProducto({ id: 'p2', nombre: 'Otro' })]);
      const result = await getProductos(db);
      expect(result).toHaveLength(2);
    });

    it('getProductoById retorna el producto correcto', async () => {
      await saveProductos(db, [makeProducto({ id: 'target', nombre: 'Objetivo' })]);
      const result = await getProductoById(db, 'target');
      expect(result?.nombre).toBe('Objetivo');
    });

    it('getProductoById retorna undefined si no existe', async () => {
      const result = await getProductoById(db, 'inexistente');
      expect(result).toBeUndefined();
    });

    it('saveProductos sobreescribe un producto existente (put)', async () => {
      await saveProductos(db, [makeProducto({ nombre: 'Leche v1' })]);
      await saveProductos(db, [makeProducto({ nombre: 'Leche v2' })]);

      const result = await getProductoById(db, 'prod-1');
      expect(result?.nombre).toBe('Leche v2');
    });
  });

  // ─── Precios ─────────────────────────────────────────────────────────────

  describe('precios', () => {
    const makePrecio = (override?: Partial<PrecioCacheItem>): PrecioCacheItem => ({
      productoId: 'prod-1',
      sucursalId: 1,
      precio: 3500,
      precioBase: 3200,
      ...override,
    });

    it('savePrecios y getPreciosBySucursal roundtrip', async () => {
      await savePrecios(db, 1, [makePrecio()]);
      const result = await getPreciosBySucursal(db, 1);
      expect(result).toHaveLength(1);
      expect(result[0].precio).toBe(3500);
    });

    it('getPrecio retorna precio por clave compuesta', async () => {
      await savePrecios(db, 1, [makePrecio({ productoId: 'p1', precio: 4000 })]);
      const result = await getPrecio(db, 'p1', 1);
      expect(result?.precio).toBe(4000);
    });

    it('getPrecio retorna undefined si no existe la combinación', async () => {
      const result = await getPrecio(db, 'no-existe', 99);
      expect(result).toBeUndefined();
    });

    it('savePrecios sobreescribe precios previos de la misma sucursal', async () => {
      await savePrecios(db, 1, [makePrecio({ precio: 3000 })]);
      await savePrecios(db, 1, [makePrecio({ precio: 3800 })]);
      const result = await getPrecio(db, 'prod-1', 1);
      expect(result?.precio).toBe(3800);
    });

    it('getPreciosBySucursal no mezcla sucursales', async () => {
      await savePrecios(db, 1, [makePrecio({ sucursalId: 1 })]);
      await savePrecios(db, 2, [makePrecio({ productoId: 'prod-2', sucursalId: 2 })]);

      const sucursal1 = await getPreciosBySucursal(db, 1);
      expect(sucursal1).toHaveLength(1);
      expect(sucursal1[0].productoId).toBe('prod-1');
    });
  });

  // ─── Stock ───────────────────────────────────────────────────────────────

  describe('stock', () => {
    const makeStock = (override?: Partial<StockCacheItem>): StockCacheItem => ({
      productoId: 'prod-1',
      sucursalId: 1,
      cantidad: 100,
      reservado: 5,
      disponible: 95,
      ...override,
    });

    it('saveStock y getStockBySucursal roundtrip', async () => {
      await saveStock(db, 1, [makeStock()]);
      const result = await getStockBySucursal(db, 1);
      expect(result).toHaveLength(1);
      expect(result[0].disponible).toBe(95);
    });

    it('getStockProducto retorna stock por clave compuesta', async () => {
      await saveStock(db, 1, [makeStock({ productoId: 'p-target', disponible: 42 })]);
      const result = await getStockProducto(db, 'p-target', 1);
      expect(result?.disponible).toBe(42);
    });

    it('getStockBySucursal retorna array vacío si no hay datos cacheados', async () => {
      const result = await getStockBySucursal(db, 999);
      expect(result).toEqual([]);
    });

    it('getStockProducto retorna undefined si no existe', async () => {
      const result = await getStockProducto(db, 'no-existe', 1);
      expect(result).toBeUndefined();
    });
  });

  // ─── Cola de ventas offline ──────────────────────────────────────────────

  describe('ventasQueue', () => {
    it('enqueueVenta asigna localId único con prefijo offline-', async () => {
      const id1 = await enqueueVenta(db, makeVentaPayload());
      const id2 = await enqueueVenta(db, makeVentaPayload());
      expect(id1).toMatch(/^offline-/);
      expect(id2).toMatch(/^offline-/);
      expect(id1).not.toBe(id2);
    });

    it('enqueueVenta guarda con estado pending', async () => {
      const localId = await enqueueVenta(db, makeVentaPayload());
      const ventas = await getPendingVentas(db);
      expect(ventas).toHaveLength(1);
      expect(ventas[0].localId).toBe(localId);
      expect(ventas[0].estado).toBe('pending');
    });

    it('getPendingCount retorna 0 con cola vacía', async () => {
      const count = await getPendingCount(db);
      expect(count).toBe(0);
    });

    it('getPendingCount retorna N con N ventas pendientes', async () => {
      await enqueueVenta(db, makeVentaPayload());
      await enqueueVenta(db, makeVentaPayload());
      await enqueueVenta(db, makeVentaPayload());
      const count = await getPendingCount(db);
      expect(count).toBe(3);
    });

    it('markVentaSynced cambia estado a synced y guarda ventaServerId', async () => {
      const localId = await enqueueVenta(db, makeVentaPayload());
      await markVentaSynced(db, localId, 42);

      const todas = await getAllQueuedVentas(db);
      const venta = todas.find((v) => v.localId === localId);
      expect(venta?.estado).toBe('synced');
      expect(venta?.ventaServerId).toBe(42);
    });

    it('markVentaSynced no cuenta como pending', async () => {
      const localId = await enqueueVenta(db, makeVentaPayload());
      await markVentaSynced(db, localId, 1);
      const count = await getPendingCount(db);
      expect(count).toBe(0);
    });

    it('markVentaFailed cambia estado y guarda errorMensaje', async () => {
      const localId = await enqueueVenta(db, makeVentaPayload());
      await markVentaFailed(db, localId, 'Stock insuficiente');

      const todas = await getAllQueuedVentas(db);
      const venta = todas.find((v) => v.localId === localId);
      expect(venta?.estado).toBe('failed');
      expect(venta?.errorMensaje).toBe('Stock insuficiente');
      expect(venta?.intentos).toBe(1);
    });

    it('getFailedCount cuenta solo las fallidas', async () => {
      const id1 = await enqueueVenta(db, makeVentaPayload());
      await enqueueVenta(db, makeVentaPayload());
      await markVentaFailed(db, id1, 'error');

      expect(await getFailedCount(db)).toBe(1);
      expect(await getPendingCount(db)).toBe(1);
    });

    it('markVentaPendingRetry restaura estado a pending y borra errorMensaje', async () => {
      const localId = await enqueueVenta(db, makeVentaPayload());
      await markVentaFailed(db, localId, 'error temporal');
      await markVentaPendingRetry(db, localId);

      const count = await getPendingCount(db);
      expect(count).toBe(1);

      const todas = await getAllQueuedVentas(db);
      const venta = todas.find((v) => v.localId === localId);
      expect(venta?.errorMensaje).toBeUndefined();
    });

    it('deleteVenta elimina la venta de la cola', async () => {
      const localId = await enqueueVenta(db, makeVentaPayload());
      await deleteVenta(db, localId);
      const count = await getPendingCount(db);
      expect(count).toBe(0);
    });
  });

  // ─── Meta ────────────────────────────────────────────────────────────────

  describe('meta', () => {
    it('setCacheMeta y getCacheMeta roundtrip', async () => {
      await setCacheMeta(db, 'productos-last-updated', '2026-03-16T10:00:00Z');
      const value = await getCacheMeta(db, 'productos-last-updated');
      expect(value).toBe('2026-03-16T10:00:00Z');
    });

    it('getCacheMeta retorna undefined para clave inexistente', async () => {
      const value = await getCacheMeta(db, 'no-existe');
      expect(value).toBeUndefined();
    });

    it('setCacheMeta sobreescribe valor existente', async () => {
      await setCacheMeta(db, 'key', 'v1');
      await setCacheMeta(db, 'key', 'v2');
      const value = await getCacheMeta(db, 'key');
      expect(value).toBe('v2');
    });
  });
});
