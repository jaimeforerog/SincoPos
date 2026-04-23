import { useState, useEffect } from 'react';
import {
  Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  FormControl, FormControlLabel, IconButton, InputLabel, MenuItem,
  Paper, Select, Switch, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TextField, Tooltip, Typography, Alert, alpha,
} from '@mui/material';

const HERO_COLOR = '#1565c0';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Block as BlockIcon,
  CheckCircleOutline as ActivarIcon,
} from '@mui/icons-material';
import { impuestosApi, retencionesApi } from '@/api/impuestos';
import type { ImpuestoDTO, RetencionReglaDTO } from '@/types/api';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';

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

type TabType = 'impuestos' | 'retenciones';

// ── Componente principal ──────────────────────────────────────────────────────

export default function ImpuestosPage() {
  const [tab, setTab] = useState<TabType>('impuestos');
  const [impuestos, setImpuestos] = useState<ImpuestoDTO[]>([]);
  const [retenciones, setRetenciones] = useState<RetencionReglaDTO[]>([]);
  const [error, setError] = useState<string | null>(null);

  // Dialogo impuesto
  const [openImpDialog, setOpenImpDialog] = useState(false);
  const [editImpuesto, setEditImpuesto] = useState<ImpuestoDTO | null>(null);
  const [impForm, setImpForm] = useState({ nombre: '', tipo: 'IVA', porcentaje: '', valorFijo: '', cuentaContable: '', aplicaSobreBase: true, descripcion: '' });

  // Dialogo retencion
  const [openRetDialog, setOpenRetDialog] = useState(false);
  const [editRetencion, setEditRetencion] = useState<RetencionReglaDTO | null>(null);
  const [retForm, setRetForm] = useState({ nombre: '', tipo: 'ReteFuente', porcentaje: '', baseMinUVT: '4', codigoMunicipio: '', perfilVendedor: 'REGIMEN_ORDINARIO', perfilComprador: 'GRAN_CONTRIBUYENTE', cuentaContable: '' });

  const [confirmState, setConfirmState] = useState<{ open: boolean; mensaje: string; onAceptar: () => void }>({ open: false, mensaje: '', onAceptar: () => {} });
  const confirmar = (mensaje: string, onAceptar: () => void) => setConfirmState({ open: true, mensaje, onAceptar });

  useEffect(() => { cargarImpuestos(); cargarRetenciones(); }, []);

  const cargarImpuestos = async () => {
    try { setImpuestos(await impuestosApi.getAll()); }
    catch { setError('Error al cargar impuestos'); }
  };

  const cargarRetenciones = async () => {
    try { setRetenciones(await retencionesApi.getAll()); }
    catch { setError('Error al cargar retenciones'); }
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

  const desactivarImpuesto = (id: number) => {
    confirmar('¿Desactivar este impuesto?', async () => {
      try { await impuestosApi.deactivate(id); await cargarImpuestos(); }
      catch (e: any) { setError(e?.response?.data ?? 'No se puede desactivar'); }
    });
  };

  const activarImpuesto = (id: number) => {
    confirmar('¿Activar este impuesto?', async () => {
      try { await impuestosApi.activate(id); await cargarImpuestos(); }
      catch { setError('No se puede activar el impuesto'); }
    });
  };

  // ── Retenciones handlers ──────────────────────────────────────────────────

  const openCrearRetencion = () => {
    setEditRetencion(null);
    setRetForm({ nombre: '', tipo: 'ReteFuente', porcentaje: '', baseMinUVT: '4', codigoMunicipio: '', perfilVendedor: 'REGIMEN_ORDINARIO', perfilComprador: 'GRAN_CONTRIBUYENTE', cuentaContable: '' });
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
      };
      if (editRetencion) await retencionesApi.update(editRetencion.id, payload);
      else await retencionesApi.create(payload);
      setOpenRetDialog(false);
      await cargarRetenciones();
    } catch { setError('Error al guardar la retencion'); }
  };

  const desactivarRetencion = (id: number) => {
    confirmar('¿Desactivar esta regla de retención?', async () => {
      try { await retencionesApi.deactivate(id); await cargarRetenciones(); }
      catch { setError('No se puede desactivar la retencion'); }
    });
  };

  const activarRetencion = (id: number) => {
    confirmar('¿Activar esta regla de retención?', async () => {
      try { await retencionesApi.activate(id); await cargarRetenciones(); }
      catch { setError('No se puede activar la retención'); }
    });
  };

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <Box sx={{ p: 3, maxWidth: 1200, mx: 'auto' }}>

      <ReportePageHeader
        title="Motor de Impuestos"
        subtitle="Configuración de IVA, INC y retenciones"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Motor de Impuestos' },
        ]}
        backPath="/configuracion"
        color="#1565c0"
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
          </Box>
        }
      />

      {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>{error}</Alert>}

      {/* Tabs */}
      <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
        {([
          { key: 'impuestos' as TabType, label: 'Impuestos (IVA, INC...)' },
          { key: 'retenciones' as TabType, label: 'Retenciones' },
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
                    <Tooltip title="Activar"><IconButton id={`btn-activar-impuesto-${imp.id}`} size="small" color="success" onClick={() => activarImpuesto(imp.id)}><ActivarIcon fontSize="small" /></IconButton></Tooltip>
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
                  <TableCell>{r.codigoMunicipio ?? 'Nacional'}</TableCell>
                  <TableCell>
                    <Typography variant="caption">{r.perfilVendedor} / {r.perfilComprador}</Typography>
                  </TableCell>
                  <TableCell><Chip label={r.activo ? 'Activa' : 'Inactiva'} size="small" color={r.activo ? 'success' : 'default'} /></TableCell>
                  <TableCell align="center">
                    <Tooltip title="Editar"><IconButton id={`btn-editar-retencion-${r.id}`} size="small" onClick={() => openEditarRetencion(r)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                    {r.activo
                      ? <Tooltip title="Desactivar"><IconButton id={`btn-desactivar-retencion-${r.id}`} size="small" color="error" onClick={() => desactivarRetencion(r.id)}><BlockIcon fontSize="small" /></IconButton></Tooltip>
                      : <Tooltip title="Activar"><IconButton id={`btn-activar-retencion-${r.id}`} size="small" color="success" onClick={() => activarRetencion(r.id)}><ActivarIcon fontSize="small" /></IconButton></Tooltip>
                    }
                  </TableCell>
                </TableRow>
              ))}
              {retenciones.length === 0 && (
                <TableRow><TableCell colSpan={8} align="center" sx={{ py: 4, color: 'text.secondary' }}>No hay reglas de retencion</TableCell></TableRow>
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

      <ConfirmDialog
        open={confirmState.open}
        mensaje={confirmState.mensaje}
        onAceptar={() => { confirmState.onAceptar(); setConfirmState(s => ({ ...s, open: false })); }}
        onCancelar={() => setConfirmState(s => ({ ...s, open: false }))}
      />
    </Box>
  );
}
