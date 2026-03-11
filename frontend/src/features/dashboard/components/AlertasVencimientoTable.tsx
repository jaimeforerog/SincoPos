import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
} from '@mui/material';
import type { AlertaLoteDTO } from '@/types/api';

interface Props {
  alertas: AlertaLoteDTO[];
}

function chipVencimiento(dias: number) {
  if (dias <= 0) return <Chip label="Vencido" color="error" size="small" />;
  if (dias <= 7) return <Chip label={`${dias}d`} color="error" size="small" />;
  if (dias <= 15) return <Chip label={`${dias}d`} color="warning" size="small" />;
  return <Chip label={`${dias}d`} color="info" size="small" />;
}

export function AlertasVencimientoTable({ alertas }: Props) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 2 }}>
        Vencimiento de Lotes
        {alertas.length > 0 && (
          <Chip label={alertas.length} color="warning" size="small" sx={{ ml: 1 }} />
        )}
      </Typography>

      {alertas.length === 0 ? (
        <Box sx={{ py: 3, textAlign: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            Sin lotes próximos a vencer
          </Typography>
        </Box>
      ) : (
        <TableContainer sx={{ maxHeight: 280 }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell>Producto</TableCell>
                <TableCell>Lote</TableCell>
                <TableCell align="right">Disponible</TableCell>
                <TableCell align="center">Vence en</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {alertas.slice(0, 10).map((a) => (
                <TableRow key={a.loteId}>
                  <TableCell>
                    <Typography variant="body2" noWrap sx={{ maxWidth: 160 }}>
                      {a.nombreProducto}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {a.nombreSucursal}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" fontFamily="monospace">
                      {a.numeroLote || '—'}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {a.fechaVencimiento}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">{a.cantidadDisponible}</TableCell>
                  <TableCell align="center">{chipVencimiento(a.diasParaVencer)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
}
