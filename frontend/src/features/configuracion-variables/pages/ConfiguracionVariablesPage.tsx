import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Chip,
  Switch,
  FormControlLabel,
  Tooltip,
} from '@mui/material';
import { DataGrid, GridActionsCellItem } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import { Add as AddIcon, Edit as EditIcon, Delete as DeleteIcon, CheckCircleOutline as ActivarIcon } from '@mui/icons-material';
import { configuracionVariablesApi } from '@/api/configuracionVariables';
import type { ConfiguracionVariableDTO } from '@/types/api';
import { useSnackbar } from 'notistack';
import { ConfiguracionVariableFormDialog } from '../components/ConfiguracionVariableFormDialog';
import { ReportePageHeader } from '@/features/reportes/components/ReportePageHeader';

export function ConfiguracionVariablesPage() {
  const [openDialog, setOpenDialog] = useState(false);
  const [selected, setSelected] = useState<ConfiguracionVariableDTO | null>(null);
  const [incluirInactivas, setIncluirInactivas] = useState(false);
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const { data: variables = [], isLoading } = useQuery({
    queryKey: ['configuracion-variables', incluirInactivas],
    queryFn: () => configuracionVariablesApi.getAll(incluirInactivas),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => configuracionVariablesApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configuracion-variables'] });
      queryClient.invalidateQueries({ queryKey: ['configuracion-variable'] });
      enqueueSnackbar('Variable desactivada correctamente', { variant: 'success' });
    },
    onError: () => {
      enqueueSnackbar('Error al desactivar la variable', { variant: 'error' });
    },
  });

  const activarMutation = useMutation({
    mutationFn: (id: number) => configuracionVariablesApi.activate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configuracion-variables'] });
      queryClient.invalidateQueries({ queryKey: ['configuracion-variable'] });
      enqueueSnackbar('Variable activada correctamente', { variant: 'success' });
    },
    onError: () => {
      enqueueSnackbar('Error al activar la variable', { variant: 'error' });
    },
  });

  const handleCreate = () => {
    setSelected(null);
    setOpenDialog(true);
  };

  const handleEdit = (variable: ConfiguracionVariableDTO) => {
    setSelected(variable);
    setOpenDialog(true);
  };

  const handleDelete = (id: number) => {
    if (confirm('¿Está seguro de desactivar esta variable?')) {
      deleteMutation.mutate(id);
    }
  };

  const handleActivar = (id: number) => {
    if (confirm('¿Está seguro de activar esta variable?')) {
      activarMutation.mutate(id);
    }
  };

  const handleCloseDialog = () => {
    setOpenDialog(false);
    setSelected(null);
  };

  const columns: GridColDef<ConfiguracionVariableDTO>[] = [
    {
      field: 'nombre',
      headerName: 'Nombre',
      flex: 1,
      minWidth: 200,
      renderCell: (params) => (
        <Box sx={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>{params.value}</Box>
      ),
    },
    {
      field: 'valor',
      headerName: 'Valor',
      flex: 1,
      minWidth: 150,
      renderCell: (params) => (
        <Box sx={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>{params.value}</Box>
      ),
    },
    {
      field: 'descripcion',
      headerName: 'Descripción',
      flex: 2,
      minWidth: 300,
      renderCell: (params) => (
        <Tooltip title={params.value || ''} placement="top">
          <Box
            sx={{
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
              maxWidth: '100%',
            }}
          >
            {params.value || '—'}
          </Box>
        </Tooltip>
      ),
    },
    {
      field: 'activo',
      headerName: 'Estado',
      width: 110,
      renderCell: (params) => (
        <Chip
          label={params.value ? 'Activa' : 'Inactiva'}
          color={params.value ? 'success' : 'default'}
          size="small"
        />
      ),
    },
    {
      field: 'actions',
      type: 'actions',
      headerName: 'Acciones',
      width: 100,
      getActions: (params) => [
        <GridActionsCellItem
          icon={<EditIcon />}
          label="Editar"
          onClick={() => handleEdit(params.row)}
          showInMenu={false}
        />,
        <GridActionsCellItem
          icon={<DeleteIcon />}
          label="Desactivar"
          onClick={() => handleDelete(params.row.id)}
          showInMenu={false}
          disabled={!params.row.activo}
        />,
        <GridActionsCellItem
          icon={<ActivarIcon />}
          label="Activar"
          onClick={() => handleActivar(params.row.id)}
          showInMenu={false}
          disabled={params.row.activo}
        />,
      ],
    },
  ];

  return (
    <Container maxWidth="xl">
      <ReportePageHeader
        title="Variables de Configuración"
        subtitle="Parámetros globales del sistema configurables sin redeploy"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Variables' },
        ]}
        backPath="/configuracion"
        color="#37474f"
        action={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleCreate}
            sx={{
              bgcolor: 'rgba(255,255,255,0.15)',
              color: '#fff',
              border: '1px solid rgba(255,255,255,0.35)',
              fontWeight: 700,
              '&:hover': { bgcolor: 'rgba(255,255,255,0.25)', borderColor: '#fff' },
            }}
          >
            Nueva Variable
          </Button>
        }
      />

      <Card>
        <CardContent>
          <Box mb={2}>
            <FormControlLabel
              control={
                <Switch
                  checked={incluirInactivas}
                  onChange={(e) => setIncluirInactivas(e.target.checked)}
                />
              }
              label="Incluir variables inactivas"
            />
          </Box>

          <DataGrid
            rows={variables}
            columns={columns}
            loading={isLoading}
            autoHeight
            initialState={{ pagination: { paginationModel: { pageSize: 25 } } }}
            pageSizeOptions={[25, 50, 100]}
            disableRowSelectionOnClick
            sx={{ '& .MuiDataGrid-cell:focus': { outline: 'none' } }}
          />
        </CardContent>
      </Card>

      <ConfiguracionVariableFormDialog
        open={openDialog}
        onClose={handleCloseDialog}
        variable={selected}
      />
    </Container>
  );
}
