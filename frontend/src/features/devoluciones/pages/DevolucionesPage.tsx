import { useState, type HTMLAttributes } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Card,
  CardContent,
  Typography,
  TextField,
  Button,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Alert,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  CircularProgress,
  Divider,
  Autocomplete,
  Container,
  type ChipProps,
} from '@mui/material';
import UndoIcon2 from '@mui/icons-material/AssignmentReturn';
import PersonIcon from '@mui/icons-material/Person';
import { useAuth } from '@/hooks/useAuth';
import { ventasApi } from '@/api/ventas';
import { devolucionesApi } from '@/api/devoluciones';
import { formatCurrency, formatDate } from '@/utils/format';
import { useSnackbar } from 'notistack';
import SearchIcon from '@mui/icons-material/Search';
import UndoIcon from '@mui/icons-material/Undo';
import type { VentaDTO, TerceroDTO, ApiError } from '@/types/api';

interface DevolucionFormData {
  [productoId: string]: number; // productoId -> cantidad a devolver
}

interface ValidationErrors {
  [productoId: string]: string | null; // productoId -> mensaje de error
}

// Función helper para formatear fecha a YYYY-MM-DD
const formatDateForInput = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

// Función helper para obtener fecha hace N días
const getDaysAgo = (days: number): string => {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return formatDateForInput(date);
};

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

  // Paso 1 — clientes con ventas en la sucursal activa
  const { data: clientes = [], isLoading: loadingClientes } = useQuery({
    queryKey: ['clientes-con-ventas', activeSucursalId, busquedaCliente],
    queryFn: () =>
      devolucionesApi.getClientesConVentas({
        sucursalId: activeSucursalId || undefined,
        q: busquedaCliente || undefined,
      }),
    enabled: busquedaCliente.length >= 2 || busquedaCliente === '',
  });

  // Paso 2 — cargar ventas del cliente seleccionado
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

  // Cargar devoluciones de la venta seleccionada
  const { data: devoluciones = [], isLoading: loadingDevoluciones } = useQuery({
    queryKey: ['devoluciones', ventaSeleccionada?.id],
    queryFn: () => devolucionesApi.obtenerPorVenta(ventaSeleccionada!.id),
    enabled: !!ventaSeleccionada,
  });

  // Mutación para crear devolución
  const crearDevolucionMutation = useMutation({
    mutationFn: (data: { ventaId: number; dto: any }) =>
      devolucionesApi.crearDevolucionParcial(data.ventaId, data.dto),
    onSuccess: (data) => {
      enqueueSnackbar(
        `Devolución ${data.numeroDevolucion} creada exitosamente`,
        { variant: 'success' }
      );
      queryClient.invalidateQueries({ queryKey: ['devoluciones'] });
      setShowDialog(false);
      setCantidadesDevolver({});
      setValidationErrors({});
      setMotivo('');
      // Recargar la venta para actualizar cantidades disponibles
      if (ventaSeleccionada) {
        handleSeleccionarVenta(ventaSeleccionada);
      }
    },
    onError: (error: ApiError) => {
      enqueueSnackbar(error.response?.data?.detail || 'Error al crear devolución', {
        variant: 'error',
      });
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
      // Validar que no hayan pasado más de 30 días
      const diasTranscurridos = (new Date().getTime() - new Date(venta.fechaVenta).getTime()) / (1000 * 60 * 60 * 24);
      if (diasTranscurridos > 30) {
        enqueueSnackbar(
          `La venta tiene ${Math.floor(diasTranscurridos)} días. Solo se permiten devoluciones dentro de 30 días.`,
          { variant: 'error' }
        );
        setVentaSeleccionada(null);
        return;
      }

      // Cargar detalle completo
      const ventaCompleta = await ventasApi.getById(venta.id);
      setVentaSeleccionada(ventaCompleta);
      setCantidadesDevolver({});
      setValidationErrors({});
    } catch (error) {
      enqueueSnackbar('Error al cargar venta', { variant: 'error' });
      setVentaSeleccionada(null);
    }
  };

  const calcularCantidadDevuelta = (productoId: string): number => {
    return devoluciones
      .flatMap((d) => d.detalles)
      .filter((dd) => dd.productoId === productoId)
      .reduce((sum, dd) => sum + dd.cantidadDevuelta, 0);
  };

  const calcularCantidadDisponible = (productoId: string, cantidadOriginal: number): number => {
    const devuelta = calcularCantidadDevuelta(productoId);
    return cantidadOriginal - devuelta;
  };

  const handleCantidadChange = (productoId: string, valor: string, disponible: number) => {
    const cantidad = parseInt(valor) || 0;

    // Validación en tiempo real
    let error: string | null = null;
    if (cantidad > disponible) {
      error = `Máximo ${disponible} disponible`;
    } else if (cantidad < 0) {
      error = 'La cantidad no puede ser negativa';
    }

    setCantidadesDevolver((prev) => ({
      ...prev,
      [productoId]: cantidad,
    }));

    setValidationErrors((prev) => ({
      ...prev,
      [productoId]: error,
    }));
  };

  const hasValidationErrors = (): boolean => {
    return Object.values(validationErrors).some((error) => error !== null);
  };

  const handleCrearDevolucion = () => {
    if (!ventaSeleccionada) return;

    // Validar que no hay errores de validación
    if (hasValidationErrors()) {
      enqueueSnackbar('Corrija los errores de validación antes de continuar', {
        variant: 'error',
      });
      return;
    }

    // Validar que hay al menos un producto seleccionado
    const lineas = Object.entries(cantidadesDevolver)
      .filter(([_, cantidad]) => cantidad > 0)
      .map(([productoId, cantidad]) => ({
        productoId,
        cantidad,
      }));

    if (lineas.length === 0) {
      enqueueSnackbar('Seleccione al menos un producto para devolver', {
        variant: 'warning',
      });
      return;
    }

    if (!motivo.trim()) {
      enqueueSnackbar('Ingrese el motivo de la devolución', { variant: 'warning' });
      return;
    }

    // Validar cantidades
    for (const linea of lineas) {
      const detalleOriginal = ventaSeleccionada.detalles.find(
        (d) => d.productoId === linea.productoId
      );
      if (!detalleOriginal) continue;

      const disponible = calcularCantidadDisponible(
        linea.productoId,
        detalleOriginal.cantidad
      );
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
      dto: {
        motivo: motivo.trim(),
        lineas,
      },
    });
  };

  const calcularTotalDevolucion = (): number => {
    if (!ventaSeleccionada) return 0;

    return Object.entries(cantidadesDevolver)
      .filter(([_, cantidad]) => cantidad > 0)
      .reduce((total, [productoId, cantidad]) => {
        const detalle = ventaSeleccionada.detalles.find((d) => d.productoId === productoId);
        if (!detalle) return total;
        return total + detalle.precioUnitario * cantidad;
      }, 0);
  };

  const getEstadoColor = (estado: string): ChipProps['color'] => {
    switch (estado) {
      case 'Completada':
        return 'success';
      case 'Cancelada':
      case 'Anulada':
        return 'error';
      default:
        return 'default';
    }
  };

  const HERO_COLOR = '#1565c0';

  return (
    <Container maxWidth="xl">
      {/* Hero */}
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

      {/* Búsqueda y Filtros */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom fontWeight={600}>
            Buscar Venta para Devolución
          </Typography>

          <Stack spacing={2}>
            {/* Paso 1 — Cliente */}
            <Box>
              <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                Paso 1 — Seleccionar cliente
              </Typography>
              <Autocomplete
                options={clientes}
                getOptionLabel={(option) => `${option.nombre} (${option.identificacion})`}
                value={clienteSeleccionado}
                onChange={(_, newValue) => handleSeleccionarCliente(newValue)}
                onInputChange={(_, value) => setBusquedaCliente(value)}
                loading={loadingClientes}
                size="small"
                sx={{ maxWidth: 480 }}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Nombre o identificación del cliente"
                    placeholder="Escriba para buscar..."
                    InputProps={{
                      ...params.InputProps,
                      startAdornment: <PersonIcon sx={{ color: 'text.secondary', fontSize: 18, mr: 0.5 }} />,
                      endAdornment: (
                        <>
                          {loadingClientes ? <CircularProgress color="inherit" size={16} /> : null}
                          {params.InputProps.endAdornment}
                        </>
                      ),
                    }}
                  />
                )}
                renderOption={(props, option) => {
                  const { key, ...restProps } = props as HTMLAttributes<HTMLLIElement> & { key: string };
                  return (
                    <Box component="li" key={key} {...restProps}>
                      <Box>
                        <Typography variant="body2" fontWeight={600}>{option.nombre}</Typography>
                        <Typography variant="caption" color="text.secondary">
                          {option.tipoIdentificacion} {option.identificacion}
                          {option.telefono ? ` · ${option.telefono}` : ''}
                        </Typography>
                      </Box>
                    </Box>
                  );
                }}
                noOptionsText={busquedaCliente.length < 2 ? 'Escriba al menos 2 caracteres' : 'No se encontraron clientes'}
              />
            </Box>

            {/* Paso 2 — Venta (solo si hay cliente seleccionado) */}
            {clienteSeleccionado && (
              <Box>
                <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                  Paso 2 — Seleccionar venta de <strong>{clienteSeleccionado.nombre}</strong>
                </Typography>
                <Box sx={{ display: 'flex', gap: 2, alignItems: 'flex-start', flexWrap: 'wrap' }}>
                  <TextField
                    label="Fecha Desde"
                    type="date"
                    value={fechaDesde}
                    onChange={(e) => { setFechaDesde(e.target.value); setVentaSeleccionada(null); }}
                    size="small"
                    sx={{ minWidth: 155 }}
                    InputLabelProps={{ shrink: true }}
                  />
                  <TextField
                    label="Fecha Hasta"
                    type="date"
                    value={fechaHasta}
                    onChange={(e) => { setFechaHasta(e.target.value); setVentaSeleccionada(null); }}
                    size="small"
                    sx={{ minWidth: 155 }}
                    InputLabelProps={{ shrink: true }}
                  />
                  <Autocomplete
                    options={ventas}
                    getOptionLabel={(option) => option.numeroVenta}
                    value={ventaSeleccionada}
                    onChange={(_, newValue) => handleSeleccionarVenta(newValue)}
                    loading={loadingVentas}
                    size="small"
                    sx={{ minWidth: 220 }}
                    renderInput={(params) => (
                      <TextField
                        {...params}
                        label="N° venta"
                        placeholder="V-000001"
                        InputProps={{
                          ...params.InputProps,
                          startAdornment: <SearchIcon sx={{ color: 'text.secondary', fontSize: 18, mr: 0.5 }} />,
                          endAdornment: (
                            <>
                              {loadingVentas ? <CircularProgress color="inherit" size={16} /> : null}
                              {params.InputProps.endAdornment}
                            </>
                          ),
                        }}
                      />
                    )}
                    renderOption={(props, option) => {
                      const { key, ...restProps } = props as HTMLAttributes<HTMLLIElement> & { key: string };
                      const diasTranscurridos = Math.floor(
                        (new Date().getTime() - new Date(option.fechaVenta).getTime()) / (1000 * 60 * 60 * 24)
                      );
                      const fueraDeLimite = diasTranscurridos > 30;
                      return (
                        <Box component="li" key={key} {...restProps} sx={{ opacity: fueraDeLimite ? 0.5 : 1 }}>
                          <Box sx={{ flexGrow: 1 }}>
                            <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: 'monospace' }}>
                              {option.numeroVenta}
                            </Typography>
                            <Typography variant="caption" color="text.secondary">
                              {formatDate(option.fechaVenta)} · {formatCurrency(option.total)}
                              {fueraDeLimite && ` · ${diasTranscurridos} días — fuera de límite`}
                            </Typography>
                          </Box>
                          <Chip label={option.estado} color={getEstadoColor(option.estado)} size="small" />
                        </Box>
                      );
                    }}
                    noOptionsText={loadingVentas ? 'Cargando ventas...' : 'No hay ventas completadas en el período'}
                  />
                  <Typography variant="caption" color="text.secondary" sx={{ alignSelf: 'center' }}>
                    {loadingVentas ? 'Cargando...' : `${ventas.length} venta(s)`}
                  </Typography>
                </Box>
              </Box>
            )}

            {ventaSeleccionada && (
              <Alert severity="info" icon={<SearchIcon />} sx={{ py: 0.5 }}>
                Venta seleccionada: <strong>{ventaSeleccionada.numeroVenta}</strong> — {formatDate(ventaSeleccionada.fechaVenta)} — {formatCurrency(ventaSeleccionada.total)}
              </Alert>
            )}
          </Stack>
        </CardContent>
      </Card>

      {ventaSeleccionada && (
        <Stack spacing={3}>
          {/* Información de la Venta */}
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom fontWeight={600}>
                Venta {ventaSeleccionada.numeroVenta}
              </Typography>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ mb: 2 }}>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Fecha
                  </Typography>
                  <Typography variant="body1">
                    {formatDate(ventaSeleccionada.fechaVenta)}
                  </Typography>
                </Box>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Sucursal
                  </Typography>
                  <Typography variant="body1">
                    {ventaSeleccionada.nombreSucursal}
                  </Typography>
                </Box>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Total Venta
                  </Typography>
                  <Typography variant="h6" fontWeight={600} color="primary.main">
                    {formatCurrency(ventaSeleccionada.total)}
                  </Typography>
                </Box>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Estado
                  </Typography>
                  <Chip label={ventaSeleccionada.estado} color="success" size="small" />
                </Box>
              </Stack>

              <Divider sx={{ my: 2 }} />

              {/* Tabla de Productos */}
              <Typography variant="subtitle1" gutterBottom fontWeight={600}>
                Productos de la Venta
              </Typography>
              <TableContainer component={Paper} variant="outlined">
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Producto</TableCell>
                      <TableCell align="right">Cant. Vendida</TableCell>
                      <TableCell align="right">Precio Unit.</TableCell>
                      <TableCell align="right">Subtotal</TableCell>
                      <TableCell align="right">Devuelta</TableCell>
                      <TableCell align="right">Disponible</TableCell>
                      <TableCell align="right">Devolver</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {ventaSeleccionada.detalles.map((detalle) => {
                      const devuelta = calcularCantidadDevuelta(detalle.productoId);
                      const disponible = calcularCantidadDisponible(
                        detalle.productoId,
                        detalle.cantidad
                      );
                      return (
                        <TableRow key={detalle.id}>
                          <TableCell>{detalle.nombreProducto}</TableCell>
                          <TableCell align="right">{detalle.cantidad}</TableCell>
                          <TableCell align="right">
                            {formatCurrency(detalle.precioUnitario)}
                          </TableCell>
                          <TableCell align="right">
                            {formatCurrency(detalle.subtotal)}
                          </TableCell>
                          <TableCell align="right">
                            {devuelta > 0 ? (
                              <Chip label={devuelta} size="small" color="warning" />
                            ) : (
                              '-'
                            )}
                          </TableCell>
                          <TableCell align="right">
                            <Chip
                              label={disponible}
                              size="small"
                              color={disponible > 0 ? 'success' : 'default'}
                            />
                          </TableCell>
                          <TableCell align="right">
                            <TextField
                              type="number"
                              size="small"
                              value={cantidadesDevolver[detalle.productoId] || ''}
                              onChange={(e) =>
                                handleCantidadChange(detalle.productoId, e.target.value, disponible)
                              }
                              disabled={disponible === 0}
                              error={!!validationErrors[detalle.productoId]}
                              helperText={
                                validationErrors[detalle.productoId] ||
                                (disponible > 0 ? `Max: ${disponible}` : 'Sin disponible')
                              }
                              inputProps={{ min: 0, max: disponible }}
                              sx={{ width: 120 }}
                            />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>

              <Stack
                direction="row"
                justifyContent="space-between"
                alignItems="center"
                sx={{ mt: 2 }}
              >
                <Typography variant="h6">
                  Total a Devolver: {formatCurrency(calcularTotalDevolucion())}
                </Typography>
                <Button
                  variant="contained"
                  color="error"
                  startIcon={<UndoIcon />}
                  onClick={() => setShowDialog(true)}
                  disabled={calcularTotalDevolucion() === 0 || hasValidationErrors()}
                >
                  Procesar Devolución
                </Button>
              </Stack>
            </CardContent>
          </Card>

          {/* Historial de Devoluciones */}
          {loadingDevoluciones ? (
            <Box display="flex" justifyContent="center" py={3}>
              <CircularProgress />
            </Box>
          ) : devoluciones.length > 0 ? (
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom fontWeight={600}>
                  Devoluciones Anteriores ({devoluciones.length})
                </Typography>
                {devoluciones.map((devolucion) => (
                  <Card key={devolucion.id} variant="outlined" sx={{ mb: 2 }}>
                    <CardContent>
                      <Stack
                        direction="row"
                        justifyContent="space-between"
                        alignItems="center"
                        sx={{ mb: 2 }}
                      >
                        <Box>
                          <Typography variant="subtitle1" fontWeight={600}>
                            {devolucion.numeroDevolucion}
                          </Typography>
                          <Typography variant="body2" color="text.secondary">
                            {formatDate(devolucion.fechaDevolucion)}
                          </Typography>
                        </Box>
                        <Box textAlign="right">
                          <Typography variant="h6" color="error.main">
                            -{formatCurrency(devolucion.totalDevuelto)}
                          </Typography>
                          {devolucion.autorizadoPor && (
                            <Typography variant="caption" color="text.secondary">
                              Por: {devolucion.autorizadoPor}
                            </Typography>
                          )}
                        </Box>
                      </Stack>
                      <Alert severity="info" sx={{ mb: 2 }}>
                        <strong>Motivo:</strong> {devolucion.motivo}
                      </Alert>
                      <TableContainer>
                        <Table size="small">
                          <TableHead>
                            <TableRow>
                              <TableCell>Producto</TableCell>
                              <TableCell align="right">Cantidad</TableCell>
                              <TableCell align="right">Precio Unit.</TableCell>
                              <TableCell align="right">Subtotal</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {devolucion.detalles.map((detalle) => (
                              <TableRow key={detalle.id}>
                                <TableCell>{detalle.nombreProducto}</TableCell>
                                <TableCell align="right">
                                  {detalle.cantidadDevuelta}
                                </TableCell>
                                <TableCell align="right">
                                  {formatCurrency(detalle.precioUnitario)}
                                </TableCell>
                                <TableCell align="right">
                                  {formatCurrency(detalle.subtotalDevuelto)}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    </CardContent>
                  </Card>
                ))}
              </CardContent>
            </Card>
          ) : null}
        </Stack>
      )}

      {/* Dialog de Confirmación */}
      <Dialog open={showDialog} onClose={() => setShowDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Confirmar Devolución</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            Esta acción devolverá los productos al inventario y ajustará el monto de caja.
          </Alert>
          <TextField
            label="Motivo de la devolución"
            multiline
            rows={3}
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            fullWidth
            required
            placeholder="Ej: Producto defectuoso, Error en la venta, etc."
          />
          <Box sx={{ mt: 2 }}>
            <Typography variant="h6">
              Total a devolver: {formatCurrency(calcularTotalDevolucion())}
            </Typography>
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setShowDialog(false)} disabled={crearDevolucionMutation.isPending}>
            Cancelar
          </Button>
          <Button
            onClick={handleCrearDevolucion}
            variant="contained"
            color="error"
            disabled={crearDevolucionMutation.isPending || !motivo.trim()}
          >
            {crearDevolucionMutation.isPending ? <CircularProgress size={24} /> : 'Confirmar Devolución'}
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
