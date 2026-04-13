import { z } from 'zod';
import type { FieldError } from 'react-hook-form';

export const lineaSchema = z.object({
  productoId: z.string().min(1, 'Seleccione un producto'),
  cantidad: z.number().min(0.01, 'Cantidad debe ser mayor a 0'),
  precioUnitario: z.number().min(0, 'Precio debe ser mayor o igual a 0'),
  impuestoId: z.number().optional(),
});

export const ordenCompraSchema = z.object({
  sucursalId: z.number().min(1, 'Seleccione una sucursal'),
  proveedorId: z.number().min(1, 'Seleccione un proveedor'),
  fechaEntregaEsperada: z.string().optional(),
  formaPago: z.enum(['Contado', 'Credito']),
  diasPlazo: z.number().min(0, 'Días de plazo no puede ser negativo'),
  observaciones: z.string().optional(),
  lineas: z.array(lineaSchema).min(1, 'Debe agregar al menos un producto'),
});

export type OrdenCompraFormData = z.infer<typeof ordenCompraSchema>;

export type LineaOrdenError = {
  productoId?: FieldError;
  cantidad?: FieldError;
  precioUnitario?: FieldError;
};
