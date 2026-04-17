import { useEffect } from 'react';
import { useForm, Controller, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Box,
  Button,
  Paper,
  Typography,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Chip,
  Divider,
  alpha,
} from '@mui/material';
import UndoIcon from '@mui/icons-material/Undo';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import { inventarioApi } from '@/api/inventario';
import type { OrdenCompraDTO } from '@/types/api';
import { formatDateOnly } from '@/utils/format';

const HERO_COLOR = '#c62828';

const lineaSchema = z.object({
  productoId: z.string(),
  cantidadADevolver: z.number().min(0, 'Debe ser mayor o igual a 0'),
});

const devolucionSchema = z.object({
  motivo: z.string().min(5, 'El motivo debe tener al menos 5 caracteres'),
  lineas: z.array(lineaSchema),
});

type DevolucionFormData = z.infer<typeof devolucionSchema>;

interface Props {
  orden: OrdenCompraDTO;
  onCancel: () => void;
  onDone: () => void;
}

const fmt = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

export function OrdenCompraDevolucion({ orden, onCancel, onDone }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  // Cargar devoluciones previas para calcular "ya devuelto" por producto
  const { data: devolucionesAnteriores = [] } = useQuery({
    queryKey: ['compra-devoluciones', orden.id],
    queryFn: () => comprasApi.obtenerDevoluciones(orden.id),
    staleTime: 0,
  });

  // Stock actual en la sucursal de la OC (límite físico real)
  const { data: stockActual = [] } = useQuery({
    queryKey: ['inventario', orden.sucursalId],
    queryFn: () => inventarioApi.getStock({ sucursalId: orden.sucursalId }),
    staleTime: 0,
  });
  const stockMap = stockActual.reduce<Record<string, number>>((acc, s) => {
    acc[s.productoId] = s.cantidad;
    return acc;
  }, {});

  // Mapa: productoId → cantidad ya devuelta en devoluciones previas
  const yaDevueltoMap = devolucionesAnteriores
    .flatMap((d) => d.detalles)
    .reduce<Record<string, number>>((acc, dd) => {
      acc[dd.productoId] = (acc[dd.productoId] ?? 0) + dd.cantidadDevuelta;
      return acc;
    }, {});

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<DevolucionFormData>({
    resolver: zodResolver(devolucionSchema),
    defaultValues: {
      motivo: '',
      lineas: [],
    },
  });

  const { fields } = useFieldArray({ control, name: 'lineas' });

  useEffect(() => {
    reset({
      motivo: '',
      lineas: orden.detalles.map((d) => ({
        productoId: d.productoId,
        cantidadADevolver: 0,
      })),
    });
  }, [orden, reset]);

  const lineas = watch('lineas');

  const mutation = useMutation({
    mutationFn: (data: DevolucionFormData) =>
      comprasApi.crearDevolucion(orden.id, {
        motivo: data.motivo,
        lineas: data.lineas
          .filter((l) => l.cantidadADevolver > 0)
          .map((l) => ({ productoId: l.productoId, cantidad: l.cantidadADevolver })),
      }),
    onSuccess: (devolucion) => {
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      queryClient.invalidateQueries({ queryKey: ['compra', orden.id] });
      queryClient.invalidateQueries({ queryKey: ['compra-devoluciones', orden.id] });
      queryClient.invalidateQueries({ queryKey: ['inventario', orden.sucursalId] });
      enqueueSnackbar(`Devolución ${devolucion.numeroDevolucion} registrada`, { variant: 'success' });
      onDone();
    },
    onError: (error: any) => {
      enqueueSnackbar(error.message || 'Error al registrar la devolución', { variant: 'error' });
    },
  });

  const onSubmit = (data: DevolucionFormData) => {
    const lineasConCantidad = data.lineas.filter((l) => l.cantidadADevolver > 0);
    if (lineasConCantidad.length === 0) {
      enqueueSnackbar('Debe indicar al menos una unidad a devolver', { variant: 'warning' });
      return;
    }
    mutation.mutate(data);
  };

  const totalADevolver = lineas.reduce((sum, linea, idx) => {
    const detalle = orden.detalles[idx];
    return sum + (linea.cantidadADevolver || 0) * (detalle?.precioUnitario ?? 0);
  }, 0);

  return (
    <Paper
      variant="outlined"
      sx={{ borderRadius: 2, overflow: 'hidden', borderColor: alpha(HERO_COLOR, 0.4) }}
    >
      {/* Encabezado */}
      <Box
        sx={{
          px: 2.5,
          py: 1.5,
          display: 'flex',
          alignItems: 'center',
          gap: 1,
          bgcolor: alpha(HERO_COLOR, 0.06),
          borderBottom: '1px solid',
          borderColor: alpha(HERO_COLOR, 0.2),
        }}
      >
        <UndoIcon sx={{ color: HERO_COLOR, fontSize: 20 }} />
        <Typography variant="subtitle1" fontWeight={700} color={HERO_COLOR}>
          Devolución de Mercancía — {orden.numeroOrden}
        </Typography>
      </Box>

      <form onSubmit={handleSubmit(onSubmit)}>
        <Box sx={{ p: 2.5, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Alert severity="info" sx={{ py: 0.5 }}>
            Indique las cantidades a devolver al proveedor. Solo puede devolver hasta la cantidad recibida menos lo ya devuelto.
          </Alert>

          {/* Motivo */}
          <Controller
            name="motivo"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Motivo de la devolución *"
                multiline
                rows={2}
                fullWidth
                size="small"
                error={!!errors.motivo}
                helperText={errors.motivo?.message}
                placeholder="Ej: Mercancía llegó en mal estado, producto incorrecto..."
              />
            )}
          />

          {/* Tabla de líneas */}
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow sx={{ bgcolor: 'grey.50' }}>
                  <TableCell>Producto</TableCell>
                  <TableCell align="center" width={80}>Recibida<br/><Typography variant="caption" color="text.secondary">en esta OC</Typography></TableCell>
                  <TableCell align="center" width={80}>Ya devuelta</TableCell>
                  <TableCell align="center" width={80}>En stock<br/><Typography variant="caption" color="text.secondary">actual</Typography></TableCell>
                  <TableCell align="center" width={90}>Disponible<br/><Typography variant="caption" color="text.secondary">a devolver</Typography></TableCell>
                  <TableCell align="center" width={110}>A devolver</TableCell>
                  <TableCell align="right" width={120}>Subtotal</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {fields.map((field, index) => {
                  const detalle = orden.detalles[index];
                  if (!detalle) return null;
                  const yaDevuelto = yaDevueltoMap[detalle.productoId] ?? 0;
                  const maxPorOC = Math.max(0, detalle.cantidadRecibida - yaDevuelto);
                  const stockDisponible = stockMap[detalle.productoId] ?? 0;
                  // Disponible = mínimo entre lo pendiente de devolver en esta OC y el stock físico actual
                  const disponible = Math.min(maxPorOC, stockDisponible);
                  const cantidadActual = lineas[index]?.cantidadADevolver ?? 0;
                  const subtotalLinea = cantidadActual * detalle.precioUnitario;

                  return (
                    <TableRow
                      key={field.id}
                      sx={{
                        opacity: disponible === 0 ? 0.45 : 1,
                        '&:last-child td': { borderBottom: 0 },
                      }}
                    >
                      <TableCell>
                        <Typography variant="body2" fontWeight={500}>
                          {detalle.nombreProducto}
                        </Typography>
                        {disponible === 0 && maxPorOC > 0 && (
                          <Chip label="Sin stock físico" size="small" color="warning" sx={{ height: 16, fontSize: '0.65rem' }} />
                        )}
                        {disponible === 0 && maxPorOC === 0 && (
                          <Chip label="Ya devuelta" size="small" color="default" sx={{ height: 16, fontSize: '0.65rem' }} />
                        )}
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2">{detalle.cantidadRecibida}</Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2" color={yaDevuelto > 0 ? 'warning.main' : 'text.secondary'}>
                          {yaDevuelto}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2" color={stockDisponible > 0 ? 'text.primary' : 'error.main'}>
                          {stockDisponible}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Typography variant="body2" fontWeight={600} color={disponible > 0 ? 'success.main' : 'text.disabled'}>
                          {disponible}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Controller
                          name={`lineas.${index}.cantidadADevolver`}
                          control={control}
                          render={({ field: { value, onChange, ...f } }) => (
                            <TextField
                              {...f}
                              type="number"
                              value={value}
                              onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                              size="small"
                              disabled={disponible === 0}
                              sx={{ width: 85 }}
                              inputProps={{ min: 0, max: disponible, step: 1 }}
                            />
                          )}
                        />
                      </TableCell>
                      <TableCell align="right">
                        <Typography variant="body2" fontWeight={cantidadActual > 0 ? 600 : 400}>
                          {cantidadActual > 0 ? fmt(subtotalLinea) : '—'}
                        </Typography>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          {/* Resumen */}
          {totalADevolver > 0 && (
            <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
              <Box sx={{ width: 240 }}>
                <Divider sx={{ mb: 1 }} />
                <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                  <Typography variant="body2" fontWeight={700} color={HERO_COLOR}>Total a devolver:</Typography>
                  <Typography variant="body2" fontWeight={700} color={HERO_COLOR}>{fmt(totalADevolver)}</Typography>
                </Box>
              </Box>
            </Box>
          )}

          {/* Historial de devoluciones anteriores */}
          {devolucionesAnteriores.length > 0 && (
            <Box>
              <Typography variant="caption" color="text.secondary" fontWeight={700} sx={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}>
                Devoluciones anteriores
              </Typography>
              {devolucionesAnteriores.map((dev) => (
                <Box
                  key={dev.id}
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    py: 0.5,
                    borderBottom: '1px solid',
                    borderColor: 'divider',
                  }}
                >
                  <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>{dev.numeroDevolucion}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {formatDateOnly(dev.fechaDevolucion)}
                  </Typography>
                  <Typography variant="body2" fontWeight={600}>{fmt(dev.total)}</Typography>
                </Box>
              ))}
            </Box>
          )}

          {/* Acciones */}
          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, pt: 1 }}>
            <Button variant="outlined" onClick={onCancel} disabled={mutation.isPending}>
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="contained"
              color="error"
              startIcon={<UndoIcon />}
              disabled={mutation.isPending || totalADevolver === 0}
            >
              {mutation.isPending ? 'Registrando...' : 'Confirmar Devolución'}
            </Button>
          </Box>
        </Box>
      </form>
    </Paper>
  );
}
