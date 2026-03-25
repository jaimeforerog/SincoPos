import React, { useState, useEffect } from 'react';
import { useForm, Controller, useFieldArray, type FieldError } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  TextField,
  MenuItem,
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
  alpha,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
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

type LineaOrdenError = { productoId?: FieldError; cantidad?: FieldError; precioUnitario?: FieldError };

const HERO_COLOR = '#1565c0';

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
  formaPago: z.enum(['Contado', 'Credito']),
  diasPlazo: z.number().min(0, 'Días de plazo no puede ser negativo'),
  observaciones: z.string().optional(),
  lineas: z.array(lineaSchema).min(1, 'Debe agregar al menos un producto'),
});

type OrdenCompraFormData = z.infer<typeof ordenCompraSchema>;

interface Props {
  onBack: () => void;
  onSuccess: () => void;
}

export function OrdenCompraFormView({ onBack, onSuccess }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [backendError, setBackendError] = useState<string | null>(null);
  const { user, activeEmpresaId } = useAuth();
  const activeSucursalId = useAuthStore((s) => s.activeSucursalId);

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.getAll(),
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
    staleTime: 0,
  });

  const proveedores = tercerosData?.items || [];

  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
    staleTime: 0,
  });

  const productos = productosData?.items || [];

  const { data: stockData = [] } = useQuery({
    queryKey: ['inventario-stock', activeSucursalId],
    queryFn: () => inventarioApi.getStock({ sucursalId: activeSucursalId }),
    enabled: activeSucursalId != null,
    staleTime: 60_000,
  });
  const stockMap = React.useMemo(
    () => new Map(stockData.map((s) => [s.productoId, s.cantidad])),
    [stockData]
  );

  const { data: impuestos = [] } = useQuery({
    queryKey: ['impuestos', activeEmpresaId],
    queryFn: () => impuestosApi.getAll(),
    staleTime: 0,
  });

  const valoresLimpios: OrdenCompraFormData = {
    sucursalId: activeSucursalId ?? 0,
    proveedorId: 0,
    fechaEntregaEsperada: new Date().toISOString().split('T')[0],
    formaPago: 'Contado',
    diasPlazo: 0,
    observaciones: '',
    lineas: [],
  };

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<OrdenCompraFormData>({
    resolver: zodResolver(ordenCompraSchema),
    defaultValues: valoresLimpios,
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
    mutation.mutate(data);
  };

  const agregarLinea = () => {
    append({
      productoId: '',
      cantidad: 1,
      precioUnitario: 0,
      impuestoId: undefined,
    });
  };

  // Inicializar form; también reacciona si activeSucursalId llega tarde (auto-fix post login)
  useEffect(() => {
    reset((prev) => ({
      ...valoresLimpios,
      ...prev,
      sucursalId: activeSucursalId ?? prev.sucursalId ?? 0,
    }));
    setBackendError(null);
  }, [activeSucursalId]); // eslint-disable-line react-hooks/exhaustive-deps

  const formaPago = watch('formaPago');

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      {/* Hero */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          px: { xs: 3, md: 4 },
          py: { xs: 1.5, md: 2 },
          mb: 3,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""', position: 'absolute', top: -60, right: -60,
            width: 200, height: 200, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
          '&::after': {
            content: '""', position: 'absolute', bottom: -40, right: 80,
            width: 120, height: 120, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
        }}
      >
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            position: 'relative',
            zIndex: 1,
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <IconButton
              aria-label="regresar"
              onClick={onBack}
              sx={{
                color: '#fff',
                bgcolor: 'rgba(255,255,255,0.12)',
                '&:hover': { bgcolor: 'rgba(255,255,255,0.22)' },
              }}
            >
              <ArrowBackIcon />
            </IconButton>
            <Box>
              <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
                Nueva Orden de Compra
              </Typography>
              <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.25 }}>
                Complete los datos y agregue los productos
              </Typography>
            </Box>
          </Box>

          <Button
            variant="contained"
            startIcon={<ShoppingCartIcon />}
            onClick={handleSubmit(onSubmit)}
            disabled={mutation.isPending || fields.length === 0}
            sx={{
              bgcolor: 'rgba(255,255,255,0.15)',
              color: '#fff',
              border: '1px solid rgba(255,255,255,0.35)',
              fontWeight: 700,
              backdropFilter: 'blur(4px)',
              '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              '&.Mui-disabled': { bgcolor: 'rgba(255,255,255,0.07)', color: 'rgba(255,255,255,0.4)' },
            }}
          >
            {mutation.isPending ? 'Creando...' : 'Crear Orden'}
          </Button>
        </Box>
      </Box>

      {/* Body */}
      <Box sx={{ px: { xs: 2, md: 4 }, pb: 4 }}>
        {backendError && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setBackendError(null)}>
            {backendError}
          </Alert>
        )}

        <form onSubmit={handleSubmit(onSubmit)}>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: '330px 1fr' },
              gap: 3,
              alignItems: 'start',
            }}
          >
            {/* Panel izquierdo — Datos de la orden */}
            <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 2 }}>
              <Typography
                variant="caption"
                fontWeight={700}
                color="text.secondary"
                sx={{ letterSpacing: '0.08em', textTransform: 'uppercase', display: 'block', mb: 2 }}
              >
                Datos de la Orden
              </Typography>

              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
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
                      size="small"
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
                      size="small"
                    >
                      <MenuItem value="">Seleccione un proveedor</MenuItem>
                      {proveedores.map((prov) => (
                        <MenuItem key={prov.id} value={prov.id}>
                          {prov.nombre} — {prov.identificacion}
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
                      size="small"
                    >
                      <MenuItem value="Contado">Contado</MenuItem>
                      <MenuItem value="Credito">Crédito</MenuItem>
                    </TextField>
                  )}
                />

                {/* Días Plazo — solo visible en Crédito */}
                {formaPago === 'Credito' && (
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
                        size="small"
                        inputProps={{ min: 1 }}
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
                      size="small"
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
                      rows={2}
                      fullWidth
                      size="small"
                    />
                  )}
                />
              </Box>
            </Paper>

            {/* Panel derecho — Productos */}
            <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
              {/* Toolbar de productos */}
              <Box
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  px: 2,
                  py: 1.5,
                  borderBottom: '1px solid',
                  borderColor: 'divider',
                  bgcolor: alpha(HERO_COLOR, 0.04),
                }}
              >
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <Typography variant="subtitle1" fontWeight={700}>
                    Productos
                  </Typography>
                  {fields.length > 0 && (
                    <Typography
                      variant="caption"
                      sx={{
                        bgcolor: alpha(HERO_COLOR, 0.12),
                        color: HERO_COLOR,
                        px: 0.75,
                        py: 0.125,
                        borderRadius: 1,
                        fontWeight: 700,
                      }}
                    >
                      {fields.length} línea{fields.length !== 1 ? 's' : ''}
                    </Typography>
                  )}
                </Box>
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
                <Alert severity="error" sx={{ mx: 2, mt: 1.5 }}>
                  {errors.lineas.message}
                </Alert>
              )}

              <TableContainer sx={{ maxHeight: 420, overflowY: 'auto' }}>
                <Table
                  size="small"
                  stickyHeader
                  sx={{ tableLayout: 'fixed', '& .MuiTableCell-root': { py: 0.25, px: 0.75 } }}
                >
                  <TableHead>
                    <TableRow
                      sx={{
                        '& th': {
                          bgcolor: 'grey.50',
                          fontWeight: 700,
                          fontSize: '0.72rem',
                          textTransform: 'uppercase',
                          letterSpacing: '0.04em',
                          color: 'text.secondary',
                          borderBottom: `2px solid ${alpha(HERO_COLOR, 0.15)}`,
                        },
                      }}
                    >
                      <TableCell width={36}>#</TableCell>
                      <TableCell>Producto</TableCell>
                      <TableCell width={80}>Cant.</TableCell>
                      <TableCell width={110}>Precio Unit.</TableCell>
                      <TableCell width={100}>IVA</TableCell>
                      <TableCell width={110} align="right">Subtotal</TableCell>
                      <TableCell width={44} />
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {fields.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={7} align="center" sx={{ py: 5 }}>
                          <ShoppingCartIcon sx={{ fontSize: 48, color: 'text.disabled', mb: 1 }} />
                          <Typography variant="body2" color="text.secondary">
                            No hay productos. Haga clic en "Agregar Producto"
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
                              <Typography variant="caption" color="text.secondary">
                                {index + 1}
                              </Typography>
                            </TableCell>
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
                                render={({ field: { value, onChange, ...f } }) => (
                                  <TextField
                                    {...f}
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
                                render={({ field: { value, onChange, ...f } }) => (
                                  <TextField
                                    {...f}
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
                                render={({ field: { value, onChange, ...f } }) => (
                                  <TextField
                                    {...f}
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
                            <TableCell padding="none" align="center">
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
              {fields.length > 0 && (
                <Box sx={{ px: 2, py: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
                  <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <Box sx={{ width: 220 }}>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                        <Typography variant="caption" color="text.secondary">Subtotal:</Typography>
                        <Typography variant="caption">${totales.subtotal.toLocaleString('es-CO')}</Typography>
                      </Box>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                        <Typography variant="caption" color="text.secondary">IVA:</Typography>
                        <Typography variant="caption">${totales.impuestos.toLocaleString('es-CO')}</Typography>
                      </Box>
                      <Divider sx={{ my: 0.75 }} />
                      <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                        <Typography variant="body2" fontWeight={700}>Total:</Typography>
                        <Typography variant="body2" fontWeight={700} color="primary">
                          ${totales.total.toLocaleString('es-CO')}
                        </Typography>
                      </Box>
                    </Box>
                  </Box>
                </Box>
              )}
            </Paper>
          </Box>
        </form>
      </Box>
    </Box>
  );
}
