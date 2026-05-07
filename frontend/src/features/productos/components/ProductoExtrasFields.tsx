import {
  Box,
  TextField,
  FormControl,
  FormControlLabel,
  InputLabel,
  Select,
  MenuItem,
  Switch,
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
}

export function ProductoExtrasFields({
  impuestos,
  impuestoId,
  onImpuestoChange,
  manejaLotes,
  onManejaLotesChange,
  diasVidaUtil,
  onDiasVidaUtilChange,
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
    </>
  );
}
