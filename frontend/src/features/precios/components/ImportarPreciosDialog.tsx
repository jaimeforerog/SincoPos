import { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  LinearProgress,
  Chip,
} from '@mui/material';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import DownloadIcon from '@mui/icons-material/Download';
import { useSnackbar } from 'notistack';
import type { ProductoDTO } from '@/types/api';

interface PrecioImportar {
  codigoBarras: string;
  precioVenta: number;
  precioMinimo?: number;
  producto?: ProductoDTO;
  error?: string;
}

interface ImportarPreciosDialogProps {
  open: boolean;
  sucursalId: number;
  sucursalNombre: string;
  productos: ProductoDTO[];
  preciosActuales: Map<string, { precioVenta: number; precioMinimo?: number }>;
  onClose: () => void;
  onImport: (precios: Array<{ productoId: string; precioVenta: number; precioMinimo?: number }>) => Promise<void>;
}

export function ImportarPreciosDialog({
  open,
  sucursalId: _sucursalId,
  sucursalNombre,
  productos,
  preciosActuales,
  onClose,
  onImport,
}: ImportarPreciosDialogProps) {
  const { enqueueSnackbar } = useSnackbar();
  const [precios, setPrecios] = useState<PrecioImportar[]>([]);
  const [importing, setImporting] = useState(false);
  const [progress, setProgress] = useState(0);

  const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    try {
      // Importar xlsx dinámicamente
      const XLSX = await import('xlsx');

      const data = await file.arrayBuffer();
      const workbook = XLSX.read(data);
      const worksheet = workbook.Sheets[workbook.SheetNames[0]];
      const jsonData = XLSX.utils.sheet_to_json<any>(worksheet);

      // Validar y mapear datos
      const preciosImportados: PrecioImportar[] = jsonData.map((row: any) => {
        const codigoBarras = String(
          row['Código de Barras'] ||
          row['Codigo de Barras'] ||
          row['Codigo'] ||
          row['CodigoBarras'] ||
          ''
        ).trim();

        const precioVenta = parseFloat(
          row['P. Venta'] ||
          row['Precio Venta'] ||
          row['PrecioVenta'] ||
          row['Precio'] ||
          0
        );

        const precioMinimo = (
          row['P. Mínimo'] ||
          row['P. Minimo'] ||
          row['Precio Mínimo'] ||
          row['Precio Minimo'] ||
          row['PrecioMinimo']
        ) ? parseFloat(
          row['P. Mínimo'] ||
          row['P. Minimo'] ||
          row['Precio Mínimo'] ||
          row['Precio Minimo'] ||
          row['PrecioMinimo']
        ) : undefined;

        // Buscar producto
        const producto = productos.find(
          (p) => p.codigoBarras.toLowerCase() === codigoBarras.toLowerCase()
        );

        let error: string | undefined;
        if (!codigoBarras) {
          error = 'Código de barras vacío';
        } else if (!producto) {
          error = 'Producto no encontrado';
        } else if (!precioVenta || precioVenta <= 0) {
          error = 'Precio inválido';
        } else if (!producto.activo) {
          error = 'Producto inactivo';
        }

        return {
          codigoBarras,
          precioVenta,
          precioMinimo,
          producto,
          error,
        };
      });

      setPrecios(preciosImportados);

      const validos = preciosImportados.filter((p) => !p.error).length;
      const invalidos = preciosImportados.filter((p) => p.error).length;

      enqueueSnackbar(
        `Archivo procesado: ${validos} válidos, ${invalidos} con errores`,
        { variant: validos > 0 ? 'success' : 'warning' }
      );
    } catch (error) {
      console.error('Error al leer archivo:', error);
      enqueueSnackbar('Error al leer el archivo Excel', { variant: 'error' });
    }

    // Limpiar input
    event.target.value = '';
  };

  const handleImport = async () => {
    const preciosValidos = precios.filter((p) => !p.error && p.producto);

    if (preciosValidos.length === 0) {
      enqueueSnackbar('No hay precios válidos para importar', { variant: 'warning' });
      return;
    }

    setImporting(true);
    setProgress(0);

    try {
      const preciosParaImportar = preciosValidos.map((p) => ({
        productoId: p.producto!.id,
        precioVenta: p.precioVenta,
        precioMinimo: p.precioMinimo,
      }));

      await onImport(preciosParaImportar);

      enqueueSnackbar(
        `${preciosValidos.length} precio(s) importado(s) exitosamente`,
        { variant: 'success' }
      );
      handleClose();
    } catch (error: any) {
      enqueueSnackbar(
        error.message || 'Error al importar precios',
        { variant: 'error' }
      );
    } finally {
      setImporting(false);
      setProgress(0);
    }
  };

  const handleClose = () => {
    setPrecios([]);
    setProgress(0);
    onClose();
  };

  const handleDownloadTemplate = async () => {
    try {
      // Importar xlsx dinámicamente
      const XLSX = await import('xlsx');

      // Crear datos pre-cargados con todos los productos
      const templateData = productos.map((producto) => {
        const precioActual = preciosActuales.get(producto.id);
        const precioVenta = precioActual?.precioVenta || '';
        const precioMinimo = precioActual?.precioMinimo || '';

        // Calcular margen solo si hay precio de venta
        const margen = precioVenta && producto.precioCosto > 0
          ? (((Number(precioVenta) - producto.precioCosto) / producto.precioCosto) * 100).toFixed(1) + '%'
          : '';

        return {
          'Sucursal': sucursalNombre,
          'Código de Barras': producto.codigoBarras,
          'Producto': producto.nombre,
          'P. Costo': producto.precioCosto,
          'P. Venta': precioVenta,
          'P. Mínimo': precioMinimo,
          'Margen': margen,
        };
      });

      // Crear hoja de trabajo
      const worksheet = XLSX.utils.json_to_sheet(templateData);

      // Ajustar ancho de columnas
      const columnWidths = [
        { wch: 20 }, // Sucursal
        { wch: 18 }, // Código de Barras
        { wch: 35 }, // Producto
        { wch: 12 }, // P. Costo
        { wch: 12 }, // P. Venta
        { wch: 12 }, // P. Mínimo
        { wch: 10 }, // Margen
      ];
      worksheet['!cols'] = columnWidths;

      // Crear libro de trabajo
      const workbook = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(workbook, worksheet, 'Precios');

      // Descargar archivo
      const fileName = `precios_${sucursalNombre.replace(/\s+/g, '_')}_${new Date().toISOString().split('T')[0]}.xlsx`;
      XLSX.writeFile(workbook, fileName);

      enqueueSnackbar(
        `Plantilla descargada con ${productos.length} producto(s)`,
        { variant: 'success' }
      );
    } catch (error) {
      console.error('Error al generar plantilla:', error);
      enqueueSnackbar('Error al generar la plantilla', { variant: 'error' });
    }
  };

  const validos = precios.filter((p) => !p.error).length;
  const invalidos = precios.filter((p) => p.error).length;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            Importar Precios desde Excel
          </Typography>
          <Button
            startIcon={<DownloadIcon />}
            onClick={handleDownloadTemplate}
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
            1. <strong>Descarga la plantilla</strong> - Se generará con todos los productos actuales
            <br />
            2. <strong>Abre en Excel</strong> - Verás todos los productos con sus datos
            <br />
            3. <strong>Completa las columnas:</strong>
            <br />
            &nbsp;&nbsp;&nbsp;• <strong>P. Venta:</strong> Precio de venta (obligatorio, mayor a 0)
            <br />
            &nbsp;&nbsp;&nbsp;• <strong>P. Mínimo:</strong> Precio mínimo para descuentos (opcional)
            <br />
            4. <strong>Guarda y sube el archivo</strong>
            <br />
            <br />
            <strong>Columnas pre-cargadas (solo lectura):</strong>
            <br />
            • Sucursal, Código de Barras, Producto, P. Costo, Margen
          </Typography>
        </Alert>

        {/* Botón de carga */}
        {precios.length === 0 && (
          <Box
            sx={{
              border: '2px dashed',
              borderColor: 'divider',
              borderRadius: 2,
              p: 4,
              textAlign: 'center',
              cursor: 'pointer',
              '&:hover': {
                borderColor: 'primary.main',
                bgcolor: 'action.hover',
              },
            }}
          >
            <input
              accept=".xlsx,.xls,.csv"
              style={{ display: 'none' }}
              id="upload-excel-file"
              type="file"
              onChange={handleFileUpload}
            />
            <label htmlFor="upload-excel-file">
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

        {/* Vista previa */}
        {precios.length > 0 && (
          <>
            <Box sx={{ mb: 2, display: 'flex', gap: 2 }}>
              <Chip
                icon={<CheckCircleIcon />}
                label={`${validos} válidos`}
                color="success"
              />
              <Chip
                icon={<ErrorIcon />}
                label={`${invalidos} con errores`}
                color="error"
              />
              <Box sx={{ flexGrow: 1 }} />
              <input
                accept=".xlsx,.xls,.csv"
                style={{ display: 'none' }}
                id="upload-excel-file-reload"
                type="file"
                onChange={handleFileUpload}
              />
              <label htmlFor="upload-excel-file-reload">
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
                    <TableCell sx={{ fontWeight: 700 }}>Código Barras</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Producto</TableCell>
                    <TableCell sx={{ fontWeight: 700 }} align="right">
                      Precio Venta
                    </TableCell>
                    <TableCell sx={{ fontWeight: 700 }} align="right">
                      Precio Mínimo
                    </TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {precios.map((precio, index) => (
                    <TableRow
                      key={index}
                      sx={{
                        bgcolor: precio.error ? 'error.lighter' : 'inherit',
                      }}
                    >
                      <TableCell>
                        {precio.error ? (
                          <Chip
                            icon={<ErrorIcon />}
                            label={precio.error}
                            color="error"
                            size="small"
                          />
                        ) : (
                          <Chip
                            icon={<CheckCircleIcon />}
                            label="OK"
                            color="success"
                            size="small"
                          />
                        )}
                      </TableCell>
                      <TableCell sx={{ fontFamily: 'monospace' }}>
                        {precio.codigoBarras}
                      </TableCell>
                      <TableCell>
                        {precio.producto?.nombre || (
                          <Typography variant="body2" color="text.secondary">
                            -
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell align="right">${precio.precioVenta.toLocaleString()}</TableCell>
                      <TableCell align="right">
                        {precio.precioMinimo ? `$${precio.precioMinimo.toLocaleString()}` : '-'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>

            {importing && (
              <Box sx={{ mt: 2 }}>
                <LinearProgress variant="determinate" value={progress} />
                <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5 }}>
                  Importando precios... {Math.round(progress)}%
                </Typography>
              </Box>
            )}
          </>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} disabled={importing}>
          Cancelar
        </Button>
        <Button
          variant="contained"
          onClick={handleImport}
          disabled={precios.length === 0 || validos === 0 || importing}
        >
          {importing ? 'Importando...' : `Importar ${validos} Precio(s)`}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
