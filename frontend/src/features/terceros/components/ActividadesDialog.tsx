import { useState, useEffect } from 'react';
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
  FormControlLabel,
  IconButton,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
} from '@mui/material';
import {
  StarBorder as StarBorderIcon,
  Delete as DeleteIcon,
} from '@mui/icons-material';
import { tercerosApi } from '@/api/terceros';
import type { TerceroDTO, TerceroActividadDTO } from '@/types/api';

export interface ActividadesDialogProps {
  open: boolean;
  tercero: TerceroDTO | null;
  onClose: () => void;
  onChanged: () => void;
}

export function ActividadesDialog({ open, tercero, onClose, onChanged }: ActividadesDialogProps) {
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
