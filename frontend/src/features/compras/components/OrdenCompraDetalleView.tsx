import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Button,
  Typography,
  Paper,
  Chip,
  Divider,
  CircularProgress,
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  IconButton,
  alpha,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import LocalShippingIcon from '@mui/icons-material/LocalShipping';
import { useQuery } from '@tanstack/react-query';
import { comprasApi } from '@/api/compras';
import { AccionAprobar, AccionRechazar, AccionCancelar } from './OrdenCompraAcciones';
import { AccionRecibir } from './OrdenCompraRecibir';
import { OrdenCompraDevolucion } from './OrdenCompraDevolucion';

const HERO_COLOR = '#1565c0';

type Accion = 'aprobar' | 'rechazar' | 'cancelar' | 'recibir' | 'devolver' | null;

const ESTADO_META: Record<string, { color: 'warning' | 'info' | 'primary' | 'success' | 'error' | 'default'; label: string }> = {
  Pendiente:        { color: 'warning', label: 'Pendiente' },
  Aprobada:         { color: 'info',    label: 'Aprobada' },
  RecibidaParcial:  { color: 'primary', label: 'Rec. Parcial' },
  RecibidaCompleta: { color: 'success', label: 'Recibida Completa' },
  Rechazada:        { color: 'error',   label: 'Rechazada' },
  Cancelada:        { color: 'default', label: 'Cancelada' },
};

const ESTADOS_TERMINALES = ['RecibidaCompleta', 'Rechazada', 'Cancelada'];

const formatFecha = (fecha?: string) => {
  if (!fecha) return '—';
  return new Date(fecha).toLocaleDateString('es-CO');
};

interface Props {
  ordenId: number;
  onBack: () => void;
}

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  if (!value) return null;
  return (
    <Box sx={{ mb: 1.25 }}>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.2, mb: 0.25 }}>
        {label}
      </Typography>
      <Typography variant="body2" fontWeight={500}>
        {value}
      </Typography>
    </Box>
  );
}

export function OrdenCompraDetalleView({ ordenId, onBack }: Props) {
  const navigate = useNavigate();
  const [accion, setAccion] = useState<Accion>(null);

  const { data: orden, isLoading } = useQuery({
    queryKey: ['compra', ordenId],
    queryFn: () => comprasApi.getById(ordenId),
  });

  const handleAccionDone = () => {
    setAccion(null);
    onBack();
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 300 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!orden) {
    return (
      <Box sx={{ p: 4 }}>
        <Alert severity="error">No se pudo cargar la orden de compra.</Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={onBack} sx={{ mt: 2 }}>
          Volver
        </Button>
      </Box>
    );
  }

  const estadoMeta = ESTADO_META[orden.estado] ?? { color: 'default' as const, label: orden.estado };
  const esTerminal = ESTADOS_TERMINALES.includes(orden.estado);
  const mostrarBarra = !esTerminal && accion === null;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      {/* Hero */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          px: { xs: 3, md: 4 },
          py: { xs: 1.5, md: 2 },
          mb: 3,
          position: 'relative',
          overflow: 'hidden',
          '&::before': {
            content: '""', position: 'absolute', top: -60, right: -60,
            width: 200, height: 200, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
          '&::after': {
            content: '""', position: 'absolute', bottom: -40, right: 80,
            width: 120, height: 120, borderRadius: '50%', background: 'rgba(255,255,255,0.05)',
          },
        }}
      >
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            position: 'relative',
            zIndex: 1,
            flexWrap: 'wrap',
            gap: 1,
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <IconButton
              aria-label="regresar"
              onClick={onBack}
              sx={{
                color: '#fff',
                bgcolor: 'rgba(255,255,255,0.12)',
                '&:hover': { bgcolor: 'rgba(255,255,255,0.22)' },
              }}
            >
              <ArrowBackIcon />
            </IconButton>
            <Typography
              variant="h5"
              fontWeight={700}
              sx={{ color: '#fff', fontFamily: 'monospace', letterSpacing: '0.05em' }}
            >
              {orden.numeroOrden}
            </Typography>
            <Chip
              label={estadoMeta.label}
              color={estadoMeta.color}
              size="small"
              sx={{ fontWeight: 700, bgcolor: 'rgba(255,255,255,0.18)', color: '#fff', border: '1px solid rgba(255,255,255,0.3)' }}
            />
          </Box>
          <Box sx={{ textAlign: { xs: 'left', md: 'right' } }}>
            <Typography variant="body1" fontWeight={600} sx={{ color: '#fff' }}>
              {orden.nombreProveedor}
            </Typography>
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.75)' }}>
              {new Date(orden.fechaOrden).toLocaleDateString('es-CO')}
            </Typography>
          </Box>
        </Box>
      </Box>

      {/* Body */}
      <Box sx={{ px: { xs: 2, md: 4 }, pb: 4 }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '280px 1fr' },
            gap: 3,
            alignItems: 'start',
          }}
        >
          {/* Panel izquierdo — Información */}
          <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 2 }}>
            <Typography
              variant="caption"
              fontWeight={700}
              color="text.secondary"
              sx={{ letterSpacing: '0.08em', textTransform: 'uppercase', display: 'block', mb: 2 }}
            >
              Información
            </Typography>

            <InfoRow label="Número" value={<span style={{ fontFamily: 'monospace' }}>{orden.numeroOrden}</span>} />
            <InfoRow label="Sucursal" value={orden.nombreSucursal} />
            <InfoRow label="Proveedor" value={orden.nombreProveedor} />
            <InfoRow label="Fecha de Orden" value={formatFecha(orden.fechaOrden)} />
            <InfoRow
              label="Entrega Esperada"
              value={orden.fechaEntregaEsperada ? formatFecha(orden.fechaEntregaEsperada) : '—'}
            />
            <InfoRow
              label="Forma de Pago"
              value={orden.formaPago === 'Credito' ? `Crédito (${orden.diasPlazo} días)` : 'Contado'}
            />

            {orden.aprobadoPor && (
              <>
                <InfoRow label="Aprobado por" value={orden.aprobadoPor} />
                {orden.fechaAprobacion && (
                  <InfoRow label="Fecha aprobación" value={formatFecha(orden.fechaAprobacion)} />
                )}
              </>
            )}

            {orden.recibidoPor && (
              <>
                <InfoRow label="Recibido por" value={orden.recibidoPor} />
                {orden.fechaRecepcion && (
                  <InfoRow label="Fecha recepción" value={formatFecha(orden.fechaRecepcion)} />
                )}
              </>
            )}

            {orden.observaciones && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                  Observaciones
                </Typography>
                <Typography variant="body2" sx={{ color: 'text.secondary', fontStyle: 'italic' }}>
                  {orden.observaciones}
                </Typography>
              </>
            )}

            {(orden.estado === 'RecibidaParcial' || orden.estado === 'RecibidaCompleta') && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Typography
                  variant="caption"
                  fontWeight={700}
                  color="text.secondary"
                  sx={{ display: 'block', mb: 1, textTransform: 'uppercase', letterSpacing: '0.06em' }}
                >
                  ERP Sinco
                </Typography>
                {orden.sincronizadoErp ? (
                  <Chip
                    label={`Sincronizado • Ref: ${orden.erpReferencia ?? '—'}`}
                    color="success"
                    size="small"
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                ) : orden.errorSincronizacion ? (
                  <Chip
                    label="Error ERP"
                    color="error"
                    size="small"
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                ) : (
                  <Chip
                    label="Pendiente ERP"
                    color="default"
                    size="small"
                    variant="outlined"
                    sx={{ fontWeight: 600 }}
                  />
                )}
                {orden.errorSincronizacion && (
                  <Typography variant="caption" color="error" sx={{ display: 'block', mt: 0.5 }}>
                    {orden.errorSincronizacion}
                  </Typography>
                )}
              </>
            )}

            {orden.motivoRechazo && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                  Motivo Rechazo / Cancelación
                </Typography>
                <Typography variant="body2" color="error.main" fontWeight={500}>
                  {orden.motivoRechazo}
                </Typography>
              </>
            )}
          </Paper>

          {/* Panel derecho — Productos */}
          <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
            <Box
              sx={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                px: 2,
                py: 1.5,
                borderBottom: '1px solid',
                borderColor: 'divider',
                bgcolor: alpha(HERO_COLOR, 0.04),
              }}
            >
              <Box>
                <Typography variant="subtitle1" fontWeight={700}>
                  Productos ({orden.detalles.length})
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {orden.detalles.reduce((sum, d) => sum + d.cantidadSolicitada, 0)} unidades totales
                </Typography>
              </Box>
            </Box>

            <TableContainer>
              <Table
                size="small"
                sx={{
                  '& .MuiTableCell-root': { py: 0.75, px: 1 },
                  '& .MuiTableCell-head': {
                    bgcolor: 'grey.50',
                    fontWeight: 700,
                    fontSize: '0.72rem',
                    textTransform: 'uppercase',
                    letterSpacing: '0.04em',
                    color: 'text.secondary',
                    borderBottom: `2px solid ${alpha(HERO_COLOR, 0.15)}`,
                  },
                }}
              >
                <TableHead>
                  <TableRow>
                    <TableCell width={36}>#</TableCell>
                    <TableCell>Producto</TableCell>
                    <TableCell width={140} sx={{ fontFamily: 'monospace' }}>Código</TableCell>
                    <TableCell align="center" width={90}>Solicitada</TableCell>
                    <TableCell align="center" width={90}>Recibida</TableCell>
                    <TableCell align="right" width={110}>P. Unit.</TableCell>
                    <TableCell align="right" width={110}>Subtotal</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {orden.detalles.map((detalle, index) => {
                    const recibidaCompleta = detalle.cantidadRecibida >= detalle.cantidadSolicitada;
                    const recibidaParcial = detalle.cantidadRecibida > 0 && !recibidaCompleta;

                    return (
                      <TableRow
                        key={detalle.id}
                        sx={{
                          '&:hover': { bgcolor: alpha(HERO_COLOR, 0.02) },
                          '&:last-child td': { borderBottom: 0 },
                        }}
                      >
                        <TableCell>
                          <Typography variant="caption" color="text.secondary">{index + 1}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" fontWeight={500}>
                            {detalle.nombreProducto}
                          </Typography>
                          {detalle.nombreImpuesto && (
                            <Typography variant="caption" color="text.secondary">
                              {detalle.nombreImpuesto}
                            </Typography>
                          )}
                        </TableCell>
                        <TableCell>
                          <Typography
                            variant="caption"
                            sx={{ fontFamily: 'monospace', color: 'text.secondary', fontSize: '0.72rem' }}
                          >
                            {detalle.productoId}
                          </Typography>
                        </TableCell>
                        <TableCell align="center">
                          <Typography variant="body2">{detalle.cantidadSolicitada}</Typography>
                        </TableCell>
                        <TableCell align="center">
                          <Typography
                            variant="body2"
                            fontWeight={detalle.cantidadRecibida > 0 ? 600 : 400}
                            color={
                              recibidaCompleta
                                ? 'success.main'
                                : recibidaParcial
                                ? 'warning.main'
                                : 'text.disabled'
                            }
                          >
                            {detalle.cantidadRecibida || '—'}
                          </Typography>
                        </TableCell>
                        <TableCell align="right">
                          <Typography variant="body2">
                            ${detalle.precioUnitario.toLocaleString('es-CO')}
                          </Typography>
                        </TableCell>
                        <TableCell align="right">
                          <Typography variant="body2" fontWeight={500}>
                            ${detalle.subtotal.toLocaleString('es-CO')}
                          </Typography>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </TableContainer>

            {/* Footer totales */}
            <Box
              sx={{
                px: 2,
                py: 1.5,
                borderTop: '1px solid',
                borderColor: 'divider',
                bgcolor: alpha(HERO_COLOR, 0.02),
              }}
            >
              <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                <Box sx={{ width: 220 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                    <Typography variant="caption" color="text.secondary">Subtotal:</Typography>
                    <Typography variant="caption">${orden.subtotal.toLocaleString('es-CO')}</Typography>
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                    <Typography variant="caption" color="text.secondary">IVA:</Typography>
                    <Typography variant="caption">${orden.impuestos.toLocaleString('es-CO')}</Typography>
                  </Box>
                  <Divider sx={{ my: 0.75 }} />
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2" fontWeight={700}>Total:</Typography>
                    <Typography variant="body2" fontWeight={700} color="primary">
                      ${orden.total.toLocaleString('es-CO')}
                    </Typography>
                  </Box>
                </Box>
              </Box>
            </Box>
          </Paper>
        </Box>

        {/* Barra de acciones */}
        {mostrarBarra && (
          <Paper
            variant="outlined"
            sx={{
              mt: 3,
              p: 2,
              borderRadius: 2,
              borderColor: alpha(HERO_COLOR, 0.3),
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
              flexWrap: 'wrap',
            }}
          >
            <Typography variant="body2" color="text.secondary" fontWeight={500} sx={{ mr: 'auto' }}>
              Acciones disponibles
            </Typography>

            {orden.estado === 'Pendiente' && (
              <>
                <Button variant="outlined" color="error" size="small" onClick={() => setAccion('rechazar')}>
                  Rechazar
                </Button>
                <Button variant="outlined" color="warning" size="small" onClick={() => setAccion('cancelar')}>
                  Cancelar
                </Button>
                <Button variant="contained" color="success" size="small" onClick={() => setAccion('aprobar')}>
                  Aprobar
                </Button>
              </>
            )}

            {orden.estado === 'Aprobada' && (
              <>
                <Button variant="outlined" color="warning" size="small" onClick={() => setAccion('cancelar')}>
                  Cancelar
                </Button>
                <Button
                  variant="contained"
                  color="primary"
                  size="small"
                  startIcon={<LocalShippingIcon />}
                  onClick={() => setAccion('recibir')}
                >
                  Recibir Mercancía
                </Button>
              </>
            )}

            {orden.estado === 'RecibidaParcial' && (
              <>
                <Button
                  variant="contained"
                  color="primary"
                  size="small"
                  startIcon={<LocalShippingIcon />}
                  onClick={() => setAccion('recibir')}
                >
                  Recibir Mercancía
                </Button>
                <Button
                  variant="outlined"
                  color="error"
                  size="small"
                  onClick={() => navigate('/compras/devoluciones')}
                >
                  Devolver al Proveedor
                </Button>
              </>
            )}

            {orden.estado === 'RecibidaCompleta' && (
              <Button
                variant="outlined"
                color="error"
                size="small"
                onClick={() => navigate('/compras/devoluciones')}
              >
                Devolver al Proveedor
              </Button>
            )}
          </Paper>
        )}

        {/* Paneles de acción inline */}
        {accion !== null && (
          <Box sx={{ mt: 3 }}>
            {accion === 'aprobar' && (
              <AccionAprobar
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'rechazar' && (
              <AccionRechazar
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'cancelar' && (
              <AccionCancelar
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'recibir' && (
              <AccionRecibir
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={handleAccionDone}
              />
            )}
            {accion === 'devolver' && (
              <OrdenCompraDevolucion
                orden={orden}
                onCancel={() => setAccion(null)}
                onDone={() => { setAccion(null); }}
              />
            )}
          </Box>
        )}
      </Box>
    </Box>
  );
}
