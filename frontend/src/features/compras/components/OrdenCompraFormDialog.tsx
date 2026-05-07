import React, { useState, useEffect } from 'react';
import { useForm, useFieldArray, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Alert,
  Divider,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { comprasApi } from '@/api/compras';
import { tercerosApi } from '@/api/terceros';
import { productosApi } from '@/api/productos';
import { impuestosApi } from '@/api/impuestos';
import { inventarioApi } from '@/api/inventario';
import { sucursalesApi } from '@/api/sucursales';
import type { CrearOrdenCompraDTO, ApiError } from '@/types/api';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { useConfiguracionVariableInt } from '@/hooks/useConfiguracionVariable';
import { localDateStr, localDateStrDaysAgo } from '@/utils/dates';
import { ordenCompraSchema, type OrdenCompraFormData } from './ordenCompraSchema';
import { OrdenCompraHeaderFields } from './OrdenCompraHeaderFields';
import { LineasOrdenTable } from './LineasOrdenTable';
import { TotalesOrden } from './TotalesOrden';

interface OrdenCompraFormDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const VALORES_LIMPIOS: OrdenCompraFormData = {
  sucursalId: 0,
  proveedorId: 0,
  fechaEntregaEsperada: localDateStr(),
  fechaOrden: localDateStr(),
  formaPago: 'Contado',
  diasPlazo: 0,
  observaciones: '',
  lineas: [],
};

function formatBackendError(error: ApiError): string {
  const statusCode = error?.statusCode ?? 0;
  const msg = error?.message ?? '';
  const errores = error?.errors;

  if (statusCode === 400) {
    if (errores && Object.keys(errores).length > 0) {
      const detalle = Object.entries(errores)
        .map(([campo, msgs]) => `${campo}: ${msgs.join(', ')}`)
        .join('\n');
      return `Errores de validación:\n${detalle}`;
    }
    return msg || 'Los datos proporcionados no son válidos. Revisa el formulario.';
  }
  if (statusCode === 401) return 'No estás autenticado. Por favor inicia sesión nuevamente.';
  if (statusCode === 403) return 'No tienes permisos suficientes. Se requiere rol Supervisor para crear órdenes de compra.';
  if (statusCode === 404) return msg || 'Recurso no encontrado. Verifica que la sucursal y el proveedor existan.';
  if (statusCode >= 500) return msg || 'Error interno del servidor. Contacta al administrador.';
  if (statusCode === 0) return msg || 'No se pudo conectar con el servidor.';
  return msg || `Error HTTP ${statusCode}`;
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
  const today = localDateStr();
  const minFechaOrden = mostrarFechaOrden ? localDateStrDaysAgo(diasMaxCompra) : '';

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.getAll(),
    enabled: open,
    staleTime: 0,
  });

  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId) &&
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
    defaultValues: VALORES_LIMPIOS,
  });

  const { fields, append, remove } = useFieldArray({ control, name: 'lineas' });

  const lineas = watch('lineas');
  const fechaOrdenValue = useWatch({ control, name: 'fechaOrden' });
  const minFechaEntrega = mostrarFechaOrden ? (fechaOrdenValue || today) : today;

  const totales = (() => {
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
    return { subtotal, impuestos: impuestosTotal, total: subtotal + impuestosTotal };
  })();

  const mutation = useMutation({
    mutationFn: (data: CrearOrdenCompraDTO) => comprasApi.create(data),
    onSuccess: (data) => {
      setBackendError(null);
      queryClient.invalidateQueries({ queryKey: ['compras'] });
      enqueueSnackbar(`Orden de compra ${data.numeroOrden} creada exitosamente`, { variant: 'success' });
      onSuccess();
      handleClose();
    },
    onError: (error: ApiError) => {
      const mensaje = formatBackendError(error);
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

  const handleClose = () => {
    reset(VALORES_LIMPIOS);
    setBackendError(null);
    onClose();
  };

  useEffect(() => {
    if (open) {
      const sucursalInicial = activeSucursalId ?? 0;
      reset({ ...VALORES_LIMPIOS, sucursalId: sucursalInicial });
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

          <OrdenCompraHeaderFields
            control={control}
            errors={errors}
            reset={reset}
            watch={watch}
            sucursales={sucursales}
            proveedores={proveedores}
            mostrarFechaOrden={mostrarFechaOrden}
            minFechaOrden={minFechaOrden}
            minFechaEntrega={minFechaEntrega}
            today={today}
          />

          <Divider sx={{ my: 2 }} />

          <LineasOrdenTable
            fields={fields}
            control={control}
            errors={errors}
            reset={reset}
            watch={watch}
            append={append}
            remove={remove}
            productos={productos}
            impuestos={impuestos}
            stockMap={stockMap}
            lineas={lineas}
          />

          <Box sx={{ mt: 2 }}>
            <TotalesOrden subtotal={totales.subtotal} impuestos={totales.impuestos} total={totales.total} />
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
