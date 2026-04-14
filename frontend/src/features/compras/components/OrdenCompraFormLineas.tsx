import React from 'react';
import { Controller, type Control, type FieldErrors, type UseFormWatch, type UseFormReset, type UseFieldArrayReturn } from 'react-hook-form';
import {
  Box,
  Button,
  IconButton,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
  Alert,
  Divider,
  Autocomplete,
  alpha,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
import { useSnackbar } from 'notistack';
import type { ImpuestoDTO, ProductoDTO, InventarioStockDTO } from '@/types/api';
import type { OrdenCompraFormData, LineaOrdenError } from './OrdenCompraFormTypes';

const HERO_COLOR = '#1565c0';
const COLS = '100px 1fr 60px 80px';

function ProductoPaperHeader({ children, ...props }: React.HTMLAttributes<HTMLElement>) {
  return (
    <Paper {...(props as object)}>
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: COLS,
          px: 1.5, py: '3px',
          borderBottom: '1px solid',
          borderColor: 'divider',
          bgcolor: 'grey.100',
          position: 'sticky',
          top: 0,
          zIndex: 1,
        }}
      >
        <Typography variant="caption" fontWeight={700} color="text.secondary">Código</Typography>
        <Typography variant="caption" fontWeight={700} color="text.secondary">Nombre</Typography>
        <Typography variant="caption" fontWeight={700} color="text.secondary" sx={{ textAlign: 'right' }}>Stock</Typography>
        <Typography variant="caption" fontWeight={700} color="text.secondary" sx={{ textAlign: 'right' }}>Valor</Typography>
      </Box>
      {children}
    </Paper>
  );
}

interface Props {
  control: Control<OrdenCompraFormData>;
  errors: FieldErrors<OrdenCompraFormData>;
  watch: UseFormWatch<OrdenCompraFormData>;
  reset: UseFormReset<OrdenCompraFormData>;
  fields: UseFieldArrayReturn<OrdenCompraFormData, 'lineas'>['fields'];
  append: UseFieldArrayReturn<OrdenCompraFormData, 'lineas'>['append'];
  remove: UseFieldArrayReturn<OrdenCompraFormData, 'lineas'>['remove'];
  productos: ProductoDTO[];
  impuestos: ImpuestoDTO[];
  stockData: InventarioStockDTO[];
}

export function OrdenCompraFormLineas({
  control,
  errors,
  watch,
  reset,
  fields,
  append,
  remove,
  productos,
  impuestos,
  stockData,
}: Props) {
  const { enqueueSnackbar } = useSnackbar();

  const stockMap = React.useMemo(
    () => new Map(stockData.map((s) => [s.productoId, s.cantidad])),
    [stockData]
  );

  const lineas = watch('lineas');

  const calcularTotales = () => {
    let subtotal = 0;
    let impuestosTotal = 0;
    lineas.forEach((linea) => {
      const subtotalLinea = linea.cantidad * linea.precioUnitario;
      const impuestoSeleccionado = linea.impuestoId
        ? impuestos.find((imp) => imp.id === linea.impuestoId)
        : null;
      subtotal += subtotalLinea;
      impuestosTotal += subtotalLinea * (impuestoSeleccionado?.porcentaje ?? 0);
    });
    return { subtotal, impuestos: impuestosTotal, total: subtotal + impuestosTotal };
  };

  const totales = calcularTotales();

  const agregarLinea = () => {
    append({ productoId: '', cantidad: 1, precioUnitario: 0, impuestoId: undefined });
  };

  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
      {/* Toolbar */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          px: 2,
          py: 1.5,
          borderBottom: '1px solid',
          borderColor: 'divider',
          bgcolor: alpha(HERO_COLOR, 0.04),
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="subtitle1" fontWeight={700}>
            Productos
          </Typography>
          {fields.length > 0 && (
            <Typography
              variant="caption"
              sx={{
                bgcolor: alpha(HERO_COLOR, 0.12),
                color: HERO_COLOR,
                px: 0.75,
                py: 0.125,
                borderRadius: 1,
                fontWeight: 700,
              }}
            >
              {fields.length} línea{fields.length !== 1 ? 's' : ''}
            </Typography>
          )}
        </Box>
        <Button
          type="button"
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={agregarLinea}
          size="small"
        >
          Agregar Producto
        </Button>
      </Box>

      {errors.lineas && typeof errors.lineas.message === 'string' && (
        <Alert severity="error" sx={{ mx: 2, mt: 1.5 }}>
          {errors.lineas.message}
        </Alert>
      )}

      <TableContainer sx={{ maxHeight: 420, overflowY: 'auto' }}>
        <Table
          size="small"
          stickyHeader
          sx={{ tableLayout: 'fixed', '& .MuiTableCell-root': { py: 0.25, px: 0.75 } }}
        >
          <TableHead>
            <TableRow
              sx={{
                '& th': {
                  bgcolor: 'grey.50',
                  fontWeight: 700,
                  fontSize: '0.72rem',
                  textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                  color: 'text.secondary',
                  borderBottom: `2px solid ${alpha(HERO_COLOR, 0.15)}`,
                },
              }}
            >
              <TableCell width={36}>#</TableCell>
              <TableCell>Producto</TableCell>
              <TableCell width={80}>Cant.</TableCell>
              <TableCell width={110}>Precio Unit.</TableCell>
              <TableCell width={100}>IVA</TableCell>
              <TableCell width={110} align="right">Subtotal</TableCell>
              <TableCell width={44} />
            </TableRow>
          </TableHead>
          <TableBody>
            {fields.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 5 }}>
                  <ShoppingCartIcon sx={{ fontSize: 48, color: 'text.disabled', mb: 1 }} />
                  <Typography variant="body2" color="text.secondary">
                    No hay productos. Haga clic en "Agregar Producto"
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              fields.map((field, index) => {
                const linea = lineas[index];
                const subtotalLinea = linea ? linea.cantidad * linea.precioUnitario : 0;

                return (
                  <TableRow key={field.id} sx={{ '&:hover': { bgcolor: 'action.hover' } }}>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">
                        {index + 1}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Controller
                        name={`lineas.${index}.productoId`}
                        control={control}
                        render={({ field: { value, onChange } }) => (
                          <Autocomplete
                            value={productos.find((p) => p.id === value) || null}
                            onChange={(_, newValue) => {
                              if (newValue) {
                                const currentLineas = watch('lineas');
                                const lineaDuplicada = currentLineas.findIndex(
                                  (l, i) => i !== index && l.productoId === newValue.id
                                );
                                if (lineaDuplicada !== -1) {
                                  enqueueSnackbar(
                                    `"${newValue.nombre}" ya está en la línea ${lineaDuplicada + 1}. Modifica la cantidad en esa línea.`,
                                    { variant: 'warning' }
                                  );
                                  return;
                                }
                                onChange(newValue.id);
                                currentLineas[index].precioUnitario = newValue.precioCosto;
                                currentLineas[index].impuestoId = newValue.impuestoId;
                                reset({ ...watch(), lineas: currentLineas });
                              } else {
                                onChange('');
                              }
                            }}
                            options={productos}
                            getOptionLabel={(option) => `${option.nombre} (${option.codigoBarras})`}
                            PaperComponent={ProductoPaperHeader}
                            slotProps={{ listbox: { style: { maxHeight: 480 } } }}
                            renderOption={(props, option) => {
                              const stock = stockMap.get(option.id);
                              return (
                                <Box
                                  component="li"
                                  {...props}
                                  sx={{
                                    display: 'grid !important',
                                    gridTemplateColumns: COLS,
                                    px: '12px !important',
                                    py: '2px !important',
                                    minHeight: '0 !important',
                                    gap: 0.5,
                                    alignItems: 'center',
                                  }}
                                >
                                  <Typography variant="caption" noWrap sx={{ fontFamily: 'monospace', color: 'text.secondary', fontSize: '0.72rem' }}>
                                    {option.codigoBarras}
                                  </Typography>
                                  <Typography variant="caption" noWrap sx={{ fontSize: '0.75rem' }}>
                                    {option.nombre}
                                  </Typography>
                                  <Typography variant="caption" sx={{ textAlign: 'right', fontSize: '0.72rem', color: stock === 0 ? 'error.main' : stock != null ? 'success.main' : 'text.disabled' }}>
                                    {stock != null ? stock : '—'}
                                  </Typography>
                                  <Typography variant="caption" sx={{ textAlign: 'right', fontSize: '0.72rem', fontWeight: 500 }}>
                                    ${option.precioCosto.toLocaleString('es-CO')}
                                  </Typography>
                                </Box>
                              );
                            }}
                            renderInput={(params) => (
                              <TextField
                                {...params}
                                error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.productoId}
                                size="small"
                                sx={{ '& .MuiInputBase-input': { fontSize: '0.8rem', py: '2px' } }}
                              />
                            )}
                            size="small"
                          />
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <Controller
                        name={`lineas.${index}.cantidad`}
                        control={control}
                        render={({ field: { value, onChange, ...f } }) => (
                          <TextField
                            {...f}
                            type="number"
                            value={value}
                            onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                            error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.cantidad}
                            size="small"
                            fullWidth
                            inputProps={{ min: 0, step: 1, style: { fontSize: '0.8rem', padding: '2px 4px', textAlign: 'right' } }}
                          />
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <Controller
                        name={`lineas.${index}.precioUnitario`}
                        control={control}
                        render={({ field: { value, onChange, ...f } }) => (
                          <TextField
                            {...f}
                            type="number"
                            value={value}
                            onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
                            error={!!(errors.lineas?.[index] as LineaOrdenError | undefined)?.precioUnitario}
                            size="small"
                            fullWidth
                            inputProps={{ min: 0, step: 1, style: { fontSize: '0.8rem', padding: '2px 4px', textAlign: 'right' } }}
                          />
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <Controller
                        name={`lineas.${index}.impuestoId`}
                        control={control}
                        render={({ field: { value, onChange, ...f } }) => (
                          <TextField
                            {...f}
                            select
                            value={value ?? ''}
                            onChange={(e) =>
                              onChange(e.target.value ? Number(e.target.value) : undefined)
                            }
                            size="small"
                            fullWidth
                            sx={{ '& .MuiSelect-select': { fontSize: '0.8rem', py: '2px' } }}
                          >
                            {impuestos.map((impuesto) => (
                              <MenuItem key={impuesto.id} value={impuesto.id} sx={{ fontSize: '0.8rem' }}>
                                {impuesto.nombre}
                              </MenuItem>
                            ))}
                          </TextField>
                        )}
                      />
                    </TableCell>
                    <TableCell align="right">
                      <Typography variant="caption" fontWeight={500}>
                        ${subtotalLinea.toLocaleString('es-CO')}
                      </Typography>
                    </TableCell>
                    <TableCell padding="none" align="center">
                      <IconButton
                        size="small"
                        color="error"
                        onClick={() => remove(index)}
                        sx={{ p: 0.5 }}
                      >
                        <DeleteIcon sx={{ fontSize: 16 }} />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Totales */}
      {fields.length > 0 && (
        <Box sx={{ px: 2, py: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
          <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Box sx={{ width: 220 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary">Subtotal:</Typography>
                <Typography variant="caption">${totales.subtotal.toLocaleString('es-CO')}</Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary">IVA:</Typography>
                <Typography variant="caption">${totales.impuestos.toLocaleString('es-CO')}</Typography>
              </Box>
              <Divider sx={{ my: 0.75 }} />
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2" fontWeight={700}>Total:</Typography>
                <Typography variant="body2" fontWeight={700} color="primary">
                  ${totales.total.toLocaleString('es-CO')}
                </Typography>
              </Box>
            </Box>
          </Box>
        </Box>
      )}
    </Paper>
  );
}
