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
  Button,
  Alert,
} from '@mui/material';
import { Search, Add } from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { usuariosApi, type UsuarioDto } from '@/api/usuarios';
import { sucursalesApi } from '@/api/sucursales';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { useAuthStore } from '@/stores/auth.store';
import { useAuth } from '@/hooks/useAuth';
import { CrearUsuarioDialog } from '../components/CrearUsuarioDialog';
import { EditarUsuarioDialog } from '../components/EditarUsuarioDialog';
import { AsignarSucursalDialog } from '../components/AsignarSucursalDialog';
import { AsignarSucursalesDialog } from '../components/AsignarSucursalesDialog';
import { CambiarEstadoDialog } from '../components/CambiarEstadoDialog';
import { UsuariosTable } from '../components/UsuariosTable';

const ROLES_FILTRO = ['admin', 'supervisor', 'cajero', 'vendedor'];

export function UsuariosPage() {
  const queryClient = useQueryClient();
  const { enqueueSnackbar } = useSnackbar();
  const { user: currentUser, setUser, activeEmpresaId } = useAuthStore();
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

  const { data: usuarios = [], isLoading, error } = useQuery({
    queryKey: ['usuarios', busqueda, rolFiltro, activoFiltro],
    queryFn: () => usuariosApi.listar({
      busqueda: busqueda || undefined,
      rol: rolFiltro || undefined,
      activo: activoFiltro !== '' ? activoFiltro === 'true' : undefined,
    }),
  });

  const { data: todasSucursalesPage = [] } = useQuery({
    queryKey: ['sucursales', activeEmpresaId],
    queryFn: () => sucursalesApi.listar(),
    staleTime: 0,
  });

  const sucursales = todasSucursalesPage.filter(
    (s) => activeEmpresaId == null || s.empresaId === activeEmpresaId
  );

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
    onError: () => enqueueSnackbar('Error al asignar sucursal', { variant: 'error' }),
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
    onError: () => enqueueSnackbar('Error al asignar sucursales', { variant: 'error' }),
  });

  const mutEstado = useMutation({
    mutationFn: ({ id, activo, motivo }: { id: number; activo: boolean; motivo?: string }) =>
      usuariosApi.cambiarEstado(id, activo, motivo),
    onSuccess: () => {
      enqueueSnackbar('Estado actualizado correctamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['usuarios'] });
      setUsuarioEstado(null);
    },
    onError: () => enqueueSnackbar('Error al cambiar estado', { variant: 'error' }),
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
        color="#1565c0"
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

      <UsuariosTable
        usuarios={usuarios}
        isLoading={isLoading}
        isAdmin={isAdmin()}
        onEditar={setUsuarioEditar}
        onAsignarSucursal={setUsuarioSucursal}
        onAsignarSucursales={setUsuarioSucursales}
        onCambiarEstado={setUsuarioEstado}
      />

      {usuarioSucursal && (
        <AsignarSucursalDialog
          usuario={usuarioSucursal}
          sucursales={sucursales}
          onClose={() => setUsuarioSucursal(null)}
          onConfirm={(sucursalId) => mutSucursal.mutate({ id: usuarioSucursal.id, sucursalId })}
          loading={mutSucursal.isPending}
        />
      )}

      {usuarioSucursales && (
        <AsignarSucursalesDialog
          usuario={usuarioSucursales}
          sucursales={sucursales}
          onClose={() => setUsuarioSucursales(null)}
          onConfirm={(sucursalIds) => mutSucursales.mutate({ id: usuarioSucursales.id, sucursalIds })}
          loading={mutSucursales.isPending}
        />
      )}

      {usuarioEstado && (
        <CambiarEstadoDialog
          usuario={usuarioEstado}
          onClose={() => setUsuarioEstado(null)}
          onConfirm={(motivo) => mutEstado.mutate({ id: usuarioEstado.id, activo: !usuarioEstado.activo, motivo })}
          loading={mutEstado.isPending}
        />
      )}

      <CrearUsuarioDialog
        open={crearDialogOpen}
        onClose={() => setCrearDialogOpen(false)}
      />

      <EditarUsuarioDialog
        key={usuarioEditar?.id ?? 'new'}
        open={!!usuarioEditar}
        usuario={usuarioEditar}
        onClose={() => setUsuarioEditar(null)}
      />
    </Container>
  );
}
