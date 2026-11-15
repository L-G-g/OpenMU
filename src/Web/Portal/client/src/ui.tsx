import { ReactNode, useEffect, useState } from 'react';
import { ApiError, errorText } from './api';
import { go, href, Page } from './router';

export function A({ to, param, children, className }: { to: Page; param?: string; children: ReactNode; className?: string }) {
  return (
    <a
      href={href(to, param)}
      className={className}
      onClick={(e) => {
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.button !== 0) return;
        e.preventDefault();
        go(to, param);
      }}
    >
      {children}
    </a>
  );
}

export const num = (n: number) => n.toLocaleString('es-AR');

/** Loads data once per key and keeps the last result; `missing` is true on a 404. */
export function useLoad<T>(load: () => Promise<T>, key: string, refreshMs?: number) {
  const [state, setState] = useState<{ key: string; data: T | null; error: string | null; missing: boolean }>({ key, data: null, error: null, missing: false });
  useEffect(() => {
    let alive = true;
    const run = () =>
      load().then(
        (data) => alive && setState({ key, data, error: null, missing: false }),
        (e) => alive && setState((s) => ({ key, data: s.key === key ? s.data : null, error: e instanceof ApiError && e.status === 404 ? null : errorText(e), missing: e instanceof ApiError && e.status === 404 })),
      );
    run();
    const t = refreshMs ? setInterval(run, refreshMs) : undefined;
    return () => {
      alive = false;
      if (t) clearInterval(t);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  const fresh = state.key === key;
  return { data: fresh ? state.data : null, error: fresh ? state.error : null, missing: fresh && state.missing };
}

export function Who({ name, online }: { name: string; online?: boolean }) {
  return (
    <>
      <A to="personaje" param={name} className="who">{name}</A>
      {online && <span className="online">en línea</span>}
    </>
  );
}

/** The 16 colors of MU guild marks. Index 0 is transparent. */
const markColors = ['transparent', '#000000', '#8c8a8d', '#ffffff', '#fe0000', '#ff8a00', '#ffff00', '#8cff01', '#00ff00', '#01ff8d', '#00ffff', '#008aff', '#0000fe', '#8c00ff', '#ff00fe', '#ff008c'];

/** Draws a guild mark: 64 pixels of 4 bits, two per byte, row by row. */
export function GuildMark({ logo, size = 32, label }: { logo: string | null; size?: number; label: string }) {
  const cells: ReactNode[] = [];
  if (logo && /^[0-9a-f]{64}$/i.test(logo)) {
    for (let i = 0; i < 64; i++) {
      const c = parseInt(logo[i], 16);
      if (c > 0) cells.push(<rect key={i} x={i % 8} y={Math.floor(i / 8)} width={1.02} height={1.02} fill={markColors[c]} />);
    }
  }
  return (
    <svg className="mark" width={size} height={size} viewBox="0 0 8 8" role="img" aria-label={label} shapeRendering="crispEdges">
      <rect width="8" height="8" fill="#0b0a09" />
      {cells}
    </svg>
  );
}

export function useNow(intervalMs = 1000) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(t);
  }, [intervalMs]);
  return now;
}

export function untilText(ms: number) {
  if (ms <= 0) return 'arrancando';
  const s = Math.floor(ms / 1000);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  if (h >= 24) return `en ${Math.floor(h / 24)} d ${h % 24} h`;
  if (h > 0) return `en ${h} h ${String(m).padStart(2, '0')} min`;
  if (m > 0) return `en ${m} min ${String(sec).padStart(2, '0')} s`;
  return `en ${sec} s`;
}

export const clock = (iso: string) => new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });

export function Loading({ what }: { what: string }) {
  return <p className="muted">Cargando {what}…</p>;
}
