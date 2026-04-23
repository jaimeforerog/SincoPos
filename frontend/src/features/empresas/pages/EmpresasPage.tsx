import { useState } from 'react';
import {
  Box, Container, Typography, Button, Paper, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, IconButton, Chip, Dialog, DialogTitle,
  DialogContent, DialogActions, TextField, Switch, FormControlLabel, Alert,
  Skeleton, Tooltip,
} from '@mui/material';
import AddIcon      from '@mui/icons-material/Add';
import EditIcon     from '@mui/icons-material/Edit';
import BusinessIcon from '@mui/icons-material/Business';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { empresasApi } from '@/api/empresas';
import type { EmpresaDTO, CrearEmpresaDTO, ActualizarEmpresaDTO } from '@/types/api';

// ── Formulario (crear / editar) ────────────────────────────────────────────

interface FormState {
  nombre:       string;
  nit:          string;
  razonSocial:  string;
  activo:       boolean;
}

const EMPTY_FORM: FormState = { nombre: '', nit: '', razonSocial: '', activo: true };

interface EmpresaFormDialogProps {
  open:      boolean;
  empresa?:  EmpresaDTO;
  onClose:   () => void;
  onSaved:   () => void;
}

function EmpresaFormDialog({ open, empresa, onClose, onSaved }: EmpresaFormDialogProps) {
  const [form, setForm]     = useState<FormState>(empresa
    ? { nombre: empresa.nombre, nit: empresa.nit ?? '', razonSocial: empresa.razonSocial ?? '', activo: empresa.activo }
    : EMPTY_FORM);
  const [error, setError]   = useState('');
  const { enqueueSnackbar } = useSnackbar();
  const queryClient         = useQueryClient();

  const isEdit = !!empresa;

  const mutation = useMutation({
    mutationFn: () => isEdit
      ? empresasApi.update(empresa!.id, form as ActualizarEmpresaDTO)
      : empresasApi.create({ nombre: form.nombre, nit: form.nit || undefined, razonSocial: form.razonSocial || undefined } as CrearEmpresaDTO),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['empresas'] });
      enqueueSnackbar(isEdit ? 'Empresa actualizada' : 'Empresa creada', { variant: 'success' });
      onSaved();
    },
    onError: (err: unknown) => {
      setError((err as { message?: string })?.message ?? 'Error al guardar');
    },
  });

  const handleSubmit = () => {
    if (!form.nombre.trim()) { setError('El nombre es requerido'); return; }
    setError('');
    mutation.mutate();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{isEdit ? 'Editar empresa' : 'Nueva empresa'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          label="Nombre *"
          value={form.nombre}
          onChange={e => setForm(f => ({ ...f, nombre: e.target.value }))}
          size="small"
          fullWidth
        />
        <TextField
          label="NIT"
          value={form.nit}
          onChange={e => setForm(f => ({ ...f, nit: e.target.value }))}
          size="small"
          fullWidth
        />
        <TextField
          label="Razón social"
          value={form.razonSocial}
          onChange={e => setForm(f => ({ ...f, razonSocial: e.target.value }))}
          size="small"
          fullWidth
        />
        {isEdit && (
          <FormControlLabel
            control={<Switch checked={form.activo} onChange={e => setForm(f => ({ ...f, activo: e.target.checked }))} />}
            label="Activa"
          />
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={mutation.isPending}>
          {mutation.isPending ? 'Guardando…' : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Página principal ───────────────────────────────────────────────────────

export function EmpresasPage() {
  const [dialogOpen, setDialogOpen]         = useState(false);
  const [selected, setSelected]             = useState<EmpresaDTO | undefined>();

  const { data: empresas = [], isLoading, isError } = useQuery({
    queryKey: ['empresas'],
    queryFn:  empresasApi.getAll,
  });

  const openCreate = () => { setSelected(undefined); setDialogOpen(true); };
  const openEdit   = (e: EmpresaDTO) => { setSelected(e); setDialogOpen(true); };
  const handleClose = () => { setDialogOpen(false); setSelected(undefined); };

  return (
    <Container maxWidth="lg" sx={{ py: 3 }}>
      {/* Header */}
      <Box
        sx={{
          background: 'linear-gradient(135deg, #1565c0 0%, #0d47a1 60%, #01579b 100%)',
          borderRadius: 2,
          px: 3, py: 2.5, mb: 3,
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <BusinessIcon sx={{ color: '#fff', fontSize: 28 }} />
          <Box>
            <Typography variant="h5" fontWeight={700} color="#fff">Empresas</Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              Gestión de empresas (tenants) del sistema
            </Typography>
          </Box>
        </Box>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          sx={{ bgcolor: 'rgba(255,255,255,0.15)', '&:hover': { bgcolor: 'rgba(255,255,255,0.25)' }, color: '#fff' }}
        >
          Nueva empresa
        </Button>
      </Box>

      {/* Tabla */}
      {isError && <Alert severity="error" sx={{ mb: 2 }}>No se pudieron cargar las empresas.</Alert>}

      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow sx={{ bgcolor: 'grey.50' }}>
              <TableCell><strong>Nombre</strong></TableCell>
              <TableCell><strong>NIT</strong></TableCell>
              <TableCell><strong>Razón social</strong></TableCell>
              <TableCell align="center"><strong>Sucursales</strong></TableCell>
              <TableCell align="center"><strong>Estado</strong></TableCell>
              <TableCell align="right"><strong>Acciones</strong></TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading
              ? Array.from({ length: 3 }).map((_, i) => (
                  <TableRow key={i}>
                    {Array.from({ length: 6 }).map((__, j) => (
                      <TableCell key={j}><Skeleton /></TableCell>
                    ))}
                  </TableRow>
                ))
              : empresas.length === 0
              ? (
                  <TableRow>
                    <TableCell colSpan={6} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      No hay empresas registradas
                    </TableCell>
                  </TableRow>
                )
              : empresas.map(emp => (
                  <TableRow key={emp.id} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{emp.nombre}</TableCell>
                    <TableCell>{emp.nit ?? '—'}</TableCell>
                    <TableCell>{emp.razonSocial ?? '—'}</TableCell>
                    <TableCell align="center">
                      <Chip label={emp.cantidadSucursales} size="small" color="primary" variant="outlined" />
                    </TableCell>
                    <TableCell align="center">
                      <Chip
                        label={emp.activo ? 'Activa' : 'Inactiva'}
                        size="small"
                        color={emp.activo ? 'success' : 'default'}
                      />
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Editar">
                        <IconButton size="small" onClick={() => openEdit(emp)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))
            }
          </TableBody>
        </Table>
      </TableContainer>

      <EmpresaFormDialog
        open={dialogOpen}
        empresa={selected}
        onClose={handleClose}
        onSaved={handleClose}
      />
    </Container>
  );
}
