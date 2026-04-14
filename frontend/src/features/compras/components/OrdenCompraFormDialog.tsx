import React, { useState, useEffect } from 'react';
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
import { tercerosApi } from '@/api/terceros';
import { productosApi } from '@/api/productos';
import { impuestosApi } from '@/api/impuestos';
import { inventarioApi } from '@/api/inventario';
import { sucursalesApi } from '@/api/sucursales';
import type { CrearOrdenCompraDTO } from '@/types/api';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { useConfiguracionVariableInt } from '@/hooks/useConfiguracionVariable';

type LineaOrdenError = { productoId?: FieldError; cantidad?: FieldError; precioUnitario?: FieldError };

// Columnas del dropdown: código | nombre | stock | valor (precioCosto)
const COLS = '100px 1fr 60px 80px';

const ProductoPaperHeader = ({ children, ...props }: React.HTMLAttributes<HTMLElement>) => (
  <Paper {...(props as object)}>
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: COLS,
        px: 1.5, py: '3px',
        borderBottom: '1px solid',
        borderColor: 'divider',
        bgcolor: 'grey.100',
        position: 'sticky',
        top: 0,
        zIndex: 1,
      }}
    >
      <Typography variant="caption" fontWeight={700} color="text.secondary">Código</Typography>
      <Typography variant="caption" fontWeight={700} color="text.secondary">Nombre</Typography>
      <Typography variant="caption" fontWeight={700} color="text.secondary" sx={{ textAlign: 'right' }}>Stock</Typography>
      <Typography variant="caption" fontWeight={700} color="text.secondary" sx={{ textAlign: 'right' }}>Valor</Typography>
    </Box>
    {children}
  </Paper>
);

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
  fechaOrden: z.string().optional(),
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
  const { user, activeEmpresaId } = useAuth();

  const diasMaxCompra = useConfiguracionVariableInt('DiasMax_CompraAtrazada');
  const mostrarFechaOrden = diasMaxCompra > 0;
  const today = new Date().toISOString().split('T')[0];
  const minFechaOrden = (() => {
    if (!mostrarFechaOrden) return '';
    const d = new Date();
    d.setDate(d.getDate() - diasMaxCompra);
    return d.toISOString().split('T')[0];
  })();

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.getAll(),
    enabled: open,
    staleTime: 0,
  });

  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId || s.empresaId == null) &&
      (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  const { data: tercerosData } = useQuery({
    queryKey: ['terceros-proveedores', activeEmpresaId],
    queryFn: () => tercerosApi.getAll({ tipoTercero: 'Proveedor', pageSize: 100 }),
    enabled: open,
    staleTime: 0,
  });

  const proveedores = tercerosData?.items || [];

  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
    enabled: open,
    staleTime: 0,
  });

  const productos = productosData?.items || [];

  const activeSucursalId = useAuthStore((s) => s.activeSucursalId);
  const { data: stockData = [] } = useQuery({
    queryKey: ['inventario-stock', activeSucursalId],
    queryFn: () => inventarioApi.getStock({ sucursalId: activeSucursalId }),
    enabled: open && activeSucursalId != null,
    staleTime: 60_000,
  });
  const stockMap = React.useMemo(
    () => new Map(stockData.map((s) => [s.productoId, s.cantidad])),
    [stockData]
  );

  const { data: impuestos = [] } = useQuery({
    queryKey: ['impuestos', activeEmpresaId],
    queryFn: () => impuestosApi.getAll(),
    enabled: open,
    staleTime: 0,
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
      fechaOrden: new Date().toISOString().split('T')[0],
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

  const lineas = watch('lineas');

  const calcularTotales = () => {
    let subtotal = 0;
    let impuestosTotal = 0;

    lineas.forEach((linea) => {
      const subtotalLinea = linea.cantidad * linea.precioUnitario;
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
      const statusCode: number = error?.statusCode ?? 0;
      const msg: string = error?.message ?? '';
      const errores: Record<string, string[]> | undefined = error?.errors;

      let mensaje = 'Error al crear la orden de compra';

      if (statusCode === 400) {
        if (errores && Object.keys(errores).length > 0) {
          const detalle = Object.entries(errores)
            .map(([campo, msgs]) => `${campo}: ${msgs.join(', ')}`)
            .join('\n');
          mensaje = `Errores de validación:\n${detalle}`;
        } else {
          mensaje = msg || 'Los datos proporcionados no son válidos. Revisa el formulario.';
        }
      } else if (statusCode === 401) {
        mensaje = 'No estás autenticado. Por favor inicia sesión nuevamente.';
      } else if (statusCode === 403) {
        mensaje = 'No tienes permisos suficientes. Se requiere rol Supervisor para crear órdenes de compra.';
      } else if (statusCode === 404) {
        mensaje = msg || 'Recurso no encontrado. Verifica que la sucursal y el proveedor existan.';
      } else if (statusCode >= 500) {
        mensaje = msg || 'Error interno del servidor. Contacta al administrador.';
      } else if (statusCode === 0) {
        mensaje = msg || 'No se pudo conectar con el servidor.';
      } else {
        mensaje = msg || `Error HTTP ${statusCode}`;
      }

      setBackendError(mensaje);
      enqueueSnackbar(mensaje.split('\n')[0], { variant: 'error' });
    },
  });

  const onSubmit = (data: OrdenCompraFormData) => {
    mutation.mutate({
      ...data,
      fechaOrden: mostrarFechaOrden && data.fechaOrden
        ? new Date(data.fechaOrden).toISOString()
        : undefined,
    });
  };

  const valoresLimpios: OrdenCompraFormData = {
    sucursalId: 0,
    proveedorId: 0,
    fechaEntregaEsperada: new Date().toISOString().split('T')[0],
    fechaOrden: new Date().toISOString().split('T')[0],
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
      const sucursalInicial = activeSucursalId ?? 0;
      reset({ ...valoresLimpios, sucursalId: sucursalInicial });
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

            {/* Días Plazo — solo visible en Crédito */}
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

            {/* Fecha de orden — solo visible si DiasMax_CompraAtrazada > 0 */}
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
                    InputLabelProps={{ shrink: true }}
                    inputProps={{ min: minFechaOrden, max: today }}
                    error={!!errors.fechaOrden}
                    helperText={errors.fechaOrden?.message}
                    fullWidth
                  />
                )}
              />
            )}

            {/* Fecha de entrega esperada */}
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
              type="button"
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

          <TableContainer component={Paper} variant="outlined" sx={{ mb: 3, maxHeight: 360, overflowY: 'auto' }}>
            <Table size="small" sx={{ tableLayout: 'fixed', '& .MuiTableCell-root': { py: 0.25, px: 0.75 } }}>
              <TableHead>
                <TableRow sx={{ bgcolor: 'grey.50', '& th': { position: 'sticky', top: 0, bgcolor: 'grey.50', zIndex: 1 } }}>
                  <TableCell width="52%">Producto</TableCell>
                  <TableCell width="8%">Cant.</TableCell>
                  <TableCell width="14%">Precio Unit.</TableCell>
                  <TableCell width="10%">IVA</TableCell>
                  <TableCell width="11%" align="right">Subtotal</TableCell>
                  <TableCell width="5%"></TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {fields.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} align="center">
                      <Typography variant="body2" color="text.secondary" sx={{ py: 1.5 }}>
                        No hay productos. Clic en "Agregar Producto"
                      </Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  fields.map((field, index) => {
                    const linea = lineas[index];
                    const subtotalLinea = linea ? linea.cantidad * linea.precioUnitario : 0;

                    return (
                      <TableRow key={field.id} sx={{ '&:hover': { bgcolor: 'action.hover' } }}>
                        <TableCell>
                          <Controller
                            name={`lineas.${index}.productoId`}
                            control={control}
                            render={({ field: { value, onChange } }) => (
                              <Autocomplete
                                value={productos.find((p) => p.id === value) || null}
                                onChange={(_, newValue) => {
                                  if (newValue) {
                                    const currentLineas = watch('lineas');
                                    const lineaDuplicada = currentLineas.findIndex(
                                      (l, i) => i !== index && l.productoId === newValue.id
                                    );
                                    if (lineaDuplicada !== -1) {
                                      enqueueSnackbar(
                                        `"${newValue.nombre}" ya está en la línea ${lineaDuplicada + 1}. Modifica la cantidad en esa línea.`,
                                        { variant: 'warning' }
                                      );
                                      return;
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
                                getOptionLabel={(option) => `${option.nombre} (${option.codigoBarras})`}
                                PaperComponent={ProductoPaperHeader}
                                slotProps={{ listbox: { style: { maxHeight: 480 } } }}
                                renderOption={(props, option) => {
                                  const stock = stockMap.get(option.id);
                                  return (
                                    <Box
                                      component="li"
                                      {...props}
                                      sx={{
                                        display: 'grid !important',
                                        gridTemplateColumns: COLS,
                                        px: '12px !important',
                                        py: '2px !important',
                                        minHeight: '0 !important',
                                        gap: 0.5,
                                        alignItems: 'center',
                                      }}
                                    >
                                      <Typography variant="caption" noWrap sx={{ fontFamily: 'monospace', color: 'text.secondary', fontSize: '0.72rem' }}>
                                        {option.codigoBarras}
                                      </Typography>
                                      <Typography variant="caption" noWrap sx={{ fontSize: '0.75rem' }}>
                                        {option.nombre}
                                      </Typography>
                                      <Typography variant="caption" sx={{ textAlign: 'right', fontSize: '0.72rem', color: stock === 0 ? 'error.main' : stock != null ? 'success.main' : 'text.disabled' }}>
                                        {stock != null ? stock : '—'}
                                      </Typography>
                                      <Typography variant="caption" sx={{ textAlign: 'right', fontSize: '0.72rem', fontWeight: 500 }}>
                                        ${option.precioCosto.toLocaleString('es-CO')}
                                      </Typography>
                                    </Box>
                                  );
                                }}
                                renderInput={(params) => (
                                  <TextField
                                    {...params}
                                    error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.productoId}
                                    size="small"
                                    sx={{ '& .MuiInputBase-input': { fontSize: '0.8rem', py: '2px' } }}
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
                                inputProps={{ min: 0, step: 1, style: { fontSize: '0.8rem', padding: '2px 4px', textAlign: 'right' } }}
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
                                inputProps={{ min: 0, step: 1, style: { fontSize: '0.8rem', padding: '2px 4px', textAlign: 'right' } }}
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
                                sx={{ '& .MuiSelect-select': { fontSize: '0.8rem', py: '2px' } }}
                              >
                                <MenuItem value="" sx={{ fontSize: '0.8rem' }}>Exento (0%)</MenuItem>
                                {impuestos.map((impuesto) => (
                                  <MenuItem key={impuesto.id} value={impuesto.id} sx={{ fontSize: '0.8rem' }}>
                                    {impuesto.nombre}
                                  </MenuItem>
                                ))}
                              </TextField>
                            )}
                          />
                        </TableCell>
                        <TableCell align="right">
                          <Typography variant="caption" fontWeight={500}>
                            ${subtotalLinea.toLocaleString('es-CO')}
                          </Typography>
                        </TableCell>
                        <TableCell padding="none">
                          <IconButton
                            size="small"
                            color="error"
                            onClick={() => remove(index)}
                            sx={{ p: 0.5 }}
                          >
                            <DeleteIcon sx={{ fontSize: 16 }} />
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
            <Box sx={{ width: 200 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary">Subtotal:</Typography>
                <Typography variant="caption">${totales.subtotal.toLocaleString('es-CO')}</Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary">IVA:</Typography>
                <Typography variant="caption">${totales.impuestos.toLocaleString('es-CO')}</Typography>
              </Box>
              <Divider sx={{ my: 0.5 }} />
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2" fontWeight={700}>Total:</Typography>
                <Typography variant="body2" fontWeight={700} color="primary">
                  ${totales.total.toLocaleString('es-CO')}
                </Typography>
              </Box>
            </Box>
          </Box>
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
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
