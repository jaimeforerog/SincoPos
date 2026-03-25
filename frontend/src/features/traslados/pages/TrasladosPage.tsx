import { useState, useMemo } from 'react';
import {
  Container,
  Typography,
  Box,
  Button,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  IconButton,
  Alert,
  alpha,
  Skeleton,
} from '@mui/material';
import {
  Add as AddIcon,
  Visibility as ViewIcon,
  SwapHoriz as SwapIcon,
  PendingActions as PendingIcon,
  LocalShipping as TransitIcon,
  CheckCircle as RecibidoIcon,
  FilterList as FilterListIcon,
} from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { trasladosApi } from '@/api/traslados';
import { CrearTrasladoView } from '../components/CrearTrasladoView';
import { DetallesTrasladoView } from '../components/DetallesTrasladoView';
import { TableSkeleton } from '@/components/common/TableSkeleton';

const HERO_COLOR = '#1565c0';

const ESTADO_META: Record<string, { color: 'warning' | 'info' | 'success' | 'error' | 'default'; label: string }> = {
  Pendiente:  { color: 'warning', label: 'Pendiente' },
  EnTransito: { color: 'info',    label: 'En Tránsito' },
  Recibido:   { color: 'success', label: 'Recibido' },
  Rechazado:  { color: 'error',   label: 'Rechazado' },
  Cancelado:  { color: 'error',   label: 'Cancelado' },
};

const ESTADOS_FILTRO = [
  { value: '', label: 'Todos' },
  { value: 'Pendiente',  label: 'Pendiente' },
  { value: 'EnTransito', label: 'En Tránsito' },
  { value: 'Recibido',   label: 'Recibido' },
  { value: 'Rechazado',  label: 'Rechazado' },
  { value: 'Cancelado',  label: 'Cancelado' },
];

const formatFecha = (fecha?: string) => {
  if (!fecha) return '-';
  return new Date(fecha).toLocaleString('es-CO', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit',
  });
};

interface HeroStatProps {
  icon: React.ReactElement;
  label: string;
  value: number;
  loading: boolean;
}

function HeroStat({ icon, label, value, loading }: HeroStatProps) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <Box sx={{ color: 'rgba(255,255,255,0.7)', display: 'flex', fontSize: 18 }}>{icon}</Box>
      <Box>
        <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.65)', display: 'block', lineHeight: 1 }}>
          {label}
        </Typography>
        {loading ? (
          <Skeleton variant="text" width={40} sx={{ bgcolor: 'rgba(255,255,255,0.2)' }} />
        ) : (
          <Typography variant="body2" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
            {value}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

type View = 'list' | 'crear' | 'detalle';

export function TrasladosPage() {
  const [view, setView] = useState<View>('list');
  const [estadoFiltro, setEstadoFiltro] = useState('');
  const [trasladoSeleccionado, setTrasladoSeleccionado] = useState<number | null>(null);

  const { data: trasladosPage, isLoading, error, refetch } = useQuery({
    queryKey: ['traslados'],
    queryFn: () => trasladosApi.listar(),
  });
  const traslados = trasladosPage?.items ?? [];

  const stats = useMemo(() => ({
    total:      traslados.length,
    pendientes: traslados.filter((t) => t.estado === 'Pendiente').length,
    enTransito: traslados.filter((t) => t.estado === 'EnTransito').length,
    recibidos:  traslados.filter((t) => t.estado === 'Recibido').length,
  }), [traslados]);

  const trasladosFiltrados = estadoFiltro
    ? traslados.filter((t) => t.estado === estadoFiltro)
    : traslados;

  const handleVerDetalles = (id: number) => {
    setTrasladoSeleccionado(id);
    setView('detalle');
  };

  const handleBack = () => {
    setView('list');
    setTrasladoSeleccionado(null);
    refetch();
  };

  if (view === 'crear') {
    return (
      <Container maxWidth="xl" sx={{ mt: 1 }}>
        <CrearTrasladoView onBack={handleBack} onSuccess={handleBack} />
      </Container>
    );
  }

  if (view === 'detalle' && trasladoSeleccionado) {
    return (
      <Container maxWidth="xl" sx={{ mt: 1 }}>
        <DetallesTrasladoView trasladoId={trasladoSeleccionado} onBack={handleBack} />
      </Container>
    );
  }

  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          borderRadius: 3,
          px: { xs: 3, md: 4 },
          py: { xs: 1, md: 1.25 },
          mb: 3,
          mt: 1,
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
            flexDirection: { xs: 'column', md: 'row' },
            alignItems: { xs: 'flex-start', md: 'center' },
            justifyContent: 'space-between',
            gap: { xs: 2.5, md: 0 },
            position: 'relative',
            zIndex: 1,
          }}
        >
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
              Traslados entre Sucursales
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.5 }}>
              Gestión de traslados de inventario
            </Typography>
          </Box>

          <Box
            sx={{
              display: 'flex', flexWrap: 'wrap',
              gap: { xs: 2, md: 3 }, alignItems: 'center',
              '& > *:not(:last-child)': {
                position: 'relative',
                '&::after': {
                  content: '""', position: 'absolute',
                  right: { xs: 'unset', md: -16 }, top: '10%',
                  height: '80%', width: '1px',
                  bgcolor: 'rgba(255,255,255,0.2)',
                  display: { xs: 'none', md: 'block' },
                },
              },
            }}
          >
            <HeroStat icon={<SwapIcon />}     label="Total"       value={stats.total}      loading={isLoading} />
            <HeroStat icon={<PendingIcon />}  label="Pendientes"  value={stats.pendientes} loading={isLoading} />
            <HeroStat icon={<TransitIcon />}  label="En tránsito" value={stats.enTransito} loading={isLoading} />
            <HeroStat icon={<RecibidoIcon />} label="Recibidos"   value={stats.recibidos}  loading={isLoading} />

            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() => setView('crear')}
              sx={{
                bgcolor: 'rgba(255,255,255,0.15)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.35)',
                fontWeight: 700,
                '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              }}
            >
              Nuevo Traslado
            </Button>
          </Box>
        </Box>
      </Box>

      {/* Filtros */}
      <Box
        sx={{
          display: 'flex', alignItems: 'center', gap: 2, mb: 2.5,
          p: 1.5, bgcolor: 'background.paper', borderRadius: 2,
          border: '1px solid', borderColor: 'divider',
        }}
      >
        <FilterListIcon sx={{ color: 'text.secondary', ml: 0.5 }} fontSize="small" />
        <Typography variant="body2" color="text.secondary" fontWeight={500}>
          Estado:
        </Typography>
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {ESTADOS_FILTRO.map((e) => (
            <Chip
              key={e.value}
              label={e.label}
              size="small"
              clickable
              variant={estadoFiltro === e.value ? 'filled' : 'outlined'}
              color={estadoFiltro === e.value ? 'primary' : 'default'}
              onClick={() => setEstadoFiltro(e.value)}
              sx={{ fontWeight: estadoFiltro === e.value ? 700 : 400 }}
            />
          ))}
        </Box>
        <Box sx={{ ml: 'auto' }}>
          <Typography variant="caption" color="text.secondary">
            {trasladosFiltrados.length} resultado{trasladosFiltrados.length !== 1 ? 's' : ''}
          </Typography>
        </Box>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Error al cargar los traslados. Por favor, intenta de nuevo.
        </Alert>
      )}

      {/* Tabla */}
      {isLoading ? (
        <TableSkeleton cols={7} />
      ) : (
        <TableContainer
          component={Paper}
          sx={{ borderRadius: 2, border: '1px solid', borderColor: 'divider', overflow: 'hidden' }}
        >
          <Table>
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
                <TableCell>Número</TableCell>
                <TableCell>Origen</TableCell>
                <TableCell>Destino</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell>Fecha</TableCell>
                <TableCell>Productos</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {trasladosFiltrados.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                    <SwapIcon sx={{ fontSize: 40, color: 'text.disabled', mb: 1 }} />
                    <Typography variant="body2" color="text.secondary">
                      No hay traslados{estadoFiltro ? ` con estado "${estadoFiltro}"` : ' registrados'}
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                trasladosFiltrados.map((traslado) => {
                  const meta = ESTADO_META[traslado.estado] ?? { color: 'default' as const, label: traslado.estado };
                  return (
                    <TableRow
                      key={traslado.id}
                      hover
                      sx={{
                        cursor: 'pointer',
                        '&:hover': { bgcolor: alpha(HERO_COLOR, 0.03) },
                        '&:last-child td': { borderBottom: 0 },
                      }}
                    >
                      <TableCell>
                        <Typography variant="body2" fontWeight={700} color="primary.main" sx={{ fontFamily: 'monospace' }}>
                          {traslado.numeroTraslado}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" fontWeight={500}>{traslado.nombreSucursalOrigen}</Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" color="text.secondary">{traslado.nombreSucursalDestino}</Typography>
                      </TableCell>
                      <TableCell>
                        <Chip label={meta.label} color={meta.color} size="small" sx={{ fontWeight: 600 }} />
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">{formatFecha(traslado.fechaTraslado)}</Typography>
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={`${traslado.detalles.length} producto(s)`}
                          size="small" variant="outlined"
                          sx={{ color: 'text.secondary', borderColor: 'divider' }}
                        />
                      </TableCell>
                      <TableCell align="right">
                        <IconButton
                          size="small" color="primary"
                          onClick={() => handleVerDetalles(traslado.id)}
                          aria-label="ver detalles"
                        >
                          <ViewIcon />
                        </IconButton>
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Container>
  );
}
