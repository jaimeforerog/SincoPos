import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Box,
  Card,
  CardContent,
  Typography,
  CircularProgress,
  Alert,
  Stack,
  TextField,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
} from '@mui/material';
import { LineChart } from '@mui/x-charts/LineChart';
import { PieChart } from '@mui/x-charts/PieChart';
import { PageHeader } from '@/components/common/PageHeader';
import { reportesApi } from '@/api/reportes';
import { sucursalesApi } from '@/api/sucursales';
import { formatCurrency } from '@/utils/format';
import { format, subDays } from 'date-fns';
import SearchIcon from '@mui/icons-material/Search';

export function ReporteVentasPage() {
  const today = new Date();
  const last30Days = subDays(today, 30);

  const [fechaDesde, setFechaDesde] = useState(format(last30Days, 'yyyy-MM-dd'));
  const [fechaHasta, setFechaHasta] = useState(format(today, 'yyyy-MM-dd'));
  const [sucursalId, setSucursalId] = useState<number | ''>('');
  const [metodoPago, setMetodoPago] = useState<number | ''>('');

  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  const {
    data: reporte,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: ['reportes', 'ventas', fechaDesde, fechaHasta, sucursalId, metodoPago],
    queryFn: () =>
      reportesApi.ventas({
        fechaDesde,
        fechaHasta,
        sucursalId: sucursalId || undefined,
        metodoPago: metodoPago !== '' ? metodoPago : undefined,
      }),
  });

  if (isLoading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="60vh">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <PageHeader 
        title="Reporte de Ventas" 
        showBackButton={true}
        backPath="/reportes"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Ventas' },
        ]}
      />

      {/* Filtros */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom fontWeight={600}>
            Filtros de Búsqueda
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <TextField
              label="Fecha Desde"
              type="date"
              value={fechaDesde}
              onChange={(e) => setFechaDesde(e.target.value)}
              sx={{ flex: 1 }}
              InputLabelProps={{ shrink: true }}
            />
            <TextField
              label="Fecha Hasta"
              type="date"
              value={fechaHasta}
              onChange={(e) => setFechaHasta(e.target.value)}
              sx={{ flex: 1 }}
              InputLabelProps={{ shrink: true }}
            />
            <FormControl sx={{ flex: 1 }}>
              <InputLabel>Sucursal</InputLabel>
              <Select
                value={sucursalId}
                onChange={(e) => setSucursalId(e.target.value as number | '')}
                label="Sucursal"
              >
                <MenuItem value="">Todas</MenuItem>
                {sucursales.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.nombre}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl sx={{ flex: 1 }}>
              <InputLabel>Método de Pago</InputLabel>
              <Select
                value={metodoPago}
                onChange={(e) => setMetodoPago(e.target.value as number | '')}
                label="Método de Pago"
              >
                <MenuItem value="">Todos</MenuItem>
                <MenuItem value={0}>Efectivo</MenuItem>
                <MenuItem value={1}>Tarjeta</MenuItem>
                <MenuItem value={2}>Transferencia</MenuItem>
              </Select>
            </FormControl>
          </Stack>
          <Button
            variant="contained"
            startIcon={<SearchIcon />}
            onClick={() => refetch()}
            fullWidth
          >
            Generar Reporte
          </Button>
        </CardContent>
      </Card>

      {!reporte ? (
        <Alert severity="info">Configure los filtros y haga clic en "Generar Reporte"</Alert>
      ) : (
        <Stack spacing={3}>
          {/* Resumen de Métricas */}
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Total Ventas
                </Typography>
                <Typography variant="h4" fontWeight={600} color="success.main">
                  {formatCurrency(reporte.totalVentas)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {reporte.cantidadVentas} ventas
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Utilidad Total
                </Typography>
                <Typography variant="h4" fontWeight={600} color="primary.main">
                  {formatCurrency(reporte.utilidadTotal)}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Margen: {reporte.margenPromedio.toFixed(1)}%
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Ticket Promedio
                </Typography>
                <Typography variant="h4" fontWeight={600}>
                  {formatCurrency(reporte.ticketPromedio)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Costo Total
                </Typography>
                <Typography variant="h4" fontWeight={600} color="error.main">
                  {formatCurrency(reporte.costoTotal)}
                </Typography>
              </CardContent>
            </Card>
          </Stack>

          {/* Gráficos */}
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={3}>
            {/* Ventas por Día */}
            <Card sx={{ flex: 2 }}>
              <CardContent>
                <Typography variant="h6" gutterBottom fontWeight={600}>
                  Ventas por Día
                </Typography>
                {reporte.ventasPorDia.length > 0 ? (
                  <LineChart
                    height={350}
                    series={[
                      {
                        data: reporte.ventasPorDia.map((v) => v.total),
                        label: 'Ventas',
                        color: '#4caf50',
                      },
                      {
                        data: reporte.ventasPorDia.map((v) => v.utilidad),
                        label: 'Utilidad',
                        color: '#2196f3',
                      },
                    ]}
                    xAxis={[
                      {
                        scaleType: 'point',
                        data: reporte.ventasPorDia.map((v) =>
                          format(new Date(v.fecha), 'dd/MM')
                        ),
                      },
                    ]}
                  />
                ) : (
                  <Alert severity="info">No hay datos para mostrar</Alert>
                )}
              </CardContent>
            </Card>

            {/* Métodos de Pago */}
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography variant="h6" gutterBottom fontWeight={600}>
                  Métodos de Pago
                </Typography>
                {reporte.ventasPorMetodoPago.length > 0 ? (
                  <PieChart
                    height={350}
                    series={[
                      {
                        data: reporte.ventasPorMetodoPago.map((m, idx) => ({
                          id: idx,
                          value: m.total,
                          label: `${m.metodo} (${m.cantidad})`,
                        })),
                      },
                    ]}
                  />
                ) : (
                  <Alert severity="info">No hay datos para mostrar</Alert>
                )}
              </CardContent>
            </Card>
          </Stack>

          {/* Tabla Detallada */}
          <Card>
            <CardContent>
              <Typography variant="h6" fontWeight={600} gutterBottom>
                Detalle por Día
              </Typography>
              <TableContainer component={Paper} variant="outlined">
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Fecha</TableCell>
                      <TableCell align="right">Cantidad</TableCell>
                      <TableCell align="right">Total Ventas</TableCell>
                      <TableCell align="right">Costo Total</TableCell>
                      <TableCell align="right">Utilidad</TableCell>
                      <TableCell align="right">Margen %</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {reporte.ventasPorDia.map((dia) => {
                      const margen = dia.total > 0 ? (dia.utilidad / dia.total) * 100 : 0;
                      return (
                        <TableRow key={dia.fecha}>
                          <TableCell>{format(new Date(dia.fecha), 'dd/MM/yyyy')}</TableCell>
                          <TableCell align="right">{dia.cantidad}</TableCell>
                          <TableCell align="right">{formatCurrency(dia.total)}</TableCell>
                          <TableCell align="right">{formatCurrency(dia.costoTotal)}</TableCell>
                          <TableCell align="right">{formatCurrency(dia.utilidad)}</TableCell>
                          <TableCell align="right">
                            <Chip
                              label={`${margen.toFixed(1)}%`}
                              color={margen > 30 ? 'success' : margen > 15 ? 'warning' : 'error'}
                              size="small"
                            />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        </Stack>
      )}
    </Box>
  );
}
