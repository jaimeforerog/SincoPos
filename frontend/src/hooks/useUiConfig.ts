import { useMemo } from 'react';
import { useAuth } from './useAuth';

/**
 * Capa 8 — Adaptación dinámica de UI por rol.
 *
 * Centraliza QUÉ puede ver y hacer cada rol, en lugar de dispersar
 * condicionales `isAdmin() ? ... : ...` por toda la app.
 *
 * Regla: el cajero solo ve lo que necesita para operar.
 * El supervisor puede auditar y ajustar. El gerente tiene vista global.
 */
export interface UiConfig {
  /** Etiqueta legible del rol activo */
  rolLabel:             string;
  /** Puede ver stock y costos en detalle */
  showInventoryDetails: boolean;
  /** Puede editar precio en el carrito */
  showPriceOverride:    boolean;
  /** Puede aplicar descuentos manuales */
  showDiscountOverride: boolean;
  /** Puede ver log de auditoría */
  showAuditLog:         boolean;
  /** Puede ver datos de otras sucursales */
  showCrossStoreData:   boolean;
  /** Puede acceder al dashboard gerencial */
  showDashboard:        boolean;
  /** Límite de productos rápidos (0 = sin límite) */
  quickProductsLimit:   number;
  /** Densidad visual de la UI */
  layout:               'simplified' | 'extended' | 'dashboard';
}

export function useUiConfig(): UiConfig {
  const { isAdmin, isSupervisor, user } = useAuth();

  return useMemo((): UiConfig => {
    if (isAdmin()) return {
      rolLabel:             'Admin',
      showInventoryDetails: true,
      showPriceOverride:    true,
      showDiscountOverride: true,
      showAuditLog:         true,
      showCrossStoreData:   true,
      showDashboard:        true,
      quickProductsLimit:   0,
      layout:               'dashboard',
    };

    if (isSupervisor()) return {
      rolLabel:             'Supervisor',
      showInventoryDetails: true,
      showPriceOverride:    true,
      showDiscountOverride: true,
      showAuditLog:         true,
      showCrossStoreData:   false,
      showDashboard:        true,
      quickProductsLimit:   20,
      layout:               'extended',
    };

    // Cajero — mínimo operativo
    return {
      rolLabel:             'Cajero',
      showInventoryDetails: false,
      showPriceOverride:    false,
      showDiscountOverride: false,
      showAuditLog:         false,
      showCrossStoreData:   false,
      showDashboard:        false,
      quickProductsLimit:   12,
      layout:               'simplified',
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.roles]);
}
