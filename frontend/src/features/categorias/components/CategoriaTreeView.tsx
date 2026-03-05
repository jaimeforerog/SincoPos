import { useState } from 'react';
import { Box, Chip, IconButton, Typography, Collapse, List, ListItem, Paper } from '@mui/material';
import {
  ExpandMore as ExpandMoreIcon,
  ChevronRight as ChevronRightIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
  DriveFileMove as MoveIcon,
  Add as AddIcon,
  FolderOpen as FolderOpenIcon,
  Folder as FolderIcon,
} from '@mui/icons-material';
import type { CategoriaArbolDTO } from '@/types/api';

interface CategoriaTreeViewProps {
  categorias: CategoriaArbolDTO[];
  onEdit: (categoria: CategoriaArbolDTO) => void;
  onDelete: (id: number) => void;
  onMover: (categoria: CategoriaArbolDTO) => void;
  onAddSubcategoria: (padreId: number) => void;
}

interface TreeNodeProps {
  node: CategoriaArbolDTO;
  nivel: number;
  onEdit: (categoria: CategoriaArbolDTO) => void;
  onDelete: (id: number) => void;
  onMover: (categoria: CategoriaArbolDTO) => void;
  onAddSubcategoria: (padreId: number) => void;
}

function TreeNode({ node, nivel, onEdit, onDelete, onMover, onAddSubcategoria }: TreeNodeProps) {
  const [expanded, setExpanded] = useState(true);
  const hasChildren = node.subCategorias && node.subCategorias.length > 0;

  return (
    <>
      <ListItem
        sx={{
          pl: nivel * 3 + 1,
          py: 0.5,
          display: 'flex',
          alignItems: 'center',
          '&:hover': {
            backgroundColor: 'action.hover',
          },
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', flexGrow: 1, gap: 1 }}>
          {hasChildren ? (
            <IconButton
              size="small"
              onClick={() => setExpanded(!expanded)}
              sx={{ p: 0.5 }}
            >
              {expanded ? <ExpandMoreIcon fontSize="small" /> : <ChevronRightIcon fontSize="small" />}
            </IconButton>
          ) : (
            <Box sx={{ width: 28 }} />
          )}

          {hasChildren ? (
            expanded ? (
              <FolderOpenIcon fontSize="small" color="primary" />
            ) : (
              <FolderIcon fontSize="small" color="primary" />
            )
          ) : (
            <Box sx={{ width: 20 }} />
          )}

          <Typography variant="body2" sx={{ fontWeight: nivel === 0 ? 600 : 400, flexGrow: 1 }}>
            {node.nombre}
          </Typography>

          {!node.activa && (
            <Chip label="Inactiva" size="small" color="default" sx={{ height: 20 }} />
          )}

          {node.cantidadProductos > 0 && (
            <Chip
              label={`${node.cantidadProductos} producto${node.cantidadProductos !== 1 ? 's' : ''}`}
              size="small"
              color="info"
              variant="outlined"
              sx={{ height: 20 }}
            />
          )}
        </Box>

        <Box sx={{ display: 'flex', gap: 0.5 }}>
          <IconButton
            size="small"
            onClick={() => onAddSubcategoria(node.id)}
            title="Agregar subcategoría"
            disabled={node.nivel >= 2}
          >
            <AddIcon fontSize="small" />
          </IconButton>

          <IconButton
            size="small"
            onClick={() => onEdit(node)}
            title="Editar"
          >
            <EditIcon fontSize="small" />
          </IconButton>

          <IconButton
            size="small"
            onClick={() => onMover(node)}
            title="Mover"
          >
            <MoveIcon fontSize="small" />
          </IconButton>

          <IconButton
            size="small"
            onClick={() => onDelete(node.id)}
            disabled={hasChildren || node.cantidadProductos > 0}
            title={
              hasChildren
                ? 'No se puede eliminar: tiene subcategorías'
                : node.cantidadProductos > 0
                ? 'No se puede eliminar: tiene productos'
                : 'Eliminar'
            }
          >
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Box>
      </ListItem>

      {hasChildren && (
        <Collapse in={expanded} timeout="auto" unmountOnExit>
          <List component="div" disablePadding>
            {node.subCategorias.map((child) => (
              <TreeNode
                key={child.id}
                node={child}
                nivel={nivel + 1}
                onEdit={onEdit}
                onDelete={onDelete}
                onMover={onMover}
                onAddSubcategoria={onAddSubcategoria}
              />
            ))}
          </List>
        </Collapse>
      )}
    </>
  );
}

export function CategoriaTreeView({
  categorias,
  onEdit,
  onDelete,
  onMover,
  onAddSubcategoria,
}: CategoriaTreeViewProps) {
  if (categorias.length === 0) {
    return (
      <Box sx={{ p: 3, textAlign: 'center' }}>
        <Typography color="text.secondary">No hay categorías creadas</Typography>
      </Box>
    );
  }

  return (
    <Paper variant="outlined" sx={{ maxHeight: 600, overflow: 'auto' }}>
      <List dense>
        {categorias.map((cat) => (
          <TreeNode
            key={cat.id}
            node={cat}
            nivel={0}
            onEdit={onEdit}
            onDelete={onDelete}
            onMover={onMover}
            onAddSubcategoria={onAddSubcategoria}
          />
        ))}
      </List>
    </Paper>
  );
}
