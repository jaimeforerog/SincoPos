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
  Divider,
  CircularProgress,
} from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import LockResetIcon from '@mui/icons-material/LockReset';
import { useSnackbar } from 'notistack';
import { usuariosApi, type UsuarioDto } from '@/api/usuarios';
import { sucursalesApi } from '@/api/sucursales';
import { useAuthStore } from '@/stores/auth.store';

const ROLES = ['admin', 'supervisor', 'cajero', 'vendedor'] as const;

const editarUsuarioSchema = z.object({
  nombreCompleto: z.string().min(1, 'Nombre completo es requerido').max(200, 'Maximo 200 caracteres'),
  telefono: z.string().optional(),
  rol: z.string().min(1, 'Rol es requerido'),
  sucursalDefaultId: z.number().optional(),
  sucursalIds: z.array(z.number()).optional(),
});

type EditarUsuarioFormData = z.infer<typeof editarUsuarioSchema>;

interface EditarUsuarioDialogProps {
  open: boolean;
  usuario: UsuarioDto | null;
  onClose: () => void;
}

export function EditarUsuarioDialog({ open, usuario, onClose }: EditarUsuarioDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const [backendError, setBackendError] = useState<string | null>(null);
  const [tempPassword, setTempPassword] = useState<string | null>(null);
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
    formState: { errors },
  } = useForm<EditarUsuarioFormData>({
    resolver: zodResolver(editarUsuarioSchema),
    defaultValues: {
      nombreCompleto: '',
      telefono: '',
      rol: '',
      sucursalDefaultId: undefined,
      sucursalIds: [],
    },
  });

  // Reset form when usuario changes
  useEffect(() => {
    if (open && usuario) {
      reset({
        nombreCompleto: usuario.nombreCompleto,
        telefono: usuario.telefono ?? '',
        rol: usuario.rol.toLowerCase(),
        sucursalDefaultId: usuario.sucursalDefaultId ?? undefined,
        sucursalIds: usuario.sucursalesAsignadas?.map((s) => s.id) ?? [],
      });
      setBackendError(null);
      setTempPassword(null);
    }
  }, [open, usuario]); // eslint-disable-line react-hooks/exhaustive-deps

  const mutation = useMutation({
    mutationFn: (data: EditarUsuarioFormData) =>
      usuariosApi.actualizar(usuario!.id, {
        nombreCompleto: data.nombreCompleto,
        telefono: data.telefono || undefined,
        rol: data.rol,
        sucursalDefaultId: data.sucursalDefaultId,
        sucursalIds: data.sucursalIds?.length ? data.sucursalIds : undefined,
      }),
    onSuccess: () => {
      setBackendError(null);
      queryClient.invalidateQueries({ queryKey: ['usuarios'] });
      enqueueSnackbar('Usuario actualizado exitosamente', { variant: 'success' });
      handleClose();
    },
    onError: (error: any) => {
      const mensaje = error?.message || 'Error al actualizar el usuario';
      setBackendError(mensaje);
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: () => usuariosApi.resetPassword(usuario!.id),
    onSuccess: (result) => {
      setTempPassword(result.passwordTemporal);
      enqueueSnackbar('Contrasena reseteada exitosamente', { variant: 'success' });
    },
    onError: (error: any) => {
      const mensaje = error?.message || 'Error al resetear la contrasena';
      enqueueSnackbar(mensaje, { variant: 'error' });
    },
  });

  const onSubmit = (data: EditarUsuarioFormData) => {
    setBackendError(null);
    mutation.mutate(data);
  };

  const handleClose = () => {
    reset({
      nombreCompleto: '',
      telefono: '',
      rol: '',
      sucursalDefaultId: undefined,
      sucursalIds: [],
    });
    setBackendError(null);
    setTempPassword(null);
    onClose();
  };

  const handleCopyPassword = () => {
    if (tempPassword) {
      navigator.clipboard.writeText(tempPassword);
      enqueueSnackbar('Contrasena copiada al portapapeles', { variant: 'info' });
    }
  };

  if (!usuario) return null;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Editar Usuario</DialogTitle>
      <form onSubmit={handleSubmit(onSubmit)}>
        <DialogContent>
          {backendError && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setBackendError(null)}>
              {backendError}
            </Alert>
          )}

          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            {/* Email (read-only) */}
            <TextField
              label="Email"
              value={usuario.email}
              fullWidth
              InputProps={{ readOnly: true }}
              disabled
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
                  <InputLabel id="edit-sucursal-default-label">Sucursal default</InputLabel>
                  <Select
                    {...field}
                    labelId="edit-sucursal-default-label"
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

            {/* Reset password section */}
            <Divider sx={{ my: 1 }} />
            <Box>
              <Typography variant="subtitle2" sx={{ mb: 1 }}>
                Contrasena
              </Typography>
              <Button
                variant="outlined"
                color="warning"
                startIcon={resetPasswordMutation.isPending ? <CircularProgress size={16} /> : <LockResetIcon />}
                onClick={() => resetPasswordMutation.mutate()}
                disabled={resetPasswordMutation.isPending}
              >
                Resetear contrasena
              </Button>
              {tempPassword && (
                <Box sx={{ mt: 2 }}>
                  <TextField
                    fullWidth
                    label="Contrasena temporal"
                    value={tempPassword}
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
            {mutation.isPending ? 'Guardando...' : 'Guardar Cambios'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
