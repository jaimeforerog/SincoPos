import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Container,
  Paper,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  IconButton,
  TextField,
  MenuItem,
  Alert,
  CircularProgress,
  Tooltip,
  Stack,
  Pagination,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import ReplayIcon from '@mui/icons-material/Replay';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { useSnackbar } from 'notistack';
import { useAuth } from '@/hooks/useAuth';
import { facturacionApi } from '@/api/facturacion';
import type { DocumentoElectronicoDTO } from '@/types/api';

const ESTADO_LABELS: Record<number, { label: string; color: any }> = {
  0: { label: 'Pendiente', color: 'default' },
  1: { label: 'Generado', color: 'info' },
  2: { label: 'Firmado', color: 'info' },
  3: { label: 'Enviado', color: 'warning' },
  4: { label: 'Aceptado', color: 'success' },
  5: { label: 'Rechazado', color: 'error' },
};

const TIPO_LABELS: Record<string, string> = {
  FV: 'Factura Venta',
  NC: 'Nota Crédito',
  ND: 'Nota Débito',
};

export function DocumentosElectronicosPage() {
  const { activeSucursalId, user } = useAuth();
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();
  const isAdmin = user?.rol === 'admin';

  const [filtroEstado, setFiltroEstado] = useState<string>('');
  const [filtroTipo, setFiltroTipo] = useState<string>('');
  const [fechaDesde, setFechaDesde] = useState<string>('');
  const [fechaHasta, setFechaHasta] = useState<string>('');
  const [page, setPage] = useState(1);
  const [confirmReintentar, setConfirmReintentar] = useState<DocumentoElectronicoDTO | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['documentos-electronicos', activeSucursalId, filtroEstado, filtroTipo, fechaDesde, fechaHasta, page],
    queryFn: () =>
      facturacionApi.listarDocumentos({
        sucursalId: activeSucursalId ?? undefined,
        estado: filtroEstado !== '' ? parseInt(filtroEstado) : undefined,
        tipoDocumento: filtroTipo || undefined,
        fechaDesde: fechaDesde ? `${fechaDesde}T00:00:00Z` : undefined,
        fechaHasta: fechaHasta ? `${fechaHasta}T23:59:59Z` : undefined,
        pageNumber: page,
        pageSize: 20,
      }),
    enabled: true,
    refetchInterval: 30000,
  });

  const reintentarMutation = useMutation({
    mutationFn: (id: number) => facturacionApi.reintentar(id),
    onSuccess: () => {
      enqueueSnackbar('Documento reenviado a DIAN', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['documentos-electronicos'] });
      setConfirmReintentar(null);
    },
    onError: (err: any) => {
      enqueueSnackbar(err.response?.data?.error ?? 'Error reenviando documento', { variant: 'error' });
      setConfirmReintentar(null);
    },
  });

  const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleString('es-CO', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit',
    });

  const handleDescarga = (doc: DocumentoElectronicoDTO) => {
    const url = facturacionApi.descargarXml(doc.id);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${doc.numeroCompleto}.xml`;
    a.click();
  };

  return (
    <Container maxWidth="xl">
      <Typography variant="h4" sx={{ mb: 3, fontWeight: 700 }}>
        Documentos Electrónicos DIAN
      </Typography>

      {/* Filtros */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
          <TextField
            label="Fecha Desde" type="date" value={fechaDesde}
            onChange={(e) => { setFechaDesde(e.target.value); setPage(1); }}
            size="small" sx={{ minWidth: 160 }} InputLabelProps={{ shrink: true }}
          />
          <TextField
            label="Fecha Hasta" type="date" value={fechaHasta}
            onChange={(e) => { setFechaHasta(e.target.value); setPage(1); }}
            size="small" sx={{ minWidth: 160 }} InputLabelProps={{ shrink: true }}
          />
          <TextField
            select label="Tipo" value={filtroTipo}
            onChange={(e) => { setFiltroTipo(e.target.value); setPage(1); }}
            size="small" sx={{ minWidth: 160 }}
          >
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="FV">Factura Venta</MenuItem>
            <MenuItem value="NC">Nota Crédito</MenuItem>
            <MenuItem value="ND">Nota Débito</MenuItem>
          </TextField>
          <TextField
            select label="Estado" value={filtroEstado}
            onChange={(e) => { setFiltroEstado(e.target.value); setPage(1); }}
            size="small" sx={{ minWidth: 160 }}
          >
            <MenuItem value="">Todos</MenuItem>
            {Object.entries(ESTADO_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={k}>{v.label}</MenuItem>
            ))}
          </TextField>
          <Box flexGrow={1} />
          <Typography variant="body2" color="text.secondary">
            {isLoading ? 'Cargando...' : `${data?.totalCount ?? 0} documento(s)`}
          </Typography>
        </Stack>
      </Paper>

      {/* Tabla */}
      {isLoading ? (
        <Box display="flex" justifyContent="center" p={4}>
          <CircularProgress />
        </Box>
      ) : !data?.items?.length ? (
        <Alert severity="info">No se encontraron documentos con los filtros seleccionados.</Alert>
      ) : (
        <>
          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700 }}>Número</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Tipo</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Fecha Emisión</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Sucursal</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Estado</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Fecha Envío DIAN</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Código DIAN</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>Intentos</TableCell>
                  <TableCell sx={{ fontWeight: 700 }} align="center">Acciones</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.items.map((doc) => {
                  const estadoInfo = ESTADO_LABELS[doc.codigoEstado] ?? { label: doc.estado, color: 'default' };
                  return (
                    <TableRow key={doc.id} hover>
                      <TableCell>
                        <Typography variant="body2" fontFamily="monospace" fontWeight={600}>
                          {doc.numeroCompleto}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip label={TIPO_LABELS[doc.tipoDocumento] ?? doc.tipoDocumento} size="small" />
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">{formatDate(doc.fechaEmision)}</Typography>
                      </TableCell>
                      <TableCell>{doc.nombreSucursal}</TableCell>
                      <TableCell>
                        <Chip label={estadoInfo.label} color={estadoInfo.color} size="small" />
                      </TableCell>
                      <TableCell>
                        {doc.fechaEnvioDian ? formatDate(doc.fechaEnvioDian) : '—'}
                      </TableCell>
                      <TableCell>
                        {doc.codigoRespuestaDian ? (
                          <Tooltip title={doc.mensajeRespuestaDian ?? ''}>
                            <Box display="flex" alignItems="center" gap={0.5}>
                              <Typography variant="body2" fontFamily="monospace">
                                {doc.codigoRespuestaDian}
                              </Typography>
                              <InfoOutlinedIcon fontSize="inherit" color="action" />
                            </Box>
                          </Tooltip>
                        ) : '—'}
                      </TableCell>
                      <TableCell align="center">{doc.intentos}</TableCell>
                      <TableCell align="center">
                        <Tooltip title="Descargar XML">
                          <IconButton size="small" onClick={() => handleDescarga(doc)}>
                            <DownloadIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        {isAdmin && doc.codigoEstado === 5 && (
                          <Tooltip title="Reintentar envío a DIAN">
                            <IconButton
                              size="small"
                              color="warning"
                              onClick={() => setConfirmReintentar(doc)}
                            >
                              <ReplayIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          {data.totalPages > 1 && (
            <Box display="flex" justifyContent="center" mt={2}>
              <Pagination
                count={data.totalPages}
                page={page}
                onChange={(_, p) => setPage(p)}
                color="primary"
              />
            </Box>
          )}
        </>
      )}

      {/* Diálogo confirmar reintento */}
      <Dialog open={!!confirmReintentar} onClose={() => setConfirmReintentar(null)}>
        <DialogTitle>Reintentar envío a DIAN</DialogTitle>
        <DialogContent>
          <DialogContentText>
            ¿Desea reenviar el documento <strong>{confirmReintentar?.numeroCompleto}</strong> a DIAN?
            {confirmReintentar?.mensajeRespuestaDian && (
              <><br /><br />Último error: {confirmReintentar.mensajeRespuestaDian}</>
            )}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmReintentar(null)}>Cancelar</Button>
          <Button
            variant="contained"
            color="warning"
            onClick={() => confirmReintentar && reintentarMutation.mutate(confirmReintentar.id)}
            disabled={reintentarMutation.isPending}
          >
            {reintentarMutation.isPending ? <CircularProgress size={16} /> : 'Reintentar'}
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
