import { useEffect, useRef, useState } from 'react';
import { IconButton, Tooltip, Box } from '@mui/material';
import MicIcon from '@mui/icons-material/Mic';
import MicOffIcon from '@mui/icons-material/MicOff';
import GraphicEqIcon from '@mui/icons-material/GraphicEq';

interface VoiceInputProps {
  onResult: (transcript: string) => void;
  /** Idioma BCP-47. Por defecto 'es-CO' (español Colombia). */
  language?: string;
}

// Compatibilidad Chrome/Edge/Safari — Web Speech API no tiene tipos oficiales en lib.dom
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type SpeechRecognitionCtor = new () => any;
interface SpeechResultEvent { results: Array<Array<{ transcript: string }>> }
interface SpeechErrorEvent { error: string }
declare global {
  interface Window {
    SpeechRecognition: SpeechRecognitionCtor;
    webkitSpeechRecognition: SpeechRecognitionCtor;
  }
}

/**
 * Capa 6 — Entrada por voz.
 * Botón de micrófono que usa la Web Speech API para transcribir voz
 * y emitir el resultado a través de `onResult`.
 * Se oculta automáticamente si el navegador no soporta la API.
 */
export function VoiceInput({ onResult, language = 'es-CO' }: VoiceInputProps) {
  const [listening, setListening]   = useState(false);
  const [error, setError]           = useState<string | null>(null);
  const recognitionRef              = useRef<InstanceType<SpeechRecognitionCtor> | null>(null);

  const isSupported =
    typeof window !== 'undefined' &&
    !!(window.SpeechRecognition || window.webkitSpeechRecognition);

  // Detener al desmontar
  useEffect(() => {
    return () => { recognitionRef.current?.stop(); };
  }, []);

  const start = () => {
    const SpeechRecognitionAPI = window.SpeechRecognition || window.webkitSpeechRecognition;
    const recognition = new SpeechRecognitionAPI();

    recognition.lang            = language;
    recognition.interimResults  = false;
    recognition.maxAlternatives = 3;
    recognition.continuous      = false;

    recognition.onresult = (e: SpeechResultEvent) => {
      const transcript = e.results[0][0].transcript.trim().toLowerCase();
      onResult(transcript);
      setListening(false);
    };

    recognition.onerror = (e: SpeechErrorEvent) => {
      if (e.error === 'not-allowed') {
        setError('Permiso de micrófono denegado');
      } else if (e.error !== 'no-speech') {
        setError('Error de reconocimiento de voz');
      }
      setListening(false);
    };

    recognition.onend = () => setListening(false);

    recognitionRef.current = recognition;
    recognition.start();
    setListening(true);
    setError(null);
  };

  const stop = () => {
    recognitionRef.current?.stop();
    setListening(false);
  };

  if (!isSupported) return null;

  const tooltipTitle = error
    ? error
    : listening
      ? 'Escuchando… (clic para detener)'
      : 'Buscar por voz (Ctrl+M)';

  return (
    <Tooltip title={tooltipTitle}>
      <IconButton
        size="small"
        onClick={listening ? stop : start}
        aria-label={listening ? 'Detener grabación de voz' : 'Iniciar búsqueda por voz'}
        sx={{
          color: listening
            ? 'error.main'
            : error
              ? 'warning.main'
              : 'text.secondary',
          transition: 'color 0.2s',
        }}
      >
        {listening ? (
          <Box sx={{ display: 'flex', alignItems: 'center' }}>
            <GraphicEqIcon
              fontSize="small"
              sx={{
                animation: 'voicePulse 0.8s ease-in-out infinite',
                '@keyframes voicePulse': {
                  '0%, 100%': { opacity: 1, transform: 'scaleY(1)' },
                  '50%':      { opacity: 0.5, transform: 'scaleY(1.4)' },
                },
              }}
            />
          </Box>
        ) : error ? (
          <MicOffIcon fontSize="small" />
        ) : (
          <MicIcon fontSize="small" />
        )}
      </IconButton>
    </Tooltip>
  );
}
