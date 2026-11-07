import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { Account, api, errorText, ServerInfo } from './api';
import { LedSign } from './LedSign';
import { CastleCard, CharacterPage, EventsPage, GuildPage, GuildsPage, InfoPage, NewsList, NewsPage, NextEvents, OnlinePage, RankingPage, RankingTable } from './pages';
import { go, Page, useRoute } from './router';
import { A, num, useLoad } from './ui';

export function App() {
  const route = useRoute();
  const [server, setServer] = useState<ServerInfo | null>(null);
  const [account, setAccount] = useState<Account | null | undefined>(undefined);

  const refreshAccount = useCallback(() => {
    api.me().then(setAccount, () => setAccount(null));
  }, []);

  useEffect(() => {
    const load = () => api.server().then(setServer, () => setServer(null));
    load();
    refreshAccount();
    const t = setInterval(load, 30000);
    return () => clearInterval(t);
  }, [refreshAccount]);

  useEffect(() => {
    const titles: Record<Page, string> = { inicio: '', ranking: 'Ranking', online: 'Jugando ahora', guilds: 'Guilds', guild: route.param ?? 'Guild', personaje: route.param ?? 'Personaje', eventos: 'Eventos', info: 'Cómo es el server', noticias: 'Novedades', registro: 'Crear cuenta', entrar: 'Entrar', cuenta: 'Mi cuenta', descargar: 'Descargar' };
    const t = titles[route.page];
    document.title = t ? `${t} · PeroniaMU` : 'PeroniaMU';
  }, [route]);

  const page = route.page;
  const nav = (to: Page, label: string, also: Page[] = []) => (
    <A to={to} className={page === to || also.includes(page) ? 'on' : ''}>{label}</A>
  );

  return (
    <>
      <a className="skip" href="#main">Ir al contenido</a>
      <header className="top">
        <div className="wrap top-in">
          <A to="inicio" className="logo">PERONIA MU</A>
          <A to="online" className={`status ${server?.playersOnline ? 'live' : ''}`}>
            {server ? `${server.playersOnline} jugando` : 'server sin respuesta'}
          </A>
          <A to={account ? 'cuenta' : 'entrar'} className={`acct ${page === 'cuenta' || page === 'entrar' ? 'on' : ''}`}>{account ? account.loginName : 'Entrar'}</A>
          <nav aria-label="Principal">
            {nav('ranking', 'Ranking', ['personaje'])}
            {nav('online', 'Online')}
            {nav('guilds', 'Guilds', ['guild'])}
            {nav('eventos', 'Eventos')}
            {nav('info', 'Info')}
            {nav('noticias', 'Novedades')}
            {nav('descargar', 'Descargar')}
          </nav>
        </div>
      </header>
      <main id="main" className="wrap">
        {page === 'inicio' && <Home server={server} account={account} />}
        {page === 'ranking' && <RankingPage />}
        {page === 'online' && <OnlinePage />}
        {page === 'guilds' && <GuildsPage />}
        {page === 'guild' && <GuildPage key={route.param} name={route.param ?? ''} />}
        {page === 'personaje' && <CharacterPage key={route.param} name={route.param ?? ''} />}
        {page === 'eventos' && <EventsPage />}
        {page === 'info' && <InfoPage />}
        {page === 'noticias' && <NewsPage />}
        {page === 'registro' && <RegisterPage server={server} />}
        {page === 'entrar' && <LoginPage onLogin={refreshAccount} />}
        {page === 'cuenta' && <AccountPage account={account} onChange={refreshAccount} onLogout={() => setAccount(null)} />}
        {page === 'descargar' && <DownloadPage server={server} />}
      </main>
      <footer className="wrap foot">
        <span>PeroniaMU · MU Online Season 6 · server privado entre amigos</span>
        <span>Hecho sobre OpenMU</span>
      </footer>
    </>
  );
}

function Home({ server, account }: { server: ServerInfo | null; account: Account | null | undefined }) {
  const ranking = useLoad(() => api.ranking(10), 'ranking10', 60000);
  const events = useLoad(() => api.events(), 'events', 300000);
  const news = useLoad(() => api.news(), 'news');
  const rows = ranking.data;
  const next = events.data?.find((e) => new Date(e.startsAtUtc).getTime() > Date.now());
  const messages = useMemo(() => {
    const m = ['BIENVENIDOS A PERONIA MU'];
    if (server) m.push(`${server.playersOnline} JUGANDO AHORA`);
    if (rows?.[0]) m.push(`PUNTERO: ${rows[0].name.toUpperCase()} · ${rows[0].resets} RESETS`);
    if (next) m.push(`PROXIMO: ${next.name.toUpperCase()} ${new Date(next.startsAtUtc).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })}`);
    return m;
  }, [server, rows, next]);

  return (
    <>
      <section className="hero" aria-labelledby="hero-t">
        <LedSign messages={messages} label="Cartel del server" />
        <div className="hero-copy">
          <h1 id="hero-t">El MU de siempre, con tus amigos y sin pagar nada</h1>
          <p className="lead">PeroniaMU es un server privado de MU Online Season 6 con resets, dungeons y espectros. Si Gabi te pasó un código, te subís en un minuto.</p>
          <div className="actions">
            {account ? (
              <A to="cuenta" className="btn">Ver mi cuenta</A>
            ) : (
              <A to="registro" className="btn">Crear cuenta</A>
            )}
            <A to="info" className="btn ghost">Cómo es el server</A>
          </div>
        </div>
      </section>

      <section className="grid2">
        <div>
          <div className="head-row">
            <h2>Ranking</h2>
            <A to="ranking" className="more">Ver los 100</A>
          </div>
          <RankingTable rows={rows} error={ranking.error} compact />
          {news.data && news.data.length > 0 && (
            <>
              <div className="head-row">
                <h2>Novedades</h2>
                <A to="noticias" className="more">Ver todas</A>
              </div>
              <NewsList items={news.data} limit={2} />
            </>
          )}
        </div>
        <div>
          <div className="head-row">
            <h2>Próximos eventos</h2>
            <A to="eventos" className="more">Ver todos</A>
          </div>
          {events.data ? <NextEvents events={events.data} limit={4} /> : <p className="muted">{events.error ?? 'Cargando eventos…'}</p>}
          <CastleCard />
          <div className="head-row">
            <h2>Servers</h2>
            <A to="online" className="more">Quién está</A>
          </div>
          <ServersPanel server={server} />
          {!account && (
            <ol className="steps">
              <li><b>Pedile el código a Gabi.</b> Sin código no se puede crear cuenta.</li>
              <li><b>Creá tu cuenta acá.</b> Usuario de 3 a 10 caracteres.</li>
              <li><b>Bajá el cliente</b> y entrá con ese usuario.</li>
            </ol>
          )}
        </div>
      </section>
    </>
  );
}

function ServersPanel({ server }: { server: ServerInfo | null }) {
  if (!server) return <p className="muted">No se pudo leer el estado del server. Si dura, avisale a Gabi.</p>;
  return (
    <ul className="servers">
      {server.gameServers.map((s) => (
        <li key={s.id}>
          <span className={`dot ${s.isOnline ? 'ok' : 'off'}`} aria-hidden="true" />
          <span className="srv-name">{s.name}</span>
          <span className="mono">{s.isOnline ? `${s.players}/${s.maximumPlayers}` : 'apagado'}</span>
        </li>
      ))}
    </ul>
  );
}

function Field({ id, label, hint, ...rest }: { id: string; label: string; hint?: string } & React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="field" htmlFor={id}>
      <span>{label}</span>
      <input id={id} name={id} {...rest} />
      {hint && <small>{hint}</small>}
    </label>
  );
}

function RegisterPage({ server }: { server: ServerInfo | null }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const password = String(f.get('reg-pass') ?? '');
    if (password !== String(f.get('reg-pass2') ?? '')) {
      setError('Las dos contraseñas no coinciden.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const loginName = String(f.get('reg-user') ?? '').trim();
      await api.register({ inviteCode: String(f.get('reg-code') ?? ''), loginName, password, eMail: String(f.get('reg-mail') ?? '') || undefined });
      setDone(loginName);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  if (done) {
    return (
      <section className="page narrow">
        <h1>Listo, {done}</h1>
        <p className="lead">Tu cuenta ya existe. Entrá al juego con ese usuario y la contraseña que elegiste.</p>
        <div className="actions">
          <A to="descargar" className="btn">Descargar el juego</A>
          <A to="entrar" className="btn ghost">Entrar a la web</A>
        </div>
      </section>
    );
  }

  return (
    <section className="page narrow">
      <h1>Crear cuenta</h1>
      <p className="lead">Necesitás el código de invitación que te pasó Gabi. El usuario y la contraseña son los mismos que vas a usar en el juego.</p>
      {server && !server.registrationOpen && <p className="error">El registro está cerrado por ahora.</p>}
      <form className="form" onSubmit={submit} noValidate>
        <Field id="reg-code" label="Código de invitación" required autoComplete="off" />
        <Field id="reg-user" label="Usuario" hint="De 3 a 10 caracteres, sin espacios ni acentos." required minLength={3} maxLength={10} autoComplete="username" />
        <Field id="reg-pass" label="Contraseña" type="password" hint="De 3 a 20 caracteres." required minLength={3} maxLength={20} autoComplete="new-password" />
        <Field id="reg-pass2" label="Repetí la contraseña" type="password" required maxLength={20} autoComplete="new-password" />
        <Field id="reg-mail" label="E-mail (opcional)" type="email" autoComplete="email" />
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Creando…' : 'Crear cuenta'}</button>
      </form>
      <p className="muted small">¿Ya tenés cuenta? <A to="entrar">Entrá acá</A>.</p>
    </section>
  );
}

function LoginPage({ onLogin }: { onLogin: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    setBusy(true);
    setError(null);
    try {
      await api.login({ loginName: String(f.get('login-user') ?? '').trim(), password: String(f.get('login-pass') ?? '') });
      onLogin();
      go('cuenta');
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <section className="page narrow">
      <h1>Entrar</h1>
      <p className="lead">Con el mismo usuario y contraseña del juego.</p>
      <form className="form" onSubmit={submit} noValidate>
        <Field id="login-user" label="Usuario" required autoComplete="username" />
        <Field id="login-pass" label="Contraseña" type="password" required autoComplete="current-password" />
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Entrando…' : 'Entrar'}</button>
      </form>
      <p className="muted small">¿No tenés cuenta? <A to="registro">Creala con tu código</A>.</p>
    </section>
  );
}

function AccountPage({ account, onChange, onLogout }: { account: Account | null | undefined; onChange: () => void; onLogout: () => void }) {
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  if (account === undefined) return <p className="muted page">Cargando tu cuenta…</p>;
  if (account === null) {
    return (
      <section className="page narrow">
        <h1>Mi cuenta</h1>
        <p className="lead">Entrá para ver tus personajes.</p>
        <A to="entrar" className="btn">Entrar</A>
      </section>
    );
  }
  const changePassword = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget;
    const f = new FormData(form);
    const next = String(f.get('pw-new') ?? '');
    if (next !== String(f.get('pw-new2') ?? '')) {
      setMsg({ ok: false, text: 'Las dos contraseñas nuevas no coinciden.' });
      return;
    }
    setBusy(true);
    try {
      await api.changePassword({ currentPassword: String(f.get('pw-cur') ?? ''), newPassword: next });
      setMsg({ ok: true, text: 'Contraseña cambiada. Usala la próxima vez que entres al juego.' });
      form.reset();
    } catch (err) {
      setMsg({ ok: false, text: errorText(err) });
    } finally {
      setBusy(false);
    }
  };
  const logout = async () => {
    await api.logout().catch(() => undefined);
    onLogout();
    go('inicio');
  };
  return (
    <section className="page">
      <div className="head-row">
        <h1>{account.loginName}</h1>
        <button className="btn ghost small-btn" type="button" onClick={logout}>Salir</button>
      </div>
      <dl className="facts">
        <div><dt>Estado</dt><dd>{account.isOnline ? <span className="online">en el juego</span> : 'fuera del juego'}</dd></div>
        <div><dt>WCoin</dt><dd className="mono">{num(account.wCoin)} WC</dd></div>
        <div><dt>Cuenta desde</dt><dd>{new Date(account.registeredAt).toLocaleDateString('es-AR')}</dd></div>
      </dl>

      <h2>Personajes</h2>
      {account.characters.length === 0 ? (
        <p className="muted">Todavía no creaste personajes. Entrá al juego y creá el primero.</p>
      ) : (
        <ul className="chars">
          {account.characters.map((c) => (
            <li key={c.name}>
              <div className="char-top">
                <A to="personaje" param={c.name} className="who">{c.name}</A>
                {c.isOnline && <span className="online">en línea</span>}
              </div>
              <span className="cls">{c.className}</span>
              <div className="char-stats mono">
                <span><b>{c.resets}</b> resets</span>
                <span><b>{c.level}</b> nivel</span>
                {c.masterLevel > 0 && <span><b>{c.masterLevel}</b> master</span>}
              </div>
              {c.mapName && <span className="map">{c.mapName}</span>}
            </li>
          ))}
        </ul>
      )}

      <h2>Cambiar contraseña</h2>
      <form className="form narrow-form" onSubmit={changePassword} noValidate>
        <Field id="pw-cur" label="Contraseña actual" type="password" required autoComplete="current-password" />
        <Field id="pw-new" label="Contraseña nueva" type="password" hint="De 3 a 20 caracteres." required maxLength={20} autoComplete="new-password" />
        <Field id="pw-new2" label="Repetí la nueva" type="password" required maxLength={20} autoComplete="new-password" />
        {msg && <p className={msg.ok ? 'ok' : 'error'} role="status">{msg.text}</p>}
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Guardando…' : 'Cambiar contraseña'}</button>
      </form>
      <p className="muted small">Si estás conectado al juego, salí antes de cambiarla.</p>
      <button type="button" className="link" onClick={onChange}>Actualizar datos</button>
    </section>
  );
}

function DownloadPage({ server }: { server: ServerInfo | null }) {
  return (
    <section className="page narrow">
      <h1>Descargar el juego</h1>
      <p className="lead">El cliente es MU Season 6 con los arreglos de PeroniaMU: cámara 3D, 60 FPS, mapa con TAB, barras de vida y más.</p>
      {server?.clientDownloadUrl ? (
        <a className="btn" href={server.clientDownloadUrl} target="_blank" rel="noopener noreferrer">Descargar cliente</a>
      ) : (
        <p className="muted">Por ahora el cliente lo pasa Gabi directamente. Pedíselo por el grupo.</p>
      )}
      <ol className="steps">
        <li><b>Descomprimí la carpeta</b> donde quieras, por ejemplo en el escritorio.</li>
        <li><b>Si Windows Defender lo frena,</b> elegí «Más información» y «Ejecutar de todas formas». Es un falso positivo conocido del main.exe modificado.</li>
        <li><b>Abrí el launcher</b> y entrá con el usuario que creaste acá.</li>
      </ol>
    </section>
  );
}
