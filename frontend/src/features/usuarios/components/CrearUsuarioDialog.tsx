import { useState, useEffect } from 'react';
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
  MenuItem,
  Box,
  Typography,
  Alert,
  Checkbox,
  FormControlLabel,
  FormGroup,
  FormControl,
  InputLabel,
  Select,
  IconButton,
  InputAdornment,
} from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { useSnackbar } from 'notistack';
import { usuariosApi, type CrearUsuarioResult } from '@/api/usuarios';
import { sucursalesApi } from '@/api/sucursales';
import { useAuthStore } from '@/stores/auth.store';
import type { ApiError } from '@/types/api';

const ROLES = ['admin', 'supervisor', 'cajero', 'vendedor'] as const;

const crearUsuarioSchema = z.object({
  email: z.string().min(1, 'Email es requerido').email('Email no es valido'),
  nombreCompleto: z.string().min(1, 'Nombre completo es requerido').max(200, 'Maximo 200 caracteres'),
  telefono: z.string().optional(),
  rol: z.string().min(1, 'Rol es requerido'),
  sucursalDefaultId: z.number().optional(),
  sucursalIds: z.array(z.number()).optional(),
});

type CrearUsuarioFormData = z.infer<typeof crearUsuarioSchema>;

interface CrearUsuarioDialogProps {
  open: boolean;
  onClose: () => void;
}

export function CrearUsuarioDialog({ open, onClose }: CrearUsuarioDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [backendError, setBackendError] = useState<string | null>(null);
  const [resultado, setResultado] = useState<CrearUsuarioResult | null>(null);
  const { activeEmpresaId } = useAuthStore();

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.listar(),
    staleTime: 0,
    enabled: open,
  });

  const sucursales = todasSucursales.filter(
    (s) => activeEmpresaId == null || s.empresaId === activeEmpresaId
  );

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<CrearUsuarioFormData>({
    resolver: zodResolver(crearUsuarioSchema),
    defaultValues: {
      email: '',
      nombreCompleto: '',
      telefono: '',
      rol: '',
      sucursalDefaultId: undefined,
      sucursalIds: [],
    },
  });

  const selectedSucursalIds = watch('sucursalIds') ?? [];

  const mutation = useMutation({
    mutationFn: (data: CrearUsuarioFormData) =>
      usuariosApi.crear({
        email: data.email,
        nombreCompleto: data.nombreCompleto,
        telefono: data.telefono || undefined,
        rol: data.rol,
        sucursalDefaultId: data.sucursalDefaultId,
        sucursalIds: data.sucursalIds?.length ? data.sucursalIds : undefined,
      }),
    onSuccess: (result) => {
      setBackendError(null);
      queryClient.invalidateQueries({ queryKey: ['usuarios'] });
      enqueueSnackbar('Usuario creado exitosamente', { variant: 'success' });
      setResultado(result);
    },
    onError: (error: ApiError) => {
      const mensaje = error?.message || 'Error al crear el usuario';
      setBackendError(mensaje);
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmit = (data: CrearUsuarioFormData) => {
    setBackendError(null);
    mutation.mutate(data);
  };

  const handleClose = () => {
    reset({
      email: '',
      nombreCompleto: '',
      telefono: '',
      rol: '',
      sucursalDefaultId: undefined,
      sucursalIds: [],
    });
    setBackendError(null);
    setResultado(null);
    onClose();
  };

  const handleCopyPassword = () => {
    if (resultado?.passwordTemporal) {
      navigator.clipboard.writeText(resultado.passwordTemporal);
      enqueueSnackbar('Contrasena copiada al portapapeles', { variant: 'info' });
    }
  };

  useEffect(() => {
    if (open) {
      reset({
        email: '',
        nombreCompleto: '',
        telefono: '',
        rol: '',
        sucursalDefaultId: undefined,
        sucursalIds: [],
      });
      setBackendError(null);
      setResultado(null);
    }
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  // ─── Dialogo de contrasena temporal ───────────────────────────────────────
  if (resultado) {
    return (
      <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
        <DialogTitle>Usuario creado exitosamente</DialogTitle>
        <DialogContent>
          <Alert severity="success" sx={{ mb: 2 }}>
            El usuario <strong>{resultado.email}</strong> ha sido creado con el rol <strong>{resultado.rol}</strong>.
          </Alert>
          {resultado.passwordTemporal && (
            <Box sx={{ mt: 2 }}>
              <Typography variant="subtitle2" gutterBottom>
                Contrasena temporal:
              </Typography>
              <TextField
                fullWidth
                value={resultado.passwordTemporal}
                InputProps={{
                  readOnly: true,
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton onClick={handleCopyPassword} edge="end" title="Copiar">
                        <ContentCopyIcon />
                      </IconButton>
                    </InputAdornment>
                  ),
                }}
                sx={{ fontFamily: 'monospace' }}
              />
              <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
                Comparta esta contrasena con el usuario. Solo se muestra una vez.
              </Typography>
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={handleClose}>
            Cerrar
          </Button>
        </DialogActions>
      </Dialog>
    );
  }

  // ─── Formulario de creacion ───────────────────────────────────────────────
  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Nuevo Usuario</DialogTitle>
      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          {backendError && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setBackendError(null)}>
              {backendError}
            </Alert>
          )}

          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            {/* Email */}
            <Controller
              name="email"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Email *"
                  type="email"
                  error={!!errors.email}
                  helperText={errors.email?.message}
                  fullWidth
                />
              )}
            />

            {/* Nombre completo */}
            <Controller
              name="nombreCompleto"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Nombre completo *"
                  error={!!errors.nombreCompleto}
                  helperText={errors.nombreCompleto?.message}
                  fullWidth
                />
              )}
            />

            {/* Telefono */}
            <Controller
              name="telefono"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Telefono"
                  fullWidth
                />
              )}
            />

            {/* Rol */}
            <Controller
              name="rol"
              control={control}
              render={({ field: { value, onChange, ...field } }) => (
                <TextField
                  {...field}
                  select
                  label="Rol *"
                  value={value || ''}
                  onChange={onChange}
                  error={!!errors.rol}
                  helperText={errors.rol?.message}
                  fullWidth
                >
                  <MenuItem value="">Seleccione un rol</MenuItem>
                  {ROLES.map((r) => (
                    <MenuItem key={r} value={r} sx={{ textTransform: 'capitalize' }}>
                      {r}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />

            {/* Sucursal default */}
            <Controller
              name="sucursalDefaultId"
              control={control}
              render={({ field: { value, onChange, ...field } }) => (
                <FormControl fullWidth>
                  <InputLabel id="sucursal-default-label">Sucursal default</InputLabel>
                  <Select
                    {...field}
                    labelId="sucursal-default-label"
                    label="Sucursal default"
                    value={value ?? ''}
                    onChange={(e) => {
                      const val = e.target.value;
                      onChange(val === '' ? undefined : Number(val));
                    }}
                  >
                    <MenuItem value="">Ninguna</MenuItem>
                    {sucursales.map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        {s.nombre}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />

            {/* Sucursales asignadas */}
            {sucursales.length > 0 && (
              <Box>
                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                  Sucursales asignadas
                </Typography>
                <Controller
                  name="sucursalIds"
                  control={control}
                  render={({ field: { value = [], onChange } }) => (
                    <FormGroup>
                      {sucursales.map((s) => (
                        <FormControlLabel
                          key={s.id}
                          control={
                            <Checkbox
                              checked={value.includes(s.id)}
                              onChange={(e) => {
                                if (e.target.checked) {
                                  onChange([...value, s.id]);
                                } else {
                                  onChange(value.filter((id: number) => id !== s.id));
                                }
                              }}
                            />
                          }
                          label={s.nombre}
                        />
                      ))}
                    </FormGroup>
                  )}
                />
              </Box>
            )}
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
            {mutation.isPending ? 'Creando...' : 'Crear Usuario'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
