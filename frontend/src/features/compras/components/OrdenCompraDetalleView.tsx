import { useState } from 'react';
import { Box, Button, CircularProgress, Alert } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useQuery } from '@tanstack/react-query';
import { comprasApi } from '@/api/compras';
import { AccionAprobar, AccionRechazar, AccionCancelar } from './OrdenCompraAcciones';
import { AccionRecibir } from './OrdenCompraRecibir';
import { OrdenCompraDevolucion } from './OrdenCompraDevolucion';
import { OrdenCompraDetalleHero } from './OrdenCompraDetalleHero';
import { OrdenCompraInfoPanel } from './OrdenCompraInfoPanel';
import { OrdenCompraProductosTabla } from './OrdenCompraProductosTabla';
import { OrdenCompraBarraAcciones, type AccionOrden } from './OrdenCompraBarraAcciones';

const ESTADOS_TERMINALES = ['RecibidaCompleta', 'Rechazada', 'Cancelada'];

interface Props {
  ordenId: number;
  onBack: () => void;
}

export function OrdenCompraDetalleView({ ordenId, onBack }: Props) {
  const [accion, setAccion] = useState<AccionOrden>(null);

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

  const esTerminal = ESTADOS_TERMINALES.includes(orden.estado);
  const mostrarBarra = !esTerminal && accion === null;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      <OrdenCompraDetalleHero orden={orden} onBack={onBack} />

      <Box sx={{ px: { xs: 2, md: 4 }, pb: 4 }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '280px 1fr' },
            gap: 3,
            alignItems: 'start',
          }}
        >
          <OrdenCompraInfoPanel orden={orden} />
          <OrdenCompraProductosTabla orden={orden} />
        </Box>

        {mostrarBarra && <OrdenCompraBarraAcciones orden={orden} onAccion={setAccion} />}

        {accion !== null && (
          <Box sx={{ mt: 3 }}>
            {accion === 'aprobar' && (
              <AccionAprobar orden={orden} onCancel={() => setAccion(null)} onDone={handleAccionDone} />
            )}
            {accion === 'rechazar' && (
              <AccionRechazar orden={orden} onCancel={() => setAccion(null)} onDone={handleAccionDone} />
            )}
            {accion === 'cancelar' && (
              <AccionCancelar orden={orden} onCancel={() => setAccion(null)} onDone={handleAccionDone} />
            )}
            {accion === 'recibir' && (
              <AccionRecibir orden={orden} onCancel={() => setAccion(null)} onDone={handleAccionDone} />
            )}
            {accion === 'devolver' && (
              <OrdenCompraDevolucion orden={orden} onCancel={() => setAccion(null)} onDone={() => setAccion(null)} />
            )}
          </Box>
        )}
      </Box>
    </Box>
  );
}
