import { useEffect, useRef, useState, useCallback } from 'react';
import {
  Box,
  IconButton,
  Tooltip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  CircularProgress,
} from '@mui/material';
import QrCodeScannerIcon from '@mui/icons-material/QrCodeScanner';
import CloseIcon from '@mui/icons-material/Close';
import { BrowserMultiFormatReader } from '@zxing/browser';
import { NotFoundException } from '@zxing/library';

interface CameraInputProps {
  onDetected: (code: string) => void;
}

/**
 * Capa 1 — Entrada por cámara (OCR / códigos de barras).
 * Abre un diálogo con el feed de la cámara y emite el código detectado.
 * Usa @zxing/browser (BrowserMultiFormatReader) para soportar
 * EAN-13, EAN-8, Code 128, QR y DataMatrix sin config adicional.
 */
export function CameraInput({ onDetected }: CameraInputProps) {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [scanning, setScanning] = useState(false);
  const videoRef = useRef<HTMLVideoElement>(null);
  const readerRef = useRef<BrowserMultiFormatReader | null>(null);
  const controlsRef = useRef<{ stop: () => void } | null>(null);

  const stopScanner = useCallback(() => {
    controlsRef.current?.stop();
    controlsRef.current = null;
    readerRef.current = null;
    setScanning(false);
  }, []);

  const startScanner = useCallback(async () => {
    if (!videoRef.current) return;
    setError(null);
    setScanning(true);

    try {
      const reader = new BrowserMultiFormatReader();
      readerRef.current = reader;

      const controls = await reader.decodeFromVideoDevice(
        undefined, // usa la cámara trasera/por defecto
        videoRef.current,
        (result, err) => {
          if (result) {
            const code = result.getText();
            stopScanner();
            setOpen(false);
            onDetected(code);
          } else if (err && !(err instanceof NotFoundException)) {
            // NotFoundException es normal entre frames — ignorar
            console.warn('[CameraInput]', err);
          }
        }
      );

      controlsRef.current = controls;
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Error al acceder a la cámara';
      setError(
        msg.includes('Permission')
          ? 'Permiso de cámara denegado. Habilítalo en la configuración del navegador.'
          : msg.includes('device')
          ? 'No se encontró cámara en este dispositivo.'
          : msg
      );
      setScanning(false);
    }
  }, [onDetected, stopScanner]);

  // Arrancar cuando el diálogo abre, detener cuando cierra
  useEffect(() => {
    if (open) {
      void startScanner();
    } else {
      stopScanner();
    }
    return () => stopScanner();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const handleClose = () => {
    setOpen(false);
    setError(null);
  };

  return (
    <>
      <Tooltip title="Escanear código de barras">
        <IconButton
          onClick={() => setOpen(true)}
          size="small"
          sx={{ color: 'text.secondary' }}
          aria-label="Abrir escáner de cámara"
        >
          <QrCodeScannerIcon fontSize="small" />
        </IconButton>
      </Tooltip>

      <Dialog
        open={open}
        onClose={handleClose}
        maxWidth="sm"
        fullWidth
        PaperProps={{ sx: { borderRadius: 3 } }}
      >
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', pb: 1 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <QrCodeScannerIcon color="primary" />
            <Typography fontWeight={600}>Escanear código</Typography>
          </Box>
          <IconButton onClick={handleClose} size="small">
            <CloseIcon fontSize="small" />
          </IconButton>
        </DialogTitle>

        <DialogContent sx={{ p: 2 }}>
          {error ? (
            <Box
              sx={{
                bgcolor: 'error.light',
                color: '#fff',
                borderRadius: 2,
                p: 2,
                textAlign: 'center',
              }}
            >
              <Typography variant="body2">{error}</Typography>
            </Box>
          ) : (
            <Box sx={{ position: 'relative', borderRadius: 2, overflow: 'hidden', bgcolor: '#000', aspectRatio: '4/3' }}>
              {/* Video feed */}
              <video
                ref={videoRef}
                style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }}
                muted
                playsInline
              />

              {/* Overlay: línea de escaneo + spinner mientras carga */}
              {scanning && (
                <Box
                  sx={{
                    position: 'absolute',
                    inset: 0,
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    pointerEvents: 'none',
                  }}
                >
                  {/* Visor central */}
                  <Box
                    sx={{
                      width: '60%',
                      aspectRatio: '3/2',
                      border: '2px solid rgba(255,255,255,0.8)',
                      borderRadius: 1,
                      boxShadow: '0 0 0 9999px rgba(0,0,0,0.45)',
                      position: 'relative',
                      overflow: 'hidden',
                    }}
                  >
                    {/* Línea de escaneo animada */}
                    <Box
                      sx={{
                        position: 'absolute',
                        left: 0,
                        right: 0,
                        height: 2,
                        bgcolor: 'primary.main',
                        opacity: 0.9,
                        animation: 'scanLine 1.6s linear infinite',
                        '@keyframes scanLine': {
                          '0%':   { top: '10%' },
                          '50%':  { top: '88%' },
                          '100%': { top: '10%' },
                        },
                      }}
                    />
                  </Box>

                  <Typography
                    variant="caption"
                    sx={{ color: 'rgba(255,255,255,0.85)', mt: 1.5, textShadow: '0 1px 3px rgba(0,0,0,0.8)' }}
                  >
                    Apunta la cámara al código
                  </Typography>
                </Box>
              )}

              {/* Spinner inicial mientras la cámara arranca */}
              {!scanning && !error && (
                <Box
                  sx={{
                    position: 'absolute',
                    inset: 0,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    bgcolor: 'rgba(0,0,0,0.6)',
                  }}
                >
                  <CircularProgress size={36} sx={{ color: '#fff' }} />
                </Box>
              )}
            </Box>
          )}
        </DialogContent>

        <DialogActions sx={{ px: 2, pb: 2 }}>
          {error && (
            <Button variant="outlined" onClick={() => { setError(null); void startScanner(); }} sx={{ mr: 'auto' }}>
              Reintentar
            </Button>
          )}
          <Button onClick={handleClose} color="inherit">
            Cancelar
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
