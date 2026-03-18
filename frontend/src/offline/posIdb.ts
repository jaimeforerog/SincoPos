import { openDB } from 'idb';
import type { DBSchema, IDBPDatabase } from 'idb';
import type { ProductoDTO, CrearVentaDTO } from '@/types/api';

export interface OfflineVenta {
  localId: string;
  payload: CrearVentaDTO;
  creadoEn: string;
  intentos: number;
  estado: 'pending' | 'syncing' | 'failed' | 'synced';
  errorMensaje?: string;
  ventaServerId?: number;
}

export interface StockCacheItem {
  productoId: string;
  sucursalId: number;
  cantidad: number;
  reservado: number;
  disponible: number;
}

export interface PrecioCacheItem {
  productoId: string;
  sucursalId: number;
  precio: number;
  precioBase: number;
}

export interface MetaItem {
  key: string;
  value: unknown;
}

interface PosDbSchema extends DBSchema {
  productos: {
    key: string;
    value: ProductoDTO;
    indexes: { 'by-nombre': string; 'by-codigoBarras': string };
  };
  precios: {
    key: [string, number]; // [productoId, sucursalId]
    value: PrecioCacheItem;
    indexes: { 'by-sucursal': number };
  };
  stock: {
    key: [string, number]; // [productoId, sucursalId]
    value: StockCacheItem;
    indexes: { 'by-sucursal': number };
  };
  ventasQueue: {
    key: string; // localId
    value: OfflineVenta;
    indexes: { 'by-estado': string; 'by-sucursal': number };
  };
  meta: {
    key: string;
    value: MetaItem;
  };
}

export type PosDb = IDBPDatabase<PosDbSchema>;

const DB_NAME = 'sincopos-offline';
const DB_VERSION = 1;

let dbInstance: PosDb | null = null;
let dbNameOverride: string | null = null;

/** Solo para tests: permite usar un nombre de DB distinto por test */
export function setDbNameForTest(name: string | null): void {
  dbNameOverride = name;
}

export async function openPosDb(): Promise<PosDb> {
  if (dbInstance) return dbInstance;

  const name = dbNameOverride ?? DB_NAME;
  dbInstance = await openDB<PosDbSchema>(name, DB_VERSION, {
    upgrade(db) {
      // productos
      const productosStore = db.createObjectStore('productos', { keyPath: 'id' });
      productosStore.createIndex('by-nombre', 'nombre');
      productosStore.createIndex('by-codigoBarras', 'codigoBarras');

      // precios — clave compuesta [productoId, sucursalId]
      const preciosStore = db.createObjectStore('precios', {
        keyPath: ['productoId', 'sucursalId'],
      });
      preciosStore.createIndex('by-sucursal', 'sucursalId');

      // stock — clave compuesta [productoId, sucursalId]
      const stockStore = db.createObjectStore('stock', {
        keyPath: ['productoId', 'sucursalId'],
      });
      stockStore.createIndex('by-sucursal', 'sucursalId');

      // ventasQueue
      const ventasStore = db.createObjectStore('ventasQueue', { keyPath: 'localId' });
      ventasStore.createIndex('by-estado', 'estado');
      ventasStore.createIndex('by-sucursal', 'payload.sucursalId');

      // meta
      db.createObjectStore('meta', { keyPath: 'key' });
    },
  });

  return dbInstance;
}

// Para tests: resetear la instancia singleton
export function resetDbInstance(): void {
  dbInstance = null;
}
