import type { HTMLAttributes } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  TextField,
  Stack,
  Chip,
  Alert,
  Autocomplete,
  CircularProgress,
  type ChipProps,
} from '@mui/material';
import PersonIcon from '@mui/icons-material/Person';
import SearchIcon from '@mui/icons-material/Search';
import { formatCurrency, formatDate } from '@/utils/format';
import type { VentaDTO, TerceroDTO } from '@/types/api';

interface BusquedaVentaCardProps {
  clientes: TerceroDTO[];
  loadingClientes: boolean;
  clienteSeleccionado: TerceroDTO | null;
  onClienteChange: (cliente: TerceroDTO | null) => void;
  busquedaCliente: string;
  onBusquedaChange: (value: string) => void;

  ventas: VentaDTO[];
  loadingVentas: boolean;
  ventaSeleccionada: VentaDTO | null;
  onVentaChange: (venta: VentaDTO | null) => void;

  fechaDesde: string;
  onFechaDesdeChange: (value: string) => void;
  fechaHasta: string;
  onFechaHastaChange: (value: string) => void;
}

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

export function BusquedaVentaCard({
  clientes,
  loadingClientes,
  clienteSeleccionado,
  onClienteChange,
  busquedaCliente,
  onBusquedaChange,
  ventas,
  loadingVentas,
  ventaSeleccionada,
  onVentaChange,
  fechaDesde,
  onFechaDesdeChange,
  fechaHasta,
  onFechaHastaChange,
}: BusquedaVentaCardProps) {
  return (
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
              onChange={(_, newValue) => onClienteChange(newValue)}
              onInputChange={(_, value) => onBusquedaChange(value)}
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

          {/* Paso 2 — Venta */}
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
                  onChange={(e) => { onFechaDesdeChange(e.target.value); onVentaChange(null); }}
                  size="small"
                  sx={{ minWidth: 155 }}
                  InputLabelProps={{ shrink: true }}
                />
                <TextField
                  label="Fecha Hasta"
                  type="date"
                  value={fechaHasta}
                  onChange={(e) => { onFechaHastaChange(e.target.value); onVentaChange(null); }}
                  size="small"
                  sx={{ minWidth: 155 }}
                  InputLabelProps={{ shrink: true }}
                />
                <Autocomplete
                  options={ventas}
                  getOptionLabel={(option) => option.numeroVenta}
                  value={ventaSeleccionada}
                  onChange={(_, newValue) => onVentaChange(newValue)}
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
  );
}
