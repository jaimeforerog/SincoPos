import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Stack,
  CircularProgress,
  Autocomplete,
  MenuItem,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { inventarioApi } from '@/api/inventario';
import { productosApi } from '@/api/productos';
import { tercerosApi } from '@/api/terceros';
import { sucursalesApi } from '@/api/sucursales';
import { useAuth } from '@/hooks/useAuth';
import { useConfiguracionVariableInt } from '@/hooks/useConfiguracionVariable';
import { localDateTimeStr, localDateTimeStrDaysAgo } from '@/utils/dates';
interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function EntradaInventarioDialog({ open, onClose, onSuccess }: Props) {
  const { activeSucursalId, user, activeEmpresaId } = useAuth();
  const { enqueueSnackbar } = useSnackbar();

  const diasMaxEntrada = useConfiguracionVariableInt('DiasMax_EntradaAtrazada');
  const mostrarFechaMovimiento = diasMaxEntrada > 0;
  const nowDatetimeLocal = () => localDateTimeStr();
  const minFechaMovimiento = mostrarFechaMovimiento ? localDateTimeStrDaysAgo(diasMaxEntrada) : '';

  const [productoId, setProductoId] = useState<string>('');
  const [sucursalId, setSucursalId] = useState<number>(activeSucursalId || 0);
  const [cantidad, setCantidad] = useState<string>('');
  const [costoUnitario, setCostoUnitario] = useState<string>('');
  const [porcentajeImpuesto, setPorcentajeImpuesto] = useState<string>('0');
  const [terceroId, setTerceroId] = useState<number | null>(null);
  const [referencia, setReferencia] = useState<string>('');
  const [observaciones, setObservaciones] = useState<string>('');
  const [fechaMovimiento, setFechaMovimiento] = useState<string>(nowDatetimeLocal());

  // Cargar productos
  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
  });
  const productos = productosData?.items || [];

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId) &&
      (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  // Cargar proveedores
  const { data: tercerosData } = useQuery({
    queryKey: ['terceros', 'proveedor'],
    queryFn: () => tercerosApi.getAll({ esProveedor: true, activo: true }),
  });
  const terceros = tercerosData?.items || [];

  const mutation = useMutation({
    mutationFn: inventarioApi.registrarEntrada,
    onSuccess: () => {
      enqueueSnackbar('Entrada de mercancía registrada exitosamente', {
        variant: 'success',
      });
      onSuccess();
      handleClose();
    },
    onError: (error: any) => {
      enqueueSnackbar(
        error.message || 'Error al registrar entrada',
        { variant: 'error' }
      );
    },
  });

  const handleClose = () => {
    setProductoId('');
    setSucursalId(activeSucursalId || 0);
    setCantidad('');
    setCostoUnitario('');
    setPorcentajeImpuesto('0');
    setTerceroId(null);
    setReferencia('');
    setObservaciones('');
    setFechaMovimiento(nowDatetimeLocal());
    onClose();
  };

  const handleSubmit = () => {
    if (!productoId || !sucursalId || !cantidad || !costoUnitario) {
      enqueueSnackbar('Complete los campos obligatorios', { variant: 'warning' });
      return;
    }

    mutation.mutate({
      productoId,
      sucursalId,
      cantidad: parseFloat(cantidad),
      costoUnitario: parseFloat(costoUnitario),
      porcentajeImpuesto: parseFloat(porcentajeImpuesto) / 100, // Convertir a decimal
      terceroId: terceroId || undefined,
      referencia: referencia || undefined,
      observaciones: observaciones || undefined,
      fechaMovimiento: mostrarFechaMovimiento ? new Date(fechaMovimiento).toISOString() : undefined,
    });
  };

  const productoSeleccionado = productos.find((p) => p.id === productoId);

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Registrar Entrada de Mercancía</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Autocomplete
            options={productos}
            getOptionLabel={(option) => `${option.codigoBarras} - ${option.nombre}`}
            value={productoSeleccionado || null}
            onChange={(_, newValue) => setProductoId(newValue?.id || '')}
            renderInput={(params) => (
              <TextField {...params} label="Producto *" placeholder="Buscar producto..." />
            )}
          />

          <TextField
            select
            label="Sucursal *"
            value={sucursalId}
            onChange={(e) => setSucursalId(Number(e.target.value))}
            fullWidth
          >
            {sucursales.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.nombre}
              </MenuItem>
            ))}
          </TextField>

          <TextField
            label="Cantidad *"
            type="number"
            value={cantidad}
            onChange={(e) => setCantidad(e.target.value)}
            fullWidth
            inputProps={{ min: 0, step: 0.01 }}
          />

          <TextField
            label="Costo Unitario *"
            type="number"
            value={costoUnitario}
            onChange={(e) => setCostoUnitario(e.target.value)}
            fullWidth
            inputProps={{ min: 0, step: 0.01 }}
          />

          <TextField
            label="Impuesto (%)"
            type="number"
            value={porcentajeImpuesto}
            onChange={(e) => setPorcentajeImpuesto(e.target.value)}
            fullWidth
            inputProps={{ min: 0, max: 100, step: 0.01 }}
          />

          <Autocomplete
            options={terceros}
            getOptionLabel={(option) => `${option.identificacion} - ${option.nombre}`}
            value={terceros.find((t) => t.id === terceroId) || null}
            onChange={(_, newValue) => setTerceroId(newValue?.id || null)}
            renderInput={(params) => (
              <TextField {...params} label="Proveedor" placeholder="Buscar proveedor..." />
            )}
          />

          <TextField
            label="Referencia"
            value={referencia}
            onChange={(e) => setReferencia(e.target.value)}
            fullWidth
            placeholder="Número de factura, orden, etc."
          />

          {mostrarFechaMovimiento && (
            <TextField
              label="Fecha de Movimiento *"
              type="datetime-local"
              value={fechaMovimiento}
              onChange={(e) => setFechaMovimiento(e.target.value)}
              fullWidth
              slotProps={{
                inputLabel: { shrink: true },
                htmlInput: { min: minFechaMovimiento, max: nowDatetimeLocal() },
              }}
            />
          )}

          <TextField
            label="Observaciones"
            value={observaciones}
            onChange={(e) => setObservaciones(e.target.value)}
            multiline
            rows={2}
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={mutation.isPending}>
          Cancelar
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          color="success"
          disabled={mutation.isPending}
        >
          {mutation.isPending ? <CircularProgress size={24} /> : 'Registrar Entrada'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
