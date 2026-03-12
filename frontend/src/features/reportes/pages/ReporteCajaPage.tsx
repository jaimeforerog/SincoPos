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
import { ReportePageHeader } from '../components/ReportePageHeader';
import { reportesApi } from '@/api/reportes';
import { cajasApi } from '@/api/cajas';
import { sucursalesApi } from '@/api/sucursales';
import { formatCurrency, formatDate } from '@/utils/format';
import SearchIcon from '@mui/icons-material/Search';
import DownloadIcon from '@mui/icons-material/Download';
import { exportarReporteCaja } from '@/utils/exportReportes';

export function ReporteCajaPage() {
  const [sucursalId, setSucursalId] = useState<number | ''>('');
  const [cajaId, setCajaId] = useState<number | ''>('');

  const { data: sucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(true),
  });

  const { data: cajas = [] } = useQuery({
    queryKey: ['cajas', sucursalId],
    queryFn: () => cajasApi.getAll({ sucursalId: sucursalId || undefined }),
    enabled: sucursalId !== '',
  });

  const {
    data: reporte,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: ['reportes', 'caja', cajaId],
    queryFn: () => reportesApi.caja(cajaId as number),
    enabled: cajaId !== '',
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
      <ReportePageHeader
        title="Reporte de Caja"
        subtitle="Cuadre de caja, ingresos, egresos y movimientos de dinero"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Caja' },
        ]}
        color="#1976d2"
      />

      {/* Filtros */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom fontWeight={600}>
            Seleccionar Caja
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <FormControl sx={{ flex: 1 }}>
              <InputLabel>Sucursal</InputLabel>
              <Select
                value={sucursalId}
                onChange={(e) => {
                  setSucursalId(e.target.value as number | '');
                  setCajaId('');
                }}
                label="Sucursal"
              >
                <MenuItem value="">Seleccione una sucursal</MenuItem>
                {sucursales.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.nombre}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl sx={{ flex: 1 }} disabled={!sucursalId}>
              <InputLabel>Caja</InputLabel>
              <Select
                value={cajaId}
                onChange={(e) => setCajaId(e.target.value as number | '')}
                label="Caja"
              >
                <MenuItem value="">Seleccione una caja</MenuItem>
                {cajas.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.nombre} - {c.estado}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>
          <Stack direction="row" spacing={2}>
            <Button
              variant="contained"
              startIcon={<SearchIcon />}
              onClick={() => refetch()}
              sx={{ flex: 1 }}
              disabled={!cajaId}
            >
              Generar Reporte
            </Button>
            <Button
              variant="outlined"
              startIcon={<DownloadIcon />}
              onClick={() => reporte && exportarReporteCaja(reporte)}
              disabled={!reporte}
            >
              Exportar Excel
            </Button>
          </Stack>
        </CardContent>
      </Card>

      {!reporte ? (
        <Alert severity="info">Seleccione una sucursal y caja, luego haga clic en "Generar Reporte"</Alert>
      ) : (
        <Stack spacing={3}>
          {/* Información de la Caja */}
          <Card>
            <CardContent>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3}>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Caja
                  </Typography>
                  <Typography variant="h6" fontWeight={600}>
                    {reporte.nombreCaja}
                  </Typography>
                </Box>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Sucursal
                  </Typography>
                  <Typography variant="h6" fontWeight={600}>
                    {reporte.nombreSucursal}
                  </Typography>
                </Box>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Fecha Apertura
                  </Typography>
                  <Typography variant="body1">
                    {formatDate(reporte.fechaApertura)}
                  </Typography>
                </Box>
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" color="text.secondary">
                    Fecha Cierre
                  </Typography>
                  <Typography variant="body1">
                    {reporte.fechaCierre ? formatDate(reporte.fechaCierre) : 'Caja Abierta'}
                  </Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>

          {/* Resumen de Montos */}
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Monto Apertura
                </Typography>
                <Typography variant="h4" fontWeight={600}>
                  {formatCurrency(reporte.montoApertura)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Total Ventas
                </Typography>
                <Typography variant="h4" fontWeight={600} color="success.main">
                  {formatCurrency(reporte.totalVentas)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  {reporte.montoCierre ? 'Monto Cierre' : 'Monto Esperado'}
                </Typography>
                <Typography variant="h4" fontWeight={600} color="primary.main">
                  {formatCurrency(
                    reporte.montoCierre || reporte.montoApertura + reporte.totalVentasEfectivo
                  )}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Diferencia
                </Typography>
                <Typography
                  variant="h4"
                  fontWeight={600}
                  color={
                    reporte.diferenciaEsperado === 0
                      ? 'success.main'
                      : Math.abs(reporte.diferenciaEsperado || 0) < 1000
                        ? 'warning.main'
                        : 'error.main'
                  }
                >
                  {formatCurrency(reporte.diferenciaEsperado || 0)}
                </Typography>
              </CardContent>
            </Card>
          </Stack>

          {/* Ventas por Método de Pago */}
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Efectivo
                </Typography>
                <Typography variant="h5" fontWeight={600}>
                  {formatCurrency(reporte.totalVentasEfectivo)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Tarjeta
                </Typography>
                <Typography variant="h5" fontWeight={600}>
                  {formatCurrency(reporte.totalVentasTarjeta)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" variant="body2">
                  Transferencia
                </Typography>
                <Typography variant="h5" fontWeight={600}>
                  {formatCurrency(reporte.totalVentasTransferencia)}
                </Typography>
              </CardContent>
            </Card>
          </Stack>

          {/* Tabla de Ventas */}
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom fontWeight={600}>
                Detalle de Ventas ({reporte.ventas.length})
              </Typography>
              <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 500 }}>
                <Table size="small" stickyHeader>
                  <TableHead>
                    <TableRow>
                      <TableCell>Nº Venta</TableCell>
                      <TableCell>Fecha</TableCell>
                      <TableCell>Método Pago</TableCell>
                      <TableCell>Cliente</TableCell>
                      <TableCell align="right">Total</TableCell>
                      <TableCell align="right">Costo</TableCell>
                      <TableCell align="right">Utilidad</TableCell>
                      <TableCell align="right">Margen %</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {reporte.ventas.map((venta) => {
                      const margen = venta.total > 0 ? (venta.utilidad / venta.total) * 100 : 0;
                      return (
                        <TableRow key={venta.ventaId}>
                          <TableCell>{venta.numeroVenta}</TableCell>
                          <TableCell>{formatDate(venta.fechaVenta)}</TableCell>
                          <TableCell>
                            <Chip label={venta.metodoPago} size="small" variant="outlined" />
                          </TableCell>
                          <TableCell>{venta.cliente || '-'}</TableCell>
                          <TableCell align="right">{formatCurrency(venta.total)}</TableCell>
                          <TableCell align="right">{formatCurrency(venta.costoTotal)}</TableCell>
                          <TableCell align="right">{formatCurrency(venta.utilidad)}</TableCell>
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
