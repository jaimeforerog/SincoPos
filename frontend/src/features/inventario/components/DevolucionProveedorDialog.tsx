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
  Alert,
  Typography,
  Box,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { inventarioApi } from '@/api/inventario';
import { productosApi } from '@/api/productos';
import { sucursalesApi } from '@/api/sucursales';
import { tercerosApi } from '@/api/terceros';
import { useAuth } from '@/hooks/useAuth';
import type { ApiError } from '@/types/api';
interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function DevolucionProveedorDialog({ open, onClose, onSuccess }: Props) {
  const { activeSucursalId, activeEmpresaId } = useAuth();
  const { enqueueSnackbar } = useSnackbar();

  const [productoId, setProductoId] = useState<string>('');
  const [sucursalId, setSucursalId] = useState<number>(activeSucursalId || 0);
  const [cantidad, setCantidad] = useState<string>('');
  const [terceroId, setTerceroId] = useState<number | null>(null);
  const [referencia, setReferencia] = useState<string>('');
  const [observaciones, setObservaciones] = useState<string>('');

  // Cargar productos
  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
  });
  const productos = productosData?.items || [];

  // Cargar sucursales
  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  // Cargar proveedores
  const { data: tercerosData } = useQuery({
    queryKey: ['terceros', 'proveedor'],
    queryFn: () => tercerosApi.getAll({ esProveedor: true, activo: true }),
  });
  const terceros = tercerosData?.items || [];

  // Cargar stock actual
  const { data: stockActual } = useQuery({
    queryKey: ['inventario', 'stock', productoId, sucursalId],
    queryFn: () => inventarioApi.getStock({ productoId, sucursalId }),
    enabled: !!productoId && !!sucursalId,
  });

  const mutation = useMutation({
    mutationFn: inventarioApi.devolucionProveedor,
    onSuccess: () => {
      enqueueSnackbar('Devolución a proveedor registrada exitosamente', {
        variant: 'success',
      });
      onSuccess();
      handleClose();
    },
    onError: (error: ApiError) => {
      enqueueSnackbar(
        error.message || 'Error al registrar devolución',
        { variant: 'error' }
      );
    },
  });

  const handleClose = () => {
    setProductoId('');
    setSucursalId(activeSucursalId || 0);
    setCantidad('');
    setTerceroId(null);
    setReferencia('');
    setObservaciones('');
    onClose();
  };

  const handleSubmit = () => {
    if (!productoId || !sucursalId || !cantidad || !terceroId) {
      enqueueSnackbar('Complete los campos obligatorios', { variant: 'warning' });
      return;
    }

    const cantidadNum = parseFloat(cantidad);
    const stockDisponible = stockActual?.[0]?.cantidad || 0;

    if (cantidadNum > stockDisponible) {
      enqueueSnackbar(
        `No hay suficiente stock. Disponible: ${stockDisponible}`,
        { variant: 'error' }
      );
      return;
    }

    mutation.mutate({
      productoId,
      sucursalId,
      cantidad: cantidadNum,
      terceroId,
      referencia: referencia || undefined,
      observaciones: observaciones || undefined,
    });
  };

  const productoSeleccionado = productos.find((p) => p.id === productoId);
  const stockItem = stockActual?.[0];
  const cantidadDisponible = stockItem?.cantidad || 0;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Devolución a Proveedor</DialogTitle>
      <DialogContent>
        <Alert severity="warning" sx={{ mb: 2 }}>
          Esta acción reducirá el inventario y registrará la devolución al proveedor.
        </Alert>

        <Stack spacing={2}>
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

          {productoId && sucursalId && (
            <Box
              sx={{
                p: 2,
                bgcolor: cantidadDisponible > 0 ? 'success.50' : 'error.50',
                borderRadius: 1,
                border: 1,
                borderColor: cantidadDisponible > 0 ? 'success.200' : 'error.200',
              }}
            >
              <Typography variant="body2" color="text.secondary">
                Stock Disponible
              </Typography>
              <Typography
                variant="h5"
                fontWeight={600}
                color={cantidadDisponible > 0 ? 'success.main' : 'error.main'}
              >
                {cantidadDisponible.toFixed(2)} unidades
              </Typography>
            </Box>
          )}

          <TextField
            label="Cantidad a Devolver *"
            type="number"
            value={cantidad}
            onChange={(e) => setCantidad(e.target.value)}
            fullWidth
            inputProps={{ min: 0, max: cantidadDisponible, step: 0.01 }}
            disabled={!productoId || cantidadDisponible === 0}
          />

          <Autocomplete
            options={terceros}
            getOptionLabel={(option) => `${option.identificacion} - ${option.nombre}`}
            value={terceros.find((t) => t.id === terceroId) || null}
            onChange={(_, newValue) => setTerceroId(newValue?.id || null)}
            renderInput={(params) => (
              <TextField {...params} label="Proveedor *" placeholder="Buscar proveedor..." />
            )}
          />

          <TextField
            label="Referencia"
            value={referencia}
            onChange={(e) => setReferencia(e.target.value)}
            fullWidth
            placeholder="Número de nota de crédito, etc."
          />

          <TextField
            label="Observaciones"
            value={observaciones}
            onChange={(e) => setObservaciones(e.target.value)}
            multiline
            rows={2}
            fullWidth
            placeholder="Motivo de la devolución"
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
          color="error"
          disabled={mutation.isPending}
        >
          {mutation.isPending ? <CircularProgress size={24} /> : 'Registrar Devolución'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
