import { useState, useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/hooks/useAuth';
import { useAuthStore } from '@/stores/auth.store';
import { useOfflineSync } from '@/offline/useOfflineSync';
import { useContextualNotification } from '@/hooks/useContextualNotification';
import { useCartStore } from '@/stores/cart.store';
import { cajasApi } from '@/api/cajas';
import { posSessionCache } from '@/offline/posSessionCache';
import { localDateTimeStr } from '@/utils/dates';
import { useCajasAbiertas } from './useCajasAbiertas';
import { useConfiguracionVariableIntQuery } from '@/hooks/useConfiguracionVariable';
import { useTurnPreload } from './useTurnPreload';

export function usePOSSession() {
  const queryClient = useQueryClient();
  const { operacional } = useContextualNotification();
  const { user, activeSucursalId, activeEmpresaId, isLoading } = useAuth();
  const { setActiveSucursal, setActiveEmpresa } = useAuthStore();
  const { isOnline } = useOfflineSync();
  const { items, clearCart } = useCartStore();

  const { data: cajasAbiertas = [], isLoading: isLoadingCajas, isFetched: isFetchedCajas } = useCajasAbiertas();

  const { value: diaMaxVentaAtrazada, isFetched: isConfigFetched } =
    useConfiguracionVariableIntQuery('DiaMax_VentaAtrazada');
  const mostrarFechaVenta = diaMaxVentaAtrazada > 0;

  const [selectedCajaId, setSelectedCajaId] = useState<number | null>(null);
  const [sessionKey, setSessionKey] = useState<string>('');
  const [showSeleccionarCaja, setShowSeleccionarCaja] = useState(false);
  const [fechaVenta, setFechaVenta] = useState<string>(localDateTimeStr);

  useTurnPreload(selectedCajaId, activeSucursalId ?? null, sessionKey);

  const { data: cajaActual } = useQuery({
    queryKey: ['caja', selectedCajaId],
    queryFn: () => cajasApi.getById(selectedCajaId!),
    enabled: selectedCajaId !== null,
  });

  useEffect(() => {
    if (isLoading) return;
    if (isLoadingCajas) return;
    if (!isFetchedCajas && isOnline) return;
    if (!isConfigFetched && isOnline) return;
    if (selectedCajaId) return;

    if (cajasAbiertas.length === 1 && !mostrarFechaVenta) {
      handleSelectCaja(cajasAbiertas[0].id, cajasAbiertas[0].sucursalId, undefined);
      return;
    }

    if (cajasAbiertas.length === 1 && mostrarFechaVenta) {
      setShowSeleccionarCaja(true);
      return;
    }

    if (!isOnline) {
      const lastSession = posSessionCache.loadSession();
      if (lastSession) {
        setSelectedCajaId(lastSession.cajaId);
        if (!activeSucursalId || activeSucursalId !== lastSession.sucursalId) {
          setActiveSucursal(lastSession.sucursalId);
        }
        operacional(`Offline: usando "${lastSession.cajaNombre}" de la última sesión`);
        return;
      }
    }

    setShowSeleccionarCaja(true);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoading, isLoadingCajas, isFetchedCajas, cajasAbiertas, selectedCajaId, isOnline, isConfigFetched, mostrarFechaVenta]);

  const handleSelectCaja = (cajaId: number, sucursalId: number, fechaVentaDialog: string | undefined) => {
    setSelectedCajaId(cajaId);
    setFechaVenta(fechaVentaDialog ?? localDateTimeStr());
    setSessionKey(Date.now().toString());

    const sucursalInfo = user?.sucursalesDisponibles.find((s) => s.id === sucursalId);
    if (sucursalInfo?.empresaId != null && sucursalInfo.empresaId !== activeEmpresaId) {
      setActiveEmpresa(sucursalInfo.empresaId);
    }

    if (!activeSucursalId || activeSucursalId !== sucursalId) {
      setActiveSucursal(sucursalId);
    }

    void queryClient.invalidateQueries({ queryKey: ['inventario'] });
    setShowSeleccionarCaja(false);

    const allCajas = [...cajasAbiertas, ...posSessionCache.loadCajas()];
    const caja = allCajas.find((c) => c.id === cajaId);
    posSessionCache.saveSession({
      cajaId,
      cajaNombre: caja?.nombre ?? `Caja ${cajaId}`,
      sucursalId,
      sucursalNombre: caja?.nombreSucursal ?? '',
    });
  };

  const handleCambiarCaja = () => {
    if (items.length > 0) {
      if (!window.confirm('¿Estás seguro? Se perderá el carrito actual')) return;
      clearCart();
    }
    setShowSeleccionarCaja(true);
  };

  return {
    selectedCajaId,
    cajaActual,
    cajasAbiertas,
    fechaVenta,
    mostrarFechaVenta,
    diaMaxVentaAtrazada,
    showSeleccionarCaja,
    setShowSeleccionarCaja,
    handleSelectCaja,
    handleCambiarCaja,
  };
}
