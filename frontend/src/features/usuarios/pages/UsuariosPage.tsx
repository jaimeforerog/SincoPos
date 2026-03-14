import { useState } from 'react';
import {
  Container,
  Box,
  TextField,
  InputAdornment,
  MenuItem,
  Select,
  FormControl,
  InputLabel,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  IconButton,
  Tooltip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Alert,
  CircularProgress,
  Checkbox,
  FormControlLabel,
  FormGroup,
  alpha,
} from '@mui/material';

const HERO_COLOR = '#283593';
import {
  Search,
  StoreMallDirectory,
  CheckCircle,
  Cancel,
  Domain,
  Edit,
  Add,
} from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { usuariosApi, type UsuarioDto } from '@/api/usuarios';
import { sucursalesApi } from '@/api/sucursales';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { useAuthStore } from '@/stores/auth.store';
import { useAuth } from '@/hooks/useAuth';
import type { SucursalDTO } from '@/types/api';
import { CrearUsuarioDialog } from '../components/CrearUsuarioDialog';
import { EditarUsuarioDialog } from '../components/EditarUsuarioDialog';

const ROL_LABELS: Record<string, { label: string; color: 'error' | 'warning' | 'info' | 'success' }> = {
  admin:      { label: 'Admin',      color: 'error' },
  supervisor: { label: 'Supervisor', color: 'warning' },
  cajero:     { label: 'Cajero',     color: 'info' },
  vendedor:   { label: 'Vendedor',   color: 'success' },
};

const ROLES_FILTRO = ['admin', 'supervisor', 'cajero', 'vendedor'];

function formatFecha(fecha?: string): string {
  if (!fecha) return '—';
  return new Date(fecha).toLocaleString('es-CO', {
    dateStyle: 'short',
    timeStyle: 'short',
  });
}

// ─── Diálogo Asignar Sucursal Default ────────────────────────────────────────
interface AsignarSucursalDialogProps {
  usuario: UsuarioDto | null;
  sucursales: SucursalDTO[];
  onClose: () => void;
  onConfirm: (sucursalId: number) => void;
  loading: boolean;
}

function AsignarSucursalDialog({ usuario, sucursales, onClose, onConfirm, loading }: AsignarSucursalDialogProps) {
  const [sucursalId, setSucursalId] = useState<number | ''>(usuario?.sucursalDefaultId ?? '');

  if (!usuario) return null;

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Asignar Sucursal Default</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Usuario: <strong>{usuario.nombreCompleto}</strong> ({usuario.email})
        </Typography>
        <FormControl fullWidth>
          <InputLabel id="sucursal-label">Sucursal</InputLabel>
          <Select
            labelId="sucursal-label"
            label="Sucursal"
            value={sucursalId}
            onChange={(e) => setSucursalId(e.target.value as number)}
          >
            {sucursales.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.nombre}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>Cancelar</Button>
        <Button
          variant="contained"
          onClick={() => sucursalId !== '' && onConfirm(sucursalId as number)}
          disabled={sucursalId === '' || loading}
        >
          {loading ? <CircularProgress size={20} /> : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── Diálogo Asignar Múltiples Sucursales ────────────────────────────────────
interface AsignarSucursalesDialogProps {
  usuario: UsuarioDto | null;
  sucursales: SucursalDTO[];
  onClose: () => void;
  onConfirm: (sucursalIds: number[]) => void;
  loading: boolean;
}

function AsignarSucursalesDialog({ usuario, sucursales, onClose, onConfirm, loading }: AsignarSucursalesDialogProps) {
  const [selected, setSelected] = useState<Set<number>>(
    () => new Set(usuario?.sucursalesAsignadas?.map(s => s.id) ?? [])
  );

  if (!usuario) return null;

  const toggle = (id: number) => {
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Sucursales asignadas</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Usuario: <strong>{usuario.nombreCompleto}</strong> ({usuario.email})
        </Typography>
        <FormGroup>
          {sucursales.map((s) => (
            <FormControlLabel
              key={s.id}
              control={
                <Checkbox
                  checked={selected.has(s.id)}
                  onChange={() => toggle(s.id)}
                />
              }
              label={
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  {s.nombre}
                  {s.id === usuario.sucursalDefaultId && (
                    <Chip label="default" size="small" variant="outlined" />
                  )}
                </Box>
              }
            />
          ))}
        </FormGroup>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>Cancelar</Button>
        <Button
          variant="contained"
          onClick={() => onConfirm(Array.from(selected))}
          disabled={loading}
        >
          {loading ? <CircularProgress size={20} /> : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── Diálogo Cambiar Estado ───────────────────────────────────────────────────
interface CambiarEstadoDialogProps {
  usuario: UsuarioDto | null;
  onClose: () => void;
  onConfirm: (motivo: string) => void;
  loading: boolean;
}

function CambiarEstadoDialog({ usuario, onClose, onConfirm, loading }: CambiarEstadoDialogProps) {
  const [motivo, setMotivo] = useState('');

  if (!usuario) return null;
  const accion = usuario.activo ? 'desactivar' : 'activar';

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ textTransform: 'capitalize' }}>{accion} usuario</DialogTitle>
      <DialogContent>
        <Alert severity={usuario.activo ? 'warning' : 'info'} sx={{ mb: 2 }}>
          ¿Seguro que deseas {accion} a <strong>{usuario.nombreCompleto}</strong>?
        </Alert>
        <TextField
          fullWidth
          label="Motivo (opcional)"
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          multiline
          rows={2}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={loading}>Cancelar</Button>
        <Button
          variant="contained"
          color={usuario.activo ? 'error' : 'success'}
          onClick={() => onConfirm(motivo)}
          disabled={loading}
        >
          {loading ? <CircularProgress size={20} /> : `Confirmar`}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── Página principal ─────────────────────────────────────────────────────────
export function UsuariosPage() {
  const queryClient = useQueryClient();
  const { enqueueSnackbar } = useSnackbar();
  const { user: currentUser, setUser } = useAuthStore();
  const { isAdmin } = useAuth();

  // Filtros
  const [busqueda, setBusqueda] = useState('');
  const [rolFiltro, setRolFiltro] = useState('');
  const [activoFiltro, setActivoFiltro] = useState<'' | 'true' | 'false'>('');

  // Diálogos
  const [usuarioSucursal, setUsuarioSucursal] = useState<UsuarioDto | null>(null);
  const [usuarioSucursales, setUsuarioSucursales] = useState<UsuarioDto | null>(null);
  const [usuarioEstado, setUsuarioEstado] = useState<UsuarioDto | null>(null);
  const [crearDialogOpen, setCrearDialogOpen] = useState(false);
  const [usuarioEditar, setUsuarioEditar] = useState<UsuarioDto | null>(null);

  // Queries
  const { data: usuarios = [], isLoading, error } = useQuery({
    queryKey: ['usuarios', busqueda, rolFiltro, activoFiltro],
    queryFn: () => usuariosApi.listar({
      busqueda: busqueda || undefined,
      rol: rolFiltro || undefined,
      activo: activoFiltro !== '' ? activoFiltro === 'true' : undefined,
    }),
  });

  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.listar(),
  });

  // Mutations
  const mutSucursal = useMutation({
    mutationFn: ({ id, sucursalId }: { id: number; sucursalId: number }) =>
      usuariosApi.actualizarSucursal(id, sucursalId),
    onSuccess: async (_, { id }) => {
      enqueueSnackbar('Sucursal asignada correctamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['usuarios'] });
      setUsuarioSucursal(null);
      if (currentUser && String(id) === currentUser.id) {
        const perfil = await usuariosApi.me();
        setUser(perfil);
      }
    },
    onError: () => {
      enqueueSnackbar('Error al asignar sucursal', { variant: 'error' });
    },
  });

  const mutSucursales = useMutation({
    mutationFn: ({ id, sucursalIds }: { id: number; sucursalIds: number[] }) =>
      usuariosApi.asignarSucursales(id, sucursalIds),
    onSuccess: async (_, { id }) => {
      enqueueSnackbar('Sucursales asignadas correctamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['usuarios'] });
      setUsuarioSucursales(null);
      if (currentUser && String(id) === currentUser.id) {
        const perfil = await usuariosApi.me();
        setUser(perfil);
      }
    },
    onError: () => {
      enqueueSnackbar('Error al asignar sucursales', { variant: 'error' });
    },
  });

  const mutEstado = useMutation({
    mutationFn: ({ id, activo, motivo }: { id: number; activo: boolean; motivo?: string }) =>
      usuariosApi.cambiarEstado(id, activo, motivo),
    onSuccess: () => {
      enqueueSnackbar('Estado actualizado correctamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['usuarios'] });
      setUsuarioEstado(null);
    },
    onError: () => {
      enqueueSnackbar('Error al cambiar estado', { variant: 'error' });
    },
  });

  if (error) {
    const err = error as { statusCode?: number; message?: string };
    return (
      <Container maxWidth="xl">
        <Alert severity="error" sx={{ mt: 3 }}>
          Error al cargar usuarios
          {err?.statusCode ? ` — HTTP ${err.statusCode}` : ''}
          {err?.message ? `: ${err.message}` : ''}
        </Alert>
      </Container>
    );
  }

  return (
    <Container maxWidth="xl">
      <ReportePageHeader
        title="Usuarios"
        subtitle="Gestión de usuarios del sistema y asignación de roles"
        breadcrumbs={[{ label: 'Configuración', path: '/configuracion' }, { label: 'Usuarios' }]}
        backPath="/configuracion"
        color="#283593"
        action={
          isAdmin() ? (
            <Button
              variant="contained"
              startIcon={<Add />}
              onClick={() => setCrearDialogOpen(true)}
              sx={{
                bgcolor: 'rgba(255,255,255,0.15)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.35)',
                fontWeight: 700,
                '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              }}
            >
              Nuevo Usuario
            </Button>
          ) : undefined
        }
      />

      {/* Filtros */}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <TextField
          placeholder="Buscar por nombre o email…"
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
          size="small"
          sx={{ minWidth: 280 }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <Search fontSize="small" />
              </InputAdornment>
            ),
          }}
        />

        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="rol-filtro-label">Rol</InputLabel>
          <Select labelId="rol-filtro-label" label="Rol" value={rolFiltro} onChange={(e) => setRolFiltro(e.target.value)}>
            <MenuItem value="">Todos</MenuItem>
            {ROLES_FILTRO.map((r) => (
              <MenuItem key={r} value={r} sx={{ textTransform: 'capitalize' }}>{r}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="estado-filtro-label">Estado</InputLabel>
          <Select labelId="estado-filtro-label" label="Estado" value={activoFiltro} onChange={(e) => setActivoFiltro(e.target.value as '' | 'true' | 'false')}>
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="true">Activos</MenuItem>
            <MenuItem value="false">Inactivos</MenuItem>
          </Select>
        </FormControl>
      </Box>

      {/* Tabla */}
      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow
              sx={{
                background: `linear-gradient(90deg, ${alpha(HERO_COLOR, 0.08)} 0%, ${alpha(HERO_COLOR, 0.04)} 100%)`,
                '& .MuiTableCell-head': {
                  color: HERO_COLOR, fontWeight: 700,
                  fontSize: '0.75rem', textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                  borderBottom: `2px solid ${alpha(HERO_COLOR, 0.2)}`,
                },
              }}
            >
              <TableCell>Nombre</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Rol</TableCell>
              <TableCell>Sucursales</TableCell>
              <TableCell>Estado</TableCell>
              <TableCell>Último acceso</TableCell>
              <TableCell align="center">Acciones</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4 }}>
                  <CircularProgress size={32} />
                </TableCell>
              </TableRow>
            ) : usuarios.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                  No se encontraron usuarios
                </TableCell>
              </TableRow>
            ) : (
              usuarios.map((u) => {
                const rolInfo = ROL_LABELS[u.rol.toLowerCase()] ?? { label: u.rol, color: 'default' as const };
                const sucursalesAsignadas = u.sucursalesAsignadas ?? [];
                const visibles = sucursalesAsignadas.slice(0, 2);
                const extra = sucursalesAsignadas.length - 2;
                return (
                  <TableRow key={u.id} hover>
                    <TableCell sx={{ fontWeight: 500 }}>{u.nombreCompleto}</TableCell>
                    <TableCell>{u.email}</TableCell>
                    <TableCell>
                      <Chip label={rolInfo.label} color={rolInfo.color} size="small" />
                    </TableCell>
                    <TableCell>
                      <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                        {sucursalesAsignadas.length === 0 ? (
                          <Typography variant="caption" color="text.disabled">Sin sucursal</Typography>
                        ) : (
                          <>
                            {visibles.map(s => (
                              <Chip
                                key={s.id}
                                label={s.nombre}
                                size="small"
                                variant={s.id === u.sucursalDefaultId ? 'filled' : 'outlined'}
                                color={s.id === u.sucursalDefaultId ? 'primary' : 'default'}
                              />
                            ))}
                            {extra > 0 && (
                              <Chip label={`+${extra}`} size="small" variant="outlined" />
                            )}
                          </>
                        )}
                      </Box>
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={u.activo ? 'Activo' : 'Inactivo'}
                        color={u.activo ? 'success' : 'default'}
                        size="small"
                        variant="outlined"
                      />
                    </TableCell>
                    <TableCell sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
                      {formatFecha(u.ultimoAcceso)}
                    </TableCell>
                    <TableCell align="center">
                      {isAdmin() && (
                        <Tooltip title="Editar usuario">
                          <IconButton size="small" onClick={() => setUsuarioEditar(u)}>
                            <Edit fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                      <Tooltip title="Asignar sucursal default">
                        <IconButton size="small" onClick={() => setUsuarioSucursal(u)}>
                          <StoreMallDirectory fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Gestionar sucursales asignadas">
                        <IconButton size="small" onClick={() => setUsuarioSucursales(u)}>
                          <Domain fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={u.activo ? 'Desactivar usuario' : 'Activar usuario'}>
                        <IconButton
                          size="small"
                          color={u.activo ? 'error' : 'success'}
                          onClick={() => setUsuarioEstado(u)}
                        >
                          {u.activo ? <Cancel fontSize="small" /> : <CheckCircle fontSize="small" />}
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Diálogo asignar sucursal default */}
      {usuarioSucursal && (
        <AsignarSucursalDialog
          usuario={usuarioSucursal}
          sucursales={sucursales}
          onClose={() => setUsuarioSucursal(null)}
          onConfirm={(sucursalId) => mutSucursal.mutate({ id: usuarioSucursal.id, sucursalId })}
          loading={mutSucursal.isPending}
        />
      )}

      {/* Diálogo gestionar múltiples sucursales */}
      {usuarioSucursales && (
        <AsignarSucursalesDialog
          usuario={usuarioSucursales}
          sucursales={sucursales}
          onClose={() => setUsuarioSucursales(null)}
          onConfirm={(sucursalIds) => mutSucursales.mutate({ id: usuarioSucursales.id, sucursalIds })}
          loading={mutSucursales.isPending}
        />
      )}

      {/* Diálogo cambiar estado */}
      {usuarioEstado && (
        <CambiarEstadoDialog
          usuario={usuarioEstado}
          onClose={() => setUsuarioEstado(null)}
          onConfirm={(motivo) => mutEstado.mutate({ id: usuarioEstado.id, activo: !usuarioEstado.activo, motivo })}
          loading={mutEstado.isPending}
        />
      )}

      {/* Diálogo crear usuario */}
      <CrearUsuarioDialog
        open={crearDialogOpen}
        onClose={() => setCrearDialogOpen(false)}
      />

      {/* Diálogo editar usuario */}
      <EditarUsuarioDialog
        open={!!usuarioEditar}
        usuario={usuarioEditar}
        onClose={() => setUsuarioEditar(null)}
      />
    </Container>
  );
}
