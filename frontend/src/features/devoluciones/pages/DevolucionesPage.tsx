import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Box, Typography, Stack, Container } from '@mui/material';
import UndoIcon2 from '@mui/icons-material/AssignmentReturn';
import { useAuth } from '@/hooks/useAuth';
import { ventasApi } from '@/api/ventas';
import { devolucionesApi } from '@/api/devoluciones';
import { useSnackbar } from 'notistack';
import type { VentaDTO, TerceroDTO, ApiError, CrearDevolucionParcialDTO } from '@/types/api';
import { BusquedaVentaCard } from '../components/BusquedaVentaCard';
import { VentaDetalleCard } from '../components/VentaDetalleCard';
import { HistorialDevoluciones } from '../components/HistorialDevoluciones';
import { ConfirmarDevolucionDialog } from '../components/ConfirmarDevolucionDialog';
import { formatDateForInput, getDaysAgo } from '../components/dateHelpers';

interface DevolucionFormData {
  [productoId: string]: number;
}

interface ValidationErrors {
  [productoId: string]: string | null;
}

const HERO_COLOR = '#1565c0';

export function DevolucionesPage() {
  const { activeSucursalId } = useAuth();
  const [clienteSeleccionado, setClienteSeleccionado] = useState<TerceroDTO | null>(null);
  const [busquedaCliente, setBusquedaCliente] = useState('');
  const [ventaSeleccionada, setVentaSeleccionada] = useState<VentaDTO | null>(null);
  const [showDialog, setShowDialog] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [cantidadesDevolver, setCantidadesDevolver] = useState<DevolucionFormData>({});
  const [validationErrors, setValidationErrors] = useState<ValidationErrors>({});
  const [fechaDesde, setFechaDesde] = useState<string>(getDaysAgo(30));
  const [fechaHasta, setFechaHasta] = useState<string>(formatDateForInput(new Date()));
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const { data: clientes = [], isLoading: loadingClientes } = useQuery({
    queryKey: ['clientes-con-ventas', activeSucursalId, busquedaCliente],
    queryFn: () =>
      devolucionesApi.getClientesConVentas({
        sucursalId: activeSucursalId || undefined,
        q: busquedaCliente || undefined,
      }),
    enabled: busquedaCliente.length >= 2 || busquedaCliente === '',
  });

  const { data: ventasPage, isLoading: loadingVentas } = useQuery({
    queryKey: ['ventas-devoluciones', activeSucursalId, clienteSeleccionado?.id, fechaDesde, fechaHasta],
    queryFn: () =>
      ventasApi.getAll({
        sucursalId: activeSucursalId || undefined,
        clienteId: clienteSeleccionado!.id,
        estado: 'Completada',
        desde: fechaDesde ? new Date(fechaDesde + 'T00:00:00').toISOString() : undefined,
        hasta: fechaHasta ? new Date(fechaHasta + 'T23:59:59').toISOString() : undefined,
        pageSize: 100,
      }),
    enabled: !!clienteSeleccionado,
  });
  const ventas = ventasPage?.items ?? [];

  const { data: devoluciones = [], isLoading: loadingDevoluciones } = useQuery({
    queryKey: ['devoluciones', ventaSeleccionada?.id],
    queryFn: () => devolucionesApi.obtenerPorVenta(ventaSeleccionada!.id),
    enabled: !!ventaSeleccionada,
  });

  const crearDevolucionMutation = useMutation({
    mutationFn: (data: { ventaId: number; dto: CrearDevolucionParcialDTO }) =>
      devolucionesApi.crearDevolucionParcial(data.ventaId, data.dto),
    onSuccess: (data) => {
      enqueueSnackbar(`Devolución ${data.numeroDevolucion} creada exitosamente`, { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['devoluciones'] });
      setShowDialog(false);
      setCantidadesDevolver({});
      setValidationErrors({});
      setMotivo('');
      if (ventaSeleccionada) {
        handleSeleccionarVenta(ventaSeleccionada);
      }
    },
    onError: (error: ApiError) => {
      enqueueSnackbar(error.response?.data?.detail || 'Error al crear devolución', { variant: 'error' });
    },
  });

  const handleSeleccionarCliente = (tercero: TerceroDTO | null) => {
    setClienteSeleccionado(tercero);
    setVentaSeleccionada(null);
    setCantidadesDevolver({});
    setValidationErrors({});
  };

  const handleSeleccionarVenta = async (venta: VentaDTO | null) => {
    if (!venta) {
      setVentaSeleccionada(null);
      setCantidadesDevolver({});
      setValidationErrors({});
      return;
    }

    try {
      const diasTranscurridos = (new Date().getTime() - new Date(venta.fechaVenta).getTime()) / (1000 * 60 * 60 * 24);
      if (diasTranscurridos > 30) {
        enqueueSnackbar(
          `La venta tiene ${Math.floor(diasTranscurridos)} días. Solo se permiten devoluciones dentro de 30 días.`,
          { variant: 'error' }
        );
        setVentaSeleccionada(null);
        return;
      }

      const ventaCompleta = await ventasApi.getById(venta.id);
      setVentaSeleccionada(ventaCompleta);
      setCantidadesDevolver({});
      setValidationErrors({});
    } catch {
      enqueueSnackbar('Error al cargar venta', { variant: 'error' });
      setVentaSeleccionada(null);
    }
  };

  const calcularCantidadDevuelta = (productoId: string): number =>
    devoluciones
      .flatMap((d) => d.detalles)
      .filter((dd) => dd.productoId === productoId)
      .reduce((sum, dd) => sum + dd.cantidadDevuelta, 0);

  const calcularCantidadDisponible = (productoId: string, cantidadOriginal: number): number =>
    cantidadOriginal - calcularCantidadDevuelta(productoId);

  const handleCantidadChange = (productoId: string, valor: string, disponible: number) => {
    const cantidad = parseInt(valor) || 0;
    let error: string | null = null;
    if (cantidad > disponible) error = `Máximo ${disponible} disponible`;
    else if (cantidad < 0) error = 'La cantidad no puede ser negativa';

    setCantidadesDevolver((prev) => ({ ...prev, [productoId]: cantidad }));
    setValidationErrors((prev) => ({ ...prev, [productoId]: error }));
  };

  const hasValidationErrors = Object.values(validationErrors).some((error) => error !== null);

  const calcularTotalDevolucion = (): number => {
    if (!ventaSeleccionada) return 0;
    return Object.entries(cantidadesDevolver)
      .filter(([, cantidad]) => cantidad > 0)
      .reduce((total, [productoId, cantidad]) => {
        const detalle = ventaSeleccionada.detalles.find((d) => d.productoId === productoId);
        if (!detalle) return total;
        return total + detalle.precioUnitario * cantidad;
      }, 0);
  };

  const totalDevolucion = calcularTotalDevolucion();

  const handleCrearDevolucion = () => {
    if (!ventaSeleccionada) return;

    if (hasValidationErrors) {
      enqueueSnackbar('Corrija los errores de validación antes de continuar', { variant: 'error' });
      return;
    }

    const lineas = Object.entries(cantidadesDevolver)
      .filter(([, cantidad]) => cantidad > 0)
      .map(([productoId, cantidad]) => ({ productoId, cantidad }));

    if (lineas.length === 0) {
      enqueueSnackbar('Seleccione al menos un producto para devolver', { variant: 'warning' });
      return;
    }

    if (!motivo.trim()) {
      enqueueSnackbar('Ingrese el motivo de la devolución', { variant: 'warning' });
      return;
    }

    for (const linea of lineas) {
      const detalleOriginal = ventaSeleccionada.detalles.find((d) => d.productoId === linea.productoId);
      if (!detalleOriginal) continue;
      const disponible = calcularCantidadDisponible(linea.productoId, detalleOriginal.cantidad);
      if (linea.cantidad > disponible) {
        enqueueSnackbar(
          `Cantidad a devolver de ${detalleOriginal.nombreProducto} excede disponible (${disponible})`,
          { variant: 'error' }
        );
        return;
      }
    }

    crearDevolucionMutation.mutate({
      ventaId: ventaSeleccionada.id,
      dto: { motivo: motivo.trim(), lineas },
    });
  };

  return (
    <Container maxWidth="xl">
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          borderRadius: 3,
          px: { xs: 3, md: 4 },
          py: { xs: 1.5, md: 2 },
          mb: 3,
          mt: 1,
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
        <Box sx={{ position: 'relative', zIndex: 1, display: 'flex', alignItems: 'center', gap: 2 }}>
          <UndoIcon2 sx={{ color: 'rgba(255,255,255,0.8)', fontSize: 28 }} />
          <Box>
            <Typography variant="h5" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
              Devoluciones de Ventas
            </Typography>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.75)', mt: 0.3 }}>
              Busca una venta completada y procesa la devolución parcial o total
            </Typography>
          </Box>
        </Box>
      </Box>

      <BusquedaVentaCard
        clientes={clientes}
        loadingClientes={loadingClientes}
        clienteSeleccionado={clienteSeleccionado}
        onClienteChange={handleSeleccionarCliente}
        busquedaCliente={busquedaCliente}
        onBusquedaChange={setBusquedaCliente}
        ventas={ventas}
        loadingVentas={loadingVentas}
        ventaSeleccionada={ventaSeleccionada}
        onVentaChange={handleSeleccionarVenta}
        fechaDesde={fechaDesde}
        onFechaDesdeChange={setFechaDesde}
        fechaHasta={fechaHasta}
        onFechaHastaChange={setFechaHasta}
      />

      {ventaSeleccionada && (
        <Stack spacing={3}>
          <VentaDetalleCard
            venta={ventaSeleccionada}
            cantidadesDevolver={cantidadesDevolver}
            validationErrors={validationErrors}
            onCantidadChange={handleCantidadChange}
            calcularCantidadDevuelta={calcularCantidadDevuelta}
            calcularCantidadDisponible={calcularCantidadDisponible}
            totalDevolucion={totalDevolucion}
            hasValidationErrors={hasValidationErrors}
            onProcesar={() => setShowDialog(true)}
          />
          <HistorialDevoluciones loading={loadingDevoluciones} devoluciones={devoluciones} />
        </Stack>
      )}

      <ConfirmarDevolucionDialog
        open={showDialog}
        onClose={() => setShowDialog(false)}
        motivo={motivo}
        onMotivoChange={setMotivo}
        totalDevolucion={totalDevolucion}
        loading={crearDevolucionMutation.isPending}
        onConfirm={handleCrearDevolucion}
      />
    </Container>
  );
}
