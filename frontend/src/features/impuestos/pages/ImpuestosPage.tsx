import { useState, useEffect } from 'react';
import {
  Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  FormControl, FormControlLabel, IconButton, InputLabel, MenuItem,
  Paper, Select, Switch, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TextField, Tooltip, Typography, Alert, alpha,
} from '@mui/material';

const HERO_COLOR = '#c62828';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Block as BlockIcon,
} from '@mui/icons-material';
import { impuestosApi, retencionesApi, conceptosRetencionApi } from '@/api/impuestos';
import type { ImpuestoDTO, RetencionReglaDTO, ConceptoRetencionDTO } from '@/types/api';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';

// ── Helpers ───────────────────────────────────────────────────────────────────

const TIPO_IMPUESTO_LABELS: Record<string, string> = {
  IVA: 'IVA',
  INC: 'INC',
  Saludable: 'Saludable',
  Bolsa: 'Bolsa',
};

const TIPO_RETENCION_LABELS: Record<string, string> = {
  ReteFuente: 'ReteFuente',
  ReteICA: 'ReteICA',
  ReteIVA: 'ReteIVA',
};

const PERFILES_TRIBUTARIOS = [
  'GRAN_CONTRIBUYENTE',
  'REGIMEN_COMUN',
  'REGIMEN_ORDINARIO',
  'REGIMEN_SIMPLE',
  'PERSONA_NATURAL',
];

const chipColor = (tipo: string) => {
  const map: Record<string, 'success' | 'warning' | 'error' | 'info'> = {
    IVA: 'info', INC: 'warning', Saludable: 'error', Bolsa: 'success',
  };
  return map[tipo] ?? 'default';
};

type TabType = 'impuestos' | 'retenciones' | 'conceptos';

// ── Componente principal ──────────────────────────────────────────────────────

export default function ImpuestosPage() {
  const [tab, setTab] = useState<TabType>('impuestos');
  const [impuestos, setImpuestos] = useState<ImpuestoDTO[]>([]);
  const [retenciones, setRetenciones] = useState<RetencionReglaDTO[]>([]);
  const [conceptos, setConceptos] = useState<ConceptoRetencionDTO[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Dialogo impuesto
  const [openImpDialog, setOpenImpDialog] = useState(false);
  const [editImpuesto, setEditImpuesto] = useState<ImpuestoDTO | null>(null);
  const [impForm, setImpForm] = useState({ nombre: '', tipo: 'IVA', porcentaje: '', valorFijo: '', cuentaContable: '', aplicaSobreBase: true, descripcion: '' });

  // Dialogo retencion
  const [openRetDialog, setOpenRetDialog] = useState(false);
  const [editRetencion, setEditRetencion] = useState<RetencionReglaDTO | null>(null);
  const [retForm, setRetForm] = useState({ nombre: '', tipo: 'ReteFuente', porcentaje: '', baseMinUVT: '4', codigoMunicipio: '', perfilVendedor: 'REGIMEN_ORDINARIO', perfilComprador: 'GRAN_CONTRIBUYENTE', cuentaContable: '', conceptoRetencionId: '' });

  // Dialogo concepto retencion
  const [openConceptoDialog, setOpenConceptoDialog] = useState(false);
  const [editConcepto, setEditConcepto] = useState<ConceptoRetencionDTO | null>(null);
  const [conceptoForm, setConceptoForm] = useState({ nombre: '', codigoDian: '', porcentajeSugerido: '' });

  useEffect(() => { cargarImpuestos(); cargarRetenciones(); cargarConceptos(); }, []);

  const cargarImpuestos = async () => {
    try { setImpuestos(await impuestosApi.getAll()); }
    catch { setError('Error al cargar impuestos'); }
  };

  const cargarRetenciones = async () => {
    try { setRetenciones(await retencionesApi.getAll()); }
    catch { setError('Error al cargar retenciones'); }
  };

  const cargarConceptos = async () => {
    try { setConceptos(await conceptosRetencionApi.getAll()); }
    catch { setError('Error al cargar conceptos de retencion'); }
  };

  // ── Impuestos handlers ────────────────────────────────────────────────────

  const openCrearImpuesto = () => {
    setEditImpuesto(null);
    setImpForm({ nombre: '', tipo: 'IVA', porcentaje: '', valorFijo: '', cuentaContable: '', aplicaSobreBase: true, descripcion: '' });
    setOpenImpDialog(true);
  };

  const openEditarImpuesto = (imp: ImpuestoDTO) => {
    setEditImpuesto(imp);
    setImpForm({
      nombre: imp.nombre, tipo: imp.tipo,
      porcentaje: String(imp.porcentaje * 100),
      valorFijo: imp.valorFijo ? String(imp.valorFijo) : '',
      cuentaContable: imp.codigoCuentaContable ?? '',
      aplicaSobreBase: imp.aplicaSobreBase,
      descripcion: imp.descripcion ?? '',
    });
    setOpenImpDialog(true);
  };

  const guardarImpuesto = async () => {
    try {
      const payload = {
        nombre: impForm.nombre,
        tipo: impForm.tipo,
        porcentaje: parseFloat(impForm.porcentaje) / 100 || 0,
        valorFijo: impForm.valorFijo ? parseFloat(impForm.valorFijo) : undefined,
        codigoCuentaContable: impForm.cuentaContable || undefined,
        aplicaSobreBase: impForm.aplicaSobreBase,
        descripcion: impForm.descripcion || undefined,
      };
      if (editImpuesto) await impuestosApi.update(editImpuesto.id, payload);
      else await impuestosApi.create(payload);
      setOpenImpDialog(false);
      await cargarImpuestos();
    } catch { setError('Error al guardar el impuesto'); }
  };

  const desactivarImpuesto = async (id: number) => {
    if (!window.confirm('Desactivar este impuesto?')) return;
    try { await impuestosApi.deactivate(id); await cargarImpuestos(); }
    catch (e: any) { setError(e?.response?.data ?? 'No se puede desactivar'); }
  };

  // ── Retenciones handlers ──────────────────────────────────────────────────

  const openCrearRetencion = () => {
    setEditRetencion(null);
    setRetForm({ nombre: '', tipo: 'ReteFuente', porcentaje: '', baseMinUVT: '4', codigoMunicipio: '', perfilVendedor: 'REGIMEN_ORDINARIO', perfilComprador: 'GRAN_CONTRIBUYENTE', cuentaContable: '', conceptoRetencionId: '' });
    setOpenRetDialog(true);
  };

  const openEditarRetencion = (r: RetencionReglaDTO) => {
    setEditRetencion(r);
    setRetForm({
      nombre: r.nombre, tipo: r.tipo,
      porcentaje: String(r.porcentaje * 100),
      baseMinUVT: String(r.baseMinUVT),
      codigoMunicipio: r.codigoMunicipio ?? '',
      perfilVendedor: r.perfilVendedor,
      perfilComprador: r.perfilComprador,
      cuentaContable: r.codigoCuentaContable ?? '',
      conceptoRetencionId: r.conceptoRetencionId ? String(r.conceptoRetencionId) : '',
    });
    setOpenRetDialog(true);
  };

  const guardarRetencion = async () => {
    try {
      const payload = {
        nombre: retForm.nombre, tipo: retForm.tipo,
        porcentaje: parseFloat(retForm.porcentaje) / 100 || 0,
        baseMinUVT: parseFloat(retForm.baseMinUVT) || 4,
        codigoMunicipio: retForm.codigoMunicipio || undefined,
        perfilVendedor: retForm.perfilVendedor,
        perfilComprador: retForm.perfilComprador,
        codigoCuentaContable: retForm.cuentaContable || undefined,
        conceptoRetencionId: retForm.conceptoRetencionId ? parseInt(retForm.conceptoRetencionId) : undefined,
      };
      if (editRetencion) await retencionesApi.update(editRetencion.id, payload);
      else await retencionesApi.create(payload);
      setOpenRetDialog(false);
      await cargarRetenciones();
    } catch { setError('Error al guardar la retencion'); }
  };

  const desactivarRetencion = async (id: number) => {
    if (!window.confirm('Desactivar esta regla de retencion?')) return;
    try { await retencionesApi.deactivate(id); await cargarRetenciones(); }
    catch { setError('No se puede desactivar la retencion'); }
  };

  // ── Conceptos Retencion handlers ──────────────────────────────────────────

  const openCrearConcepto = () => {
    setEditConcepto(null);
    setConceptoForm({ nombre: '', codigoDian: '', porcentajeSugerido: '' });
    setOpenConceptoDialog(true);
  };

  const openEditarConcepto = (c: ConceptoRetencionDTO) => {
    setEditConcepto(c);
    setConceptoForm({
      nombre: c.nombre,
      codigoDian: c.codigoDian ?? '',
      porcentajeSugerido: c.porcentajeSugerido != null ? String(c.porcentajeSugerido) : '',
    });
    setOpenConceptoDialog(true);
  };

  const guardarConcepto = async () => {
    try {
      const payload = {
        nombre: conceptoForm.nombre,
        codigoDian: conceptoForm.codigoDian || undefined,
        porcentajeSugerido: conceptoForm.porcentajeSugerido ? parseFloat(conceptoForm.porcentajeSugerido) : undefined,
      };
      if (editConcepto) await conceptosRetencionApi.update(editConcepto.id, payload);
      else await conceptosRetencionApi.create(payload);
      setOpenConceptoDialog(false);
      await cargarConceptos();
    } catch { setError('Error al guardar el concepto de retencion'); }
  };

  const desactivarConcepto = async (id: number) => {
    if (!window.confirm('Desactivar este concepto de retencion?')) return;
    try { await conceptosRetencionApi.deactivate(id); await cargarConceptos(); }
    catch { setError('No se puede desactivar el concepto'); }
  };

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <Box sx={{ p: 3, maxWidth: 1200, mx: 'auto' }}>

      <ReportePageHeader
        title="Motor de Impuestos"
        subtitle="Configuración de IVA, INC, retenciones DIAN y conceptos"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Motor de Impuestos' },
        ]}
        backPath="/configuracion"
        color="#c62828"
        action={
          <Box>
            {tab === 'impuestos' && (
              <Button id="btn-nuevo-impuesto" variant="contained" startIcon={<AddIcon />} onClick={openCrearImpuesto}
                sx={{ bgcolor: 'rgba(255,255,255,0.15)', color: '#fff', border: '1px solid rgba(255,255,255,0.35)', fontWeight: 700, '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' } }}>
                Nuevo Impuesto
              </Button>
            )}
            {tab === 'retenciones' && (
              <Button id="btn-nueva-retencion" variant="contained" startIcon={<AddIcon />} onClick={openCrearRetencion}
                sx={{ bgcolor: 'rgba(255,255,255,0.15)', color: '#fff', border: '1px solid rgba(255,255,255,0.35)', fontWeight: 700, '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' } }}>
                Nueva Retencion
              </Button>
            )}
            {tab === 'conceptos' && (
              <Button id="btn-nuevo-concepto" variant="contained" startIcon={<AddIcon />} onClick={openCrearConcepto}
                sx={{ bgcolor: 'rgba(255,255,255,0.15)', color: '#fff', border: '1px solid rgba(255,255,255,0.35)', fontWeight: 700, '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' } }}>
                Nuevo Concepto
              </Button>
            )}
          </Box>
        }
      />

      {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>{error}</Alert>}

      {/* Tabs */}
      <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
        {([
          { key: 'impuestos' as TabType, label: 'Impuestos (IVA, INC...)' },
          { key: 'retenciones' as TabType, label: 'Retenciones' },
          { key: 'conceptos' as TabType, label: 'Conceptos Retencion DIAN' },
        ]).map((t) => (
          <Button key={t.key} id={`tab-${t.key}`}
            variant={tab === t.key ? 'contained' : 'outlined'}
            onClick={() => setTab(t.key)} size="small" sx={{ borderRadius: 2 }}>
            {t.label}
          </Button>
        ))}
      </Box>

      {/* ── Tabla de Impuestos ─────────────────────────────────────────────── */}
      {tab === 'impuestos' && (
        <TableContainer component={Paper} variant="outlined">
          <Table id="tabla-impuestos" size="small">
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
                <TableCell>Tipo</TableCell>
                <TableCell align="right">Tarifa</TableCell>
                <TableCell>Acumulable</TableCell>
                <TableCell>Cuenta Contable</TableCell>
                <TableCell>País</TableCell>
                <TableCell align="center">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {impuestos.map((imp) => (
                <TableRow key={imp.id} hover>
                  <TableCell>
                    <Typography fontWeight={600}>{imp.nombre}</Typography>
                    {imp.descripcion && <Typography variant="caption" color="text.secondary">{imp.descripcion}</Typography>}
                  </TableCell>
                  <TableCell>
                    <Chip label={TIPO_IMPUESTO_LABELS[imp.tipo] ?? imp.tipo} color={chipColor(imp.tipo)} size="small" />
                  </TableCell>
                  <TableCell align="right">
                    {imp.valorFijo ? (
                      <Typography>${imp.valorFijo.toLocaleString('es-CO')}<Typography component="span" variant="caption" color="text.secondary">/unidad</Typography></Typography>
                    ) : (
                      <Typography>{(imp.porcentaje * 100).toFixed(2)}%</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Chip label={imp.aplicaSobreBase ? 'Acumulable' : 'Monofasico'} size="small" variant="outlined" color={imp.aplicaSobreBase ? 'success' : 'default'} />
                  </TableCell>
                  <TableCell><code>{imp.codigoCuentaContable ?? '\u2014'}</code></TableCell>
                  <TableCell>{imp.codigoPais}</TableCell>
                  <TableCell align="center">
                    <Tooltip title="Editar"><IconButton id={`btn-editar-impuesto-${imp.id}`} size="small" onClick={() => openEditarImpuesto(imp)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                    <Tooltip title="Desactivar"><IconButton id={`btn-desactivar-impuesto-${imp.id}`} size="small" color="error" onClick={() => desactivarImpuesto(imp.id)}><BlockIcon fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {impuestos.length === 0 && (
                <TableRow><TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>No hay impuestos activos</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* ── Tabla de Retenciones ───────────────────────────────────────────── */}
      {tab === 'retenciones' && (
        <TableContainer component={Paper} variant="outlined">
          <Table id="tabla-retenciones" size="small">
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
                <TableCell>Tipo</TableCell>
                <TableCell align="right">%</TableCell>
                <TableCell align="right">Base mín. (UVT)</TableCell>
                <TableCell>Concepto DIAN</TableCell>
                <TableCell>Municipio</TableCell>
                <TableCell>Perfil Vendedor / Comprador</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="center">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {retenciones.map((r) => (
                <TableRow key={r.id} hover sx={{ opacity: r.activo ? 1 : 0.5 }}>
                  <TableCell><Typography fontWeight={600}>{r.nombre}</Typography></TableCell>
                  <TableCell><Chip label={TIPO_RETENCION_LABELS[r.tipo] ?? r.tipo} size="small" color="warning" /></TableCell>
                  <TableCell align="right">{(r.porcentaje * 100).toFixed(3)}%</TableCell>
                  <TableCell align="right">{r.baseMinUVT}</TableCell>
                  <TableCell>
                    {r.conceptoRetencionNombre
                      ? <Chip label={r.conceptoRetencionNombre} size="small" variant="outlined" color="info" />
                      : <Typography variant="caption" color="text.secondary">Todos</Typography>}
                  </TableCell>
                  <TableCell>{r.codigoMunicipio ?? 'Nacional'}</TableCell>
                  <TableCell>
                    <Typography variant="caption">{r.perfilVendedor} / {r.perfilComprador}</Typography>
                  </TableCell>
                  <TableCell><Chip label={r.activo ? 'Activa' : 'Inactiva'} size="small" color={r.activo ? 'success' : 'default'} /></TableCell>
                  <TableCell align="center">
                    <Tooltip title="Editar"><IconButton id={`btn-editar-retencion-${r.id}`} size="small" onClick={() => openEditarRetencion(r)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                    {r.activo && <Tooltip title="Desactivar"><IconButton id={`btn-desactivar-retencion-${r.id}`} size="small" color="error" onClick={() => desactivarRetencion(r.id)}><BlockIcon fontSize="small" /></IconButton></Tooltip>}
                  </TableCell>
                </TableRow>
              ))}
              {retenciones.length === 0 && (
                <TableRow><TableCell colSpan={9} align="center" sx={{ py: 4, color: 'text.secondary' }}>No hay reglas de retencion</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* ── Tabla de Conceptos de Retencion ─────────────────────────────────── */}
      {tab === 'conceptos' && (
        <TableContainer component={Paper} variant="outlined">
          <Table id="tabla-conceptos-retencion" size="small">
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
                <TableCell>Código DIAN</TableCell>
                <TableCell align="right">% Sugerido</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="center">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {conceptos.map((c) => (
                <TableRow key={c.id} hover sx={{ opacity: c.activo ? 1 : 0.5 }}>
                  <TableCell><Typography fontWeight={600}>{c.nombre}</Typography></TableCell>
                  <TableCell><code>{c.codigoDian ?? '\u2014'}</code></TableCell>
                  <TableCell align="right">{c.porcentajeSugerido != null ? `${c.porcentajeSugerido}%` : '\u2014'}</TableCell>
                  <TableCell><Chip label={c.activo ? 'Activo' : 'Inactivo'} size="small" color={c.activo ? 'success' : 'default'} /></TableCell>
                  <TableCell align="center">
                    <Tooltip title="Editar"><IconButton id={`btn-editar-concepto-${c.id}`} size="small" onClick={() => openEditarConcepto(c)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                    {c.activo && <Tooltip title="Desactivar"><IconButton id={`btn-desactivar-concepto-${c.id}`} size="small" color="error" onClick={() => desactivarConcepto(c.id)}><BlockIcon fontSize="small" /></IconButton></Tooltip>}
                  </TableCell>
                </TableRow>
              ))}
              {conceptos.length === 0 && (
                <TableRow><TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>No hay conceptos de retencion</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* ── Dialog Impuesto ────────────────────────────────────────────────── */}
      <Dialog open={openImpDialog} onClose={() => setOpenImpDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editImpuesto ? 'Editar Impuesto' : 'Nuevo Impuesto'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          <TextField id="imp-nombre" label="Nombre" value={impForm.nombre} onChange={e => setImpForm(f => ({ ...f, nombre: e.target.value }))} fullWidth required />
          <FormControl fullWidth>
            <InputLabel id="imp-tipo-label">Tipo</InputLabel>
            <Select id="imp-tipo" labelId="imp-tipo-label" label="Tipo" value={impForm.tipo} onChange={e => setImpForm(f => ({ ...f, tipo: e.target.value }))}>
              {Object.keys(TIPO_IMPUESTO_LABELS).map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </Select>
          </FormControl>
          {impForm.tipo !== 'Bolsa' && (
            <TextField id="imp-porcentaje" label="Porcentaje (%)" type="number" value={impForm.porcentaje} onChange={e => setImpForm(f => ({ ...f, porcentaje: e.target.value }))} helperText="Ej: 19 para 19%" />
          )}
          {impForm.tipo === 'Bolsa' && (
            <TextField id="imp-valor-fijo" label="Valor fijo por unidad ($)" type="number" value={impForm.valorFijo} onChange={e => setImpForm(f => ({ ...f, valorFijo: e.target.value }))} helperText="Ej: 66 para $66 por bolsa" />
          )}
          <TextField id="imp-cuenta" label="Cuenta Contable GL" value={impForm.cuentaContable} onChange={e => setImpForm(f => ({ ...f, cuentaContable: e.target.value }))} helperText="Ej: 2408 (IVA por pagar)" />
          <FormControlLabel
            control={<Switch id="imp-aplica-base" checked={impForm.aplicaSobreBase} onChange={e => setImpForm(f => ({ ...f, aplicaSobreBase: e.target.checked }))} />}
            label={impForm.aplicaSobreBase ? 'Acumulable con otros impuestos (IVA)' : 'Monofasico \u2014 no acumula (INC)'}
          />
          <TextField id="imp-descripcion" label="Descripcion" value={impForm.descripcion} onChange={e => setImpForm(f => ({ ...f, descripcion: e.target.value }))} multiline rows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenImpDialog(false)}>Cancelar</Button>
          <Button id="btn-guardar-impuesto" variant="contained" onClick={guardarImpuesto} disabled={!impForm.nombre}>Guardar</Button>
        </DialogActions>
      </Dialog>

      {/* ── Dialog Retencion ───────────────────────────────────────────────── */}
      <Dialog open={openRetDialog} onClose={() => setOpenRetDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editRetencion ? 'Editar Retencion' : 'Nueva Regla de Retencion'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          <TextField id="ret-nombre" label="Nombre" value={retForm.nombre} onChange={e => setRetForm(f => ({ ...f, nombre: e.target.value }))} fullWidth required />
          <FormControl fullWidth>
            <InputLabel>Tipo</InputLabel>
            <Select id="ret-tipo" label="Tipo" value={retForm.tipo} onChange={e => setRetForm(f => ({ ...f, tipo: e.target.value }))}>
              {Object.keys(TIPO_RETENCION_LABELS).map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </Select>
          </FormControl>
          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
            <TextField id="ret-porcentaje" label="Porcentaje (%)" type="number" value={retForm.porcentaje} onChange={e => setRetForm(f => ({ ...f, porcentaje: e.target.value }))} helperText="Ej: 2.5" />
            <TextField id="ret-base-uvt" label="Base minima (UVT)" type="number" value={retForm.baseMinUVT} onChange={e => setRetForm(f => ({ ...f, baseMinUVT: e.target.value }))} helperText="Ej: 4 UVT" />
          </Box>
          {retForm.tipo === 'ReteFuente' && (
            <FormControl fullWidth>
              <InputLabel id="ret-concepto-label">Concepto Retencion DIAN</InputLabel>
              <Select
                id="ret-concepto"
                labelId="ret-concepto-label"
                label="Concepto Retencion DIAN"
                value={retForm.conceptoRetencionId}
                onChange={e => setRetForm(f => ({ ...f, conceptoRetencionId: e.target.value }))}
              >
                <MenuItem value="">
                  <em>Todos los conceptos</em>
                </MenuItem>
                {conceptos.filter(c => c.activo).map(c => (
                  <MenuItem key={c.id} value={String(c.id)}>
                    {c.codigoDian ? `${c.codigoDian} - ` : ''}{c.nombre}{c.porcentajeSugerido != null ? ` (${c.porcentajeSugerido}%)` : ''}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
          <TextField id="ret-municipio" label="Codigo Municipio DANE" value={retForm.codigoMunicipio} onChange={e => setRetForm(f => ({ ...f, codigoMunicipio: e.target.value }))} helperText="Solo ReteICA: 11001 = Bogota. Vacio = Nacional." />
          <FormControl fullWidth>
            <InputLabel>Perfil Vendedor</InputLabel>
            <Select id="ret-vendedor" label="Perfil Vendedor" value={retForm.perfilVendedor} onChange={e => setRetForm(f => ({ ...f, perfilVendedor: e.target.value }))}>
              {PERFILES_TRIBUTARIOS.map(p => <MenuItem key={p} value={p}>{p}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl fullWidth>
            <InputLabel>Perfil Comprador</InputLabel>
            <Select id="ret-comprador" label="Perfil Comprador" value={retForm.perfilComprador} onChange={e => setRetForm(f => ({ ...f, perfilComprador: e.target.value }))}>
              {PERFILES_TRIBUTARIOS.map(p => <MenuItem key={p} value={p}>{p}</MenuItem>)}
            </Select>
          </FormControl>
          <TextField id="ret-cuenta" label="Cuenta Contable GL" value={retForm.cuentaContable} onChange={e => setRetForm(f => ({ ...f, cuentaContable: e.target.value }))} helperText="Ej: 1355 (Anticipo de impuestos)" />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenRetDialog(false)}>Cancelar</Button>
          <Button id="btn-guardar-retencion" variant="contained" onClick={guardarRetencion} disabled={!retForm.nombre}>Guardar</Button>
        </DialogActions>
      </Dialog>

      {/* ── Dialog Concepto Retencion ──────────────────────────────────────── */}
      <Dialog open={openConceptoDialog} onClose={() => setOpenConceptoDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editConcepto ? 'Editar Concepto de Retencion' : 'Nuevo Concepto de Retencion'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          <TextField
            id="concepto-nombre"
            label="Nombre"
            value={conceptoForm.nombre}
            onChange={e => setConceptoForm(f => ({ ...f, nombre: e.target.value }))}
            fullWidth
            required
            helperText="Ej: Compras generales, Servicios generales, Honorarios"
          />
          <TextField
            id="concepto-codigo-dian"
            label="Codigo DIAN"
            value={conceptoForm.codigoDian}
            onChange={e => setConceptoForm(f => ({ ...f, codigoDian: e.target.value }))}
            fullWidth
            helperText="Ej: 2307 (Compras), 2304 (Servicios), 2301 (Honorarios)"
          />
          <TextField
            id="concepto-porcentaje"
            label="Porcentaje Sugerido (%)"
            type="number"
            value={conceptoForm.porcentajeSugerido}
            onChange={e => setConceptoForm(f => ({ ...f, porcentajeSugerido: e.target.value }))}
            fullWidth
            helperText="Referencia para la interfaz. Ej: 2.5, 4, 11"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenConceptoDialog(false)}>Cancelar</Button>
          <Button id="btn-guardar-concepto" variant="contained" onClick={guardarConcepto} disabled={!conceptoForm.nombre}>Guardar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
