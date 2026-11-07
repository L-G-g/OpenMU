import { useEffect, useState } from 'react';

export type Route = 'inicio' | 'ranking' | 'registro' | 'entrar' | 'cuenta' | 'descargar';
const paths: Record<Route, string> = { inicio: '/', ranking: '/ranking', registro: '/registro', entrar: '/entrar', cuenta: '/cuenta', descargar: '/descargar' };

export function routeFromPath(path: string): Route {
  const found = (Object.keys(paths) as Route[]).find((r) => paths[r] === path.replace(/\/+$/, '') || (r === 'inicio' && path === '/'));
  return found ?? 'inicio';
}

export function href(route: Route) {
  return paths[route];
}

export function go(route: Route) {
  if (location.pathname !== paths[route]) history.pushState({}, '', paths[route]);
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
