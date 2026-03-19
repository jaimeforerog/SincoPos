import apiClient from './client';
import type { EmpresaDTO, CrearEmpresaDTO, ActualizarEmpresaDTO } from '@/types/api';

export const empresasApi = {
  getAll: () =>
    apiClient.get<EmpresaDTO[]>('/empresas').then(r => r.data),

  getById: (id: number) =>
    apiClient.get<EmpresaDTO>(`/empresas/${id}`).then(r => r.data),

  create: (dto: CrearEmpresaDTO) =>
    apiClient.post<EmpresaDTO>('/empresas', dto).then(r => r.data),

  update: (id: number, dto: ActualizarEmpresaDTO) =>
    apiClient.put<EmpresaDTO>(`/empresas/${id}`, dto).then(r => r.data),
};
