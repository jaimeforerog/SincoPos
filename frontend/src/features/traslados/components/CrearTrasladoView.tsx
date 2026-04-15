import { useState, useEffect } from 'react';
import {
  Box,
  Button,
  TextField,
  Autocomplete,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  IconButton,
  Chip,
  alpha,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import RemoveIcon from '@mui/icons-material/Remove';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useSnackbar } from 'notistack';
import { trasladosApi } from '@/api/traslados';
import { sucursalesApi } from '@/api/sucursales';
import { useAuth } from '@/hooks/useAuth';
import { productosApi } from '@/api/productos';
import { inventarioApi } from '@/api/inventario';
import type { ProductoDTO, SucursalDTO, CrearTrasladoDTO, LineaTrasladoDTO } from '@/types/api';
import { localDateStr } from '@/utils/dates';

const HERO_COLOR = '#1565c0';

interface Props {
  onBack: () => void;
  onSuccess: () => void;
}

interface LineaLocal extends LineaTrasladoDTO {
  producto: ProductoDTO;
  stockDisponible: number;
}

export function CrearTrasladoView({ onBack, onSuccess }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const { user, activeEmpresaId, activeSucursalId } = useAuth();
  const [sucursalOrigen, setSucursalOrigen] = useState<SucursalDTO | null>(null);
  const [sucursalDestino, setSucursalDestino] = useState<SucursalDTO | null>(null);
  const [observaciones, setObservaciones] = useState('');
  const [fechaTraslado, setFechaTraslado] = useState(() => localDateStr());
  const [lineas, setLineas] = useState<LineaLocal[]>([]);
  const [productoSeleccionado, setProductoSeleccionado] = useState<ProductoDTO | null>(null);
  const [cantidad, setCantidad] = useState(1);

  const { data: todasSucursales = [] } = useQuery({
    queryKey: ['sucursales'],
    queryFn: () => sucursalesApi.getAll(),
    staleTime: 5 * 60 * 1000,
  });
  const sucursales = todasSucursales.filter(
    (s) =>
      (activeEmpresaId == null || s.empresaId === activeEmpresaId) &&
      (!user?.sucursalesDisponibles?.length || user.sucursalesDisponibles.some((sd) => sd.id === s.id))
  );

  // Pre-cargar sucursal de origen con la sucursal activa de sesión
  useEffect(() => {
    if (activeSucursalId && sucursales.length > 0 && !sucursalOrigen) {
      const activa = sucursales.find(s => s.id === activeSucursalId);
      if (activa) setSucursalOrigen(activa);
    }
  }, [activeSucursalId, sucursales]); // eslint-disable-line react-hooks/exhaustive-deps

  const { data: productosData } = useQuery({
    queryKey: ['productos', activeEmpresaId],
    queryFn: () => productosApi.listar(),
  });
  const productos = productosData?.items || [];

  const { data: inventario } = useQuery({
    queryKey: ['inventario', sucursalOrigen?.id],
    queryFn: () => inventarioApi.obtenerStock({ sucursalId: sucursalOrigen!.id }),
    enabled: !!sucursalOrigen,
  });

  const crearMutation = useMutation({
    mutationFn: trasladosApi.crear,
    onSuccess: () => {
      enqueueSnackbar('Traslado creado exitosamente', { variant: 'success' });
      onSuccess();
    },
    onError: () => {
      enqueueSnackbar('Error al crear el traslado', { variant: 'error' });
    },
  });

  const getStock = (productoId: string) =>
    inventario?.find((s) => s.productoId === productoId)?.cantidad ?? 0;

  const handleAgregarLinea = () => {
    if (!productoSeleccionado || !sucursalOrigen) return;
    const stockDisponible = getStock(productoSeleccionado.id);
    if (cantidad > stockDisponible) {
      enqueueSnackbar(`Stock insuficiente. Disponible: ${stockDisponible}`, { variant: 'warning' });
      return;
    }
    if (lineas.find((l) => l.productoId === productoSeleccionado.id)) {
      enqueueSnackbar('El producto ya está en la lista', { variant: 'warning' });
      return;
    }
    setLineas([
      ...lineas,
      { productoId: productoSeleccionado.id, cantidad, producto: productoSeleccionado, stockDisponible },
    ]);
    setProductoSeleccionado(null);
    setCantidad(1);
  };

  const handleUpdateCantidad = (productoId: string, nuevaCantidad: number) => {
    setLineas(
      lineas.map((l) => {
        if (l.productoId !== productoId) return l;
        return { ...l, cantidad: Math.max(1, Math.min(nuevaCantidad, l.stockDisponible)) };
      })
    );
  };

  const handleEliminar = (productoId: string) => {
    setLineas(lineas.filter((l) => l.productoId !== productoId));
  };

  const handleCrear = () => {
    if (!sucursalOrigen || !sucursalDestino) {
      enqueueSnackbar('Selecciona sucursal origen y destino', { variant: 'warning' });
      return;
    }
    if (lineas.length === 0) {
      enqueueSnackbar('Agrega al menos un producto', { variant: 'warning' });
      return;
    }
    const dto: CrearTrasladoDTO = {
      sucursalOrigenId: sucursalOrigen.id,
      sucursalDestinoId: sucursalDestino.id,
      observaciones,
      lineas: lineas.map((l) => ({ productoId: l.productoId, cantidad: l.cantidad })),
      fechaTraslado: fechaTraslado ? new Date(fechaTraslado).toISOString() : undefined,
    };
    crearMutation.mutate(dto);
  };

  const stockSeleccionado = productoSeleccionado ? getStock(productoSeleccionado.id) : 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {/* Header */}
      <Box
        sx={{
          background: `linear-gradient(135deg, ${HERO_COLOR} 0%, #0d47a1 50%, #01579b 100%)`,
          borderRadius: 3,
          px: { xs: 2, md: 3 },
          py: 1.5,
          display: 'flex',
          alignItems: 'center',
          gap: 2,
        }}
      >
        <IconButton
          onClick={onBack}
          aria-label="regresar"
          sx={{ color: '#fff', '&:hover': { bgcolor: 'rgba(255,255,255,0.15)' } }}
        >
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h6" fontWeight={700} sx={{ color: '#fff', lineHeight: 1.2 }}>
            Nuevo Traslado
          </Typography>
          <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.75)' }}>
            Traslado de inventario entre sucursales
          </Typography>
        </Box>
        {lineas.length > 0 && (
          <Chip
            label={`${lineas.length} producto${lineas.length > 1 ? 's' : ''} · ${lineas.reduce((a, l) => a + l.cantidad, 0)} uds`}
            sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', fontWeight: 700 }}
          />
        )}
        <Button
          variant="contained"
          startIcon={<SwapHorizIcon />}
          onClick={handleCrear}
          disabled={crearMutation.isPending || lineas.length === 0 || !sucursalOrigen || !sucursalDestino}
          sx={{
            bgcolor: 'rgba(255,255,255,0.15)',
            color: '#fff',
            border: '1px solid rgba(255,255,255,0.35)',
            fontWeight: 700,
            '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
            '&.Mui-disabled': { bgcolor: 'rgba(255,255,255,0.08)', color: 'rgba(255,255,255,0.4)', borderColor: 'transparent' },
          }}
        >
          Crear Traslado
        </Button>
      </Box>

      {/* Body: 2 columns */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '320px 1fr' },
          gap: 2,
          alignItems: 'start',
        }}
      >
        {/* Left: Form */}
        <Paper
          sx={{
            p: 2.5,
            borderRadius: 2,
            border: '1px solid',
            borderColor: 'divider',
            display: 'flex',
            flexDirection: 'column',
            gap: 2,
          }}
        >
          <Typography
            variant="caption"
            fontWeight={700}
            color="text.secondary"
            sx={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}
          >
            Datos del Traslado
          </Typography>

          <Autocomplete
            options={sucursales}
            getOptionLabel={(o) => o.nombre}
            value={sucursalOrigen}
            onChange={(_, v) => {
              setSucursalOrigen(v);
              setLineas([]);
              setProductoSeleccionado(null);
            }}
            size="small"
            renderInput={(params) => <TextField {...params} label="Sucursal Origen" required />}
          />
          <Autocomplete
            options={sucursales.filter((s) => s.id !== sucursalOrigen?.id)}
            getOptionLabel={(o) => o.nombre}
            value={sucursalDestino}
            onChange={(_, v) => setSucursalDestino(v)}
            size="small"
            renderInput={(params) => <TextField {...params} label="Sucursal Destino" required />}
          />
          <TextField
            fullWidth
            label="Fecha del Traslado"
            type="date"
            size="small"
            value={fechaTraslado}
            onChange={(e) => setFechaTraslado(e.target.value)}
            InputLabelProps={{ shrink: true }}
            inputProps={{ max: localDateStr() }}
          />
          <TextField
            fullWidth
            label="Observaciones"
            multiline
            rows={2}
            size="small"
            value={observaciones}
            onChange={(e) => setObservaciones(e.target.value)}
          />
        </Paper>

        {/* Right: search bar + POS-style product table */}
        <Paper
          sx={{
            borderRadius: 2,
            border: '1px solid',
            borderColor: 'divider',
            overflow: 'hidden',
          }}
        >
          {/* Buscador de producto */}
          <Box
            sx={{
              px: 2,
              py: 1.5,
              borderBottom: '1px solid',
              borderColor: 'divider',
              display: 'flex',
              gap: 1.5,
              alignItems: 'flex-start',
              flexWrap: 'wrap',
            }}
          >
            <Autocomplete
              options={productos}
              getOptionLabel={(o) => `${o.nombre} (${o.codigoBarras})`}
              value={productoSeleccionado}
              onChange={(_, v) => {
                setProductoSeleccionado(v);
                if (v) {
                  const s = getStock(v.id);
                  if (cantidad > s) setCantidad(Math.max(1, s));
                }
              }}
              disabled={!sucursalOrigen}
              size="small"
              sx={{ flex: 1, minWidth: 200 }}
              renderOption={(props, option) => {
                const { key, ...otherProps } = props;
                const stock = getStock(option.id);
                return (
                  <Box component="li" key={key} {...otherProps}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', width: '100%', gap: 1 }}>
                      <Typography variant="body2" noWrap>{option.nombre}</Typography>
                      <Typography
                        variant="caption"
                        sx={{ fontWeight: 700, color: stock > 0 ? 'success.main' : 'error.main', whiteSpace: 'nowrap' }}
                      >
                        {stock} uds
                      </Typography>
                    </Box>
                  </Box>
                );
              }}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Buscar producto"
                  helperText={
                    productoSeleccionado
                      ? `Stock disponible: ${stockSeleccionado} unidades`
                      : sucursalOrigen
                      ? 'Escribe para buscar'
                      : 'Primero selecciona sucursal origen'
                  }
                  FormHelperTextProps={{
                    sx: {
                      color: productoSeleccionado && stockSeleccionado > 0 ? 'success.main' : 'text.secondary',
                      fontWeight: productoSeleccionado ? 600 : 400,
                    },
                  }}
                />
              )}
            />
            <TextField
              type="number"
              label="Cantidad"
              size="small"
              value={cantidad}
              onChange={(e) => {
                const v = Number(e.target.value);
                const max = productoSeleccionado ? stockSeleccionado : Infinity;
                setCantidad(Math.min(Math.max(1, v), max));
              }}
              inputProps={{ min: 1, max: productoSeleccionado ? stockSeleccionado : undefined }}
              sx={{ width: 95 }}
            />
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={handleAgregarLinea}
              disabled={
                !productoSeleccionado ||
                !sucursalOrigen ||
                cantidad <= 0 ||
                cantidad > stockSeleccionado
              }
              sx={{ height: 40 }}
            >
              Agregar
            </Button>
          </Box>

          {/* Contador */}
          <Box
            sx={{
              px: 2,
              py: 0.75,
              borderBottom: '1px solid',
              borderColor: 'divider',
              background: alpha(HERO_COLOR, 0.04),
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <Typography variant="subtitle2" fontWeight={700} color={HERO_COLOR} sx={{ flex: 1 }}>
              Productos a trasladar
            </Typography>
            {lineas.length > 0 && (
              <Typography variant="caption" color="text.secondary">
                {lineas.length} producto{lineas.length > 1 ? 's' : ''} ·{' '}
                {lineas.reduce((a, l) => a + l.cantidad, 0)} unidades
              </Typography>
            )}
          </Box>

          {lineas.length === 0 ? (
            <Box
              sx={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 1,
                py: 8,
              }}
            >
              <SwapHorizIcon sx={{ fontSize: 48, color: 'text.disabled' }} />
              <Typography variant="body2" color="text.secondary">
                Aún no hay productos agregados
              </Typography>
              <Typography variant="caption" color="text.disabled">
                Selecciona una sucursal origen y usa el buscador
              </Typography>
            </Box>
          ) : (
            <TableContainer>
              <Table size="small" stickyHeader sx={{ tableLayout: 'fixed', width: '100%' }}>
                <TableHead>
                  <TableRow
                    sx={{
                      '& .MuiTableCell-head': {
                        color: HERO_COLOR,
                        fontWeight: 700,
                        fontSize: '0.72rem',
                        textTransform: 'uppercase',
                        letterSpacing: '0.04em',
                        borderBottom: `2px solid ${alpha(HERO_COLOR, 0.2)}`,
                        bgcolor: alpha(HERO_COLOR, 0.06),
                        py: 0.75,
                      },
                    }}
                  >
                    <TableCell sx={{ width: 36 }}>#</TableCell>
                    <TableCell>Producto</TableCell>
                    <TableCell sx={{ width: 140 }}>Código</TableCell>
                    <TableCell align="center" sx={{ width: 90 }}>
                      Stock
                    </TableCell>
                    <TableCell align="center" sx={{ width: 140 }}>
                      Cantidad
                    </TableCell>
                    <TableCell sx={{ width: 44 }} />
                  </TableRow>
                </TableHead>
                <TableBody>
                  {lineas.map((linea, idx) => (
                    <TableRow
                      key={linea.productoId}
                      sx={{
                        '&:hover': { bgcolor: alpha(HERO_COLOR, 0.03) },
                        '&:last-child td': { borderBottom: 0 },
                      }}
                    >
                      <TableCell sx={{ py: 0.5, color: 'text.disabled', fontSize: '0.75rem' }}>
                        {idx + 1}
                      </TableCell>
                      <TableCell sx={{ py: 0.5 }}>
                        <Typography
                          variant="body2"
                          fontWeight={600}
                          sx={{ lineHeight: 1.2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                        >
                          {linea.producto.nombre}
                        </Typography>
                      </TableCell>
                      <TableCell
                        sx={{ py: 0.5, fontFamily: 'monospace', fontSize: '0.75rem', color: 'text.secondary' }}
                      >
                        {linea.producto.codigoBarras}
                      </TableCell>
                      <TableCell align="center" sx={{ py: 0.5 }}>
                        <Chip
                          label={linea.stockDisponible}
                          size="small"
                          color={linea.stockDisponible > linea.cantidad ? 'default' : 'warning'}
                          variant="outlined"
                          sx={{ fontSize: '0.72rem', height: 20 }}
                        />
                      </TableCell>
                      <TableCell align="center" sx={{ py: 0.5 }}>
                        <Box
                          sx={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            gap: 0.25,
                          }}
                        >
                          <IconButton
                            size="small"
                            onClick={() => handleUpdateCantidad(linea.productoId, linea.cantidad - 1)}
                            disabled={linea.cantidad <= 1}
                            sx={{ p: 0.25 }}
                          >
                            <RemoveIcon sx={{ fontSize: 16 }} />
                          </IconButton>
                          <TextField
                            type="number"
                            value={linea.cantidad}
                            onChange={(e) =>
                              handleUpdateCantidad(linea.productoId, Number(e.target.value))
                            }
                            size="small"
                            sx={{
                              width: 44,
                              '& .MuiOutlinedInput-root': { '& fieldset': { border: 'none' } },
                              '& input': { textAlign: 'center', p: 0.25, fontSize: '0.85rem' },
                            }}
                          />
                          <IconButton
                            size="small"
                            onClick={() => handleUpdateCantidad(linea.productoId, linea.cantidad + 1)}
                            disabled={linea.cantidad >= linea.stockDisponible}
                            sx={{ p: 0.25 }}
                          >
                            <AddIcon sx={{ fontSize: 16 }} />
                          </IconButton>
                        </Box>
                      </TableCell>
                      <TableCell sx={{ py: 0.5 }}>
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => handleEliminar(linea.productoId)}
                          sx={{ p: 0.25 }}
                        >
                          <DeleteIcon sx={{ fontSize: 16 }} />
                        </IconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Paper>
      </Box>
    </Box>
  );
}
