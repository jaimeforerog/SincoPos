import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { VoiceInput } from '../VoiceInput';

// ── Mock de SpeechRecognition ─────────────────────────────────────────────────

type ResultCallback  = (e: any) => void;
type ErrorCallback   = (e: any) => void;
type EndCallback     = () => void;

let _onresult: ResultCallback | null  = null;
let _onerror:  ErrorCallback  | null  = null;
let _onend:    EndCallback    | null  = null;
const startSpy = vi.fn();
const stopSpy  = vi.fn();
let capturedLang = '';

function MockSpeechRecognitionCtor(this: any) {
  this.lang            = '';
  this.interimResults  = false;
  this.maxAlternatives = 1;
  this.continuous      = false;
  Object.defineProperty(this, 'onresult', {
    set(fn: ResultCallback) { _onresult = fn; },
    get() { return _onresult; },
    configurable: true,
  });
  Object.defineProperty(this, 'onerror', {
    set(fn: ErrorCallback) { _onerror = fn; },
    get() { return _onerror; },
    configurable: true,
  });
  Object.defineProperty(this, 'onend', {
    set(fn: EndCallback) { _onend = fn; },
    get() { return _onend; },
    configurable: true,
  });
  this.start = startSpy.mockImplementation(() => {
    capturedLang = this.lang;
  });
  this.stop = stopSpy.mockImplementation(() => {
    _onend?.();
  });
}

// Helper para simular eventos de voz
const simulateResult = (transcript: string) => {
  act(() => {
    _onresult?.({ results: [[{ transcript, confidence: 0.9 }]] });
    _onend?.();
  });
};

// Helper: clic en botón real (ignora clones de Tooltip MUI)
const clickReal = async (name: RegExp) => {
  const user = userEvent.setup();
  const all = screen.getAllByRole('button', { name });
  const real = all.find(b => !b.hasAttribute('data-mui-internal-clone-element')) ?? all[0];
  await user.click(real);
};

describe('VoiceInput', () => {
  beforeEach(() => {
    startSpy.mockClear();
    stopSpy.mockClear();
    _onresult = null;
    _onerror  = null;
    _onend    = null;
    capturedLang = '';
    vi.stubGlobal('SpeechRecognition', MockSpeechRecognitionCtor);
    vi.stubGlobal('webkitSpeechRecognition', undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renderiza el botón de micrófono cuando SpeechRecognition está disponible', () => {
    render(<VoiceInput onResult={vi.fn()} />);
    expect(screen.getAllByRole('button', { name: /iniciar búsqueda por voz/i }).length).toBeGreaterThan(0);
  });

  it('no renderiza nada cuando SpeechRecognition no está disponible', () => {
    vi.stubGlobal('SpeechRecognition', undefined);
    const { container } = render(<VoiceInput onResult={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });

  it('inicia el reconocimiento al hacer clic', async () => {
    render(<VoiceInput onResult={vi.fn()} />);
    await clickReal(/iniciar búsqueda por voz/i);
    expect(startSpy).toHaveBeenCalledOnce();
  });

  it('configura el idioma español colombia por defecto', async () => {
    render(<VoiceInput onResult={vi.fn()} />);
    await clickReal(/iniciar búsqueda por voz/i);
    expect(capturedLang).toBe('es-CO');
  });

  it('acepta idioma personalizado via prop', async () => {
    render(<VoiceInput onResult={vi.fn()} language="es-MX" />);
    await clickReal(/iniciar búsqueda por voz/i);
    expect(capturedLang).toBe('es-MX');
  });

  it('llama onResult con el transcript al reconocer', async () => {
    const onResult = vi.fn();
    render(<VoiceInput onResult={onResult} />);
    await clickReal(/iniciar búsqueda por voz/i);
    simulateResult('dos coca cola');
    expect(onResult).toHaveBeenCalledWith('dos coca cola');
  });

  it('cambia aria-label a "Detener" mientras escucha', async () => {
    render(<VoiceInput onResult={vi.fn()} />);
    await clickReal(/iniciar búsqueda por voz/i);
    expect(screen.getAllByRole('button', { name: /detener grabación/i }).length).toBeGreaterThan(0);
  });

  it('detiene el reconocimiento al hacer clic de nuevo', async () => {
    render(<VoiceInput onResult={vi.fn()} />);
    await clickReal(/iniciar búsqueda por voz/i);
    await clickReal(/detener grabación/i);
    expect(stopSpy).toHaveBeenCalled();
  });

  it('vuelve al estado inicial después del reconocimiento exitoso', async () => {
    render(<VoiceInput onResult={vi.fn()} />);
    await clickReal(/iniciar búsqueda por voz/i);
    simulateResult('leche');
    expect(screen.getAllByRole('button', { name: /iniciar búsqueda por voz/i }).length).toBeGreaterThan(0);
  });
});
