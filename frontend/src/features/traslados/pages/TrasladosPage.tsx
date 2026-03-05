import { useState } from 'react';
import {
  Container,
  Typography,
  Box,
  Button,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  IconButton,
  CircularProgress,
  Alert,
} from '@mui/material';
import {
  Add as AddIcon,
  Visibility as ViewIcon,
  Send as SendIcon,
  Check as CheckIcon,
  Close as CloseIcon,
  Cancel as CancelIcon,
} from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { trasladosApi } from '@/api/traslados';
import { CrearTrasladoDialog } from '../components/CrearTrasladoDialog';
import { DetallesTrasladoDialog } from '../components/DetallesTrasladoDialog';
import { useAuth } from '@/hooks/useAuth';

const getEstadoColor = (estado: string) => {
  switch (estado) {
    case 'Pendiente':
      return 'warning';
    case 'EnTransito':
      return 'info';
    case 'Recibido':
      return 'success';
    case 'Rechazado':
    case 'Cancelado':
      return 'error';
    default:
      return 'default';
  }
};

const formatFecha = (fecha?: string) => {
  if (!fecha) return '-';
  return new Date(fecha).toLocaleString('es-CO', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
};

export function TrasladosPage() {
  const { user } = useAuth();
  const [crearDialogOpen, setCrearDialogOpen] = useState(false);
  const [detallesDialogOpen, setDetallesDialogOpen] = useState(false);
  const [trasladoSeleccionado, setTrasladoSeleccionado] = useState<number | null>(null);

  const { data: traslados, isLoading, error, refetch } = useQuery({
    queryKey: ['traslados'],
    queryFn: () => trasladosApi.listar(),
  });

  const handleVerDetalles = (id: number) => {
    setTrasladoSeleccionado(id);
    setDetallesDialogOpen(true);
  };

  const handleCloseDetalles = () => {
    setDetallesDialogOpen(false);
    setTrasladoSeleccionado(null);
    refetch();
  };

  if (isLoading) {
    return (
      <Container maxWidth="xl">
        <Box
          sx={{
            display: 'flex',
            justifyContent: 'center',
            alignItems: 'center',
            minHeight: '60vh',
          }}
        >
          <CircularProgress />
        </Box>
      </Container>
    );
  }

  if (error) {
    return (
      <Container maxWidth="xl">
        <Alert severity="error" sx={{ mt: 3 }}>
          Error al cargar los traslados. Por favor, intenta de nuevo.
        </Alert>
      </Container>
    );
  }

  return (
    <Container maxWidth="xl">
      <Box sx={{ mb: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <Typography variant="h4" sx={{ fontWeight: 700, mb: 1 }}>
            Traslados entre Sucursales
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Gestión de traslados de inventario
          </Typography>
        </div>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setCrearDialogOpen(true)}
        >
          Nuevo Traslado
        </Button>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Número</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Origen</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Destino</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Estado</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Fecha Creación</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Productos</TableCell>
              <TableCell sx={{ fontWeight: 700 }} align="right">
                Acciones
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {traslados && traslados.length > 0 ? (
              traslados.map((traslado) => (
                <TableRow key={traslado.id} hover>
                  <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>
                    {traslado.numeroTraslado}
                  </TableCell>
                  <TableCell>{traslado.nombreSucursalOrigen}</TableCell>
                  <TableCell>{traslado.nombreSucursalDestino}</TableCell>
                  <TableCell>
                    <Chip
                      label={traslado.estado}
                      color={getEstadoColor(traslado.estado)}
                      size="small"
                      sx={{ fontWeight: 600 }}
                    />
                  </TableCell>
                  <TableCell>{formatFecha(traslado.fechaTraslado)}</TableCell>
                  <TableCell>{traslado.detalles.length} producto(s)</TableCell>
                  <TableCell align="right">
                    <IconButton
                      size="small"
                      onClick={() => handleVerDetalles(traslado.id)}
                      color="primary"
                    >
                      <ViewIcon />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4 }}>
                  <Typography color="text.secondary">
                    No hay traslados registrados
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <CrearTrasladoDialog
        open={crearDialogOpen}
        onClose={() => setCrearDialogOpen(false)}
        onSuccess={() => {
          setCrearDialogOpen(false);
          refetch();
        }}
      />

      {trasladoSeleccionado && (
        <DetallesTrasladoDialog
          open={detallesDialogOpen}
          trasladoId={trasladoSeleccionado}
          onClose={handleCloseDetalles}
        />
      )}
    </Container>
  );
}
