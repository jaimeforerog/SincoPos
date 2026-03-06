import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Container,
  Paper,
  Typography,
  TextField,
  MenuItem,
  Button,
  Grid,
  Alert,
  CircularProgress,
  Divider,
  Chip,
  Stack,
} from '@mui/material';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import SaveIcon from '@mui/icons-material/Save';
import { useSnackbar } from 'notistack';
import { useAuth } from '@/hooks/useAuth';
import { facturacionApi } from '@/api/facturacion';
import type { ActualizarConfiguracionEmisorDTO } from '@/types/api';

export function ConfiguracionEmisorPage() {
  const { activeSucursalId } = useAuth();
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const [form, setForm] = useState<ActualizarConfiguracionEmisorDTO>({
    nit: '',
    digitoVerificacion: '',
    razonSocial: '',
    nombreComercial: '',
    direccion: '',
    codigoMunicipio: '',
    codigoDepartamento: '',
    telefono: '',
    email: '',
    codigoCiiu: '',
    perfilTributario: 'REGIMEN_ORDINARIO',
    numeroResolucion: '',
    fechaResolucion: new Date().toISOString().split('T')[0],
    prefijo: 'FV',
    numeroDesde: 1,
    numeroHasta: 1000,
    fechaVigenciaDesde: new Date().toISOString().split('T')[0],
    fechaVigenciaHasta: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString().split('T')[0],
    ambiente: '2',
    pinSoftware: '',
    idSoftware: '',
  });

  const [certFileName, setCertFileName] = useState<string>('');

  const { data: config, isLoading } = useQuery({
    queryKey: ['facturacion-config', activeSucursalId],
    queryFn: () => facturacionApi.getConfiguracion(activeSucursalId!),
    enabled: !!activeSucursalId,
  });

  // Poblar formulario cuando se carga la config
  useEffect(() => {
    if (config) {
      setForm({
        nit: config.nit,
        digitoVerificacion: config.digitoVerificacion,
        razonSocial: config.razonSocial,
        nombreComercial: config.nombreComercial,
        direccion: config.direccion,
        codigoMunicipio: config.codigoMunicipio,
        codigoDepartamento: config.codigoDepartamento,
        telefono: config.telefono,
        email: config.email,
        codigoCiiu: config.codigoCiiu,
        perfilTributario: config.perfilTributario,
        numeroResolucion: config.numeroResolucion,
        fechaResolucion: config.fechaResolucion.split('T')[0],
        prefijo: config.prefijo,
        numeroDesde: config.numeroDesde,
        numeroHasta: config.numeroHasta,
        fechaVigenciaDesde: config.fechaVigenciaDesde.split('T')[0],
        fechaVigenciaHasta: config.fechaVigenciaHasta.split('T')[0],
        ambiente: config.ambiente,
        pinSoftware: config.pinSoftware,
        idSoftware: config.idSoftware,
      });
    }
  }, [config]);

  const mutation = useMutation({
    mutationFn: (data: ActualizarConfiguracionEmisorDTO) =>
      facturacionApi.actualizarConfiguracion(activeSucursalId!, data),
    onSuccess: () => {
      enqueueSnackbar('Configuración guardada correctamente', { variant: 'success' });
      queryClient.invalidateQueries({ queryKey: ['facturacion-config'] });
    },
    onError: () => {
      enqueueSnackbar('Error guardando la configuración', { variant: 'error' });
    },
  });

  const handleCertUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setCertFileName(file.name);
    const reader = new FileReader();
    reader.onload = (ev) => {
      const base64 = (ev.target?.result as string).split(',')[1];
      setForm((prev) => ({ ...prev, certificadoBase64: base64 }));
    };
    reader.readAsDataURL(file);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    mutation.mutate(form);
  };

  const handleChange = (field: keyof ActualizarConfiguracionEmisorDTO) =>
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setForm((prev) => ({
        ...prev,
        [field]: field === 'numeroDesde' || field === 'numeroHasta'
          ? parseInt(e.target.value) || 0
          : e.target.value,
      }));
    };

  if (!activeSucursalId) {
    return (
      <Container maxWidth="lg">
        <Alert severity="warning" sx={{ mt: 3 }}>
          Seleccione una sucursal para configurar la facturación electrónica.
        </Alert>
      </Container>
    );
  }

  if (isLoading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={200}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Container maxWidth="lg">
      <Box sx={{ mb: 3, display: 'flex', alignItems: 'center', gap: 2 }}>
        <Typography variant="h4" fontWeight={700}>
          Configuración Emisor DIAN
        </Typography>
        <Chip
          label={config?.ambiente === '1' ? 'PRODUCCIÓN' : 'PRUEBAS (HAB)'}
          color={config?.ambiente === '1' ? 'error' : 'warning'}
          size="small"
        />
      </Box>

      {!config && (
        <Alert severity="info" sx={{ mb: 3 }}>
          No hay configuración para esta sucursal. Complete los datos y guarde para habilitarla.
        </Alert>
      )}

      <Box component="form" onSubmit={handleSubmit}>
        {/* Datos Fiscales */}
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 2 }}>
            Datos Fiscales del Emisor
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={4}>
              <TextField fullWidth label="NIT" value={form.nit} onChange={handleChange('nit')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={2}>
              <TextField fullWidth label="DV" value={form.digitoVerificacion} onChange={handleChange('digitoVerificacion')} required size="small" inputProps={{ maxLength: 1 }} />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField fullWidth label="Razón Social" value={form.razonSocial} onChange={handleChange('razonSocial')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField fullWidth label="Nombre Comercial" value={form.nombreComercial} onChange={handleChange('nombreComercial')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField fullWidth label="Dirección" value={form.direccion} onChange={handleChange('direccion')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Código Municipio (DANE)" value={form.codigoMunicipio} onChange={handleChange('codigoMunicipio')} required size="small" helperText="Ej: 11001 = Bogotá" />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Código Departamento" value={form.codigoDepartamento} onChange={handleChange('codigoDepartamento')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={4}>
              <TextField fullWidth label="Teléfono" value={form.telefono} onChange={handleChange('telefono')} size="small" />
            </Grid>
            <Grid item xs={12} sm={4}>
              <TextField fullWidth label="Email" type="email" value={form.email} onChange={handleChange('email')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={4}>
              <TextField fullWidth label="Código CIIU" value={form.codigoCiiu} onChange={handleChange('codigoCiiu')} size="small" />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                select fullWidth label="Régimen Tributario"
                value={form.perfilTributario} onChange={handleChange('perfilTributario')}
                size="small" required
              >
                <MenuItem value="REGIMEN_ORDINARIO">Régimen Ordinario</MenuItem>
                <MenuItem value="GRAN_CONTRIBUYENTE">Gran Contribuyente</MenuItem>
                <MenuItem value="REGIMEN_SIMPLE">Régimen Simple (SIMPLE)</MenuItem>
                <MenuItem value="PERSONA_NATURAL">Persona Natural</MenuItem>
              </TextField>
            </Grid>
          </Grid>
        </Paper>

        {/* Resolución DIAN */}
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 2 }}>
            Resolución de Numeración DIAN
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={6}>
              <TextField fullWidth label="Número de Resolución" value={form.numeroResolucion} onChange={handleChange('numeroResolucion')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Fecha Resolución" type="date" value={form.fechaResolucion} onChange={handleChange('fechaResolucion')} required size="small" InputLabelProps={{ shrink: true }} />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Prefijo" value={form.prefijo} onChange={handleChange('prefijo')} required size="small" inputProps={{ maxLength: 5 }} helperText="Ej: FV, SETP, etc." />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Número Desde" type="number" value={form.numeroDesde} onChange={handleChange('numeroDesde')} required size="small" />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Número Hasta" type="number" value={form.numeroHasta} onChange={handleChange('numeroHasta')} required size="small" />
            </Grid>
            {config && (
              <Grid item xs={12} sm={3}>
                <TextField fullWidth label="Número Actual" value={config.numeroActual} size="small" disabled helperText="Gestionado automáticamente" />
              </Grid>
            )}
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Vigencia Desde" type="date" value={form.fechaVigenciaDesde} onChange={handleChange('fechaVigenciaDesde')} required size="small" InputLabelProps={{ shrink: true }} />
            </Grid>
            <Grid item xs={12} sm={3}>
              <TextField fullWidth label="Vigencia Hasta" type="date" value={form.fechaVigenciaHasta} onChange={handleChange('fechaVigenciaHasta')} required size="small" InputLabelProps={{ shrink: true }} />
            </Grid>
          </Grid>
        </Paper>

        {/* Software DIAN y Ambiente */}
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 2 }}>
            Software y Ambiente DIAN
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={4}>
              <TextField
                select fullWidth label="Ambiente"
                value={form.ambiente} onChange={handleChange('ambiente')}
                size="small" required
              >
                <MenuItem value="2">Pruebas (Habilitación)</MenuItem>
                <MenuItem value="1">Producción</MenuItem>
              </TextField>
            </Grid>
            <Grid item xs={12} sm={4}>
              <TextField fullWidth label="ID Software (MUISCA)" value={form.idSoftware} onChange={handleChange('idSoftware')} size="small" />
            </Grid>
            <Grid item xs={12} sm={4}>
              <TextField fullWidth label="PIN Software (MUISCA)" value={form.pinSoftware} onChange={handleChange('pinSoftware')} size="small" type="password" />
            </Grid>
          </Grid>
        </Paper>

        {/* Certificado Digital */}
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>
            Certificado Digital (.p12)
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Certificado emitido por CERTICÁMARA o GS1 Colombia. Solo se actualiza si selecciona un nuevo archivo.
          </Typography>
          <Stack direction="row" spacing={2} alignItems="center">
            <Button
              component="label"
              variant="outlined"
              startIcon={<CloudUploadIcon />}
              size="small"
            >
              Seleccionar .p12
              <input type="file" hidden accept=".p12,.pfx" onChange={handleCertUpload} />
            </Button>
            {certFileName && (
              <Chip label={certFileName} color="success" size="small" onDelete={() => {
                setCertFileName('');
                setForm((prev) => ({ ...prev, certificadoBase64: undefined }));
              }} />
            )}
            {config?.tieneCertificado && !certFileName && (
              <Chip label="Certificado cargado" color="primary" size="small" />
            )}
          </Stack>
          {form.certificadoBase64 && (
            <Box sx={{ mt: 2 }}>
              <TextField
                fullWidth
                label="Contraseña del certificado"
                type="password"
                value={form.certificadoPassword ?? ''}
                onChange={handleChange('certificadoPassword')}
                size="small"
                required
                helperText="Contraseña del archivo .p12"
              />
            </Box>
          )}
        </Paper>

        <Divider sx={{ mb: 3 }} />

        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
          <Button
            type="submit"
            variant="contained"
            startIcon={mutation.isPending ? <CircularProgress size={16} /> : <SaveIcon />}
            disabled={mutation.isPending}
            size="large"
          >
            {mutation.isPending ? 'Guardando...' : 'Guardar Configuración'}
          </Button>
        </Box>
      </Box>
    </Container>
  );
}
