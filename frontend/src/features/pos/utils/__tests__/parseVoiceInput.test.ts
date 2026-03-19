import { describe, it, expect } from 'vitest';
import { parseVoiceInput } from '../parseVoiceInput';

describe('parseVoiceInput', () => {
  it('retorna quantity=1 y el texto completo cuando no hay número', () => {
    expect(parseVoiceInput('café tostado')).toEqual({ quantity: 1, searchTerm: 'café tostado' });
  });

  it('extrae dígito al inicio', () => {
    expect(parseVoiceInput('3 leche')).toEqual({ quantity: 3, searchTerm: 'leche' });
  });

  it('extrae número en español — dos', () => {
    expect(parseVoiceInput('dos coca cola')).toEqual({ quantity: 2, searchTerm: 'coca cola' });
  });

  it('extrae número en español — cinco', () => {
    expect(parseVoiceInput('cinco panes')).toEqual({ quantity: 5, searchTerm: 'panes' });
  });

  it('extrae número en español — diez', () => {
    expect(parseVoiceInput('diez gaseosas')).toEqual({ quantity: 10, searchTerm: 'gaseosas' });
  });

  it('elimina "unidades de" del término de búsqueda', () => {
    expect(parseVoiceInput('cinco unidades de pan')).toEqual({ quantity: 5, searchTerm: 'pan' });
  });

  it('elimina "unidad de" del término de búsqueda', () => {
    expect(parseVoiceInput('una unidad de agua')).toEqual({ quantity: 1, searchTerm: 'agua' });
  });

  it('elimina "de" al inicio del término después de cantidad', () => {
    expect(parseVoiceInput('tres de leche')).toEqual({ quantity: 3, searchTerm: 'leche' });
  });

  it('no extrae cantidad si la palabra numérica es la única', () => {
    // Un solo token no parsea cantidad
    expect(parseVoiceInput('dos')).toEqual({ quantity: 1, searchTerm: 'dos' });
  });

  it('limita quantity a máximo 999', () => {
    const { quantity } = parseVoiceInput('1000 productos');
    expect(quantity).toBe(999);
  });

  it('maneja input vacío devolviendo quantity=1', () => {
    const result = parseVoiceInput('');
    expect(result.quantity).toBe(1);
  });

  it('maneja números grandes con dígitos', () => {
    expect(parseVoiceInput('20 unidades de arroz')).toEqual({ quantity: 20, searchTerm: 'arroz' });
  });

  it('preserva el texto completo si no hay cantidad', () => {
    expect(parseVoiceInput('aceite de oliva extra virgen')).toEqual({
      quantity: 1,
      searchTerm: 'aceite de oliva extra virgen',
    });
  });

  it('extrae veinte', () => {
    expect(parseVoiceInput('veinte cervezas')).toEqual({ quantity: 20, searchTerm: 'cervezas' });
  });

  it('extrae doce', () => {
    expect(parseVoiceInput('doce huevos')).toEqual({ quantity: 12, searchTerm: 'huevos' });
  });
});
