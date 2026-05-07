import { useNavigate } from 'react-router-dom';
import { Paper, Typography, Button, alpha } from '@mui/material';
import LocalShippingIcon from '@mui/icons-material/LocalShipping';
import type { OrdenCompraDTO } from '@/types/api';

const HERO_COLOR = '#1565c0';

export type AccionOrden = 'aprobar' | 'rechazar' | 'cancelar' | 'recibir' | 'devolver' | null;

interface OrdenCompraBarraAccionesProps {
  orden: OrdenCompraDTO;
  onAccion: (accion: AccionOrden) => void;
}

export function OrdenCompraBarraAcciones({ orden, onAccion }: OrdenCompraBarraAccionesProps) {
  const navigate = useNavigate();

  return (
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
          <Button variant="outlined" color="error" size="small" onClick={() => onAccion('rechazar')}>
            Rechazar
          </Button>
          <Button variant="outlined" color="warning" size="small" onClick={() => onAccion('cancelar')}>
            Cancelar
          </Button>
          <Button variant="contained" color="success" size="small" onClick={() => onAccion('aprobar')}>
            Aprobar
          </Button>
        </>
      )}

      {orden.estado === 'Aprobada' && (
        <>
          <Button variant="outlined" color="warning" size="small" onClick={() => onAccion('cancelar')}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            color="primary"
            size="small"
            startIcon={<LocalShippingIcon />}
            onClick={() => onAccion('recibir')}
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
            onClick={() => onAccion('recibir')}
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
  );
}
