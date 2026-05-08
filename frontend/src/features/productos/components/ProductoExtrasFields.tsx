import {
  Box,
  TextField,
  FormControl,
  FormControlLabel,
  InputLabel,
  Select,
  MenuItem,
  Switch,
  Typography,
} from '@mui/material';
import type { ImpuestoDTO } from '@/types/api';

interface ProductoExtrasFieldsProps {
  impuestos: ImpuestoDTO[];
  impuestoId: number | '';
  onImpuestoChange: (id: number | '') => void;
  manejaLotes: boolean;
  onManejaLotesChange: (value: boolean) => void;
  diasVidaUtil: number | '';
  onDiasVidaUtilChange: (value: number | '') => void;
  esAlimentoUltraprocesado: boolean;
  onEsAlimentoUltraprocesadoChange: (value: boolean) => void;
  gramosAzucarPor100ml: number | '';
  onGramosAzucarPor100mlChange: (value: number | '') => void;
  cantidadMlPorUnidad: number | '';
  onCantidadMlPorUnidadChange: (value: number | '') => void;
}

export function ProductoExtrasFields({
  impuestos,
  impuestoId,
  onImpuestoChange,
  manejaLotes,
  onManejaLotesChange,
  diasVidaUtil,
  onDiasVidaUtilChange,
  esAlimentoUltraprocesado,
  onEsAlimentoUltraprocesadoChange,
  gramosAzucarPor100ml,
  onGramosAzucarPor100mlChange,
  cantidadMlPorUnidad,
  onCantidadMlPorUnidadChange,
}: ProductoExtrasFieldsProps) {
  return (
    <>
      <FormControl fullWidth>
        <InputLabel id="impuesto-label">IVA / Impuesto *</InputLabel>
        <Select
          labelId="impuesto-label"
          label="IVA / Impuesto *"
          value={impuestoId}
          onChange={(e) => onImpuestoChange(e.target.value as number | '')}
        >
          <MenuItem value="">
            <em>Exento (sin IVA)</em>
          </MenuItem>
          {impuestos.map((imp) => (
            <MenuItem key={imp.id} value={imp.id}>
              {imp.nombre}
              {imp.porcentaje != null && imp.porcentaje > 0 ? ` — ${(imp.porcentaje * 100).toFixed(0)}%` : ''}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      <FormControlLabel
        control={
          <Switch
            checked={manejaLotes}
            onChange={(e) => {
              onManejaLotesChange(e.target.checked);
              if (!e.target.checked) onDiasVidaUtilChange('');
            }}
            color="primary"
          />
        }
        label={
          <Box>
            <Box component="span" sx={{ fontWeight: 500 }}>Maneja lotes y vencimiento</Box>
            <Box component="span" sx={{ display: 'block', fontSize: '0.75rem', color: 'text.secondary' }}>
              Activa el control FEFO por número de lote y fecha de vencimiento
            </Box>
          </Box>
        }
      />

      {manejaLotes && (
        <TextField
          type="number"
          label="Plazo de vencimiento (días)"
          value={diasVidaUtil}
          onChange={(e) => {
            const val = parseInt(e.target.value);
            onDiasVidaUtilChange(isNaN(val) || val <= 0 ? '' : val);
          }}
          fullWidth
          inputProps={{ min: 1 }}
          helperText="Vida útil en días. Al recibir un lote sin fecha explícita, se calcula automáticamente."
        />
      )}

      {/* ── Impuesto Saludable (Ley 2277/2022) ──────────────────────────── */}
      <Box sx={{ borderTop: 1, borderColor: 'divider', pt: 2, mt: 1 }}>
        <Typography variant="subtitle2" sx={{ mb: 1, color: 'text.secondary' }}>
          Impuesto Saludable (Ley 2277/2022)
        </Typography>

        <FormControlLabel
          control={
            <Switch
              checked={esAlimentoUltraprocesado}
              onChange={(e) => onEsAlimentoUltraprocesadoChange(e.target.checked)}
              color="primary"
            />
          }
          label={
            <Box>
              <Box component="span" sx={{ fontWeight: 500 }}>Es alimento ultraprocesado</Box>
              <Box component="span" sx={{ display: 'block', fontSize: '0.75rem', color: 'text.secondary' }}>
                Aplica la tarifa de Impuesto Saludable sobre la base imponible.
              </Box>
            </Box>
          }
          sx={{ mb: 1 }}
        />

        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
          <TextField
            type="number"
            label="Azúcar (g / 100 ml)"
            value={gramosAzucarPor100ml}
            onChange={(e) => {
              const val = parseFloat(e.target.value);
              onGramosAzucarPor100mlChange(isNaN(val) || val < 0 ? '' : val);
            }}
            inputProps={{ min: 0, step: 0.1 }}
            helperText="Solo bebidas azucaradas. Vacío = no aplica."
            fullWidth
          />
          <TextField
            type="number"
            label="Volumen por unidad (ml)"
            value={cantidadMlPorUnidad}
            onChange={(e) => {
              const val = parseFloat(e.target.value);
              onCantidadMlPorUnidadChange(isNaN(val) || val <= 0 ? '' : val);
            }}
            inputProps={{ min: 1, step: 1 }}
            helperText="Escala el impuesto por (ml / 100). Vacío = se asume 100 ml."
            fullWidth
          />
        </Box>
      </Box>
    </>
  );
}
