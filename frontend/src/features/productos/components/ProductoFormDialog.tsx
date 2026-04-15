import { useEffect, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
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
  Alert,
  InputAdornment,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Box,
  IconButton,
  Tooltip,
  FormControlLabel,
  Switch,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { useSnackbar } from 'notistack';
import { useAuthStore } from '@/stores/auth.store';
import { productosApi } from '@/api/productos';
import { categoriasApi } from '@/api/categorias';
import { conceptosRetencionApi } from '@/api/impuestos';
import type { ProductoDTO, CrearProductoDTO, ActualizarProductoDTO } from '@/types/api';

const UNIDADES_MEDIDA = [
  { codigo: '94',  label: 'Unidad (94)' },
  { codigo: 'NIU', label: 'Artículo (NIU)' },
  { codigo: 'KGM', label: 'Kilogramo (KGM)' },
  { codigo: 'GRM', label: 'Gramo (GRM)' },
  { codigo: 'LTR', label: 'Litro (LTR)' },
  { codigo: 'MLT', label: 'Mililitro (MLT)' },
  { codigo: 'MTR', label: 'Metro (MTR)' },
  { codigo: 'CMT', label: 'Centímetro (CMT)' },
  { codigo: 'GLL', label: 'Galón (GLL)' },
  { codigo: 'BX',  label: 'Caja (BX)' },
];

const crearProductoSchema = z.object({
  codigoBarras: z.string().min(1, 'Código de barras es requerido').max(50, 'Máximo 50 caracteres'),
  nombre: z.string().min(1, 'Nombre es requerido').max(200, 'Máximo 200 caracteres'),
  descripcion: z.string().max(500, 'Máximo 500 caracteres').optional(),
  categoriaId: z.number().min(1, 'Categoría es requerida'),
  precioCosto: z.number().min(0, 'Debe ser mayor o igual a 0'),
  unidadMedida: z.string().min(1, 'Unidad de medida es requerida'),
});

const actualizarProductoSchema = z.object({
  nombre: z.string().min(1, 'Nombre es requerido').max(200, 'Máximo 200 caracteres'),
  descripcion: z.string().max(500, 'Máximo 500 caracteres').optional(),
  precioCosto: z.number().min(0, 'Debe ser mayor o igual a 0'),
  unidadMedida: z.string().min(1, 'Unidad de medida es requerida'),
});

type CrearProductoFormData = z.infer<typeof crearProductoSchema>;
type ActualizarProductoFormData = z.infer<typeof actualizarProductoSchema>;

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
  const { activeEmpresaId } = useAuthStore();
  const [showCategoriaInput, setShowCategoriaInput] = useState(false);
  const [nuevaCategoria, setNuevaCategoria] = useState('');
  const [backendError, setBackendError] = useState<string | null>(null);
  const [conceptoRetencionId, setConceptoRetencionId] = useState<number | ''>('');
  const [manejaLotes, setManejaLotes] = useState(false);
  const [diasVidaUtil, setDiasVidaUtil] = useState<number | ''>('');

  const isEdit = !!producto;

  // Cargar categorias
  const { data: categorias = [] } = useQuery({
    queryKey: ['categorias', activeEmpresaId],
    queryFn: () => categoriasApi.getAll(false),
    staleTime: 0,
    enabled: open,
  });

  // Cargar conceptos de retencion
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

  // Reset form cuando cambia el producto o se abre el dialogo
  useEffect(() => {
    if (open) {
      setBackendError(null);
      if (producto) {
        resetActualizar({
          nombre: producto.nombre,
          descripcion: producto.descripcion || '',
          precioCosto: producto.precioCosto,
          unidadMedida: producto.unidadMedida || '94',
        });
        setConceptoRetencionId(producto.conceptoRetencionId ?? '');
        setManejaLotes(producto.manejaLotes ?? false);
        setDiasVidaUtil(producto.diasVidaUtil ?? '');
      } else {
        resetCrear({
          codigoBarras: '',
          nombre: '',
          descripcion: '',
          categoriaId: 0,
          precioCosto: 0,
          unidadMedida: '94',
        });
        
        // Buscar el concepto "Compras generales" (codigoDian: 2307) para asignarlo por defecto
        const conceptoComprasDefault = conceptosRetencion.find(c => c.codigoDian === '2307');
        setConceptoRetencionId(conceptoComprasDefault ? conceptoComprasDefault.id : '');
        setManejaLotes(false);
        setDiasVidaUtil('');
      }
      setShowCategoriaInput(false);
      setNuevaCategoria('');
    }
  }, [open, producto, resetCrear, resetActualizar, conceptosRetencion]);

  // Mutación para crear categoría
  const crearCategoriaMutation = useMutation({
    mutationFn: (nombre: string) => categoriasApi.create({ nombre }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categorias'] });
      enqueueSnackbar('Categoría creada exitosamente', { variant: 'success' });
      setShowCategoriaInput(false);
      setNuevaCategoria('');
    },
    onError: (error: any) => {
      const mensaje =
        error.message || 'Error al crear la categoría';
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  // Mutación para crear/actualizar producto
  const mutation = useMutation({
    mutationFn: async (data: CrearProductoFormData | ActualizarProductoFormData) => {
      if (isEdit) {
        const updateData: ActualizarProductoDTO = {
          ...(data as ActualizarProductoFormData),
          precioVenta: 0, // Precio de venta se maneja por sucursal
          conceptoRetencionId: conceptoRetencionId || undefined,
          manejaLotes,
          diasVidaUtil: diasVidaUtil !== '' ? diasVidaUtil : undefined,
        };
        await productosApi.update(producto!.id, updateData);
        return null;
      } else {
        const createData: CrearProductoDTO = {
          ...(data as CrearProductoFormData),
          precioVenta: 0, // Precio de venta se maneja por sucursal
          conceptoRetencionId: conceptoRetencionId || undefined,
          manejaLotes,
          diasVidaUtil: diasVidaUtil !== '' ? diasVidaUtil : undefined,
        };
        return await productosApi.create(createData);
      }
    },
    onSuccess: () => {
      setBackendError(null);
      queryClient.invalidateQueries({ queryKey: ['productos'] });
      enqueueSnackbar(
        `Producto ${isEdit ? 'actualizado' : 'creado'} exitosamente`,
        { variant: 'success' }
      );
      onSuccess();
      onClose();
    },
    onError: (error: any) => {
      const mensaje =
        error.message ||
        `Error al ${isEdit ? 'actualizar' : 'crear'} el producto`;
      setBackendError(mensaje);
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmitCrear = (data: CrearProductoFormData) => {
    mutation.mutate(data);
  };

  const onSubmitActualizar = (data: ActualizarProductoFormData) => {
    mutation.mutate(data);
  };

  const handleCrearCategoria = () => {
    if (nuevaCategoria.trim()) {
      crearCategoriaMutation.mutate(nuevaCategoria.trim());
    }
  };

  // const errors = isEdit ? errorsActualizar : errorsCrear; // TODO: usar para mostrar errores

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        {isEdit ? 'Editar Producto' : 'Nuevo Producto'}
      </DialogTitle>

      <form onSubmit={isEdit ? handleSubmitActualizar(onSubmitActualizar) : handleSubmitCrear(onSubmitCrear)}>
        <DialogContent>
          {backendError && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setBackendError(null)}>
              {backendError}
            </Alert>
          )}

          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr', gap: 2 }}>
            {/* Código de Barras - SOLO en creación */}
            {!isEdit && (
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
                    autoFocus
                  />
                )}
              />
            )}

            {/* Mostrar código de barras en modo solo lectura cuando se edita */}
            {isEdit && (
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

            {/* Categoría - SOLO en creación */}
            {!isEdit && (
              <>
                {!showCategoriaInput ? (
                  <Box sx={{ display: 'flex', gap: 1 }}>
                    <Controller
                      name="categoriaId"
                      control={controlCrear}
                      render={({ field: { value, onChange, ...field } }) => (
                        <FormControl fullWidth error={!!errorsCrear.categoriaId}>
                          <InputLabel>Categoría *</InputLabel>
                          <Select
                            {...field}
                            value={value || ''}
                            onChange={(e) => onChange(Number(e.target.value))}
                            label="Categoría *"
                          >
                            <MenuItem value="">
                              <em>Selecciona una categoría</em>
                            </MenuItem>
                            {categorias
                              .sort((a, b) => a.rutaCompleta.localeCompare(b.rutaCompleta))
                              .map((cat) => (
                                <MenuItem
                                  key={cat.id}
                                  value={cat.id}
                                  sx={{ pl: cat.nivel * 2 + 2 }}
                                >
                                  {cat.rutaCompleta}
                                </MenuItem>
                              ))}
                          </Select>
                          {errorsCrear.categoriaId && (
                            <Box component="span" sx={{ color: 'error.main', fontSize: '0.75rem', mt: 0.5 }}>
                              {errorsCrear.categoriaId.message}
                            </Box>
                          )}
                        </FormControl>
                      )}
                    />
                    <Tooltip title="Crear nueva categoría">
                      <IconButton
                        onClick={() => setShowCategoriaInput(true)}
                        color="primary"
                      >
                        <AddIcon />
                      </IconButton>
                    </Tooltip>
                  </Box>
                ) : (
                  <Box sx={{ display: 'flex', gap: 1 }}>
                    <TextField
                      label="Nueva Categoría"
                      value={nuevaCategoria}
                      onChange={(e) => setNuevaCategoria(e.target.value)}
                      fullWidth
                      autoFocus
                    />
                    <Button
                      onClick={handleCrearCategoria}
                      variant="contained"
                      disabled={
                        !nuevaCategoria.trim() || crearCategoriaMutation.isPending
                      }
                    >
                      Crear
                    </Button>
                    <Button onClick={() => setShowCategoriaInput(false)}>
                      Cancelar
                    </Button>
                  </Box>
                )}
              </>
            )}

            {/* Mostrar categoría en modo solo lectura cuando se edita */}
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
                    value={value}
                    onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                    error={!!errorsActualizar.precioCosto}
                    helperText={errorsActualizar.precioCosto?.message}
                    fullWidth
                    InputProps={{
                      startAdornment: (
                        <InputAdornment position="start">$</InputAdornment>
                      ),
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
                    value={value}
                    onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                    error={!!errorsCrear.precioCosto}
                    helperText={errorsCrear.precioCosto?.message || "Los precios de venta se configuran por sucursal"}
                    fullWidth
                    InputProps={{
                      startAdornment: (
                        <InputAdornment position="start">$</InputAdornment>
                      ),
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

            {/* Concepto de Retencion DIAN */}
            <FormControl fullWidth>
              <InputLabel id="concepto-retencion-label">Concepto Retencion DIAN</InputLabel>
              <Select
                labelId="concepto-retencion-label"
                label="Concepto Retencion DIAN"
                value={conceptoRetencionId}
                onChange={(e) => setConceptoRetencionId(e.target.value as number | '')}
              >
                <MenuItem value="">
                  <em>Sin concepto (aplican todas las reglas)</em>
                </MenuItem>
                {conceptosRetencion.filter(c => c.activo).map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.codigoDian ? `${c.codigoDian} - ` : ''}{c.nombre}{c.porcentajeSugerido != null ? ` (${c.porcentajeSugerido}%)` : ''}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {/* Manejo de Lotes */}
            <FormControlLabel
              control={
                <Switch
                  checked={manejaLotes}
                  onChange={(e) => { setManejaLotes(e.target.checked); if (!e.target.checked) setDiasVidaUtil(''); }}
                  color="primary"
                />
              }
              label={
                <Box>
                  <Box component="span" sx={{ fontWeight: 500 }}>Maneja lotes y vencimiento</Box>
                  <Box component="span" sx={{ display: 'block', fontSize: '0.75rem', color: 'text.secondary' }}>
                    Activa el control FEFO por número de lote y fecha de vencimiento
                  </Box>
                </Box>
              }
            />

            {/* Plazo de vencimiento — solo visible cuando manejaLotes = true */}
            {manejaLotes && (
              <TextField
                type="number"
                label="Plazo de vencimiento (días)"
                value={diasVidaUtil}
                onChange={(e) => {
                  const val = parseInt(e.target.value);
                  setDiasVidaUtil(isNaN(val) || val <= 0 ? '' : val);
                }}
                fullWidth
                inputProps={{ min: 1 }}
                helperText="Vida útil en días. Al recibir un lote sin fecha explícita, se calcula automáticamente."
              />
            )}

            <Alert severity="info">
              Los precios de venta se configuran en el modulo de <strong>Precios por Sucursal</strong>.
              El sistema calculara automaticamente el precio usando el margen de la categoria si no se define un precio especifico.
            </Alert>
          </Box>

          {mutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {mutation.error instanceof Error
                ? mutation.error.message
                : 'Error al procesar la solicitud'}
            </Alert>
          )}
        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose} disabled={mutation.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={mutation.isPending}
          >
            {mutation.isPending
              ? 'Guardando...'
              : isEdit
              ? 'Actualizar'
              : 'Crear'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
