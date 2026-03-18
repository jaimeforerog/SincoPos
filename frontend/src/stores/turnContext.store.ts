import { create } from 'zustand';
import type { ClienteRecienteDTO, OrdenPendienteResumenDTO } from '@/types/api';

/**
 * Capa 3 — Repetición cero.
 * Almacena el contexto precargado del turno POS.
 * Poblado por useTurnPreload al seleccionar caja.
 * Accesible desde CartHeader (clientes recientes) y
 * futuros componentes de órdenes pendientes.
 */
interface TurnContextState {
  clientesRecientes: ClienteRecienteDTO[];
  ordenesPendientes: OrdenPendienteResumenDTO[];
  sucursalId: number | null;
  setContext: (sucursalId: number, clientesRecientes: ClienteRecienteDTO[], ordenesPendientes: OrdenPendienteResumenDTO[]) => void;
  clear: () => void;
}

export const useTurnContextStore = create<TurnContextState>((set) => ({
  clientesRecientes: [],
  ordenesPendientes: [],
  sucursalId: null,

  setContext: (sucursalId, clientesRecientes, ordenesPendientes) =>
    set({ sucursalId, clientesRecientes, ordenesPendientes }),

  clear: () =>
    set({ clientesRecientes: [], ordenesPendientes: [], sucursalId: null }),
}));
