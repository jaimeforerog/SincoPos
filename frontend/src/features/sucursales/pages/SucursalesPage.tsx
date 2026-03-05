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
} from '@mui/material';
import { DataGrid, GridActionsCellItem } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
} from '@mui/icons-material';
import { sucursalesApi } from '@/api/sucursales';
import type { SucursalDTO } from '@/types/api';
import { useSnackbar } from 'notistack';
import { SucursalFormDialog } from '../components/SucursalFormDialog';
import { PageHeader } from '@/components/common/PageHeader';

export function SucursalesPage() {
  const [openDialog, setOpenDialog] = useState(false);
  const [selectedSucursal, setSelectedSucursal] = useState<SucursalDTO | null>(null);
  const [incluirInactivas, setIncluirInactivas] = useState(false);
  const { enqueueSnackbar } = useSnackbar();
  const queryClient = useQueryClient();

  const { data: sucursales, isLoading } = useQuery({
    queryKey: ['sucursales', incluirInactivas],
    queryFn: () => sucursalesApi.getAll(incluirInactivas),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => sucursalesApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sucursales'] });
      enqueueSnackbar('Sucursal desactivada correctamente', { variant: 'success' });
    },
    onError: () => {
      enqueueSnackbar('Error al desactivar la sucursal', { variant: 'error' });
    },
  });

  const handleCreate = () => {
    setSelectedSucursal(null);
    setOpenDialog(true);
  };

  const handleEdit = (sucursal: SucursalDTO) => {
    setSelectedSucursal(sucursal);
    setOpenDialog(true);
  };

  const handleDelete = (id: number) => {
    if (confirm('¿Está seguro de desactivar esta sucursal?')) {
      deleteMutation.mutate(id);
    }
  };

  const handleCloseDialog = () => {
    setOpenDialog(false);
    setSelectedSucursal(null);
  };

  const columns: GridColDef<SucursalDTO>[] = [
    {
      field: 'nombre',
      headerName: 'Nombre',
      flex: 1,
      minWidth: 250,
    },
    {
      field: 'nombrePais',
      headerName: 'País',
      flex: 1,
      minWidth: 150,
      renderCell: (params) => (
        <Box>
          {params.row.nombrePais || 'Colombia'}
        </Box>
      ),
    },
    {
      field: 'ciudad',
      headerName: 'Ciudad',
      flex: 1,
      minWidth: 180,
    },
    {
      field: 'metodoCosteo',
      headerName: 'Método de Costeo',
      flex: 1,
      minWidth: 180,
      renderCell: (params) => (
        <Chip
          label={params.value}
          size="small"
          color="info"
          variant="outlined"
        />
      ),
    },
    {
      field: 'activa',
      headerName: 'Estado',
      width: 120,
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
          disabled={!params.row.activa}
        />,
      ],
    },
  ];

  return (
    <Container maxWidth="xl">
      <PageHeader
        title="Sucursales"
        breadcrumbs={[
          { label: 'Configuración', path: '/configuracion' },
          { label: 'Sucursales' }
        ]}
        showBackButton={true}
        backPath="/configuracion"
        action={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleCreate}
          >
            Nueva Sucursal
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
              label="Incluir sucursales inactivas"
            />
          </Box>

          <DataGrid
            rows={sucursales || []}
            columns={columns}
            loading={isLoading}
            autoHeight
            initialState={{
              pagination: {
                paginationModel: { pageSize: 10 },
              },
            }}
            pageSizeOptions={[10, 25, 50]}
            disableRowSelectionOnClick
            sx={{
              '& .MuiDataGrid-cell:focus': {
                outline: 'none',
              },
            }}
          />
        </CardContent>
      </Card>

      <SucursalFormDialog
        open={openDialog}
        onClose={handleCloseDialog}
        sucursal={selectedSucursal}
      />
    </Container>
  );
}
