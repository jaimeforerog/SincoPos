import { useState, useEffect, useRef } from 'react';
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
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Tab,
  Tabs,
  TextField,
} from '@mui/material';
import { tercerosApi } from '@/api/terceros';
import type { TerceroDTO } from '@/types/api';

const TIPOS_IDENTIFICACION = ['CC', 'NIT', 'CE', 'Pasaporte', 'TI', 'Otro'];
const TIPOS_TERCERO = ['Cliente', 'Proveedor', 'Ambos'];
const PERFILES_TRIBUTARIOS = [
  'REGIMEN_COMUN',
  'GRAN_CONTRIBUYENTE',
  'REGIMEN_SIMPLE',
  'PERSONA_NATURAL',
];

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

export interface TerceroFormDialogProps {
  open: boolean;
  editando: TerceroDTO | null;
  onClose: () => void;
  onSaved: () => void;
}

export function TerceroFormDialog({ open, editando, onClose, onSaved }: TerceroFormDialogProps) {
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
