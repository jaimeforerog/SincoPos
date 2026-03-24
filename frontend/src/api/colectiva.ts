import { apiClient } from './client';

export interface ComboProductoDto {
  productoAId: string;
  productoANombre: string;
  productoBId: string;
  productoBNombre: string;
  vecesJuntos: number;
  frecuencia: number;
}

export interface ProductoVelocidadComparativoDto {
  productoId: string;
  nombreProducto: string;
  velocidadPorSucursal: Record<string, number>;
}

export interface PatronComparativoDto {
  sucursales: string[];
  items: ProductoVelocidadComparativoDto[];
}

export interface EstadoGlobalDto {
  servicioCentralDisponible: boolean;
  mensaje: string;
  ultimaActualizacionGlobal: string | null;
}

export const colectivaApi = {
  getCombos: (sucursalId: number, top = 15) =>
    apiClient
      .get<ComboProductoDto[]>(`/colectiva/combos/${sucursalId}`, { params: { top } })
      .then((r) => r.data),

  compararSucursales: (empresaId: number) =>
    apiClient
      .get<PatronComparativoDto>(`/colectiva/comparar/${empresaId}`)
      .then((r) => r.data),

  getEstadoGlobal: () =>
    apiClient.get<EstadoGlobalDto>('/colectiva/estado-global').then((r) => r.data),
};
