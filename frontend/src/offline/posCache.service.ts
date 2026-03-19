import type { PosDb, OfflineVenta, StockCacheItem, PrecioCacheItem } from './posIdb';
import type { ProductoDTO, CrearVentaDTO } from '@/types/api';

// ─── Productos ───────────────────────────────────────────────────────────────

export async function saveProductos(db: PosDb, productos: ProductoDTO[]): Promise<void> {
  const tx = db.transaction('productos', 'readwrite');
  await Promise.all([
    ...productos.map((p) => tx.store.put(p)),
    tx.done,
  ]);
}

export async function getProductos(db: PosDb, query?: string): Promise<ProductoDTO[]> {
  const all = await db.getAll('productos');
  if (!query || query.trim() === '') return all;

  const q = query.toLowerCase();
  return all.filter(
    (p) =>
      p.nombre.toLowerCase().includes(q) ||
      p.codigoBarras?.toLowerCase().includes(q)
  );
}

export async function getProductoById(db: PosDb, id: string): Promise<ProductoDTO | undefined> {
  return db.get('productos', id);
}

// ─── Precios ─────────────────────────────────────────────────────────────────

export async function savePrecios(
  db: PosDb,
  sucursalId: number,
  precios: PrecioCacheItem[]
): Promise<void> {
  const tx = db.transaction('precios', 'readwrite');
  await Promise.all([
    ...precios.map((p) => tx.store.put({ ...p, sucursalId })),
    tx.done,
  ]);
}

export async function getPreciosBySucursal(
  db: PosDb,
  sucursalId: number
): Promise<PrecioCacheItem[]> {
  return db.getAllFromIndex('precios', 'by-sucursal', sucursalId);
}

export async function getPrecio(
  db: PosDb,
  productoId: string,
  sucursalId: number
): Promise<PrecioCacheItem | undefined> {
  return db.get('precios', [productoId, sucursalId]);
}

// ─── Stock ───────────────────────────────────────────────────────────────────

export async function saveStock(
  db: PosDb,
  sucursalId: number,
  items: StockCacheItem[]
): Promise<void> {
  const tx = db.transaction('stock', 'readwrite');
  await Promise.all([
    ...items.map((s) => tx.store.put({ ...s, sucursalId })),
    tx.done,
  ]);
}

export async function getStockBySucursal(
  db: PosDb,
  sucursalId: number
): Promise<StockCacheItem[]> {
  return db.getAllFromIndex('stock', 'by-sucursal', sucursalId);
}

export async function getStockProducto(
  db: PosDb,
  productoId: string,
  sucursalId: number
): Promise<StockCacheItem | undefined> {
  return db.get('stock', [productoId, sucursalId]);
}

// ─── Cola de ventas offline ──────────────────────────────────────────────────

function generateLocalId(): string {
  return `offline-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

export async function enqueueVenta(db: PosDb, payload: CrearVentaDTO): Promise<string> {
  const localId = generateLocalId();
  const venta: OfflineVenta = {
    localId,
    payload,
    creadoEn: new Date().toISOString(),
    intentos: 0,
    estado: 'pending',
  };
  await db.put('ventasQueue', venta);
  return localId;
}

export async function getPendingVentas(db: PosDb): Promise<OfflineVenta[]> {
  return db.getAllFromIndex('ventasQueue', 'by-estado', 'pending');
}

export async function getAllQueuedVentas(db: PosDb): Promise<OfflineVenta[]> {
  return db.getAll('ventasQueue');
}

export async function getPendingCount(db: PosDb): Promise<number> {
  return db.countFromIndex('ventasQueue', 'by-estado', 'pending');
}

export async function getFailedCount(db: PosDb): Promise<number> {
  return db.countFromIndex('ventasQueue', 'by-estado', 'failed');
}

export async function getFailedVentas(db: PosDb): Promise<OfflineVenta[]> {
  return db.getAllFromIndex('ventasQueue', 'by-estado', 'failed');
}

export async function clearFailedVentas(db: PosDb): Promise<void> {
  const all = await db.getAllFromIndex('ventasQueue', 'by-estado', 'failed');
  await Promise.all(all.map((v) => db.delete('ventasQueue', v.localId)));
}

export async function markVentaSyncing(db: PosDb, localId: string): Promise<void> {
  const venta = await db.get('ventasQueue', localId);
  if (!venta) return;
  await db.put('ventasQueue', { ...venta, estado: 'syncing' });
}

export async function markVentaSynced(
  db: PosDb,
  localId: string,
  ventaServerId: number
): Promise<void> {
  const venta = await db.get('ventasQueue', localId);
  if (!venta) return;
  await db.put('ventasQueue', { ...venta, estado: 'synced', ventaServerId });
}

export async function markVentaFailed(
  db: PosDb,
  localId: string,
  errorMensaje: string
): Promise<void> {
  const venta = await db.get('ventasQueue', localId);
  if (!venta) return;
  await db.put('ventasQueue', {
    ...venta,
    estado: 'failed',
    intentos: venta.intentos + 1,
    errorMensaje,
  });
}

export async function markVentaPendingRetry(db: PosDb, localId: string): Promise<void> {
  const venta = await db.get('ventasQueue', localId);
  if (!venta) return;
  await db.put('ventasQueue', { ...venta, estado: 'pending', errorMensaje: undefined });
}

export async function deleteVenta(db: PosDb, localId: string): Promise<void> {
  await db.delete('ventasQueue', localId);
}

// ─── Meta ────────────────────────────────────────────────────────────────────

export async function setCacheMeta(db: PosDb, key: string, value: unknown): Promise<void> {
  await db.put('meta', { key, value });
}

export async function getCacheMeta(db: PosDb, key: string): Promise<unknown> {
  const item = await db.get('meta', key);
  return item?.value;
}
