import { useState, useEffect } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  LinearProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  FileUpload as FileUploadIcon,
  Download as DownloadIcon,
  CheckCircle as CheckCircleIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  CloudUpload as CloudUploadIcon,
} from '@mui/icons-material';
import { tercerosApi } from '@/api/terceros';
import type { ResultadoImportacionTercerosDTO } from '@/types/api';

export interface ImportarTercerosDialogProps {
  open: boolean;
  onClose: () => void;
  onImportado: () => void;
}

export function ImportarTercerosDialog({ open, onClose, onImportado }: ImportarTercerosDialogProps) {
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
