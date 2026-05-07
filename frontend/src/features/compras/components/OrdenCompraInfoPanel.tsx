import { Box, Typography, Paper, Chip, Divider } from '@mui/material';
import { formatDateOnly } from '@/utils/format';
import type { OrdenCompraDTO } from '@/types/api';

const formatFecha = (fecha?: string) => {
  if (!fecha) return '—';
  return formatDateOnly(fecha);
};

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

interface OrdenCompraInfoPanelProps {
  orden: OrdenCompraDTO;
}

export function OrdenCompraInfoPanel({ orden }: OrdenCompraInfoPanelProps) {
  const muestraErp = orden.estado === 'RecibidaParcial' || orden.estado === 'RecibidaCompleta';

  return (
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

      {muestraErp && (
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
            <Chip label="Error ERP" color="error" size="small" variant="outlined" sx={{ fontWeight: 600 }} />
          ) : (
            <Chip label="Pendiente ERP" color="default" size="small" variant="outlined" sx={{ fontWeight: 600 }} />
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
  );
}
