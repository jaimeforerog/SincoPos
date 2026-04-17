import { format, parseISO } from 'date-fns';
import { es } from 'date-fns/locale';

export const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(amount);
};

export const formatNumber = (value: number, decimals: number = 2): string => {
  return new Intl.NumberFormat('es-CO', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value);
};

export const formatDate = (
  date: string | Date,
  formatStr: string = 'dd/MM/yyyy'
): string => {
  const dateObj = typeof date === 'string' ? parseISO(date) : date;
  return format(dateObj, formatStr, { locale: es });
};

/**
 * Formatea un campo fecha-solo (sin hora) almacenado como UTC midnight (ej. "2026-04-01T00:00:00Z").
 * Extrae la parte YYYY-MM-DD del ISO string y construye una fecha local para evitar que la
 * conversión UTC→local desplace el día (ej. medianoche UTC = 31/3 en Colombia UTC-5).
 */
export const formatDateOnly = (date: string | Date): string => {
  const isoStr = typeof date === 'string' ? date : date.toISOString();
  const datePart = isoStr.substring(0, 10); // "YYYY-MM-DD"
  const [y, m, d] = datePart.split('-').map(Number);
  return format(new Date(y, m - 1, d), 'dd/MM/yyyy', { locale: es });
};

export const formatDateTime = (date: string | Date): string => {
  const dateObj = typeof date === 'string' ? parseISO(date) : date;
  return format(dateObj, 'dd/MM/yyyy HH:mm', { locale: es });
};

export const formatDateTimeShort = (date: string | Date): string => {
  const dateObj = typeof date === 'string' ? parseISO(date) : date;
  return format(dateObj, 'dd/MM/yy HH:mm', { locale: es });
};
