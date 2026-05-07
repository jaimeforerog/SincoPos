import { z } from 'zod';

export const UNIDADES_MEDIDA = [
  { codigo: '94',  label: 'Unidad (94)' },
  { codigo: 'NIU', label: 'Artículo (NIU)' },
  { codigo: 'KGM', label: 'Kilogramo (KGM)' },
  { codigo: 'GRM', label: 'Gramo (GRM)' },
  { codigo: 'LTR', label: 'Litro (LTR)' },
  { codigo: 'MLT', label: 'Mililitro (MLT)' },
  { codigo: 'MTR', label: 'Metro (MTR)' },
  { codigo: 'CMT', label: 'Centímetro (CMT)' },
  { codigo: 'GLL', label: 'Galón (GLL)' },
  { codigo: 'BX',  label: 'Caja (BX)' },
];

export const crearProductoSchema = z.object({
  codigoBarras: z.string().min(1, 'Código de barras es requerido').max(50, 'Máximo 50 caracteres'),
  nombre: z.string().min(1, 'Nombre es requerido').max(200, 'Máximo 200 caracteres'),
  descripcion: z.string().max(500, 'Máximo 500 caracteres').optional(),
  categoriaId: z.number().min(1, 'Categoría es requerida'),
  precioCosto: z.number({ message: 'Ingrese un precio válido' }).min(0, 'Debe ser mayor o igual a 0'),
  unidadMedida: z.string().min(1, 'Unidad de medida es requerida'),
});

export const actualizarProductoSchema = z.object({
  nombre: z.string().min(1, 'Nombre es requerido').max(200, 'Máximo 200 caracteres'),
  descripcion: z.string().max(500, 'Máximo 500 caracteres').optional(),
  precioCosto: z.number({ message: 'Ingrese un precio válido' }).min(0, 'Debe ser mayor o igual a 0'),
  unidadMedida: z.string().min(1, 'Unidad de medida es requerida'),
});

export type CrearProductoFormData = z.infer<typeof crearProductoSchema>;
export type ActualizarProductoFormData = z.infer<typeof actualizarProductoSchema>;
