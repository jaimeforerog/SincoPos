import React from 'react';
import {
  Controller,
  type Control,
  type FieldErrors,
  type UseFormReset,
  type UseFormWatch,
  type UseFieldArrayAppend,
  type UseFieldArrayRemove,
  type FieldArrayWithId,
} from 'react-hook-form';
import {
  Box,
  Button,
  TextField,
  MenuItem,
  Typography,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Autocomplete,
  Alert,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import { useSnackbar } from 'notistack';
import type { ProductoDTO, ImpuestoDTO } from '@/types/api';
import {
  PRODUCTO_DROPDOWN_COLS,
  type LineaOrdenError,
  type OrdenCompraFormData,
} from './ordenCompraSchema';

const ProductoPaperHeader = ({ children, ...props }: React.HTMLAttributes<HTMLElement>) => (
  <Paper {...(props as object)}>
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: PRODUCTO_DROPDOWN_COLS,
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

interface LineasOrdenTableProps {
  fields: FieldArrayWithId<OrdenCompraFormData, 'lineas', 'id'>[];
  control: Control<OrdenCompraFormData>;
  errors: FieldErrors<OrdenCompraFormData>;
  reset: UseFormReset<OrdenCompraFormData>;
  watch: UseFormWatch<OrdenCompraFormData>;
  append: UseFieldArrayAppend<OrdenCompraFormData, 'lineas'>;
  remove: UseFieldArrayRemove;
  productos: ProductoDTO[];
  impuestos: ImpuestoDTO[];
  stockMap: Map<string, number>;
  lineas: OrdenCompraFormData['lineas'];
}

export function LineasOrdenTable({
  fields,
  control,
  errors,
  reset,
  watch,
  append,
  remove,
  productos,
  impuestos,
  stockMap,
  lineas,
}: LineasOrdenTableProps) {
  const { enqueueSnackbar } = useSnackbar();

  const agregarLinea = () => {
    append({
      productoId: '',
      cantidad: 1,
      precioUnitario: 0,
      impuestoId: undefined,
    });
  };

  return (
    <>
      <Box sx={{ mb: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Productos</Typography>
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
        <Alert severity="error" sx={{ mb: 2 }}>
          {errors.lineas.message}
        </Alert>
      )}

      <TableContainer component={Paper} variant="outlined" sx={{ mb: 3, maxHeight: 360, overflowY: 'auto' }}>
        <Table size="small" sx={{ tableLayout: 'fixed', '& .MuiTableCell-root': { py: 0.25, px: 0.75 } }}>
          <TableHead>
            <TableRow sx={{ bgcolor: 'grey.50', '& th': { position: 'sticky', top: 0, bgcolor: 'grey.50', zIndex: 1 } }}>
              <TableCell width="52%">Producto</TableCell>
              <TableCell width="8%">Cant.</TableCell>
              <TableCell width="14%">Precio Unit.</TableCell>
              <TableCell width="10%">IVA</TableCell>
              <TableCell width="11%" align="right">Subtotal</TableCell>
              <TableCell width="5%"></TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {fields.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  <Typography variant="body2" color="text.secondary" sx={{ py: 1.5 }}>
                    No hay productos. Clic en "Agregar Producto"
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
                                    gridTemplateColumns: PRODUCTO_DROPDOWN_COLS,
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
                        render={({ field: { value, onChange, ...field } }) => (
                          <TextField
                            {...field}
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
                        render={({ field: { value, onChange, ...field } }) => (
                          <TextField
                            {...field}
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
                        render={({ field: { value, onChange, ...field } }) => (
                          <TextField
                            {...field}
                            select
                            value={value ?? ''}
                            onChange={(e) =>
                              onChange(e.target.value ? Number(e.target.value) : undefined)
                            }
                            size="small"
                            fullWidth
                            sx={{ '& .MuiSelect-select': { fontSize: '0.8rem', py: '2px' } }}
                          >
                            <MenuItem value="" sx={{ fontSize: '0.8rem', color: 'text.secondary' }}>
                              Excluido de IVA
                            </MenuItem>
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
                    <TableCell padding="none">
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
    </>
  );
}
