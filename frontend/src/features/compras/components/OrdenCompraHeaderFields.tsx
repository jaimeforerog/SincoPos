import { Controller, type Control, type FieldErrors, type UseFormReset, type UseFormWatch } from 'react-hook-form';
import { Box, TextField, MenuItem } from '@mui/material';
import type { OrdenCompraFormData } from './ordenCompraSchema';
import type { SucursalDTO, TerceroDTO } from '@/types/api';

interface OrdenCompraHeaderFieldsProps {
  control: Control<OrdenCompraFormData>;
  errors: FieldErrors<OrdenCompraFormData>;
  reset: UseFormReset<OrdenCompraFormData>;
  watch: UseFormWatch<OrdenCompraFormData>;
  sucursales: SucursalDTO[];
  proveedores: TerceroDTO[];
  mostrarFechaOrden: boolean;
  minFechaOrden: string;
  minFechaEntrega: string;
  today: string;
}

export function OrdenCompraHeaderFields({
  control,
  errors,
  reset,
  watch,
  sucursales,
  proveedores,
  mostrarFechaOrden,
  minFechaOrden,
  minFechaEntrega,
  today,
}: OrdenCompraHeaderFieldsProps) {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mb: 3 }}>
      <Controller
        name="sucursalId"
        control={control}
        render={({ field: { value, onChange, ...field } }) => (
          <TextField
            {...field}
            select
            label="Sucursal *"
            value={value || ''}
            onChange={(e) => onChange(Number(e.target.value))}
            error={!!errors.sucursalId}
            helperText={errors.sucursalId?.message}
            fullWidth
          >
            <MenuItem value="">Seleccione una sucursal</MenuItem>
            {sucursales.map((suc) => (
              <MenuItem key={suc.id} value={suc.id}>
                {suc.nombre}
              </MenuItem>
            ))}
          </TextField>
        )}
      />

      <Controller
        name="proveedorId"
        control={control}
        render={({ field: { value, onChange, ...field } }) => (
          <TextField
            {...field}
            select
            label="Proveedor *"
            value={value || ''}
            onChange={(e) => onChange(Number(e.target.value))}
            error={!!errors.proveedorId}
            helperText={errors.proveedorId?.message}
            fullWidth
          >
            <MenuItem value="">Seleccione un proveedor</MenuItem>
            {proveedores.map((prov) => (
              <MenuItem key={prov.id} value={prov.id}>
                {prov.nombre} - {prov.identificacion}
              </MenuItem>
            ))}
          </TextField>
        )}
      />

      <Controller
        name="formaPago"
        control={control}
        render={({ field: { onChange, ...field } }) => (
          <TextField
            {...field}
            select
            label="Forma de Pago *"
            onChange={(e) => {
              onChange(e);
              if (e.target.value === 'Contado') {
                reset({ ...watch(), formaPago: 'Contado' as const, diasPlazo: 0 });
              }
            }}
            error={!!errors.formaPago}
            helperText={errors.formaPago?.message}
            fullWidth
          >
            <MenuItem value="Contado">Contado</MenuItem>
            <MenuItem value="Credito">Crédito</MenuItem>
          </TextField>
        )}
      />

      {watch('formaPago') === 'Credito' && (
        <Controller
          name="diasPlazo"
          control={control}
          render={({ field: { value, onChange, ...field } }) => (
            <TextField
              {...field}
              type="number"
              label="Días Plazo"
              value={value}
              onChange={(e) => onChange(Number(e.target.value))}
              error={!!errors.diasPlazo}
              helperText={errors.diasPlazo?.message}
              fullWidth
              inputProps={{ min: 1 }}
            />
          )}
        />
      )}

      {mostrarFechaOrden && (
        <Controller
          name="fechaOrden"
          control={control}
          render={({ field: { value, onChange, ref } }) => (
            <TextField
              inputRef={ref}
              type="date"
              label="Fecha de Orden *"
              value={value ?? ''}
              onChange={(e) => onChange(e.target.value)}
              slotProps={{
                inputLabel: { shrink: true },
                htmlInput: { min: minFechaOrden, max: today },
              }}
              error={!!errors.fechaOrden}
              helperText={errors.fechaOrden?.message}
              fullWidth
            />
          )}
        />
      )}

      <Controller
        name="fechaEntregaEsperada"
        control={control}
        render={({ field: { value, onChange, ref } }) => (
          <TextField
            inputRef={ref}
            type="date"
            label="Fecha de Entrega Esperada"
            value={value ?? ''}
            onChange={(e) => onChange(e.target.value)}
            slotProps={{
              inputLabel: { shrink: true },
              htmlInput: { min: minFechaEntrega },
            }}
            fullWidth
          />
        )}
      />

      <Controller
        name="observaciones"
        control={control}
        render={({ field }) => (
          <TextField
            {...field}
            label="Observaciones"
            multiline
            rows={1}
            fullWidth
          />
        )}
      />
    </Box>
  );
}
