import { useNavigate } from 'react-router-dom';
import { OrdenCompraFormView } from '../components/OrdenCompraFormView';

export function NuevaOrdenCompraPage() {
  const navigate = useNavigate();

  return (
    <OrdenCompraFormView
      onBack={() => navigate('/compras')}
      onSuccess={() => navigate('/compras')}
    />
  );
}
