import {
  Box,
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
  Typography,
  CircularProgress,
  alpha,
} from '@mui/material';
import {
  StoreMallDirectory,
  CheckCircle,
  Cancel,
  Domain,
  Edit,
} from '@mui/icons-material';
import type { UsuarioDto } from '@/api/usuarios';

const HERO_COLOR = '#1565c0';

const ROL_LABELS: Record<string, { label: string; color: 'error' | 'warning' | 'info' | 'success' }> = {
  admin:      { label: 'Admin',      color: 'error' },
  supervisor: { label: 'Supervisor', color: 'warning' },
  cajero:     { label: 'Cajero',     color: 'info' },
  vendedor:   { label: 'Vendedor',   color: 'success' },
};

function formatFecha(fecha?: string): string {
  if (!fecha) return '—';
  return new Date(fecha).toLocaleString('es-CO', {
    dateStyle: 'short',
    timeStyle: 'short',
  });
}

interface UsuariosTableProps {
  usuarios: UsuarioDto[];
  isLoading: boolean;
  isAdmin: boolean;
  onEditar: (u: UsuarioDto) => void;
  onAsignarSucursal: (u: UsuarioDto) => void;
  onAsignarSucursales: (u: UsuarioDto) => void;
  onCambiarEstado: (u: UsuarioDto) => void;
}

export function UsuariosTable({
  usuarios,
  isLoading,
  isAdmin,
  onEditar,
  onAsignarSucursal,
  onAsignarSucursales,
  onCambiarEstado,
}: UsuariosTableProps) {
  return (
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
                    {isAdmin && (
                      <Tooltip title="Editar usuario">
                        <IconButton size="small" onClick={() => onEditar(u)}>
                          <Edit fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    <Tooltip title="Asignar sucursal default">
                      <IconButton size="small" onClick={() => onAsignarSucursal(u)}>
                        <StoreMallDirectory fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Gestionar sucursales asignadas">
                      <IconButton size="small" onClick={() => onAsignarSucursales(u)}>
                        <Domain fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={u.activo ? 'Desactivar usuario' : 'Activar usuario'}>
                      <IconButton
                        size="small"
                        color={u.activo ? 'error' : 'success'}
                        onClick={() => onCambiarEstado(u)}
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
  );
}
