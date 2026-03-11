import { useState, useEffect, useCallback, useRef } from 'react';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  FormControlLabel,
  IconButton,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Block as BlockIcon,
  List as ListIcon,
  StarBorder as StarBorderIcon,
  Delete as DeleteIcon,
  FileUpload as FileUploadIcon,
  Download as DownloadIcon,
  CheckCircle as CheckCircleIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  CloudUpload as CloudUploadIcon,
} from '@mui/icons-material';
import { tercerosApi } from '@/api/terceros';
import type { TerceroDTO, TerceroActividadDTO, ResultadoImportacionTercerosDTO } from '@/types/api';
import { PageHeader } from '@/components/common/PageHeader';

// ── Constantes ─────────────────────────────────────────────────────────────────

const TIPOS_IDENTIFICACION = ['CC', 'NIT', 'CE', 'Pasaporte', 'TI', 'Otro'];
const TIPOS_TERCERO = ['Cliente', 'Proveedor', 'Ambos'];
const PERFILES_TRIBUTARIOS = [
  'REGIMEN_COMUN',
  'GRAN_CONTRIBUYENTE',
  'REGIMEN_SIMPLE',
  'PERSONA_NATURAL',
];

// ── Tipos del formulario ───────────────────────────────────────────────────────

interface TerceroForm {
  tipoIdentificacion: string;
  identificacion: string;
  nombre: string;
  tipoTercero: string;
  telefono: string;
  email: string;
  direccion: string;
  ciudad: string;
  codigoDepartamento: string;
  codigoMunicipio: string;
  perfilTributario: string;
  esGranContribuyente: boolean;
  esAutorretenedor: boolean;
  esResponsableIVA: boolean;
}

const FORM_VACIO: TerceroForm = {
  tipoIdentificacion: 'CC',
  identificacion: '',
  nombre: '',
  tipoTercero: 'Cliente',
  telefono: '',
  email: '',
  direccion: '',
  ciudad: '',
  codigoDepartamento: '',
  codigoMunicipio: '',
  perfilTributario: 'REGIMEN_COMUN',
  esGranContribuyente: false,
  esAutorretenedor: false,
  esResponsableIVA: false,
};

// ── Helper chips ───────────────────────────────────────────────────────────────

function tipoColor(tipo: string): 'primary' | 'secondary' | 'default' {
  if (tipo === 'Cliente') return 'primary';
  if (tipo === 'Proveedor') return 'secondary';
  return 'default';
}

// ── Sub-componente: Dialog de Actividades CIIU ────────────────────────────────

interface ActividadesDialogProps {
  open: boolean;
  tercero: TerceroDTO | null;
  onClose: () => void;
  onChanged: () => void;
}

function ActividadesDialog({ open, tercero, onClose, onChanged }: ActividadesDialogProps) {
  const [actividades, setActividades] = useState<TerceroActividadDTO[]>([]);
  const [nuevoCIIU, setNuevoCIIU] = useState('');
  const [nuevaDesc, setNuevaDesc] = useState('');
  const [esPrincipal, setEsPrincipal] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (tercero) setActividades(tercero.actividades ?? []);
  }, [tercero]);

  const handleAgregar = async () => {
    if (!tercero) return;
    setError('');
    setLoading(true);
    try {
      const nueva = await tercerosApi.agregarActividad(tercero.id, {
        codigoCIIU: nuevoCIIU.trim(),
        descripcion: nuevaDesc.trim(),
        esPrincipal,
      });
      if (esPrincipal) {
        setActividades(prev =>
          prev.map(a => ({ ...a, esPrincipal: false })).concat(nueva),
        );
      } else {
        setActividades(prev => [...prev, nueva]);
      }
      setNuevoCIIU('');
      setNuevaDesc('');
      setEsPrincipal(false);
      onChanged();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'Error al agregar actividad.');
    } finally {
      setLoading(false);
    }
  };

  const handleEliminar = async (actividadId: number) => {
    if (!tercero) return;
    await tercerosApi.eliminarActividad(tercero.id, actividadId);
    setActividades(prev => prev.filter(a => a.id !== actividadId));
    onChanged();
  };

  const handlePrincipal = async (actividadId: number) => {
    if (!tercero) return;
    await tercerosApi.establecerPrincipal(tercero.id, actividadId);
    setActividades(prev => prev.map(a => ({ ...a, esPrincipal: a.id === actividadId })));
    onChanged();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        Actividades CIIU — {tercero?.nombre}
      </DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <TableContainer component={Paper} variant="outlined" sx={{ mb: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Código CIIU</TableCell>
                <TableCell>Descripción</TableCell>
                <TableCell align="center">Principal</TableCell>
                <TableCell align="center">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {actividades.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ color: 'text.secondary' }}>
                    Sin actividades registradas
                  </TableCell>
                </TableRow>
              )}
              {actividades.map(act => (
                <TableRow key={act.id}>
                  <TableCell>
                    <strong>{act.codigoCIIU}</strong>
                  </TableCell>
                  <TableCell>{act.descripcion}</TableCell>
                  <TableCell align="center">
                    {act.esPrincipal ? (
                      <Chip label="Principal" color="success" size="small" />
                    ) : (
                      <Tooltip title="Establecer como principal">
                        <IconButton size="small" onClick={() => handlePrincipal(act.id)}>
                          <StarBorderIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </TableCell>
                  <TableCell align="center">
                    <Tooltip title="Eliminar">
                      <IconButton size="small" color="error" onClick={() => handleEliminar(act.id)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>

        <Typography variant="subtitle2" sx={{ mb: 1 }}>Agregar actividad</Typography>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', flexWrap: 'wrap' }}>
          <TextField
            label="Código CIIU"
            value={nuevoCIIU}
            onChange={e => setNuevoCIIU(e.target.value)}
            size="small"
            sx={{ width: 130 }}
          />
          <TextField
            label="Descripción"
            value={nuevaDesc}
            onChange={e => setNuevaDesc(e.target.value)}
            size="small"
            sx={{ flex: 1, minWidth: 160 }}
          />
          <FormControlLabel
            control={
              <Checkbox
                checked={esPrincipal}
                onChange={e => setEsPrincipal(e.target.checked)}
                size="small"
              />
            }
            label="Principal"
          />
          <Button
            variant="contained"
            size="small"
            disabled={!nuevoCIIU.trim() || !nuevaDesc.trim() || loading}
            onClick={handleAgregar}
          >
            Agregar
          </Button>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Sub-componente: Dialog de Formulario Tercero ──────────────────────────────

interface TerceroFormDialogProps {
  open: boolean;
  editando: TerceroDTO | null;
  onClose: () => void;
  onSaved: () => void;
}

function TerceroFormDialog({ open, editando, onClose, onSaved }: TerceroFormDialogProps) {
  const [tabIdx, setTabIdx] = useState(0);
  const [form, setForm] = useState<TerceroForm>(FORM_VACIO);
  const [dvCalculado, setDvCalculado] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const dvTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (!open) return;
    setTabIdx(0);
    setError('');
    setDvCalculado('');
    if (editando) {
      setForm({
        tipoIdentificacion: editando.tipoIdentificacion,
        identificacion: editando.identificacion,
        nombre: editando.nombre,
        tipoTercero: editando.tipoTercero,
        telefono: editando.telefono ?? '',
        email: editando.email ?? '',
        direccion: editando.direccion ?? '',
        ciudad: editando.ciudad ?? '',
        codigoDepartamento: editando.codigoDepartamento ?? '',
        codigoMunicipio: editando.codigoMunicipio ?? '',
        perfilTributario: editando.perfilTributario ?? 'REGIMEN_COMUN',
        esGranContribuyente: editando.esGranContribuyente,
        esAutorretenedor: editando.esAutorretenedor,
        esResponsableIVA: editando.esResponsableIVA,
      });
      if (editando.digitoVerificacion) setDvCalculado(editando.digitoVerificacion);
    } else {
      setForm(FORM_VACIO);
    }
  }, [open, editando]);

  // Auto-calcular DV cuando es NIT
  useEffect(() => {
    if (form.tipoIdentificacion !== 'NIT') {
      setDvCalculado('');
      return;
    }
    if (dvTimerRef.current) clearTimeout(dvTimerRef.current);
    const nit = form.identificacion.replace(/\D/g, '');
    if (nit.length < 6) { setDvCalculado(''); return; }

    dvTimerRef.current = setTimeout(async () => {
      try {
        const res = await tercerosApi.calcularDV(nit);
        setDvCalculado(res.dv);
      } catch {
        setDvCalculado('');
      }
    }, 300);
    return () => { if (dvTimerRef.current) clearTimeout(dvTimerRef.current); };
  }, [form.tipoIdentificacion, form.identificacion]);

  const set = (field: keyof TerceroForm) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setForm(prev => ({ ...prev, [field]: e.target.value }));
  };

  const handleGuardar = async () => {
    setError('');
    setSaving(true);
    try {
      if (editando) {
        await tercerosApi.update(editando.id, {
          nombre: form.nombre,
          tipoTercero: form.tipoTercero,
          telefono: form.telefono || undefined,
          email: form.email || undefined,
          direccion: form.direccion || undefined,
          ciudad: form.ciudad || undefined,
          codigoDepartamento: form.codigoDepartamento || undefined,
          codigoMunicipio: form.codigoMunicipio || undefined,
          perfilTributario: form.perfilTributario,
          esGranContribuyente: form.esGranContribuyente,
          esAutorretenedor: form.esAutorretenedor,
          esResponsableIVA: form.esResponsableIVA,
        });
      } else {
        await tercerosApi.create({
          tipoIdentificacion: form.tipoIdentificacion,
          identificacion: form.identificacion,
          nombre: form.nombre,
          tipoTercero: form.tipoTercero,
          telefono: form.telefono || undefined,
          email: form.email || undefined,
          direccion: form.direccion || undefined,
          ciudad: form.ciudad || undefined,
          codigoDepartamento: form.codigoDepartamento || undefined,
          codigoMunicipio: form.codigoMunicipio || undefined,
          perfilTributario: form.perfilTributario,
          esGranContribuyente: form.esGranContribuyente,
          esAutorretenedor: form.esAutorretenedor,
          esResponsableIVA: form.esResponsableIVA,
        });
      }
      onSaved();
      onClose();
    } catch (e: unknown) {
      const data = (e as { response?: { data?: { error?: string; errors?: Record<string, string[]> } } })?.response?.data;
      if (data?.error) setError(data.error);
      else if (data?.errors) setError(Object.values(data.errors).flat().join(' '));
      else setError('Error al guardar el tercero.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{editando ? 'Editar Tercero' : 'Nuevo Tercero'}</DialogTitle>
      <DialogContent sx={{ pt: 0 }}>
        {error && <Alert severity="error" sx={{ mb: 1, mt: 1 }}>{error}</Alert>}
        <Tabs value={tabIdx} onChange={(_, v) => setTabIdx(v)} sx={{ mb: 2 }}>
          <Tab label="Datos Básicos" />
          <Tab label="Contacto" />
          <Tab label="Perfil Fiscal" />
        </Tabs>

        {/* Tab 0 — Datos Básicos */}
        {tabIdx === 0 && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <FormControl fullWidth size="small" disabled={!!editando}>
              <InputLabel>Tipo Identificación</InputLabel>
              <Select
                value={form.tipoIdentificacion}
                label="Tipo Identificación"
                onChange={e => setForm(prev => ({ ...prev, tipoIdentificacion: e.target.value }))}
              >
                {TIPOS_IDENTIFICACION.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
              </Select>
            </FormControl>

            <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
              <TextField
                label="Identificación"
                value={form.identificacion}
                onChange={set('identificacion')}
                size="small"
                disabled={!!editando}
                sx={{ flex: 1 }}
              />
              {form.tipoIdentificacion === 'NIT' && dvCalculado && (
                <Chip
                  label={`DV: ${dvCalculado}`}
                  color="success"
                  size="small"
                  sx={{ fontWeight: 'bold', minWidth: 60 }}
                />
              )}
            </Box>

            <TextField
              label="Nombre / Razón Social"
              value={form.nombre}
              onChange={set('nombre')}
              size="small"
              required
            />

            <FormControl fullWidth size="small">
              <InputLabel>Tipo Tercero</InputLabel>
              <Select
                value={form.tipoTercero}
                label="Tipo Tercero"
                onChange={e => setForm(prev => ({ ...prev, tipoTercero: e.target.value }))}
              >
                {TIPOS_TERCERO.map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
              </Select>
            </FormControl>
          </Box>
        )}

        {/* Tab 1 — Contacto */}
        {tabIdx === 1 && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <TextField label="Teléfono" value={form.telefono} onChange={set('telefono')} size="small" />
            <TextField label="Email" value={form.email} onChange={set('email')} size="small" type="email" />
            <TextField label="Dirección" value={form.direccion} onChange={set('direccion')} size="small" />
            <TextField label="Ciudad" value={form.ciudad} onChange={set('ciudad')} size="small" />
            <Box sx={{ display: 'flex', gap: 1 }}>
              <TextField
                label="Código Departamento"
                value={form.codigoDepartamento}
                onChange={set('codigoDepartamento')}
                size="small"
                sx={{ flex: 1 }}
                placeholder="Ej: 11"
              />
              <TextField
                label="Código Municipio"
                value={form.codigoMunicipio}
                onChange={set('codigoMunicipio')}
                size="small"
                sx={{ flex: 1 }}
                placeholder="Ej: 11001"
              />
            </Box>
          </Box>
        )}

        {/* Tab 2 — Perfil Fiscal */}
        {tabIdx === 2 && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <FormControl fullWidth size="small">
              <InputLabel>Perfil Tributario</InputLabel>
              <Select
                value={form.perfilTributario}
                label="Perfil Tributario"
                onChange={e => setForm(prev => ({ ...prev, perfilTributario: e.target.value }))}
              >
                {PERFILES_TRIBUTARIOS.map(p => <MenuItem key={p} value={p}>{p}</MenuItem>)}
              </Select>
            </FormControl>
            <FormControlLabel
              control={
                <Checkbox
                  checked={form.esGranContribuyente}
                  onChange={e => setForm(prev => ({ ...prev, esGranContribuyente: e.target.checked }))}
                />
              }
              label="Gran Contribuyente"
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={form.esAutorretenedor}
                  onChange={e => setForm(prev => ({ ...prev, esAutorretenedor: e.target.checked }))}
                />
              }
              label="Autorretenedor"
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={form.esResponsableIVA}
                  onChange={e => setForm(prev => ({ ...prev, esResponsableIVA: e.target.checked }))}
                />
              }
              label="Responsable IVA"
            />
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Cancelar</Button>
        <Button variant="contained" onClick={handleGuardar} disabled={saving}>
          {saving ? 'Guardando…' : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Sub-componente: Importar desde Excel ──────────────────────────────────────

interface ImportarDialogProps {
  open: boolean;
  onClose: () => void;
  onImportado: () => void;
}

function ImportarTercerosDialog({ open, onClose, onImportado }: ImportarDialogProps) {
  const [archivo, setArchivo] = useState<File | null>(null);
  const [cargando, setCargando] = useState(false);
  const [resultado, setResultado] = useState<ResultadoImportacionTercerosDTO | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!open) {
      setArchivo(null);
      setResultado(null);
      setError('');
    }
  }, [open]);

  const handleSeleccionar = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0] ?? null;
    setArchivo(f);
    setResultado(null);
    setError('');
    e.target.value = '';
  };

  const handleImportar = async () => {
    if (!archivo) return;
    setCargando(true);
    setError('');
    try {
      const res = await tercerosApi.importarExcel(archivo);
      setResultado(res);
      if (res.importados > 0) onImportado();
    } catch (e: unknown) {
      const msg = (e as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setError(msg ?? 'Error al procesar el archivo.');
    } finally {
      setCargando(false);
    }
  };

  const estadoColor = (estado: string): 'success' | 'warning' | 'error' | 'default' => {
    if (estado === 'Importado') return 'success';
    if (estado === 'Omitido') return 'warning';
    return 'error';
  };

  const importados = resultado?.importados ?? 0;
  const omitidos = resultado?.omitidos ?? 0;
  const errores = resultado?.errores ?? 0;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            Importar Terceros desde Excel
          </Typography>
          <Button
            startIcon={<DownloadIcon />}
            onClick={() => tercerosApi.descargarPlantilla()}
            size="small"
          >
            Descargar Plantilla
          </Button>
        </Box>
      </DialogTitle>

      <DialogContent>
        <Alert severity="success" sx={{ mb: 2 }}>
          <Typography variant="body2" sx={{ mb: 1, fontWeight: 600 }}>
            Cómo usar la plantilla:
          </Typography>
          <Typography variant="caption" component="div">
            1. <strong>Descarga la plantilla</strong> — Incluye listas desplegables para departamento, municipio, ciudad y campos fiscales
            <br />
            2. <strong>Abre en Excel</strong> — Completa los datos usando los selectores de cada columna
            <br />
            3. <strong>Columnas obligatorias:</strong> TipoIdentificacion, Identificacion, Nombre, TipoTercero
            <br />
            4. <strong>Guarda y sube el archivo</strong> — El sistema omite duplicados automáticamente
            <br />
            <br />
            <strong>Nota:</strong> Para NIT el dígito de verificación se calcula automáticamente.
          </Typography>
        </Alert>

        {/* Zona de carga (sin archivo seleccionado) */}
        {!archivo && !resultado && (
          <Box
            sx={{
              border: '2px dashed',
              borderColor: 'divider',
              borderRadius: 2,
              p: 4,
              textAlign: 'center',
              cursor: 'pointer',
              '&:hover': { borderColor: 'primary.main', bgcolor: 'action.hover' },
            }}
          >
            <input
              accept=".xlsx,.xls"
              style={{ display: 'none' }}
              id="upload-terceros-file"
              type="file"
              onChange={handleSeleccionar}
              disabled={cargando}
            />
            <label htmlFor="upload-terceros-file">
              <Button
                variant="contained"
                component="span"
                startIcon={<CloudUploadIcon />}
                size="large"
              >
                Seleccionar Archivo
              </Button>
            </label>
          </Box>
        )}

        {/* Archivo seleccionado (antes de importar) */}
        {archivo && !resultado && (
          <Box sx={{ mb: 2, display: 'flex', gap: 2, alignItems: 'center' }}>
            <Chip
              icon={<CheckCircleIcon />}
              label={`${archivo.name} (${(archivo.size / 1024).toFixed(1)} KB)`}
              color="success"
            />
            <Box sx={{ flexGrow: 1 }} />
            <input
              accept=".xlsx,.xls"
              style={{ display: 'none' }}
              id="upload-terceros-file-reload"
              type="file"
              onChange={handleSeleccionar}
            />
            <label htmlFor="upload-terceros-file-reload">
              <Button component="span" size="small">
                Cargar Otro Archivo
              </Button>
            </label>
          </Box>
        )}

        {cargando && <LinearProgress sx={{ mt: 1 }} />}
        {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}

        {/* Resultados */}
        {resultado && (
          <>
            <Box sx={{ mb: 2, display: 'flex', gap: 2, alignItems: 'center' }}>
              <Chip icon={<CheckCircleIcon />} label={`${importados} importados`} color="success" />
              <Chip icon={<WarningIcon />} label={`${omitidos} omitidos`} color="warning" />
              <Chip icon={<ErrorIcon />} label={`${errores} errores`} color="error" />
              <Box sx={{ flexGrow: 1 }} />
              <input
                accept=".xlsx,.xls"
                style={{ display: 'none' }}
                id="upload-terceros-file-new"
                type="file"
                onChange={handleSeleccionar}
              />
              <label htmlFor="upload-terceros-file-new">
                <Button component="span" size="small">
                  Cargar Otro Archivo
                </Button>
              </label>
            </Box>

            <TableContainer component={Paper} sx={{ maxHeight: 400 }}>
              <Table stickyHeader size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Estado</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Identificación</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Nombre</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Detalle</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {resultado.filas.map((f) => (
                    <TableRow
                      key={f.fila}
                      sx={{ bgcolor: f.estado === 'Error' ? 'error.lighter' : 'inherit' }}
                    >
                      <TableCell>
                        <Chip label={f.estado} color={estadoColor(f.estado)} size="small" />
                      </TableCell>
                      <TableCell sx={{ fontFamily: 'monospace' }}>{f.identificacion ?? '—'}</TableCell>
                      <TableCell>{f.nombre ?? '—'}</TableCell>
                      <TableCell>
                        <Typography variant="caption" color="text.secondary">
                          {f.mensaje ?? ''}
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={cargando}>
          {resultado ? 'Cerrar' : 'Cancelar'}
        </Button>
        {!resultado && (
          <Button
            variant="contained"
            onClick={handleImportar}
            disabled={!archivo || cargando}
            startIcon={<FileUploadIcon />}
          >
            {cargando ? 'Importando...' : 'Importar'}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

// ── Componente principal ──────────────────────────────────────────────────────

export default function TercerosPage() {
  const [terceros, setTerceros] = useState<TerceroDTO[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Filtros
  const [busqueda, setBusqueda] = useState('');
  const [tipoFiltro, setTipoFiltro] = useState('');
  const [incluirInactivos, setIncluirInactivos] = useState(false);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);

  // Dialogs
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
  }, [busqueda, tipoFiltro, incluirInactivos, page, pageSize]);

  useEffect(() => { cargar(); }, [cargar]);

  const handleNuevo = () => { setEditando(null); setFormOpen(true); };
  const handleEditar = (t: TerceroDTO) => { setEditando(t); setFormOpen(true); };

  const handleDesactivar = async (t: TerceroDTO) => {
    if (!window.confirm(`¿Desactivar "${t.nombre}"?`)) return;
    await tercerosApi.deactivate(t.id);
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
      <PageHeader
        title="Terceros"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Terceros' },
        ]}
        showBackButton={true}
        backPath="/configuracion"
        action={
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button
              variant="outlined"
              startIcon={<FileUploadIcon />}
              onClick={() => setImportarOpen(true)}
            >
              Importar Excel
            </Button>
            <Button variant="contained" startIcon={<AddIcon />} onClick={handleNuevo}>
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
          {/* ... Table content ... */}
          <TableHead>
            <TableRow>
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
                  {t.activo && (
                    <Tooltip title="Desactivar">
                      <IconButton size="small" color="error" onClick={() => handleDesactivar(t)}>
                        <BlockIcon fontSize="small" />
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

      {/* Dialogs */}
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
