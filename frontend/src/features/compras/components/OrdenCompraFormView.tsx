import { useState, useEffect } from 'react';
import { useForm, Controller, useFieldArray, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  IconButton,
  MenuItem,
  Paper,
  TextField,
  Typography,
  Alert,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
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
import { useConfiguracionVariableInt } from '@/hooks/useConfiguracionVariable';
import { localDateStr, localDateStrDaysAgo } from '@/utils/dates';
import { ordenCompraSchema, type OrdenCompraFormData } from './OrdenCompraFormTypes';
import { OrdenCompraFormLineas } from './OrdenCompraFormLineas';

const HERO_COLOR = '#1565c0';

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

  const diasMaxCompra = useConfiguracionVariableInt('DiasMax_CompraAtrazada');
  const mostrarFechaOrden = diasMaxCompra > 0;
  const today = localDateStr();
  const minFechaOrden = mostrarFechaOrden ? localDateStrDaysAgo(diasMaxCompra) : '';

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

  const { data: impuestos = [] } = useQuery({
    queryKey: ['impuestos', activeEmpresaId],
    queryFn: () => impuestosApi.getAll(),
    staleTime: 0,
  });

  const valoresLimpios: OrdenCompraFormData = {
    sucursalId: activeSucursalId ?? 0,
    proveedorId: 0,
    fechaEntregaEsperada: localDateStr(),
    fechaOrden: localDateStr(),
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

  const { fields, append, remove } = useFieldArray({ control, name: 'lineas' });

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

  const onSubmit = (data: OrdenCompraFormData) => mutation.mutate({
    ...data,
    fechaOrden: mostrarFechaOrden && data.fechaOrden
      ? new Date(data.fechaOrden).toISOString()
      : undefined,
  });

  useEffect(() => {
    reset((prev) => ({
      ...valoresLimpios,
      ...prev,
      sucursalId: activeSucursalId ?? prev.sucursalId ?? 0,
    }));
    setBackendError(null);
  }, [activeSucursalId]); // eslint-disable-line react-hooks/exhaustive-deps

  const formaPago = watch('formaPago');
  const fechaOrdenValue = useWatch({ control, name: 'fechaOrden' });
  const minFechaEntrega = mostrarFechaOrden ? (fechaOrdenValue || today) : today;

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
                        <MenuItem key={suc.id} value={suc.id}>{suc.nombre}</MenuItem>
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
                        size="small"
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
                      size="small"
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
                      rows={2}
                      fullWidth
                      size="small"
                    />
                  )}
                />
              </Box>
            </Paper>

            {/* Panel derecho — Líneas de productos */}
            <OrdenCompraFormLineas
              control={control}
              errors={errors}
              watch={watch}
              reset={reset}
              fields={fields}
              append={append}
              remove={remove}
              productos={productos}
              impuestos={impuestos}
              stockData={stockData}
            />
          </Box>
        </form>
      </Box>
    </Box>
  );
}
