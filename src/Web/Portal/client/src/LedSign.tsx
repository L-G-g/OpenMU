import { useEffect, useRef, useState } from 'react';

const ROWS = 16; // the dot-matrix sign of a colectivo: 16 rows of amber dots
const LIT = '#ffb000';
const DARK = 'rgba(255, 176, 0, 0.07)';

/** Rasterizes a line of text into a grid of lit dots, the way the bus sign's controller does. */
function rasterize(text: string): boolean[][] {
  const c = document.createElement('canvas');
  const ctx = c.getContext('2d', { willReadFrequently: true })!;
  ctx.font = `${ROWS}px "DotGothic16", monospace`;
  const width = Math.max(1, Math.ceil(ctx.measureText(text).width));
  c.width = width;
  c.height = ROWS;
  ctx.font = `${ROWS}px "DotGothic16", monospace`;
  ctx.fillStyle = '#fff';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 0, ROWS / 2 + 1);
  const data = ctx.getImageData(0, 0, width, ROWS).data;
  const cols: boolean[][] = [];
  for (let x = 0; x < width; x++) {
    const col: boolean[] = [];
    for (let y = 0; y < ROWS; y++) col.push(data[(y * width + x) * 4 + 3] > 110);
    cols.push(col);
  }
  return cols;
}

export function LedSign({ messages, label }: { messages: string[]; label: string }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  const [index, setIndex] = useState(0);
  const [fontReady, setFontReady] = useState(false);
  const text = messages[index % Math.max(1, messages.length)] ?? '';

  useEffect(() => {
    document.fonts?.load(`${ROWS}px "DotGothic16"`).finally(() => setFontReady(true));
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    const box = boxRef.current;
    if (!canvas || !box || !fontReady) return;
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const cols = rasterize(text);
    let raf = 0;
    let start = 0;
    let disposed = false;

    const draw = (now: number) => {
      if (disposed) return;
      const dpr = Math.min(2, window.devicePixelRatio || 1);
      const cssW = box.clientWidth;
      const pitch = Math.max(4, Math.min(7, Math.floor(cssW / 120))); // dot spacing in css px
      const visible = Math.floor(cssW / pitch);
      const cssH = ROWS * pitch;
      if (canvas.width !== Math.round(cssW * dpr) || canvas.height !== Math.round(cssH * dpr)) {
        canvas.width = Math.round(cssW * dpr);
        canvas.height = Math.round(cssH * dpr);
        canvas.style.height = `${cssH}px`;
      }
      const ctx = canvas.getContext('2d')!;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.clearRect(0, 0, cssW, cssH);
      const fits = cols.length <= visible;
      if (!start) start = now;
      // Scroll 22 dots per second, like the sign; short texts stand still and centered.
      const offset = fits || reduce ? -Math.floor((visible - Math.min(cols.length, visible)) / 2) : Math.floor(((now - start) / 1000) * 22) - visible;
      const r = pitch * 0.36;
      for (let x = 0; x < visible; x++) {
        const col = cols[x + offset];
        for (let y = 0; y < ROWS; y++) {
          const on = !!col && col[y];
          ctx.beginPath();
          ctx.arc(x * pitch + pitch / 2, y * pitch + pitch / 2, r, 0, Math.PI * 2);
          ctx.fillStyle = on ? LIT : DARK;
          if (on) {
            ctx.shadowColor = 'rgba(255, 176, 0, 0.55)';
            ctx.shadowBlur = pitch * 1.2;
          } else {
            ctx.shadowBlur = 0;
          }
          ctx.fill();
        }
      }
      ctx.shadowBlur = 0;
      const done = fits || reduce ? now - start > 4200 : offset > cols.length + 8;
      if (done && messages.length > 1) {
        setIndex((i) => (i + 1) % messages.length);
        return;
      }
      raf = requestAnimationFrame(draw);
    };
    raf = requestAnimationFrame(draw);
    const onResize = () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(draw);
    };
    window.addEventListener('resize', onResize);
    return () => {
      disposed = true;
      cancelAnimationFrame(raf);
      window.removeEventListener('resize', onResize);
    };
  }, [text, fontReady, messages.length]);

  return (
    <div className="sign" ref={boxRef} role="img" aria-label={`${label}: ${messages.join('. ')}`}>
      <canvas ref={canvasRef} className="sign-canvas" />
    </div>
  );
}
