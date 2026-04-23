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
import { useAuth } from '@/hooks/useAuth';
import type { ApiError } from '@/types/api';
interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function AjusteInventarioDialog({ open, onClose, onSuccess }: Props) {
  const { activeSucursalId, user, activeEmpresaId } = useAuth();
  const { enqueueSnackbar } = useSnackbar();

  const [productoId, setProductoId] = useState<string>('');
  const [sucursalId, setSucursalId] = useState<number>(activeSucursalId || 0);
  const [cantidadNueva, setCantidadNueva] = useState<string>('');
  const [observaciones, setObservaciones] = useState<string>('');

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

  // Cargar stock actual del producto seleccionado
  const { data: stockActual } = useQuery({
    queryKey: ['inventario', 'stock', productoId, sucursalId],
    queryFn: () => inventarioApi.getStock({ productoId, sucursalId }),
    enabled: !!productoId && !!sucursalId,
  });

  const mutation = useMutation({
    mutationFn: inventarioApi.ajustarInventario,
    onSuccess: (data: { diferencia: number }) => {
      enqueueSnackbar(
        `Inventario ajustado. Diferencia: ${data.diferencia > 0 ? '+' : ''}${data.diferencia}`,
        { variant: 'success' }
      );
      onSuccess();
      handleClose();
    },
    onError: (error: ApiError) => {
      enqueueSnackbar(
        error.message || 'Error al ajustar inventario',
        { variant: 'error' }
      );
    },
  });

  const handleClose = () => {
    setProductoId('');
    setSucursalId(activeSucursalId || 0);
    setCantidadNueva('');
    setObservaciones('');
    onClose();
  };

  const handleSubmit = () => {
    if (!productoId || !sucursalId || cantidadNueva === '') {
      enqueueSnackbar('Complete los campos obligatorios', { variant: 'warning' });
      return;
    }

    if (!observaciones.trim()) {
      enqueueSnackbar('Ingrese el motivo del ajuste', { variant: 'warning' });
      return;
    }

    mutation.mutate({
      productoId,
      sucursalId,
      cantidadNueva: parseFloat(cantidadNueva),
      observaciones: observaciones.trim(),
    });
  };

  const productoSeleccionado = productos.find((p) => p.id === productoId);
  const stockItem = stockActual?.[0];
  const cantidadActual = stockItem?.cantidad || 0;
  const diferencia = cantidadNueva ? parseFloat(cantidadNueva) - cantidadActual : 0;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Ajustar Inventario (Conteo Físico)</DialogTitle>
      <DialogContent>
        <Alert severity="info" sx={{ mb: 2 }}>
          Use esta función para corregir el inventario según el conteo físico real.
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
                bgcolor: 'primary.50',
                borderRadius: 1,
                border: 1,
                borderColor: 'primary.200',
              }}
            >
              <Typography variant="body2" color="text.secondary">
                Stock Actual en Sistema
              </Typography>
              <Typography variant="h5" fontWeight={600} color="primary.main">
                {cantidadActual.toFixed(2)} unidades
              </Typography>
            </Box>
          )}

          <TextField
            label="Cantidad Nueva (Conteo Físico) *"
            type="number"
            value={cantidadNueva}
            onChange={(e) => setCantidadNueva(e.target.value)}
            fullWidth
            inputProps={{ min: 0, step: 0.01 }}
            helperText="Ingrese la cantidad real según el conteo físico"
          />

          {cantidadNueva && diferencia !== 0 && (
            <Alert severity={diferencia > 0 ? 'success' : 'warning'}>
              <Typography variant="body2">
                <strong>Diferencia:</strong>{' '}
                {diferencia > 0 ? '+' : ''}
                {diferencia.toFixed(2)} unidades
              </Typography>
            </Alert>
          )}

          <TextField
            label="Motivo del Ajuste *"
            value={observaciones}
            onChange={(e) => setObservaciones(e.target.value)}
            multiline
            rows={3}
            fullWidth
            required
            placeholder="Ej: Conteo físico mensual, corrección por merma, etc."
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
          disabled={mutation.isPending}
        >
          {mutation.isPending ? <CircularProgress size={24} /> : 'Ajustar Inventario'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
