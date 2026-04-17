import { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Box,
  Typography,
  Autocomplete,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
} from '@mui/material';
import { Add as AddIcon, Delete as DeleteIcon } from '@mui/icons-material';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { trasladosApi } from '@/api/traslados';
import { sucursalesApi } from '@/api/sucursales';
import { useAuth } from '@/hooks/useAuth';
import { productosApi } from '@/api/productos';
import { inventarioApi } from '@/api/inventario';
import type { ProductoDTO, SucursalDTO, CrearTrasladoDTO, LineaTrasladoDTO } from '@/types/api';

interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

interface LineaLocal extends LineaTrasladoDTO {
  producto: ProductoDTO;
  stockDisponible: number;
}

export function CrearTrasladoDialog({ open, onClose, onSuccess }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const { user, activeEmpresaId } = useAuth();
  const [sucursalOrigen, setSucursalOrigen] = useState<SucursalDTO | null>(null);
  const [sucursalDestino, setSucursalDestino] = useState<SucursalDTO | null>(null);
  const [observaciones, setObservaciones] = useState('');
  const [lineas, setLineas] = useState<LineaLocal[]>([]);
  const [productoSeleccionado, setProductoSeleccionado] = useState<ProductoDTO | null>(null);
  const [cantidad, setCantidad] = useState(1);

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(),
    enabled: open,
    staleTime: 5 * 60 * 1000,
  });
  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId) &&
      (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  const { data: inventario } = useQuery({
    queryKey: ['inventario', sucursalOrigen?.id],
    queryFn: () => inventarioApi.obtenerStock({ sucursalId: sucursalOrigen!.id }),
    enabled: !!sucursalOrigen,
  });

  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.listar(),
    enabled: open,
  });

  // Solo mostrar productos que tienen stock > 0 en la sucursal origen al momento del traslado
  const productosConStock = (productosData?.items || []).filter((p) =>
    (inventario ?? []).some((s) => s.productoId === p.id && s.cantidad > 0)
  );

  const crearMutation = useMutation({
    mutationFn: trasladosApi.crear,
    onSuccess: () => {
      enqueueSnackbar('Traslado creado exitosamente', { variant: 'success' });
      handleReset();
      onSuccess();
    },
    onError: () => {
      enqueueSnackbar('Error al crear el traslado', { variant: 'error' });
    },
  });

  const handleAgregarLinea = () => {
    if (!productoSeleccionado || !sucursalOrigen) return;

    const stock = inventario?.find((s) => s.productoId === productoSeleccionado.id);
    const stockDisponible = stock?.cantidad ?? 0;

    if (cantidad > stockDisponible) {
      enqueueSnackbar(`Stock insuficiente. Disponible: ${stockDisponible}`, {
        variant: 'warning',
      });
      return;
    }

    const lineaExistente = lineas.find((l) => l.productoId === productoSeleccionado.id);
    if (lineaExistente) {
      enqueueSnackbar('El producto ya está en la lista', { variant: 'warning' });
      return;
    }

    setLineas([
      ...lineas,
      {
        productoId: productoSeleccionado.id,
        cantidad,
        producto: productoSeleccionado,
        stockDisponible,
      },
    ]);
    setProductoSeleccionado(null);
    setCantidad(1);
  };

  const handleEliminarLinea = (productoId: string) => {
    setLineas(lineas.filter((l) => l.productoId !== productoId));
  };

  const handleCrear = () => {
    if (!sucursalOrigen || !sucursalDestino) {
      enqueueSnackbar('Selecciona sucursal origen y destino', { variant: 'warning' });
      return;
    }

    if (lineas.length === 0) {
      enqueueSnackbar('Agrega al menos un producto', { variant: 'warning' });
      return;
    }

    const dto: CrearTrasladoDTO = {
      sucursalOrigenId: sucursalOrigen.id,
      sucursalDestinoId: sucursalDestino.id,
      observaciones,
      lineas: lineas.map((l) => ({
        productoId: l.productoId,
        cantidad: l.cantidad,
      })),
    };

    crearMutation.mutate(dto);
  };

  const handleReset = () => {
    setSucursalOrigen(null);
    setSucursalDestino(null);
    setObservaciones('');
    setLineas([]);
    setProductoSeleccionado(null);
    setCantidad(1);
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleReset} maxWidth="md" fullWidth>
      <DialogTitle>Nuevo Traslado</DialogTitle>
      <DialogContent>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mt: 2 }}>
          <Autocomplete
            options={sucursales || []}
            getOptionLabel={(option) => option.nombre}
            value={sucursalOrigen}
            onChange={(_, value) => setSucursalOrigen(value)}
            renderInput={(params) => (
              <TextField {...params} label="Sucursal Origen" required />
            )}
          />
          <Autocomplete
            options={sucursales?.filter((s) => s.id !== sucursalOrigen?.id) || []}
            getOptionLabel={(option) => option.nombre}
            value={sucursalDestino}
            onChange={(_, value) => setSucursalDestino(value)}
            renderInput={(params) => (
              <TextField {...params} label="Sucursal Destino" required />
            )}
          />
        </Box>

        <TextField
          fullWidth
          label="Observaciones"
          multiline
          rows={2}
          value={observaciones}
          onChange={(e) => setObservaciones(e.target.value)}
          sx={{ mt: 2 }}
        />

        <Typography variant="subtitle1" sx={{ mt: 3, mb: 2, fontWeight: 600 }}>
          Productos
        </Typography>

        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr auto auto', gap: 2, mb: 2 }}>
          <Autocomplete
            options={productosConStock}
            noOptionsText={!sucursalOrigen ? 'Selecciona la sucursal origen primero' : 'No hay productos con stock en esta sucursal'}
            getOptionLabel={(option) => `${option.nombre} (${option.codigoBarras})`}
            value={productoSeleccionado}
            onChange={(_, value) => {
              setProductoSeleccionado(value);
              // Resetear cantidad al seleccionar un nuevo producto
              if (value) {
                const stock = inventario?.find((s) => s.productoId === value.id);
                const stockDisponible = stock?.cantidad ?? 0;
                // Si la cantidad actual excede el stock, ajustarla
                if (cantidad > stockDisponible) {
                  setCantidad(Math.min(1, stockDisponible));
                }
              }
            }}
            disabled={!sucursalOrigen}
            renderOption={(props, option) => {
              const { key, ...otherProps } = props;
              const stock = inventario?.find((s) => s.productoId === option.id);
              const stockDisponible = stock?.cantidad ?? 0;
              return (
                <Box component="li" key={key} {...otherProps}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}>
                    <Typography variant="body2">
                      {option.nombre} ({option.codigoBarras})
                    </Typography>
                    <Typography
                      variant="body2"
                      sx={{
                        ml: 2,
                        fontWeight: 600,
                        color: stockDisponible > 0 ? 'success.main' : 'error.main',
                      }}
                    >
                      Stock: {stockDisponible}
                    </Typography>
                  </Box>
                </Box>
              );
            }}
            renderInput={(params) => {
              const stock = productoSeleccionado
                ? inventario?.find((s) => s.productoId === productoSeleccionado.id)
                : null;
              const stockDisponible = stock?.cantidad ?? 0;

              return (
                <TextField
                  {...params}
                  label="Producto"
                  helperText={
                    productoSeleccionado
                      ? `Stock disponible: ${stockDisponible} unidades`
                      : sucursalOrigen
                      ? 'Selecciona un producto'
                      : 'Primero selecciona sucursal origen'
                  }
                  FormHelperTextProps={{
                    sx: {
                      color: productoSeleccionado && stockDisponible > 0 ? 'success.main' : 'text.secondary',
                      fontWeight: productoSeleccionado ? 600 : 400,
                    },
                  }}
                />
              );
            }}
          />
          <TextField
            type="number"
            label={
              productoSeleccionado
                ? `Cantidad (máx: ${inventario?.find((s) => s.productoId === productoSeleccionado.id)?.cantidad ?? 0})`
                : 'Cantidad'
            }
            value={cantidad}
            onChange={(e) => {
              const valor = Number(e.target.value);
              const stockMax = productoSeleccionado
                ? (inventario?.find((s) => s.productoId === productoSeleccionado.id)?.cantidad ?? 0)
                : Infinity;
              // Limitar automáticamente al máximo disponible
              setCantidad(Math.min(valor, stockMax));
            }}
            inputProps={{
              min: 1,
              max: productoSeleccionado
                ? (inventario?.find((s) => s.productoId === productoSeleccionado.id)?.cantidad ?? 0)
                : undefined
            }}
            error={
              !!productoSeleccionado &&
              cantidad > (inventario?.find((s) => s.productoId === productoSeleccionado.id)?.cantidad ?? 0)
            }
            helperText={
              productoSeleccionado &&
              cantidad > (inventario?.find((s) => s.productoId === productoSeleccionado.id)?.cantidad ?? 0)
                ? 'Cantidad mayor al stock'
                : ''
            }
            sx={{ width: 160 }}
          />
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleAgregarLinea}
            disabled={
              !productoSeleccionado ||
              cantidad <= 0 ||
              cantidad > (inventario?.find((s) => s.productoId === productoSeleccionado.id)?.cantidad ?? 0)
            }
          >
            Agregar
          </Button>
        </Box>

        {lineas.length > 0 && (
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Producto</TableCell>
                  <TableCell align="center">Cantidad</TableCell>
                  <TableCell align="center">Stock</TableCell>
                  <TableCell align="right">Acción</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {lineas.map((linea) => (
                  <TableRow key={linea.productoId}>
                    <TableCell>{linea.producto.nombre}</TableCell>
                    <TableCell align="center">{linea.cantidad}</TableCell>
                    <TableCell align="center">{linea.stockDisponible}</TableCell>
                    <TableCell align="right">
                      <IconButton
                        size="small"
                        onClick={() => handleEliminarLinea(linea.productoId)}
                        color="error"
                      >
                        <DeleteIcon />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={handleReset}>Cancelar</Button>
        <Button
          variant="contained"
          onClick={handleCrear}
          disabled={crearMutation.isPending}
        >
          Crear Traslado
        </Button>
      </DialogActions>
    </Dialog>
  );
}
