import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Container,
  Paper,
  Button,
  Box,
  Typography,
  Card,
  CardContent,
  CircularProgress,
  Alert,
  Stack,
  TextField,
  Autocomplete,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
} from '@mui/material';
import { Search, Download } from '@mui/icons-material';
import { exportarReporteKardex } from '@/utils/exportReportes';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import { format } from 'date-fns';
import { es } from 'date-fns/locale';

import { reportesApi } from '@/api/reportes';
import { productosApi } from '@/api/productos';
import { sucursalesApi } from '@/api/sucursales';
import { ReportePageHeader } from '../components/ReportePageHeader';
import { formatCurrency } from '@/utils/format';
import type { ProductoDTO } from '@/types/api';
import { useAuth } from '@/hooks/useAuth';

export function ReporteKardexPage() {
  const { user, activeEmpresaId } = useAuth();
  const today = new Date();
  const firstDayOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);

  const [producto, setProducto] = useState<ProductoDTO | null>(null);
  const [sucursalId, setSucursalId] = useState<number | ''>('');
  const [fechaDesde, setFechaDesde] = useState(format(firstDayOfMonth, 'yyyy-MM-dd'));
  const [fechaHasta, setFechaHasta] = useState(format(today, 'yyyy-MM-dd'));

  const [queryParams, setQueryParams] = useState<{
    productoId: string;
    sucursalId: number;
    fechaDesde: string;
    fechaHasta: string;
  } | null>(null);

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(),
  });
  const sucursales = todasSucursales.filter((s) =>
    (activeEmpresaId == null || s.empresaId === activeEmpresaId || s.empresaId == null) &&
    (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.getAll({ incluirInactivos: false }),
  });
  const productos = productosData?.items || [];

  const { data: reporte, isLoading, isError, error } = useQuery({
    queryKey: ['kardex', queryParams],
    queryFn: () => {
      if (!queryParams) return null;
      return reportesApi.kardex({
        productoId: queryParams.productoId,
        sucursalId: queryParams.sucursalId,
        fechaDesde: queryParams.fechaDesde,
        fechaHasta: queryParams.fechaHasta,
      });
    },
    enabled: !!queryParams,
  });

  const handleBuscar = () => {
    if (!producto || !sucursalId) return;
    setQueryParams({
      productoId: producto.id,
      sucursalId: sucursalId as number,
      fechaDesde,
      fechaHasta,
    });
  };

  const columns = useMemo<GridColDef[]>(() => [
    {
      field: 'fecha',
      headerName: 'Fecha',
      width: 150,
      valueFormatter: (value) => format(new Date(value as string), 'dd/MM/yyyy HH:mm', { locale: es }),
    },
    {
      field: 'tipoMovimiento',
      headerName: 'Movimiento',
      width: 150,
      valueFormatter: (value) => {
         const val = value as string;
         const mapper: Record<string, string> = {
             'EntradaCompra': 'Compra',
             'SalidaVenta': 'Venta',
             'DevolucionCompra': 'Dev. Compra',
             'Ajuste': 'Ajuste',
             'TrasladoEntrada': 'Ent. Traslado',
             'TrasladoSalida': 'Sal. Traslado'
         };
         return mapper[val] || val;
      }
    },
    {
      field: 'referencia',
      headerName: 'Documento',
      width: 150,
    },
    {
      field: 'entrada',
      headerName: 'Entrada',
      type: 'number',
      width: 100,
      renderCell: (params) => (
        <Typography color={(params.value as number) > 0 ? "success.main" : "text.primary"}>
            {(params.value as number) > 0 ? `+${params.value}` : "-"}
        </Typography>
      )
    },
    {
      field: 'salida',
      headerName: 'Salida',
      type: 'number',
      width: 100,
      renderCell: (params) => (
        <Typography color={(params.value as number) > 0 ? "error.main" : "text.primary"}>
             {(params.value as number) > 0 ? `-${params.value}` : "-"}
        </Typography>
      )
    },
    {
      field: 'saldoAcumulado',
      headerName: 'Saldo',
      type: 'number',
      width: 120,
      cellClassName: 'font-weight-bold',
    },
    {
      field: 'costoUnitario',
      headerName: 'Costo Unit.',
      type: 'number',
      width: 130,
      valueFormatter: (value) => formatCurrency(value as number),
    },
    {
      field: 'costoTotalMovimiento',
      headerName: 'Costo Total',
      type: 'number',
      width: 130,
      valueFormatter: (value) => formatCurrency(value as number),
    },
    {
      field: 'observaciones',
      headerName: 'Observaciones',
      flex: 1,
      minWidth: 200,
    },
  ], []);

  return (
    <Container maxWidth="xl" sx={{ mt: 2, mb: 4 }}>
      <ReportePageHeader
        title="Kardex de Inventario"
        subtitle="Historial detallado de entradas, salidas y saldos por producto"
        breadcrumbs={[
          { label: 'Reportes', path: '/reportes' },
          { label: 'Kardex' },
        ]}
        color="#1976d2"
      />

      {/* Formulario de Filtros */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom fontWeight={600}>
            Filtros del Kardex
          </Typography>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }} alignItems="flex-start">
            <FormControl sx={{ minWidth: 150 }}>
              <InputLabel>Sucursal *</InputLabel>
              <Select
                value={sucursalId}
                onChange={(e) => setSucursalId(e.target.value as number | '')}
                label="Sucursal *"
              >
                <MenuItem value="">Seleccionar Sucursal</MenuItem>
                {sucursales.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.nombre}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Box sx={{ flex: 2, minWidth: 250 }}>
              <Autocomplete
                options={productos}
                getOptionLabel={(option) => `${option.codigoBarras} - ${option.nombre}`}
                value={producto}
                onChange={(_, newValue) => setProducto(newValue || null)}
                renderInput={(params) => (
                  <TextField {...params} label="Seleccionar Producto *" placeholder="Buscar producto..." />
                )}
              />
            </Box>
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
            <Button
              variant="contained"
              size="large"
              startIcon={<Search />}
              onClick={handleBuscar}
              disabled={!producto || !sucursalId}
              sx={{ height: 56, flex: { xs: 1, md: 'auto' } }}
            >
              Consultar
            </Button>
            <Button
              variant="outlined"
              size="large"
              startIcon={<Download />}
              onClick={() => reporte && exportarReporteKardex(reporte)}
              disabled={!reporte}
              sx={{ height: 56 }}
            >
              Excel
            </Button>
          </Stack>
        </CardContent>
      </Card>

      {/* Carga, Error y Resultados */}
      {isLoading && (
        <Box display="flex" justifyContent="center" p={4}>
          <CircularProgress />
        </Box>
      )}

      {isError && (
        <Alert severity="error" sx={{ mb: 3 }}>
          Ocurrió un error al cargar el kardex: {(error as Error).message}
        </Alert>
      )}

      {reporte && (
        <Stack spacing={3}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" gutterBottom>
                  Saldo Inicial
                </Typography>
                <Typography variant="h4">
                  {reporte.saldoInicial ?? "-"}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" gutterBottom>
                  Entradas
                </Typography>
                <Typography variant="h4" color="success.main">
                  {reporte.movimientos.reduce((acc, m) => acc + m.entrada, 0)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1 }}>
              <CardContent>
                <Typography color="text.secondary" gutterBottom>
                  Salidas
                </Typography>
                <Typography variant="h4" color="error.main">
                  {reporte.movimientos.reduce((acc, m) => acc + m.salida, 0)}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{ flex: 1, bgcolor: 'primary.light', color: 'primary.contrastText' }}>
              <CardContent>
                <Typography variant="subtitle2" gutterBottom>
                  Saldo Final
                </Typography>
                <Typography variant="h4" fontWeight="bold">
                  {reporte.saldoFinal ?? "-"} Unds
                </Typography>
                <Typography variant="caption" sx={{ opacity: 0.9 }}>
                  Costo: {formatCurrency(reporte.costoPromedioVigente)}
                </Typography>
              </CardContent>
            </Card>
          </Stack>

          <Paper sx={{ height: 600, width: '100%' }}>
            <DataGrid
              rows={reporte.movimientos}
              columns={columns}
              getRowId={(row) => `${row.fecha}-${row.tipoMovimiento}-${row.entrada}-${row.salida}`}
              disableRowSelectionOnClick
              density="compact"
              initialState={{
                 sorting: { sortModel: [{ field: 'fecha', sort: 'asc' }] }
              }}
              sx={{
                '& .font-weight-bold': {
                  fontWeight: 'bold',
                },
              }}
            />
          </Paper>
        </Stack>
      )}
      
      {!reporte && !isLoading && !isError && (
          <Alert severity="info">Seleccione un producto y un rango de fechas para consultar su Kardex.</Alert>
      )}
    </Container>
  );
}
