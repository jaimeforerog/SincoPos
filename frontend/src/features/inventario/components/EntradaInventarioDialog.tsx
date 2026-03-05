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
import { sucursalesApi } from '@/api/sucursales';
import { tercerosApi } from '@/api/terceros';
import { useAuth } from '@/hooks/useAuth';
interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function EntradaInventarioDialog({ open, onClose, onSuccess }: Props) {
  const { activeSucursalId } = useAuth();
  const { enqueueSnackbar } = useSnackbar();

  const [productoId, setProductoId] = useState<string>('');
  const [sucursalId, setSucursalId] = useState<number>(activeSucursalId || 0);
  const [cantidad, setCantidad] = useState<string>('');
  const [costoUnitario, setCostoUnitario] = useState<string>('');
  const [porcentajeImpuesto, setPorcentajeImpuesto] = useState<string>('0');
  const [terceroId, setTerceroId] = useState<number | null>(null);
  const [referencia, setReferencia] = useState<string>('');
  const [observaciones, setObservaciones] = useState<string>('');

  // Cargar productos
  const { data: productos = [] } = useQuery({
    queryKey: ['productos'],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
  });

  // Cargar sucursales
  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  // Cargar proveedores
  const { data: terceros = [] } = useQuery({
    queryKey: ['terceros', 'proveedor'],
    queryFn: () => tercerosApi.getAll({ esProveedor: true, activo: true }),
  });

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
        error.response?.data?.error || 'Error al registrar entrada',
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
