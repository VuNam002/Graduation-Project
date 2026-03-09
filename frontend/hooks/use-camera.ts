import { useState, useEffect, useRef, useCallback } from 'react';
import { fetchStartCamera, fetchStopCamera } from '@/lib/api';
import { toast } from 'sonner';

export function useCamera(cameraId: number = 0) {
  const [isStreaming, setIsStreaming] = useState(false);
  const [frameData, setFrameData] = useState<string | null>(null);
  const [fps, setFps] = useState(0);
  const [wsStatus, setWsStatus] = useState<'disconnected' | 'connecting' | 'connected' | 'error'>('disconnected');
  
  const wsRef = useRef<WebSocket | null>(null);
  const frameCountRef = useRef(0);
  const lastFrameTimeRef = useRef(Date.now());
  const fpsIntervalRef = useRef<NodeJS.Timeout | null>(null);

  const connectWebSocket = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    setWsStatus('connecting');
    // URL WebSocket mặc định (có thể cấu hình lại nếu cần)
    const wsUrl = 'wss://localhost:7215/ws'; 
    
    const ws = new WebSocket(wsUrl);

    ws.onopen = () => {
      setWsStatus('connected');
    };

    ws.onmessage = (event) => {
      if (typeof event.data === 'string' && event.data.startsWith('data:image')) {
        setFrameData(event.data);
        frameCountRef.current += 1;
      }
    };

    ws.onerror = (e) => {
      console.error("WebSocket error", e);
      setWsStatus('error');
      toast.error("Lỗi kết nối WebSocket");
    };

    ws.onclose = () => {
      setWsStatus('disconnected');
    };

    wsRef.current = ws;
  }, []);

  const disconnectWebSocket = useCallback(() => {
    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }
    setWsStatus('disconnected');
  }, []);

  const startCamera = async () => {
    try {
      const response = await fetchStartCamera(cameraId);
      setIsStreaming(true);
      connectWebSocket();
      
      // Reset FPS counter
      frameCountRef.current = 0;
      lastFrameTimeRef.current = Date.now();
      
      if (fpsIntervalRef.current) clearInterval(fpsIntervalRef.current);
      
      fpsIntervalRef.current = setInterval(() => {
        const now = Date.now();
        const delta = now - lastFrameTimeRef.current;
        if (delta > 0) {
           const currentFps = Math.round((frameCountRef.current * 1000) / delta);
           setFps(currentFps);
           frameCountRef.current = 0;
           lastFrameTimeRef.current = now;
        }
      }, 1000);

      toast.success(response.message || "Đã bật Camera");

    } catch (err) {
      const msg = err instanceof Error ? err.message : "Không thể bật camera";
      toast.error(msg);
      setIsStreaming(false);
    }
  };

  const stopCamera = async () => {
    try {
      const response = await fetchStopCamera(cameraId);
      setIsStreaming(false);
      disconnectWebSocket();
      setFrameData(null);
      setFps(0);
      
      if (fpsIntervalRef.current) {
        clearInterval(fpsIntervalRef.current);
        fpsIntervalRef.current = null;
      }

      toast.success(response.message || "Đã tắt Camera");
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Không thể tắt camera";
      toast.error(msg);
    }
  };

  // Cleanup khi unmount
  useEffect(() => {
    return () => {
      if (wsRef.current) {
        wsRef.current.close();
      }
      if (fpsIntervalRef.current) {
        clearInterval(fpsIntervalRef.current);
      }
    };
  }, []);

  return {
    isStreaming,
    frameData,
    fps,
    wsStatus,
    startCamera,
    stopCamera
  };
}