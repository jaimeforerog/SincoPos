import { useState, useEffect, useCallback } from 'react';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  FormControl,
  FormControlLabel,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  alpha,
} from '@mui/material';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Block as BlockIcon,
  CheckCircleOutline as ActivarIcon,
  List as ListIcon,
  FileUpload as FileUploadIcon,
} from '@mui/icons-material';
import { tercerosApi } from '@/api/terceros';
import type { TerceroDTO } from '@/types/api';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { useAuthStore } from '@/stores/auth.store';
import { ActividadesDialog } from '../components/ActividadesDialog';
import { TerceroFormDialog } from '../components/TerceroFormDialog';
import { ImportarTercerosDialog } from '../components/ImportarTercerosDialog';

const HERO_COLOR = '#1565c0';
const TIPOS_TERCERO = ['Cliente', 'Proveedor', 'Ambos'];

function tipoColor(tipo: string): 'primary' | 'secondary' | 'default' {
  if (tipo === 'Cliente') return 'primary';
  if (tipo === 'Proveedor') return 'secondary';
  return 'default';
}

export default function TercerosPage() {
  const { activeEmpresaId } = useAuthStore();
  const [terceros, setTerceros] = useState<TerceroDTO[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const [busqueda, setBusqueda] = useState('');
  const [tipoFiltro, setTipoFiltro] = useState('');
  const [incluirInactivos, setIncluirInactivos] = useState(false);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);

  useEffect(() => { setPage(0); }, [activeEmpresaId]);

  const [formOpen, setFormOpen] = useState(false);
  const [editando, setEditando] = useState<TerceroDTO | null>(null);
  const [actividadesOpen, setActividadesOpen] = useState(false);
  const [terceroActividades, setTerceroActividades] = useState<TerceroDTO | null>(null);
  const [importarOpen, setImportarOpen] = useState(false);

  const cargar = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await tercerosApi.getAll({
        q: busqueda || undefined,
        tipoTercero: tipoFiltro || undefined,
        incluirInactivos,
        page: page + 1,
        pageSize,
      });
      setTerceros(response.items);
      setTotalCount(response.totalCount);
    } catch {
      setError('Error al cargar los terceros.');
    } finally {
      setLoading(false);
    }
  }, [busqueda, tipoFiltro, incluirInactivos, page, pageSize, activeEmpresaId]);

  useEffect(() => { cargar(); }, [cargar]);

  const handleNuevo = () => { setEditando(null); setFormOpen(true); };
  const handleEditar = (t: TerceroDTO) => { setEditando(t); setFormOpen(true); };

  const handleDesactivar = async (t: TerceroDTO) => {
    if (!window.confirm(`¿Desactivar "${t.nombre}"?`)) return;
    await tercerosApi.deactivate(t.id);
    cargar();
  };

  const handleActivar = async (t: TerceroDTO) => {
    if (!window.confirm(`¿Activar "${t.nombre}"?`)) return;
    await tercerosApi.activate(t.id);
    cargar();
  };

  const handleAbrirActividades = async (t: TerceroDTO) => {
    const fresco = await tercerosApi.getById(t.id);
    setTerceroActividades(fresco);
    setActividadesOpen(true);
  };

  const handleActividadesChanged = async () => {
    if (!terceroActividades) return;
    const fresco = await tercerosApi.getById(terceroActividades.id);
    setTerceroActividades(fresco);
    cargar();
  };

  return (
    <Box sx={{ p: 3 }}>
      <ReportePageHeader
        title="Terceros"
        subtitle="Gestión de clientes, proveedores y terceros del sistema"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Terceros' },
        ]}
        backPath="/configuracion"
        color="#1565c0"
        action={
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button
              variant="outlined"
              startIcon={<FileUploadIcon />}
              onClick={() => setImportarOpen(true)}
              sx={{
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.5)',
                '&:hover': { bgcolor: 'rgba(255,255,255,0.1)', borderColor: '#fff' },
              }}
            >
              Importar Excel
            </Button>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={handleNuevo}
              sx={{
                bgcolor: 'rgba(255,255,255,0.15)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.35)',
                fontWeight: 700,
                '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              }}
            >
              Nuevo Tercero
            </Button>
          </Box>
        }
      />

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <TextField
            label="Buscar por nombre o identificación"
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            size="small"
            sx={{ minWidth: 280 }}
          />
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <InputLabel>Tipo</InputLabel>
            <Select
              value={tipoFiltro}
              label="Tipo"
              onChange={e => setTipoFiltro(e.target.value)}
            >
              <MenuItem value="">Todos</MenuItem>
              {TIPOS_TERCERO.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControlLabel
            control={
              <Checkbox
                checked={incluirInactivos}
                onChange={e => setIncluirInactivos(e.target.checked)}
                size="small"
              />
            }
            label="Incluir inactivos"
          />
          <Box sx={{ flexGrow: 1 }} />
          <Typography variant="body2" color="text.secondary">
            Total: {totalCount} tercero(s)
          </Typography>
        </Box>
      </Paper>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {/* Tabla */}
      <TableContainer component={Paper}>
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
              <TableCell>Identificación</TableCell>
              <TableCell>Tipo</TableCell>
              <TableCell>Perfil Tributario</TableCell>
              <TableCell>Ciudad</TableCell>
              <TableCell>Estado</TableCell>
              <TableCell align="center">Acciones</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading && (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ color: 'text.secondary' }}>
                  Cargando…
                </TableCell>
              </TableRow>
            )}
            {!loading && terceros.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ color: 'text.secondary' }}>
                  No se encontraron terceros.
                </TableCell>
              </TableRow>
            )}
            {terceros.map(t => (
              <TableRow key={t.id} hover>
                <TableCell>
                  <Typography variant="body2" fontWeight={500}>{t.nombre}</Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="body2" component="span">
                    {t.tipoIdentificacion}: {t.identificacion}
                    {t.digitoVerificacion && (
                      <Chip
                        label={`DV ${t.digitoVerificacion}`}
                        size="small"
                        color="success"
                        sx={{ ml: 0.5, height: 18, fontSize: '0.65rem' }}
                      />
                    )}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Chip label={t.tipoTercero} color={tipoColor(t.tipoTercero)} size="small" />
                </TableCell>
                <TableCell>
                  <Typography variant="caption">{t.perfilTributario}</Typography>
                </TableCell>
                <TableCell>{t.ciudad ?? '—'}</TableCell>
                <TableCell>
                  <Chip
                    label={t.activo ? 'Activo' : 'Inactivo'}
                    color={t.activo ? 'success' : 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell align="center">
                  <Tooltip title="Editar">
                    <IconButton size="small" onClick={() => handleEditar(t)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Actividades CIIU">
                    <IconButton size="small" onClick={() => handleAbrirActividades(t)}>
                      <ListIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  {t.activo ? (
                    <Tooltip title="Desactivar">
                      <IconButton size="small" color="error" onClick={() => handleDesactivar(t)}>
                        <BlockIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  ) : (
                    <Tooltip title="Activar">
                      <IconButton size="small" color="success" onClick={() => handleActivar(t)}>
                        <ActivarIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={totalCount}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(e) => {
            setPageSize(parseInt(e.target.value, 10));
            setPage(0);
          }}
          labelRowsPerPage="Terceros por página"
          labelDisplayedRows={({ from, to, count }) =>
            `${from}-${to} de ${count !== -1 ? count : `más de ${to}`}`
          }
        />
      </TableContainer>

      <TerceroFormDialog
        open={formOpen}
        editando={editando}
        onClose={() => setFormOpen(false)}
        onSaved={cargar}
      />

      <ActividadesDialog
        open={actividadesOpen}
        tercero={terceroActividades}
        onClose={() => setActividadesOpen(false)}
        onChanged={handleActividadesChanged}
      />

      <ImportarTercerosDialog
        open={importarOpen}
        onClose={() => setImportarOpen(false)}
        onImportado={cargar}
      />
    </Box>
  );
}
