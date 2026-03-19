/**
 * Capa 6 — Entrada por voz.
 * Parsea un transcript de voz en castellano y extrae:
 * - quantity: número de unidades (1 si no se detecta)
 * - searchTerm: texto del producto para buscar
 *
 * Ejemplos:
 *   "dos coca cola"         → { quantity: 2, searchTerm: "coca cola" }
 *   "3 leche condensada"    → { quantity: 3, searchTerm: "leche condensada" }
 *   "café tostado"          → { quantity: 1, searchTerm: "café tostado" }
 *   "cinco unidades de pan" → { quantity: 5, searchTerm: "pan" }
 */

const NUMEROS_ES: Record<string, number> = {
  un: 1, uno: 1, una: 1,
  dos: 2,
  tres: 3,
  cuatro: 4,
  cinco: 5,
  seis: 6,
  siete: 7,
  ocho: 8,
  nueve: 9,
  diez: 10,
  once: 11,
  doce: 12,
  trece: 13,
  catorce: 14,
  quince: 15,
  veinte: 20,
  veinticinco: 25,
  treinta: 30,
  cuarenta: 40,
  cincuenta: 50,
  cien: 100,
  ciento: 100,
};

export interface VoiceParsed {
  quantity: number;
  searchTerm: string;
}

export function parseVoiceInput(transcript: string): VoiceParsed {
  const text = transcript.trim().toLowerCase();
  const words = text.split(/\s+/);

  if (words.length === 0) return { quantity: 1, searchTerm: text };

  let quantity = 1;
  let startIndex = 0;

  // Detectar cantidad en la primera palabra (número o palabra española)
  if (words.length > 1) {
    const first = words[0];
    const digitMatch = first.match(/^(\d+)$/);
    if (digitMatch) {
      quantity = parseInt(digitMatch[1], 10);
      startIndex = 1;
    } else if (NUMEROS_ES[first] !== undefined) {
      quantity = NUMEROS_ES[first];
      startIndex = 1;
    }
  }

  // Quitar conectores "unidades de", "unidad de", "de" al inicio del producto
  let searchTerm = words.slice(startIndex).join(' ');
  searchTerm = searchTerm.replace(/^(unidades?\s+de\s+|unidad\s+de\s+|de\s+)/i, '').trim();

  return {
    quantity: Math.max(1, Math.min(quantity, 999)),
    searchTerm: searchTerm || text,
  };
}
