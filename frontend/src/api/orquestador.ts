import { apiClient } from './client';
import type { VentaDTO, CrearVentaDTO } from '@/types/api';

export interface PipelineStepDto {
  nombre: string;
  ms: number;
  exitoso: boolean;
  error?: string | null;
}

export interface OrchestratorResult {
  venta: VentaDTO | null;
  pipeline: PipelineStepDto[];
  totalMs: number;
  exitoso: boolean;
  error: string | null;
}

export interface PasoMetricaDto {
  nombre: string;
  promedioMs: number;
  maxMs: number;
  tasaExitoPorc: number;
}

export interface PipelineMetricsSummary {
  totalEjecuciones: number;
  exitosas: number;
  fallidas: number;
  tasaExitoPorc: number;
  latenciaPromedioMs: number;
  latenciaMaximaMs: number;
  latenciaMinimaMs: number;
  pasos: PasoMetricaDto[];
}

export interface EjecucionResumenDto {
  timestamp: string;
  totalMs: number;
  exitoso: boolean;
  error: string | null;
  pasos: PipelineStepDto[];
}

export const orquestadorApi = {
  procesarVenta: (dto: CrearVentaDTO) =>
    apiClient.post<OrchestratorResult>('/orquestador/venta', dto).then((r) => r.data),

  getMetricas: () =>
    apiClient.get<PipelineMetricsSummary>('/orquestador/metricas').then((r) => r.data),

  getEjecuciones: (take = 20) =>
    apiClient
      .get<EjecucionResumenDto[]>('/orquestador/ejecuciones', { params: { take } })
      .then((r) => r.data),
};
