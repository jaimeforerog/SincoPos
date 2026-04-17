import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Paper,
  Typography,
  Autocomplete,
  TextField,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Button,
  Alert,
  alpha,
  CircularProgress,
  Divider,
} from '@mui/material';
import StorefrontIcon from '@mui/icons-material/Storefront';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { HeroBanner } from '@/components/common/HeroBanner';
import { tercerosApi } from '@/api/terceros';
import { comprasApi } from '@/api/compras';
import { formatDateOnly } from '@/utils/format';
import { useAuth } from '@/hooks/useAuth';
import type { OrdenCompraDTO, TerceroDTO } from '@/types/api';
import { OrdenCompraDevolucion } from '../components/OrdenCompraDevolucion';

const HERO_COLOR = '#c62828';

const fmt = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(v);

const ESTADO_META: Record<string, { color: 'primary' | 'success'; label: string }> = {
  RecibidaParcial:  { color: 'primary', label: 'Recibida Parcial' },
  RecibidaCompleta: { color: 'success', label: 'Recibida Completa' },
};

export function DevolucionesCompraPage() {
  const { activeEmpresaId } = useAuth();
  const [proveedorSeleccionado, setProveedorSeleccionado] = useState<TerceroDTO | null>(null);
  const [ordenSeleccionada, setOrdenSeleccionada] = useState<OrdenCompraDTO | null>(null);

  // Cargar proveedores
  const { data: proveedoresData } = useQuery({
    queryKey: ['terceros-proveedores', activeEmpresaId],
    queryFn: () => tercerosApi.getAll({ tipoTercero: 'Proveedor', pageSize: 200 }),
    staleTime: 0,
  });
  const proveedores = proveedoresData?.items ?? [];

  // Cargar OCs del proveedor seleccionado (estados devolvibles)
  const { data: ordenesData, isLoading: loadingOrdenes } = useQuery({
    queryKey: ['compras-devolvibles', proveedorSeleccionado?.id],
    queryFn: async () => {
      const [parciales, completas] = await Promise.all([
        comprasApi.getAll({ proveedorId: proveedorSeleccionado!.id, estado: 'RecibidaParcial', pageSize: 100 }),
        comprasApi.getAll({ proveedorId: proveedorSeleccionado!.id, estado: 'RecibidaCompleta', pageSize: 100 }),
      ]);
      return [...(parciales.items ?? []), ...(completas.items ?? [])]
        .sort((a, b) => new Date(b.fechaOrden).getTime() - new Date(a.fechaOrden).getTime());
    },
    enabled: !!proveedorSeleccionado,
    staleTime: 0,
  });
  const ordenes = ordenesData ?? [];

  const handleProveedorChange = (proveedor: TerceroDTO | null) => {
    setProveedorSeleccionado(proveedor);
    setOrdenSeleccionada(null);
  };

  const handleOrdenSelect = (orden: OrdenCompraDTO) => {
    setOrdenSeleccionada(orden.id === ordenSeleccionada?.id ? null : orden);
  };

  const handleDevolucionDone = () => {
    setOrdenSeleccionada(null);
    // La query se invalida internamente desde el componente
  };

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      <HeroBanner
        title="Devoluciones de Compra"
        subtitle="Seleccione el proveedor y la orden de compra para registrar una devolución de mercancía"
        variant="blue"
      />

      <Box sx={{ px: { xs: 2, md: 4 }, pb: 6, maxWidth: 1100, mx: 'auto' }}>

        {/* ── PASO 1: Seleccionar proveedor ─────────────────────────── */}
        <Paper variant="outlined" sx={{ p: 3, borderRadius: 2, mb: 3 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
            <Box
              sx={{
                width: 28, height: 28, borderRadius: '50%',
                bgcolor: proveedorSeleccionado ? HERO_COLOR : 'grey.400',
                color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontWeight: 700, fontSize: '0.85rem', flexShrink: 0,
              }}
            >
              1
            </Box>
            <Typography variant="subtitle1" fontWeight={700}>
              Seleccionar Proveedor
            </Typography>
          </Box>

          <Autocomplete
            options={proveedores}
            getOptionLabel={(p) => `${p.nombre} — ${p.identificacion}`}
            value={proveedorSeleccionado}
            onChange={(_, val) => handleProveedorChange(val)}
            renderInput={(params) => (
              <TextField
                {...params}
                label="Proveedor *"
                placeholder="Buscar por nombre o identificación..."
                size="small"
                InputProps={{
                  ...params.InputProps,
                  startAdornment: (
                    <>
                      <StorefrontIcon sx={{ color: 'text.secondary', mr: 0.5, fontSize: 18 }} />
                      {params.InputProps.startAdornment}
                    </>
                  ),
                }}
              />
            )}
          />
        </Paper>

        {/* ── PASO 2: Órdenes devolvibles del proveedor ─────────────── */}
        {proveedorSeleccionado && (
          <Paper variant="outlined" sx={{ p: 3, borderRadius: 2, mb: 3 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
              <Box
                sx={{
                  width: 28, height: 28, borderRadius: '50%',
                  bgcolor: ordenSeleccionada ? HERO_COLOR : 'grey.400',
                  color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontWeight: 700, fontSize: '0.85rem', flexShrink: 0,
                }}
              >
                2
              </Box>
              <Box>
                <Typography variant="subtitle1" fontWeight={700}>
                  Órdenes con mercancía recibida
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Proveedor: <strong>{proveedorSeleccionado.nombre}</strong> — seleccione la OC a devolver
                </Typography>
              </Box>
            </Box>

            {loadingOrdenes ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
                <CircularProgress size={28} />
              </Box>
            ) : ordenes.length === 0 ? (
              <Alert severity="info">
                Este proveedor no tiene órdenes de compra en estado <strong>Recibida Parcial</strong> o <strong>Recibida Completa</strong>.
                Solo se pueden hacer devoluciones de mercancía ya recibida.
              </Alert>
            ) : (
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow sx={{ bgcolor: 'grey.50' }}>
                      <TableCell>Número OC</TableCell>
                      <TableCell>Fecha Orden</TableCell>
                      <TableCell>Fecha Recepción</TableCell>
                      <TableCell align="right">Total OC</TableCell>
                      <TableCell align="center">Estado</TableCell>
                      <TableCell align="center" width={130}>Acción</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {ordenes.map((orden) => {
                      const isSelected = ordenSeleccionada?.id === orden.id;
                      const estadoMeta = ESTADO_META[orden.estado];
                      return (
                        <TableRow
                          key={orden.id}
                          sx={{
                            bgcolor: isSelected ? alpha(HERO_COLOR, 0.04) : 'inherit',
                            '&:hover': { bgcolor: alpha(HERO_COLOR, 0.03) },
                            '&:last-child td': { borderBottom: 0 },
                            cursor: 'pointer',
                          }}
                          onClick={() => handleOrdenSelect(orden)}
                        >
                          <TableCell>
                            <Typography variant="body2" fontWeight={600} sx={{ fontFamily: 'monospace' }}>
                              {orden.numeroOrden}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2">
                              {formatDateOnly(orden.fechaOrden)}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2">
                              {orden.fechaRecepcion
                                ? formatDateOnly(orden.fechaRecepcion)
                                : '—'}
                            </Typography>
                          </TableCell>
                          <TableCell align="right">
                            <Typography variant="body2" fontWeight={500}>
                              {fmt(orden.total)}
                            </Typography>
                          </TableCell>
                          <TableCell align="center">
                            <Chip
                              label={estadoMeta?.label ?? orden.estado}
                              color={estadoMeta?.color ?? 'default'}
                              size="small"
                              variant="outlined"
                            />
                          </TableCell>
                          <TableCell align="center">
                            {isSelected ? (
                              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 0.5 }}>
                                <CheckCircleIcon sx={{ fontSize: 16, color: HERO_COLOR }} />
                                <Typography variant="caption" color={HERO_COLOR} fontWeight={700}>
                                  Seleccionada
                                </Typography>
                              </Box>
                            ) : (
                              <Button
                                size="small"
                                variant="outlined"
                                color="error"
                                endIcon={<ArrowForwardIcon />}
                                onClick={(e) => { e.stopPropagation(); handleOrdenSelect(orden); }}
                              >
                                Devolver
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </Paper>
        )}

        {/* ── PASO 3: Formulario de devolución ──────────────────────── */}
        {ordenSeleccionada && (
          <>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
              <Box
                sx={{
                  width: 28, height: 28, borderRadius: '50%',
                  bgcolor: HERO_COLOR, color: '#fff',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontWeight: 700, fontSize: '0.85rem', flexShrink: 0,
                }}
              >
                3
              </Box>
              <Typography variant="subtitle1" fontWeight={700}>
                Registrar devolución — {ordenSeleccionada.numeroOrden}
              </Typography>
            </Box>
            <Divider sx={{ mb: 2 }} />
            <OrdenCompraDevolucion
              orden={ordenSeleccionada}
              onCancel={() => setOrdenSeleccionada(null)}
              onDone={handleDevolucionDone}
            />
          </>
        )}
      </Box>
    </Box>
  );
}
