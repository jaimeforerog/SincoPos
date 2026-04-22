import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  CircularProgress,
  Alert,
  TextField,
  MenuItem,
  Stack,
  Typography,
  IconButton,
  Tooltip,
  Autocomplete,
} from '@mui/material';
import { Timeline } from '@mui/icons-material';
import { lotesApi } from '@/api/lotes';
import { productosApi } from '@/api/productos';
import { useAuth } from '@/hooks/useAuth';
import type { ProductoDTO, SucursalDTO } from '@/types/api';
import { formatCurrency, formatDateOnly } from '@/utils/format';
import { TrazabilidadLoteModal } from './TrazabilidadLoteModal';

interface Props {
  sucursales: SucursalDTO[];
  activeSucursalId?: number;
}

function chipVencimiento(dias: number) {
  if (dias <= 0) return <Chip label="Vencido" color="error" size="small" />;
  if (dias <= 7) return <Chip label={`${dias}d`} color="error" size="small" />;
  if (dias <= 30) return <Chip label={`${dias}d`} color="warning" size="small" />;
  return <Chip label={`${dias}d`} color="success" size="small" />;
}

export function LotesTab({ sucursales, activeSucursalId }: Props) {
  const [sucursalId, setSucursalId] = useState<number | ''>(activeSucursalId || '');
  const [soloVigentes, setSoloVigentes] = useState(true);
  const [productoQuery, setProductoQuery] = useState('');
  const [productoSeleccionado, setProductoSeleccionado] = useState<ProductoDTO | null>(null);
  const [trazabilidadLoteId, setTrazabilidadLoteId] = useState<number | null>(null);
  const { activeEmpresaId } = useAuth();

  const { data: productosData, isFetching: buscandoProductos } = useQuery({
    queryKey: ['productos', 'buscar', productoQuery, activeEmpresaId],
    queryFn: () => productosApi.getAll({ query: productoQuery || undefined }),
    enabled: productoQuery.length >= 2,
  });
  const productos = productosData?.items ?? [];

  const productoId = productoSeleccionado?.id ?? '';

  const { data: lotes = [], isLoading } = useQuery({
    queryKey: ['lotes', productoId, sucursalId, soloVigentes],
    queryFn: () => lotesApi.obtenerLotes(productoId, sucursalId as number, soloVigentes),
    enabled: !!productoId && !!sucursalId,
  });

  const { data: alertas = [], isLoading: loadingAlertas } = useQuery({
    queryKey: ['lotes', 'proximos-vencer', sucursalId],
    queryFn: () =>
      sucursalId ? lotesApi.proximosAVencer(sucursalId as number) : lotesApi.obtenerAlertas(),
    enabled: !productoId,
  });

  const modoConsulta = !!productoId && !!sucursalId;

  return (
    <Box>
      <Stack direction="row" spacing={2} sx={{ mb: 2 }} flexWrap="wrap">
        <TextField
          select
          label="Sucursal"
          value={sucursalId}
          onChange={(e) => setSucursalId(e.target.value as number | '')}
          size="small"
          sx={{ minWidth: 200 }}
        >
          <MenuItem value="">Todas</MenuItem>
          {sucursales.map((s) => (
            <MenuItem key={s.id} value={s.id}>{s.nombre}</MenuItem>
          ))}
        </TextField>

        <Autocomplete
          options={productos}
          value={productoSeleccionado}
          inputValue={productoQuery}
          loading={buscandoProductos}
          getOptionLabel={(p) => `${p.nombre}${p.codigoBarras ? ` — ${p.codigoBarras}` : ''}`}
          isOptionEqualToValue={(a, b) => a.id === b.id}
          onInputChange={(_e, value, reason) => {
            setProductoQuery(value);
            if (reason === 'clear') setProductoSeleccionado(null);
          }}
          onChange={(_e, value) => setProductoSeleccionado(value)}
          noOptionsText={productoQuery.length < 2 ? 'Escribe al menos 2 caracteres' : 'Sin resultados'}
          size="small"
          sx={{ minWidth: 280 }}
          renderInput={(params) => (
            <TextField {...params} label="Buscar producto" placeholder="Nombre o código..." />
          )}
        />

        <TextField
          select
          label="Mostrar"
          value={soloVigentes ? 'vigentes' : 'todos'}
          onChange={(e) => setSoloVigentes(e.target.value === 'vigentes')}
          size="small"
          sx={{ minWidth: 150 }}
        >
          <MenuItem value="vigentes">Solo vigentes</MenuItem>
          <MenuItem value="todos">Todos</MenuItem>
        </TextField>
      </Stack>

      {modoConsulta ? (
        isLoading ? (
          <Box display="flex" justifyContent="center" py={5}><CircularProgress /></Box>
        ) : lotes.length === 0 ? (
          <Alert severity="info">No hay lotes para este producto en la sucursal seleccionada.</Alert>
        ) : (
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Nº Lote</TableCell>
                  <TableCell>Fecha Entrada</TableCell>
                  <TableCell>Referencia</TableCell>
                  <TableCell align="right">Inicial</TableCell>
                  <TableCell align="right">Disponible</TableCell>
                  <TableCell align="right">Costo Unit.</TableCell>
                  <TableCell align="center">Vencimiento</TableCell>
                  <TableCell align="center">Estado</TableCell>
                  <TableCell align="center"></TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {lotes.map((lote) => {
                  const hoy = new Date();
                  const vence = lote.fechaVencimiento ? new Date(lote.fechaVencimiento) : null;
                  const dias = vence ? Math.floor((vence.getTime() - hoy.getTime()) / 86400000) : null;
                  const agotado = lote.cantidadDisponible <= 0;

                  return (
                    <TableRow key={lote.id} sx={{ opacity: agotado ? 0.5 : 1 }}>
                      <TableCell>
                        <Typography variant="body2" fontFamily="monospace" fontWeight={600}>
                          {lote.numeroLote || '—'}
                        </Typography>
                      </TableCell>
                      <TableCell>{formatDateOnly(lote.fechaEntrada)}</TableCell>
                      <TableCell>{lote.referencia || '—'}</TableCell>
                      <TableCell align="right">{lote.cantidadInicial}</TableCell>
                      <TableCell align="right">
                        <Typography fontWeight={600} color={agotado ? 'text.disabled' : 'text.primary'}>
                          {lote.cantidadDisponible}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">{formatCurrency(lote.costoUnitario)}</TableCell>
                      <TableCell align="center">
                        {lote.fechaVencimiento ? (
                          <Box>
                            <Typography variant="caption" display="block">{lote.fechaVencimiento}</Typography>
                            {dias !== null && chipVencimiento(dias)}
                          </Box>
                        ) : '—'}
                      </TableCell>
                      <TableCell align="center">
                        {agotado
                          ? <Chip label="Agotado" size="small" variant="outlined" />
                          : <Chip label="Disponible" color="success" size="small" />}
                      </TableCell>
                      <TableCell align="center">
                        <Tooltip title="Ver kardex">
                          <IconButton size="small" onClick={() => setTrazabilidadLoteId(lote.id)}>
                            <Timeline fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )
      ) : (
        loadingAlertas ? (
          <Box display="flex" justifyContent="center" py={5}><CircularProgress /></Box>
        ) : alertas.length === 0 ? (
          <Alert severity="success">Sin lotes próximos a vencer. Selecciona un producto para ver sus lotes.</Alert>
        ) : (
          <>
            <Alert severity="warning" sx={{ mb: 2 }}>
              <strong>{alertas.length}</strong> lote(s) próximos a vencer{sucursalId ? '' : ' en todas las sucursales'}.
              Selecciona un producto para ver todos sus lotes.
            </Alert>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Producto</TableCell>
                    <TableCell>Sucursal</TableCell>
                    <TableCell>Nº Lote</TableCell>
                    <TableCell>Fecha Entrada</TableCell>
                    <TableCell>Vence</TableCell>
                    <TableCell align="right">Disponible</TableCell>
                    <TableCell align="center">Vence en</TableCell>
                    <TableCell align="center"></TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {alertas.map((a) => (
                    <TableRow key={a.loteId}>
                      <TableCell>{a.nombreProducto}</TableCell>
                      <TableCell>{a.nombreSucursal}</TableCell>
                      <TableCell>
                        <Typography variant="body2" fontFamily="monospace">{a.numeroLote || '—'}</Typography>
                      </TableCell>
                      <TableCell>{formatDateOnly(a.fechaEntrada)}</TableCell>
                      <TableCell>{a.fechaVencimiento}</TableCell>
                      <TableCell align="right">{a.cantidadDisponible}</TableCell>
                      <TableCell align="center">{chipVencimiento(a.diasParaVencer)}</TableCell>
                      <TableCell align="center">
                        <Tooltip title="Ver kardex">
                          <IconButton size="small" onClick={() => setTrazabilidadLoteId(a.loteId)}>
                            <Timeline fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )
      )}

      <TrazabilidadLoteModal
        loteId={trazabilidadLoteId}
        onClose={() => setTrazabilidadLoteId(null)}
      />
    </Box>
  );
}
