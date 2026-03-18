import type { CajaDTO, SucursalDTO } from '@/types/api';

const LAST_SESSION_KEY = 'pos-last-session';
const CAJAS_CACHE_KEY = 'pos-cajas-cache';
const SUCURSALES_CACHE_KEY = 'pos-sucursales-cache';

export interface PosLastSession {
  cajaId: number;
  cajaNombre: string;
  sucursalId: number;
  sucursalNombre: string;
}

function safeGet<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function safeSet(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // localStorage lleno o no disponible — ignorar
  }
}

export const posSessionCache = {
  saveSession(s: PosLastSession): void {
    safeSet(LAST_SESSION_KEY, s);
  },
  loadSession(): PosLastSession | null {
    return safeGet<PosLastSession | null>(LAST_SESSION_KEY, null);
  },
  saveCajas(cajas: CajaDTO[]): void {
    safeSet(CAJAS_CACHE_KEY, cajas);
  },
  loadCajas(): CajaDTO[] {
    return safeGet<CajaDTO[]>(CAJAS_CACHE_KEY, []);
  },
  saveSucursales(sucursales: SucursalDTO[]): void {
    safeSet(SUCURSALES_CACHE_KEY, sucursales);
  },
  loadSucursales(): SucursalDTO[] {
    return safeGet<SucursalDTO[]>(SUCURSALES_CACHE_KEY, []);
  },
};
