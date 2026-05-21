import { useEffect, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Alert,
  InputAdornment,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Box,
} from '@mui/material';
import { useSnackbar } from 'notistack';
import { useAuthStore } from '@/stores/auth.store';
import { productosApi } from '@/api/productos';
import { categoriasApi } from '@/api/categorias';
import { conceptosRetencionApi, impuestosApi } from '@/api/impuestos';
import type { ProductoDTO, CrearProductoDTO, ActualizarProductoDTO, ApiError } from '@/types/api';
import {
  UNIDADES_MEDIDA,
  crearProductoSchema,
  actualizarProductoSchema,
  type CrearProductoFormData,
  type ActualizarProductoFormData,
} from './productoSchema';
import { CategoriaSelectorField } from './CategoriaSelectorField';
import { ProductoExtrasFields } from './ProductoExtrasFields';

interface ProductoFormDialogProps {
  open: boolean;
  producto: ProductoDTO | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function ProductoFormDialog({
  open,
  producto,
  onClose,
  onSuccess,
}: ProductoFormDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const activeEmpresaId = useAuthStore(s => s.activeEmpresaId);
  const [backendError, setBackendError] = useState<string | null>(null);
  const [conceptoRetencionId, setConceptoRetencionId] = useState<number | ''>('');
  const [impuestoId, setImpuestoId] = useState<number | ''>('');
  const [manejaLotes, setManejaLotes] = useState(false);
  const [diasVidaUtil, setDiasVidaUtil] = useState<number | ''>('');
  const [esAlimentoUltraprocesado, setEsAlimentoUltraprocesado] = useState(false);
  const [gramosAzucarPor100ml, setGramosAzucarPor100ml] = useState<number | ''>('');
  const [cantidadMlPorUnidad, setCantidadMlPorUnidad] = useState<number | ''>('');

  const isEdit = !!producto;

  const { data: categorias = [] } = useQuery({
    queryKey: ['categorias', activeEmpresaId],
    queryFn: () => categoriasApi.getAll(false),
    staleTime: 0,
    enabled: open,
  });

  const { data: impuestos = [] } = useQuery({
    queryKey: ['impuestos', activeEmpresaId],
    queryFn: () => impuestosApi.getAll(),
    enabled: open,
    staleTime: 0,
  });

  const { data: conceptosRetencion = [] } = useQuery({
    queryKey: ['conceptos-retencion'],
    queryFn: () => conceptosRetencionApi.getAll(),
    enabled: open,
  });

  const {
    control: controlCrear,
    handleSubmit: handleSubmitCrear,
    reset: resetCrear,
    formState: { errors: errorsCrear },
  } = useForm<CrearProductoFormData>({
    resolver: zodResolver(crearProductoSchema),
    defaultValues: {
      codigoBarras: '',
      nombre: '',
      descripcion: '',
      categoriaId: 0,
      precioCosto: 0,
      unidadMedida: '94',
    },
  });

  const {
    control: controlActualizar,
    handleSubmit: handleSubmitActualizar,
    reset: resetActualizar,
    formState: { errors: errorsActualizar },
  } = useForm<ActualizarProductoFormData>({
    resolver: zodResolver(actualizarProductoSchema),
    defaultValues: {
      nombre: '',
      descripcion: '',
      precioCosto: 0,
      unidadMedida: '94',
    },
  });

  useEffect(() => {
    if (open) {
      // dialog reset: limpia error backend al abrir el dialog
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setBackendError(null);
      if (producto) {
        resetActualizar({
          nombre: producto.nombre,
          descripcion: producto.descripcion || '',
          precioCosto: producto.precioCosto,
          unidadMedida: producto.unidadMedida || '94',
        });
        setConceptoRetencionId(producto.conceptoRetencionId ?? '');
        setImpuestoId(producto.impuestoId ?? '');
        setManejaLotes(producto.manejaLotes ?? false);
        setDiasVidaUtil(producto.diasVidaUtil ?? '');
        setEsAlimentoUltraprocesado(producto.esAlimentoUltraprocesado ?? false);
        setGramosAzucarPor100ml(producto.gramosAzucarPor100ml ?? '');
        setCantidadMlPorUnidad(producto.cantidadMlPorUnidad ?? '');
      } else {
        resetCrear({
          codigoBarras: '',
          nombre: '',
          descripcion: '',
          categoriaId: 0,
          precioCosto: 0,
          unidadMedida: '94',
        });
        setConceptoRetencionId('');
        setImpuestoId('');
        setManejaLotes(false);
        setDiasVidaUtil('');
        setEsAlimentoUltraprocesado(false);
        setGramosAzucarPor100ml('');
        setCantidadMlPorUnidad('');
      }
    }
    // El default de conceptoRetencionId en modo crear lo aplica un effect aparte
    // (depende de conceptosRetencion, cuya referencia cambia mientras la query carga
    // y dispararía resetCrear() en loop).
  }, [open, producto, resetCrear, resetActualizar]);

  // Aplica "Compras generales" (codigoDian: 2307) como default en modo crear
  // una vez que las conceptosRetencion estén cargadas. Independiente del effect
  // de reset para no re-ejecutar resetCrear cuando el array cambia de referencia.
  useEffect(() => {
    if (!open || producto || conceptosRetencion.length === 0) return;
    const def = conceptosRetencion.find(c => c.codigoDian === '2307');
    // sincroniza estado local con data async cargada por React Query: aplica el
    // default solo si el usuario aún no eligió otro valor.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (def) setConceptoRetencionId(prev => (prev === '' ? def.id : prev));
  }, [open, producto, conceptosRetencion]);

  const mutation = useMutation({
    mutationFn: async (data: CrearProductoFormData | ActualizarProductoFormData) => {
      if (isEdit) {
        const updateData: ActualizarProductoDTO = {
          ...(data as ActualizarProductoFormData),
          precioVenta: 0,
          impuestoId: impuestoId !== '' ? impuestoId : undefined,
          conceptoRetencionId: conceptoRetencionId || undefined,
          manejaLotes,
          diasVidaUtil: diasVidaUtil !== '' ? diasVidaUtil : undefined,
          esAlimentoUltraprocesado,
          gramosAzucarPor100ml: gramosAzucarPor100ml !== '' ? gramosAzucarPor100ml : undefined,
          cantidadMlPorUnidad: cantidadMlPorUnidad !== '' ? cantidadMlPorUnidad : undefined,
        };
        await productosApi.update(producto!.id, updateData);
        return null;
      } else {
        const createData: CrearProductoDTO = {
          ...(data as CrearProductoFormData),
          precioVenta: 0,
          impuestoId: impuestoId !== '' ? impuestoId : undefined,
          conceptoRetencionId: conceptoRetencionId || undefined,
          manejaLotes,
          diasVidaUtil: diasVidaUtil !== '' ? diasVidaUtil : undefined,
          esAlimentoUltraprocesado,
          gramosAzucarPor100ml: gramosAzucarPor100ml !== '' ? gramosAzucarPor100ml : undefined,
          cantidadMlPorUnidad: cantidadMlPorUnidad !== '' ? cantidadMlPorUnidad : undefined,
        };
        return await productosApi.create(createData);
      }
    },
    onSuccess: () => {
      setBackendError(null);
      queryClient.invalidateQueries({ queryKey: ['productos'] });
      enqueueSnackbar(`Producto ${isEdit ? 'actualizado' : 'creado'} exitosamente`, { variant: 'success' });
      onSuccess();
      onClose();
    },
    onError: (error: ApiError) => {
      const mensaje = error.message || `Error al ${isEdit ? 'actualizar' : 'crear'} el producto`;
      setBackendError(mensaje);
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmitCrear = (data: CrearProductoFormData) => mutation.mutate(data);
  const onSubmitActualizar = (data: ActualizarProductoFormData) => mutation.mutate(data);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>{isEdit ? 'Editar Producto' : 'Nuevo Producto'}</DialogTitle>

      <form onSubmit={isEdit ? handleSubmitActualizar(onSubmitActualizar) : handleSubmitCrear(onSubmitCrear)}>
        <DialogContent>
          {backendError && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setBackendError(null)}>
              {backendError}
            </Alert>
          )}

          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr', gap: 2 }}>
            {/* Código de Barras */}
            {!isEdit ? (
              <Controller
                name="codigoBarras"
                control={controlCrear}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Código de Barras *"
                    error={!!errorsCrear.codigoBarras}
                    helperText={errorsCrear.codigoBarras?.message}
                    fullWidth
                    // dialog UX: primer input (modo crear) recibe foco
                    // eslint-disable-next-line jsx-a11y/no-autofocus
                    autoFocus
                  />
                )}
              />
            ) : (
              <TextField
                label="Código de Barras"
                value={producto?.codigoBarras}
                fullWidth
                disabled
                helperText="El código de barras no se puede modificar"
              />
            )}

            {/* Nombre */}
            {isEdit ? (
              <Controller
                name="nombre"
                control={controlActualizar}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Nombre *"
                    error={!!errorsActualizar.nombre}
                    helperText={errorsActualizar.nombre?.message}
                    fullWidth
                    // dialog UX: primer input editable (modo editar) recibe foco
                    // eslint-disable-next-line jsx-a11y/no-autofocus
                    autoFocus
                  />
                )}
              />
            ) : (
              <Controller
                name="nombre"
                control={controlCrear}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Nombre *"
                    error={!!errorsCrear.nombre}
                    helperText={errorsCrear.nombre?.message}
                    fullWidth
                  />
                )}
              />
            )}

            {/* Descripción */}
            {isEdit ? (
              <Controller
                name="descripcion"
                control={controlActualizar}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Descripción"
                    error={!!errorsActualizar.descripcion}
                    helperText={errorsActualizar.descripcion?.message}
                    fullWidth
                    multiline
                    rows={2}
                  />
                )}
              />
            ) : (
              <Controller
                name="descripcion"
                control={controlCrear}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Descripción"
                    error={!!errorsCrear.descripcion}
                    helperText={errorsCrear.descripcion?.message}
                    fullWidth
                    multiline
                    rows={2}
                  />
                )}
              />
            )}

            {/* Categoría — sólo en creación */}
            {!isEdit && (
              <CategoriaSelectorField
                control={controlCrear}
                errors={errorsCrear}
                categorias={categorias}
              />
            )}

            {isEdit && (
              <TextField
                label="Categoría"
                value={categorias.find((c) => c.id === producto?.categoriaId)?.nombre || 'Sin categoría'}
                fullWidth
                disabled
                helperText="La categoría no se puede modificar"
              />
            )}

            {/* Precio Costo */}
            {isEdit ? (
              <Controller
                name="precioCosto"
                control={controlActualizar}
                render={({ field: { value, onChange, ...field } }) => (
                  <TextField
                    {...field}
                    type="number"
                    label="Precio Costo *"
                    value={Number.isNaN(value) ? '' : value}
                    onChange={(e) => onChange(e.target.value === '' ? NaN : parseFloat(e.target.value))}
                    error={!!errorsActualizar.precioCosto}
                    helperText={errorsActualizar.precioCosto?.message}
                    fullWidth
                    InputProps={{
                      startAdornment: <InputAdornment position="start">$</InputAdornment>,
                    }}
                  />
                )}
              />
            ) : (
              <Controller
                name="precioCosto"
                control={controlCrear}
                render={({ field: { value, onChange, ...field } }) => (
                  <TextField
                    {...field}
                    type="number"
                    label="Precio Costo *"
                    value={Number.isNaN(value) ? '' : value}
                    onChange={(e) => onChange(e.target.value === '' ? NaN : parseFloat(e.target.value))}
                    error={!!errorsCrear.precioCosto}
                    helperText={errorsCrear.precioCosto?.message || 'Los precios de venta se configuran por sucursal'}
                    fullWidth
                    InputProps={{
                      startAdornment: <InputAdornment position="start">$</InputAdornment>,
                    }}
                  />
                )}
              />
            )}

            {/* Unidad de Medida */}
            {isEdit ? (
              <Controller
                name="unidadMedida"
                control={controlActualizar}
                render={({ field }) => (
                  <FormControl fullWidth error={!!errorsActualizar.unidadMedida}>
                    <InputLabel id="um-label-edit">Unidad de Medida *</InputLabel>
                    <Select {...field} labelId="um-label-edit" label="Unidad de Medida *">
                      {UNIDADES_MEDIDA.map((u) => (
                        <MenuItem key={u.codigo} value={u.codigo}>{u.label}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
            ) : (
              <Controller
                name="unidadMedida"
                control={controlCrear}
                render={({ field }) => (
                  <FormControl fullWidth error={!!errorsCrear.unidadMedida}>
                    <InputLabel id="um-label-crear">Unidad de Medida *</InputLabel>
                    <Select {...field} labelId="um-label-crear" label="Unidad de Medida *">
                      {UNIDADES_MEDIDA.map((u) => (
                        <MenuItem key={u.codigo} value={u.codigo}>{u.label}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
            )}

            <ProductoExtrasFields
              impuestos={impuestos}
              impuestoId={impuestoId}
              onImpuestoChange={setImpuestoId}
              manejaLotes={manejaLotes}
              onManejaLotesChange={setManejaLotes}
              diasVidaUtil={diasVidaUtil}
              onDiasVidaUtilChange={setDiasVidaUtil}
              esAlimentoUltraprocesado={esAlimentoUltraprocesado}
              onEsAlimentoUltraprocesadoChange={setEsAlimentoUltraprocesado}
              gramosAzucarPor100ml={gramosAzucarPor100ml}
              onGramosAzucarPor100mlChange={setGramosAzucarPor100ml}
              cantidadMlPorUnidad={cantidadMlPorUnidad}
              onCantidadMlPorUnidadChange={setCantidadMlPorUnidad}
            />

            <Alert severity="info">
              Los precios de venta se configuran en el modulo de <strong>Precios por Sucursal</strong>.
              El sistema calculara automaticamente el precio usando el margen de la categoria si no se define un precio especifico.
            </Alert>
          </Box>

          {mutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {mutation.error instanceof Error ? mutation.error.message : 'Error al procesar la solicitud'}
            </Alert>
          )}
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={mutation.isPending}>Cancelar</Button>
          <Button type="submit" variant="contained" disabled={mutation.isPending}>
            {mutation.isPending ? 'Guardando...' : isEdit ? 'Actualizar' : 'Crear'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
