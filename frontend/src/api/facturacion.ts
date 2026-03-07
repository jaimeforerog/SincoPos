import { AxiosError } from 'axios';
import apiClient from './client';
import type {
  ConfiguracionEmisorDTO,
  ActualizarConfiguracionEmisorDTO,
  DocumentoElectronicoDTO,
  DianRespuestaDTO,
} from '@/types/api';

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface FiltroDocumentosParams {
  sucursalId?: number;
  fechaDesde?: string;
  fechaHasta?: string;
  tipoDocumento?: string;
  estado?: number;
  pageNumber?: number;
  pageSize?: number;
}

export const facturacionApi = {
  // ─── Configuración Emisor ───────────────────────────────────────────────

  getConfiguracion: async (sucursalId: number): Promise<ConfiguracionEmisorDTO | null> => {
    try {
      const response = await apiClient.get<ConfiguracionEmisorDTO>(
        `/facturacion/configuracion/${sucursalId}`
      );
      return response.data;
    } catch (err: unknown) {
      if (err instanceof AxiosError && err.response?.status === 404) return null;
      throw err;
    }
  },

  actualizarConfiguracion: async (
    sucursalId: number,
    data: ActualizarConfiguracionEmisorDTO
  ): Promise<void> => {
    await apiClient.put(`/facturacion/configuracion/${sucursalId}`, data);
  },

  // ─── Documentos Electrónicos ────────────────────────────────────────────

  listarDocumentos: async (
    params: FiltroDocumentosParams
  ): Promise<PaginatedResult<DocumentoElectronicoDTO>> => {
    const response = await apiClient.get<PaginatedResult<DocumentoElectronicoDTO>>(
      '/facturacion/documentos',
      { params }
    );
    return response.data;
  },

  getDocumento: async (id: number): Promise<DocumentoElectronicoDTO> => {
    const response = await apiClient.get<DocumentoElectronicoDTO>(
      `/facturacion/documentos/${id}`
    );
    return response.data;
  },

  descargarXml: (id: number): string => {
    return `${apiClient.defaults.baseURL ?? ''}/api/facturacion/documentos/${id}/xml`;
  },

  reintentar: async (id: number): Promise<DocumentoElectronicoDTO> => {
    const response = await apiClient.post<DocumentoElectronicoDTO>(
      `/facturacion/documentos/${id}/reintentar`
    );
    return response.data;
  },

  consultarEstadoDian: async (id: number): Promise<DianRespuestaDTO> => {
    const response = await apiClient.get<DianRespuestaDTO>(
      `/facturacion/documentos/${id}/estado-dian`
    );
    return response.data;
  },

  emitirFacturaManual: async (ventaId: number): Promise<DocumentoElectronicoDTO> => {
    const response = await apiClient.post<DocumentoElectronicoDTO>(
      `/facturacion/documentos/emitir-venta/${ventaId}`
    );
    return response.data;
  },
};
