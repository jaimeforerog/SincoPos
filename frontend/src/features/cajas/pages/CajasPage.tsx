import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Button,
  Card,
  CardContent,
  CardActions,
  Typography,
  Chip,
  Container,
  Alert,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Paper,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import LockOpenIcon from '@mui/icons-material/LockOpen';
import LockIcon from '@mui/icons-material/Lock';
import { useAuth } from '@/hooks/useAuth';
import { cajasApi } from '@/api/cajas';
import { sucursalesApi } from '@/api/sucursales';
import { AbrirCajaDialog } from '../components/AbrirCajaDialog';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';
import { CerrarCajaDialog } from '../components/CerrarCajaDialog';
import type { CajaDTO } from '@/types/api';

export function CajasPage() {
  const { activeSucursalId } = useAuth();
  const [selectedSucursalId, setSelectedSucursalId] = useState<number | null>(null);
  const [abrirDialogOpen, setAbrirDialogOpen] = useState(false);
  const [cerrarDialogOpen, setCerrarDialogOpen] = useState(false);
  const [selectedCaja, setSelectedCaja] = useState<CajaDTO | null>(null);

  // Cargar sucursales
  const { data: sucursales = [], isLoading: loadingSucursales } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  // Auto-seleccionar la sucursal activa del usuario
  useEffect(() => {
    if (activeSucursalId && !selectedSucursalId) {
      setSelectedSucursalId(activeSucursalId);
    }
  }, [activeSucursalId, selectedSucursalId]);

  const { data: cajas = [], isLoading } = useQuery({
    queryKey: ['cajas', selectedSucursalId],
    queryFn: () => cajasApi.getAll({ sucursalId: selectedSucursalId ?? undefined }),
    enabled: selectedSucursalId !== null,
    refetchInterval: 30000, // Refrescar cada 30 segundos
  });

  const handleCerrarCaja = (caja: CajaDTO) => {
    setSelectedCaja(caja);
    setCerrarDialogOpen(true);
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('es-CO', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const cajasAbiertas = cajas.filter((c) => c.estado === 'Abierta');
  const cajasCerradas = cajas.filter((c) => c.estado === 'Cerrada');

  const selectedSucursal = sucursales.find(s => s.id === selectedSucursalId);

  return (
    <Container maxWidth="xl">
      <ReportePageHeader
        title="Gestión de Cajas"
        subtitle="Apertura, cierre y arqueo de cajas registradoras"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Cajas' },
        ]}
        backPath="/configuracion"
        color="#00796b"
        action={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => setAbrirDialogOpen(true)}
            disabled={!selectedSucursalId}
            sx={{
              bgcolor: 'rgba(255,255,255,0.15)',
              color: '#fff',
              border: '1px solid rgba(255,255,255,0.35)',
              fontWeight: 700,
              '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
              '&:disabled': { bgcolor: 'rgba(255,255,255,0.05)', color: 'rgba(255,255,255,0.4)' },
            }}
          >
            Abrir Caja
          </Button>
        }
      />

      {/* Selector de Sucursal */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <FormControl fullWidth>
          <InputLabel>Sucursal *</InputLabel>
          <Select
            value={selectedSucursalId || ''}
            onChange={(e) => setSelectedSucursalId(e.target.value as number)}
            label="Sucursal *"
            disabled={loadingSucursales}
          >
            {loadingSucursales && (
              <MenuItem value="" disabled>
                Cargando...
              </MenuItem>
            )}
            {!loadingSucursales && sucursales.length === 0 && (
              <MenuItem value="" disabled>
                No hay sucursales disponibles
              </MenuItem>
            )}
            {sucursales.map((sucursal) => (
              <MenuItem key={sucursal.id} value={sucursal.id}>
                {sucursal.nombre}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {selectedSucursal && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            Mostrando cajas de: <strong>{selectedSucursal.nombre}</strong>
          </Typography>
        )}
      </Paper>

      {!selectedSucursalId && (
        <Alert severity="info" sx={{ mb: 3 }}>
          Selecciona una sucursal para ver sus cajas.
        </Alert>
      )}

      {selectedSucursalId && isLoading && (
        <Typography>Cargando cajas...</Typography>
      )}

      {selectedSucursalId && !isLoading && cajas.length === 0 && (
        <Alert severity="info" sx={{ mb: 3 }}>
          No hay cajas registradas para esta sucursal. Haz clic en "Abrir Caja" para crear y abrir una nueva caja.
        </Alert>
      )}

      {/* Cajas Abiertas */}
      {selectedSucursalId && cajasAbiertas.length > 0 && (
        <Box sx={{ mb: 4 }}>
          <Typography variant="h5" sx={{ mb: 2, fontWeight: 600 }}>
            Cajas Abiertas ({cajasAbiertas.length})
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: {
                xs: '1fr',
                sm: 'repeat(2, 1fr)',
                md: 'repeat(3, 1fr)',
                lg: 'repeat(4, 1fr)',
              },
              gap: 2,
            }}
          >
            {cajasAbiertas.map((caja) => (
              <Card key={caja.id} sx={{ position: 'relative' }}>
                <CardContent>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', mb: 2 }}>
                    <Typography variant="h6" sx={{ fontWeight: 600 }}>
                      {caja.nombre}
                    </Typography>
                    <Chip
                      label="Abierta"
                      color="success"
                      size="small"
                      icon={<LockOpenIcon />}
                    />
                  </Box>

                  <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                    {caja.nombreSucursal}
                  </Typography>

                  <Box sx={{ mt: 2, p: 1.5, bgcolor: 'grey.50', borderRadius: 1 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                      <Typography variant="caption" color="text.secondary">
                        Apertura:
                      </Typography>
                      <Typography variant="caption" sx={{ fontWeight: 600 }}>
                        {formatCurrency(caja.montoApertura)}
                      </Typography>
                    </Box>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                      <Typography variant="caption" color="text.secondary">
                        Actual:
                      </Typography>
                      <Typography variant="caption" color="primary" sx={{ fontWeight: 700 }}>
                        {formatCurrency(caja.montoActual)}
                      </Typography>
                    </Box>
                  </Box>

                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
                    Abierta: {formatDate(caja.fechaApertura)}
                  </Typography>
                </CardContent>
                <CardActions>
                  <Button
                    size="small"
                    color="error"
                    startIcon={<LockIcon />}
                    onClick={() => handleCerrarCaja(caja)}
                    fullWidth
                  >
                    Cerrar Caja
                  </Button>
                </CardActions>
              </Card>
            ))}
          </Box>
        </Box>
      )}

      {/* Cajas Cerradas */}
      {selectedSucursalId && cajasCerradas.length > 0 && (
        <Box>
          <Typography variant="h5" sx={{ mb: 2, fontWeight: 600 }}>
            Cajas Cerradas ({cajasCerradas.length})
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: {
                xs: '1fr',
                sm: 'repeat(2, 1fr)',
                md: 'repeat(3, 1fr)',
                lg: 'repeat(4, 1fr)',
              },
              gap: 2,
            }}
          >
            {cajasCerradas.map((caja) => (
              <Card key={caja.id} sx={{ opacity: 0.7 }}>
                <CardContent>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', mb: 2 }}>
                    <Typography variant="h6" sx={{ fontWeight: 600 }}>
                      {caja.nombre}
                    </Typography>
                    <Chip
                      label="Cerrada"
                      color="default"
                      size="small"
                      icon={<LockIcon />}
                    />
                  </Box>

                  <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                    {caja.nombreSucursal}
                  </Typography>

                  <Box sx={{ mt: 2, p: 1.5, bgcolor: 'grey.50', borderRadius: 1 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                      <Typography variant="caption" color="text.secondary">
                        Apertura:
                      </Typography>
                      <Typography variant="caption" sx={{ fontWeight: 600 }}>
                        {formatCurrency(caja.montoApertura)}
                      </Typography>
                    </Box>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                      <Typography variant="caption" color="text.secondary">
                        Cierre:
                      </Typography>
                      <Typography variant="caption" sx={{ fontWeight: 600 }}>
                        {formatCurrency(caja.montoActual)}
                      </Typography>
                    </Box>
                  </Box>

                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
                    Cerrada: {formatDate(caja.fechaCierre)}
                  </Typography>
                </CardContent>
              </Card>
            ))}
          </Box>
        </Box>
      )}

      {/* Dialogs */}
      <AbrirCajaDialog
        open={abrirDialogOpen}
        onClose={() => setAbrirDialogOpen(false)}
        defaultSucursalId={selectedSucursalId || undefined}
      />
      <CerrarCajaDialog
        open={cerrarDialogOpen}
        onClose={() => setCerrarDialogOpen(false)}
        caja={selectedCaja}
      />
    </Container>
  );
}
