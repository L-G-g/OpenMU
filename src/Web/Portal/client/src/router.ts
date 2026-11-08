import { useEffect, useState } from 'react';

export type Page = 'inicio' | 'ranking' | 'online' | 'guilds' | 'guild' | 'personaje' | 'eventos' | 'info' | 'noticias' | 'registro' | 'entrar' | 'cuenta' | 'descargar' | 'mercado' | 'item' | 'misordenes';
export type Route = { page: Page; param?: string };

const fixed: Partial<Record<Page, string>> = {
  inicio: '/',
  ranking: '/ranking',
  online: '/online',
  guilds: '/guilds',
  eventos: '/eventos',
  info: '/info',
  noticias: '/noticias',
  registro: '/registro',
  entrar: '/entrar',
  cuenta: '/cuenta',
  descargar: '/descargar',
  mercado: '/mercado',
  misordenes: '/mercado/mis-ordenes',
};

export function routeFromPath(path: string): Route {
  const clean = path.replace(/\/+$/, '') || '/';
  const m = clean.match(/^\/(personaje|guild)\/([^/]+)$/);
  if (m) return { page: m[1] as Page, param: decodeURIComponent(m[2]) };
  const item = clean.match(/^\/mercado\/(\d+-\d+(?:-[a-z0-9]+)?)$/);
  if (item) return { page: 'item', param: item[1] };
  const page = (Object.keys(fixed) as Page[]).find((p) => fixed[p] === clean);
  return { page: page ?? 'inicio' };
}

export function href(page: Page, param?: string) {
  if (page === 'personaje' || page === 'guild') return `/${page}/${encodeURIComponent(param ?? '')}`;
  if (page === 'item') return `/mercado/${param ?? ''}`;
  return fixed[page] ?? '/';
}

export function go(page: Page, param?: string) {
  const to = href(page, param);
  if (location.pathname !== to) history.pushState({}, '', to);
  window.dispatchEvent(new PopStateEvent('popstate'));
  window.scrollTo({ top: 0 });
}

export function useRoute(): Route {
  const [route, setRoute] = useState<Route>(() => routeFromPath(location.pathname));
  useEffect(() => {
    const on = () => setRoute(routeFromPath(location.pathname));
    window.addEventListener('popstate', on);
    return () => window.removeEventListener('popstate', on);
  }, []);
  return route;
}
