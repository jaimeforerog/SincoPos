import { useState, useEffect } from 'react';
import { useForm, Controller, useFieldArray, type FieldError } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  MenuItem,
  Box,
  Typography,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Autocomplete,
  Alert,
  Divider,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import { sucursalesApi } from '@/api/sucursales';
import { tercerosApi } from '@/api/terceros';
import { productosApi } from '@/api/productos';
import { impuestosApi } from '@/api/impuestos';
import type { CrearOrdenCompraDTO } from '@/types/api';

type LineaOrdenError = { productoId?: FieldError; cantidad?: FieldError; precioUnitario?: FieldError };

const lineaSchema = z.object({
  productoId: z.string().min(1, 'Seleccione un producto'),
  cantidad: z.number().min(0.01, 'Cantidad debe ser mayor a 0'),
  precioUnitario: z.number().min(0, 'Precio debe ser mayor o igual a 0'),
  impuestoId: z.number().optional(),
});

const ordenCompraSchema = z.object({
  sucursalId: z.number().min(1, 'Seleccione una sucursal'),
  proveedorId: z.number().min(1, 'Seleccione un proveedor'),
  fechaEntregaEsperada: z.string().optional(),
  formaPago: z.enum(['Contado', 'Credito']),
  diasPlazo: z.number().min(0, 'Días de plazo no puede ser negativo'),
  observaciones: z.string().optional(),
  lineas: z.array(lineaSchema).min(1, 'Debe agregar al menos un producto'),
});

type OrdenCompraFormData = z.infer<typeof ordenCompraSchema>;

interface OrdenCompraFormDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function OrdenCompraFormDialog({
  open,
  onClose,
  onSuccess,
}: OrdenCompraFormDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [backendError, setBackendError] = useState<string | null>(null);

  // Cargar catálogos
  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(false),
    enabled: open,
  });

  const { data: tercerosData } = useQuery({
    queryKey: ['terceros'],
    queryFn: () => tercerosApi.getAll(),
    enabled: open,
    staleTime: 10 * 60 * 1000,
  });

  const terceros = tercerosData?.items || [];

  // Proveedores: tipo 'Proveedor' o 'Ambos'
  const proveedores = terceros.filter(
    (t) => t.tipoTercero === 'Proveedor' || t.tipoTercero === 'Ambos'
  );

  const { data: productosData } = useQuery({
    queryKey: ['productos'],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
    enabled: open,
  });

  const productos = productosData?.items || [];

  const { data: impuestos = [] } = useQuery({
    queryKey: ['impuestos'],
    queryFn: () => impuestosApi.getAll(),
    enabled: open,
  });

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<OrdenCompraFormData>({
    resolver: zodResolver(ordenCompraSchema),
    defaultValues: {
      sucursalId: 0,
      proveedorId: 0,
      fechaEntregaEsperada: new Date().toISOString().split('T')[0],
      formaPago: 'Contado',
      diasPlazo: 0,
      observaciones: '',
      lineas: [],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control,
    name: 'lineas',
  });

  // Observar líneas para calcular totales
  const lineas = watch('lineas');

  const calcularTotales = () => {
    let subtotal = 0;
    let impuestosTotal = 0;

    lineas.forEach((linea) => {
      const subtotalLinea = linea.cantidad * linea.precioUnitario;
      // Preview client-side: buscar porcentaje del impuesto seleccionado
      const impuestoSeleccionado = linea.impuestoId
        ? impuestos.find((imp) => imp.id === linea.impuestoId)
        : null;
      const montoImpuesto = subtotalLinea * (impuestoSeleccionado?.porcentaje ?? 0);

      subtotal += subtotalLinea;
      impuestosTotal += montoImpuesto;
    });

    return {
      subtotal,
      impuestos: impuestosTotal,
      total: subtotal + impuestosTotal,
    };
  };

  const totales = calcularTotales();

  const mutation = useMutation({
    mutationFn: (data: CrearOrdenCompraDTO) => comprasApi.create(data),
    onSuccess: () => {
      setBackendError(null);
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar('Orden de compra creada exitosamente', { variant: 'success' });
      onSuccess();
      handleClose();
    },
    onError: (error: any) => {
      let mensaje = 'Error al crear la orden de compra';

      if (error.response) {
        // El servidor respondió con un código de error
        const { status, data } = error.response;

        if (status === 400) {
          // Error de validación
          if (data.errors) {
            // Errores de FluentValidation campo por campo
            const errores = Object.entries(data.errors)
              .map(([campo, mensajes]: [string, any]) => `${campo}: ${Array.isArray(mensajes) ? mensajes.join(', ') : mensajes}`)
              .join('\n');
            mensaje = `Errores de validación:\n${errores}`;
          } else if (data.error) {
            mensaje = data.error;
          } else if (data.message) {
            mensaje = data.message;
          } else {
            mensaje = 'Los datos proporcionados no son válidos. Revisa el formulario.';
          }
        } else if (status === 401) {
          mensaje = 'No estás autenticado. Por favor inicia sesión nuevamente.';
        } else if (status === 403) {
          mensaje = 'No tienes permisos suficientes. Se requiere rol Supervisor para crear órdenes de compra.';
        } else if (status === 404) {
          mensaje = data.error || 'Recurso no encontrado. Verifica que la sucursal y el proveedor existan.';
        } else if (status === 500) {
          mensaje = `Error del servidor: ${data.error || data.message || 'Error interno. Contacta al administrador.'}`;
        } else {
          mensaje = data.error || data.message || `Error HTTP ${status}`;
        }
      } else if (error.request) {
        // La petición se hizo pero no hubo respuesta
        mensaje = 'No se pudo conectar con el servidor. Verifica tu conexión a internet y que el backend esté corriendo.';
      } else {
        // Error al configurar la petición
        mensaje = `Error inesperado: ${error.message || JSON.stringify(error)}`;
        console.error('Error completo:', error);
      }

      setBackendError(mensaje);
      enqueueSnackbar(mensaje.split('\n')[0], { variant: 'error' }); // Solo la primera línea en snackbar
    },
  });

  const onSubmit = (data: OrdenCompraFormData) => {
    console.log('📤 Datos a enviar al backend:', JSON.stringify(data, null, 2));
    mutation.mutate(data);
  };

  const valoresLimpios: OrdenCompraFormData = {
    sucursalId: 0,
    proveedorId: 0,
    fechaEntregaEsperada: new Date().toISOString().split('T')[0],
    formaPago: 'Contado',
    diasPlazo: 0,
    observaciones: '',
    lineas: [],
  };

  const handleClose = () => {
    reset(valoresLimpios);
    setBackendError(null);
    onClose();
  };

  const agregarLinea = () => {
    append({
      productoId: '',
      cantidad: 1,
      precioUnitario: 0,
      impuestoId: undefined,
    });
  };

  useEffect(() => {
    if (open) {
      reset(valoresLimpios);
      setBackendError(null);
    }
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="lg" fullWidth>
      <DialogTitle>Nueva Orden de Compra</DialogTitle>

      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          {backendError && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setBackendError(null)}>
              {backendError}
            </Alert>
          )}

          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mb: 3 }}>
            {/* Sucursal */}
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

            {/* Proveedor */}
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

            {/* Forma de Pago */}
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
                    // Si cambia a contado, reiniciar días
                    if (e.target.value === 'Contado') {
                      control._defaultValues.diasPlazo = 0; // Para no romper reset
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

            {/* Días Plazo */}
            <Controller
              name="diasPlazo"
              control={control}
              render={({ field: { value, onChange, ...field } }) => {
                return (
                  <TextField
                    {...field}
                    type="number"
                    label="Días Plazo"
                    value={value}
                    onChange={(e) => {
                      const newDias = Number(e.target.value);
                      onChange(newDias);
                      // Si ponen días > 0, cambiar automáticamente a Crédito
                      if (newDias > 0 && watch('formaPago') === 'Contado') {
                        reset({ ...watch(), diasPlazo: newDias, formaPago: 'Credito' as const });
                      }
                    }}
                    error={!!errors.diasPlazo}
                    helperText={errors.diasPlazo?.message}
                    fullWidth
                    inputProps={{ min: 0 }}
                  />
                );
              }}
            />

            {/* Fecha de entrega esperada */}
            <Controller
              name="fechaEntregaEsperada"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  type="date"
                  label="Fecha de Entrega Esperada"
                  InputLabelProps={{ shrink: true }}
                  fullWidth
                />
              )}
            />

            {/* Observaciones */}
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

          <Divider sx={{ my: 2 }} />

          {/* Productos */}
          <Box sx={{ mb: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="h6">Productos</Typography>
            <Button
              variant="outlined"
              startIcon={<AddIcon />}
              onClick={agregarLinea}
              size="small"
            >
              Agregar Producto
            </Button>
          </Box>

          {errors.lineas && typeof errors.lineas.message === 'string' && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {errors.lineas.message}
            </Alert>
          )}

          <TableContainer component={Paper} variant="outlined" sx={{ mb: 3 }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell width="30%">Producto</TableCell>
                  <TableCell width="12%">Cantidad</TableCell>
                  <TableCell width="18%">Precio Unit.</TableCell>
                  <TableCell width="22%">Impuesto</TableCell>
                  <TableCell width="13%" align="right">Subtotal</TableCell>
                  <TableCell width="5%"></TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {fields.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} align="center">
                      <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
                        No hay productos agregados. Haga clic en "Agregar Producto"
                      </Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  fields.map((field, index) => {
                    const linea = lineas[index];
                    const subtotalLinea = linea ? linea.cantidad * linea.precioUnitario : 0;

                    return (
                      <TableRow key={field.id}>
                        <TableCell>
                          <Controller
                            name={`lineas.${index}.productoId`}
                            control={control}
                            render={({ field: { value, onChange } }) => (
                              <Autocomplete
                                value={productos.find((p) => p.id === value) || null}
                                onChange={(_, newValue) => {
                                  if (newValue) {
                                    // Verificar si el producto ya existe en otra línea
                                    const currentLineas = watch('lineas');
                                    const lineaDuplicada = currentLineas.findIndex(
                                      (l, i) => i !== index && l.productoId === newValue.id
                                    );
                                    if (lineaDuplicada !== -1) {
                                      enqueueSnackbar(
                                        `"${newValue.nombre}" ya está en la línea ${lineaDuplicada + 1}. Modifica la cantidad en esa línea.`,
                                        { variant: 'warning' }
                                      );
                                      return; // No permite agregar duplicado
                                    }
                                    onChange(newValue.id);
                                    currentLineas[index].precioUnitario = newValue.precioCosto;
                                    currentLineas[index].impuestoId = newValue.impuestoId;
                                    reset({ ...watch(), lineas: currentLineas });
                                  } else {
                                    onChange('');
                                  }
                                }}
                                options={productos}
                                getOptionLabel={(option) =>
                                  `${option.nombre} (${option.codigoBarras})`
                                }
                                renderInput={(params) => (
                                  <TextField
                                    {...params}
                                    error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.productoId}
                                    helperText={(errors.lineas?.[index] as LineaOrdenError | undefined)?.productoId?.message}
                                    size="small"
                                  />
                                )}
                                size="small"
                              />
                            )}
                          />
                        </TableCell>
                        <TableCell>
                          <Controller
                            name={`lineas.${index}.cantidad`}
                            control={control}
                            render={({ field: { value, onChange, ...field } }) => (
                              <TextField
                                {...field}
                                type="number"
                                value={value}
                                onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                                error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.cantidad}
                                size="small"
                                fullWidth
                                inputProps={{ min: 0, step: 1 }}
                              />
                            )}
                          />
                        </TableCell>
                        <TableCell>
                          <Controller
                            name={`lineas.${index}.precioUnitario`}
                            control={control}
                            render={({ field: { value, onChange, ...field } }) => (
                              <TextField
                                {...field}
                                type="number"
                                value={value}
                                onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                                error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.precioUnitario}
                                size="small"
                                fullWidth
                                inputProps={{ min: 0, step: 1 }}
                              />
                            )}
                          />
                        </TableCell>
                        <TableCell>
                          <Controller
                            name={`lineas.${index}.impuestoId`}
                            control={control}
                            render={({ field: { value, onChange, ...field } }) => (
                              <TextField
                                {...field}
                                select
                                value={value ?? ''}
                                onChange={(e) =>
                                  onChange(e.target.value ? Number(e.target.value) : undefined)
                                }
                                size="small"
                                fullWidth
                              >
                                <MenuItem value="">Auto (del producto)</MenuItem>
                                {impuestos.map((impuesto) => (
                                  <MenuItem key={impuesto.id} value={impuesto.id}>
                                    {impuesto.nombre}
                                  </MenuItem>
                                ))}
                              </TextField>
                            )}
                          />
                        </TableCell>
                        <TableCell align="right">
                          <Typography variant="body2">
                            ${subtotalLinea.toLocaleString('es-CO')}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <IconButton
                            size="small"
                            color="error"
                            onClick={() => remove(index)}
                          >
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </TableContainer>

          {/* Totales */}
          <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Box sx={{ width: 300 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                <Typography variant="body2">Subtotal:</Typography>
                <Typography variant="body2">
                  ${totales.subtotal.toLocaleString('es-CO')}
                </Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                <Typography variant="body2">Impuestos:</Typography>
                <Typography variant="body2">
                  ${totales.impuestos.toLocaleString('es-CO')}
                </Typography>
              </Box>
              <Divider sx={{ my: 1 }} />
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="h6">Total:</Typography>
                <Typography variant="h6" color="primary">
                  ${totales.total.toLocaleString('es-CO')}
                </Typography>
              </Box>
            </Box>
          </Box>
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleClose} disabled={mutation.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? 'Creando...' : 'Crear Orden'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
