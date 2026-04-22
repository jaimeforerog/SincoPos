import { useEffect, useRef } from 'react';
import { openPosDb } from '@/offline/posIdb';
import { saveProductos, saveStock, savePrecios, setCacheMeta } from '@/offline/posCache.service';
import { productosApi } from '@/api/productos';
import { inventarioApi } from '@/api/inventario';
import { preciosApi } from '@/api/precios';
import { posApi } from '@/api/pos';
import { useTurnContextStore } from '@/stores/turnContext.store';
import type { StockCacheItem, PrecioCacheItem } from '@/offline/posIdb';

/**
 * Capa 2 — Precarga proactiva al abrir turno.
 * Descarga productos, stock y precios en IndexedDB para que el POS
 * funcione offline desde el primer minuto de la sesión.
 *
 * Capa 3 — Repetición cero.
 * Precarga el contexto del turno (clientes recientes + órdenes pendientes)
 * en useTurnContextStore para que CartHeader los exponga sin búsqueda.
 *
 * - Solo corre cuando hay conexión (navigator.onLine).
 * - Solo corre una vez por combinación caja+sucursal.
 * - No bloquea la UI (todo en background).
 */
export function useTurnPreload(
  selectedCajaId: number | null,
  activeSucursalId: number | null,
  sessionKey?: string
) {
  const lastPreloadKey = useRef<string | null>(null);
  const setContext = useTurnContextStore((s) => s.setContext);

  useEffect(() => {
    if (!selectedCajaId || !activeSucursalId) return;
    if (!navigator.onLine) return;

    const key = `${selectedCajaId}-${activeSucursalId}-${sessionKey ?? ''}`;
    if (lastPreloadKey.current === key) return;
    lastPreloadKey.current = key;

    void (async () => {
      try {
        const db = await openPosDb();

        // Capa 2: productos/stock/precios → IndexedDB (offline)
        // Capa 3: contexto de turno → Zustand store (clientes recientes + órdenes pendientes)
        const [productosResult, stockResult, preciosResult, contexto] = await Promise.all([
          productosApi.getAll({ activo: true, pageSize: 1000 }),
          inventarioApi.getStock({ sucursalId: activeSucursalId }),
          preciosApi.resolverLote(activeSucursalId),
          posApi.getContexto(activeSucursalId),
        ]);

        const stockItems: StockCacheItem[] = stockResult.map((s) => ({
          productoId:  s.productoId,
          sucursalId:  s.sucursalId,
          cantidad:    s.cantidad,
          reservado:   0,
          disponible:  s.cantidad,
        }));

        const precioItems: PrecioCacheItem[] = preciosResult.map((p) => ({
          productoId: p.productoId,
          sucursalId: activeSucursalId,
          precio:     p.precioVenta,
          precioBase: p.precioVenta,
        }));

        await Promise.all([
          saveProductos(db, productosResult.items),
          saveStock(db, activeSucursalId, stockItems),
          savePrecios(db, activeSucursalId, precioItems),
          setCacheMeta(db, 'lastPreload', new Date().toISOString()),
          setCacheMeta(db, 'lastPreloadSucursal', activeSucursalId),
        ]);

        // Capa 3: poblar store con clientes recientes y órdenes pendientes
        setContext(activeSucursalId, contexto.clientesRecientes, contexto.ordenesPendientes);
      } catch {
        // Falla silenciosa — el POS sigue funcionando online
      }
    })();
  }, [selectedCajaId, activeSucursalId, setContext]);
}
