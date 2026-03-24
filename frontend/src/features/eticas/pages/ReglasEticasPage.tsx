import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Container,
  Box,
  Typography,
  Button,
  Chip,
  IconButton,
  Tooltip,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Alert,
  Tabs,
  Tab,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import HistoryIcon from '@mui/icons-material/History';
import ShieldIcon from '@mui/icons-material/Shield';
import { eticasApi, type ReglaEticaDto, type CrearReglaEticaDto } from '@/api/eticas';

const CONDICIONES = [
  { value: 'DescuentoMaximoPorcentaje', label: 'Descuento máximo (%)' },
  { value: 'MontoMaximoTransaccion', label: 'Monto máximo por transacción' },
  { value: 'MaximoLineasVenta', label: 'Máximo de líneas por venta' },
  { value: 'PrecioMinimoSobreBase', label: 'Precio mínimo sobre precio base (%)' },
];

const ACCIONES = [
  { value: 'Alertar', label: 'Alertar (registrar y permitir)' },
  { value: 'Bloquear', label: 'Bloquear (rechazar la transacción)' },
];

const defaultForm: CrearReglaEticaDto = {
  nombre: '',
  contexto: 'Venta',
  condicion: 'DescuentoMaximoPorcentaje',
  valorLimite: 20,
  accion: 'Alertar',
  mensaje: '',
  activo: true,
};

export function ReglasEticasPage() {
  const qc = useQueryClient();
  const [tab, setTab] = useState(0);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState<CrearReglaEticaDto>(defaultForm);
  const [error, setError] = useState('');

  const { data: reglas = [], isLoading } = useQuery({
    queryKey: ['reglas-eticas'],
    queryFn: eticasApi.getAll,
  });

  const { data: activaciones = [] } = useQuery({
    queryKey: ['eticas-activaciones'],
    queryFn: () => eticasApi.getActivaciones(undefined, 100),
    enabled: tab === 1,
  });

  const createMutation = useMutation({
    mutationFn: (dto: CrearReglaEticaDto) =>
      editId ? eticasApi.update(editId, dto) : eticasApi.create(dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reglas-eticas'] });
      closeDialog();
    },
    onError: () => setError('Error al guardar la regla.'),
  });

  const deleteMutation = useMutation({
    mutationFn: eticasApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['reglas-eticas'] }),
  });

  const openCreate = () => {
    setEditId(null);
    setForm(defaultForm);
    setError('');
    setDialogOpen(true);
  };

  const openEdit = (r: ReglaEticaDto) => {
    setEditId(r.id);
    setForm({
      nombre: r.nombre,
      contexto: r.contexto,
      condicion: r.condicion,
      valorLimite: r.valorLimite,
      accion: r.accion,
      mensaje: r.mensaje ?? '',
      activo: r.activo,
    });
    setError('');
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditId(null);
  };


  return (
    <Container maxWidth="xl">
      {/* Hero */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #1565c0 0%, #0d47a1 50%, #01579b 100%)',
          borderRadius: 3, px: { xs: 3, md: 4 }, py: { xs: 2.5, md: 3 },
          mb: 2, mt: 1,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
              <ShieldIcon sx={{ color: 'rgba(255,255,255,0.9)', fontSize: 20 }} />
              <Typography variant="h5" fontWeight={700} sx={{ color: '#fff' }}>
                Supervisión Ética
              </Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              Reglas de negocio configurables que se evalúan en tiempo real sin redespliegue
            </Typography>
          </Box>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={openCreate}
            sx={{ bgcolor: 'rgba(255,255,255,0.15)', '&:hover': { bgcolor: 'rgba(255,255,255,0.25)' } }}
          >
            Nueva Regla
          </Button>
        </Box>
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="Reglas Activas" />
        <Tab label="Historial de Activaciones" icon={<HistoryIcon fontSize="small" />} iconPosition="start" />
      </Tabs>

      {tab === 0 && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: 'grey.50' }}>
              <TableRow>
                <TableCell>Nombre</TableCell>
                <TableCell>Condición</TableCell>
                <TableCell align="right">Límite</TableCell>
                <TableCell>Acción</TableCell>
                <TableCell align="center">Activo</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading && (
                <TableRow>
                  <TableCell colSpan={6} align="center">Cargando...</TableCell>
                </TableRow>
              )}
              {!isLoading && reglas.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No hay reglas éticas configuradas
                  </TableCell>
                </TableRow>
              )}
              {reglas.map((r) => (
                <TableRow key={r.id} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={500}>{r.nombre}</Typography>
                    {r.mensaje && (
                      <Typography variant="caption" color="text.secondary">{r.mensaje}</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">
                      {CONDICIONES.find((c) => c.value === r.condicion)?.label ?? r.condicion}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" fontWeight={600}>{r.valorLimite}</Typography>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={r.accion === 'Bloquear' ? 'Bloquear' : 'Alertar'}
                      size="small"
                      color={r.accion === 'Bloquear' ? 'error' : 'warning'}
                    />
                  </TableCell>
                  <TableCell align="center">
                    <Switch
                      checked={r.activo}
                      size="small"
                      onChange={() => openEdit(r)}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Editar">
                      <IconButton size="small" onClick={() => openEdit(r)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Eliminar">
                      <IconButton
                        size="small"
                        color="error"
                        onClick={() => deleteMutation.mutate(r.id)}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {tab === 1 && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={{ bgcolor: 'grey.50' }}>
              <TableRow>
                <TableCell>Fecha</TableCell>
                <TableCell>Regla</TableCell>
                <TableCell>Detalle</TableCell>
                <TableCell>Acción tomada</TableCell>
                <TableCell align="right">Venta</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {activaciones.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    No hay activaciones registradas
                  </TableCell>
                </TableRow>
              )}
              {activaciones.map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell>
                    <Typography variant="caption">
                      {new Date(a.fechaActivacion).toLocaleString('es-CO')}
                    </Typography>
                  </TableCell>
                  <TableCell>{a.nombreRegla}</TableCell>
                  <TableCell>
                    <Typography variant="caption" color="text.secondary">{a.detalle}</Typography>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={a.accionTomada}
                      size="small"
                      color={a.accionTomada === 'Bloquear' ? 'error' : 'warning'}
                    />
                  </TableCell>
                  <TableCell align="right">
                    {a.ventaId ?? '-'}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Dialog crear/editar */}
      <Dialog open={dialogOpen} onClose={closeDialog} maxWidth="sm" fullWidth>
        <DialogTitle>{editId ? 'Editar Regla Ética' : 'Nueva Regla Ética'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            label="Nombre"
            value={form.nombre}
            onChange={(e) => setForm({ ...form, nombre: e.target.value })}
            fullWidth
            size="small"
          />

          <FormControl fullWidth size="small">
            <InputLabel>Condición</InputLabel>
            <Select
              value={form.condicion}
              label="Condición"
              onChange={(e) => setForm({ ...form, condicion: e.target.value })}
            >
              {CONDICIONES.map((c) => (
                <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label="Valor límite"
            type="number"
            value={form.valorLimite}
            onChange={(e) => setForm({ ...form, valorLimite: Number(e.target.value) })}
            fullWidth
            size="small"
            inputProps={{ min: 0 }}
          />

          <FormControl fullWidth size="small">
            <InputLabel>Acción</InputLabel>
            <Select
              value={form.accion}
              label="Acción"
              onChange={(e) => setForm({ ...form, accion: e.target.value })}
            >
              {ACCIONES.map((a) => (
                <MenuItem key={a.value} value={a.value}>{a.label}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label="Mensaje para el cajero (opcional)"
            value={form.mensaje}
            onChange={(e) => setForm({ ...form, mensaje: e.target.value })}
            fullWidth
            size="small"
            multiline
            rows={2}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={closeDialog}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => createMutation.mutate(form)}
            disabled={!form.nombre || createMutation.isPending}
          >
            {editId ? 'Guardar' : 'Crear'}
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
