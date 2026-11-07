import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import { Account, api, Character, errorText, ServerInfo } from './api';
import { LedSign } from './LedSign';
import { go, href, Route, useRoute } from './router';

function A({ to, children, className }: { to: Route; children: ReactNode; className?: string }) {
  return (
    <a
      href={href(to)}
      className={className}
      onClick={(e) => {
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.button !== 0) return;
        e.preventDefault();
        go(to);
      }}
    >
      {children}
    </a>
  );
}

const num = (n: number) => n.toLocaleString('es-AR');

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
    const titles: Record<Route, string> = { inicio: 'PeroniaMU', ranking: 'Ranking · PeroniaMU', registro: 'Crear cuenta · PeroniaMU', entrar: 'Entrar · PeroniaMU', cuenta: 'Mi cuenta · PeroniaMU', descargar: 'Descargar · PeroniaMU' };
    document.title = titles[route];
  }, [route]);

  return (
    <>
      <a className="skip" href="#main">Ir al contenido</a>
      <header className="top">
        <div className="wrap top-in">
          <A to="inicio" className="logo">PERONIA MU</A>
          <nav aria-label="Principal">
            <A to="ranking" className={route === 'ranking' ? 'on' : ''}>Ranking</A>
            <A to="descargar" className={route === 'descargar' ? 'on' : ''}>Descargar</A>
            {account ? (
              <A to="cuenta" className={route === 'cuenta' ? 'on' : ''}>{account.loginName}</A>
            ) : (
              <A to="entrar" className={route === 'entrar' ? 'on' : ''}>Entrar</A>
            )}
          </nav>
          <span className={`status ${server?.playersOnline ? 'live' : ''}`}>
            {server ? `${server.playersOnline} jugando` : 'server sin respuesta'}
          </span>
        </div>
      </header>
      <main id="main" className="wrap">
        {route === 'inicio' && <Home server={server} account={account} />}
        {route === 'ranking' && <RankingPage />}
        {route === 'registro' && <RegisterPage server={server} />}
        {route === 'entrar' && <LoginPage onLogin={refreshAccount} />}
        {route === 'cuenta' && <AccountPage account={account} onChange={refreshAccount} onLogout={() => setAccount(null)} />}
        {route === 'descargar' && <DownloadPage server={server} />}
      </main>
      <footer className="wrap foot">
        <span>PeroniaMU · MU Online Season 6 · server privado entre amigos</span>
        <span>Hecho sobre OpenMU</span>
      </footer>
    </>
  );
}

function useRanking(count: number) {
  const [rows, setRows] = useState<Character[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    api.ranking(count).then(setRows, (e) => setError(errorText(e)));
  }, [count]);
  return { rows, error };
}

function Home({ server, account }: { server: ServerInfo | null; account: Account | null | undefined }) {
  const { rows, error } = useRanking(10);
  const messages = useMemo(() => {
    const m = ['LORENCIA > NORIA > DEVIAS > KARUTAN'];
    if (server) m.push(`${server.playersOnline} JUGANDO AHORA`);
    if (rows?.[0]) m.push(`PUNTERO: ${rows[0].name.toUpperCase()} · ${rows[0].resets} RESETS`);
    return m;
  }, [server, rows]);

  return (
    <>
      <section className="hero" aria-labelledby="hero-t">
        <LedSign messages={messages} label="Cartel del server" />
        <div className="hero-copy">
          <h1 id="hero-t">El MU de siempre, con tus amigos y sin pagar nada</h1>
          <p className="lead">PeroniaMU es un server privado de MU Online Season 6 con resets, dungeons, espectros y mercado propio. Si Gabi te pasó un código, te subís en un minuto.</p>
          <div className="actions">
            {account ? (
              <A to="cuenta" className="btn">Ver mi cuenta</A>
            ) : (
              <A to="registro" className="btn">Crear cuenta</A>
            )}
            <A to="descargar" className="btn ghost">Descargar el juego</A>
          </div>
        </div>
      </section>

      <section className="grid2">
        <div>
          <div className="head-row">
            <h2>Ranking</h2>
            <A to="ranking" className="more">Ver los 100</A>
          </div>
          <RankingTable rows={rows} error={error} compact />
        </div>
        <div>
          <h2>Estado</h2>
          <ServersPanel server={server} />
          <ol className="steps">
            <li><b>Pedile el código a Gabi.</b> Sin código no se puede crear cuenta.</li>
            <li><b>Creá tu cuenta acá.</b> Usuario de 3 a 10 caracteres.</li>
            <li><b>Bajá el cliente</b> y entrá con ese usuario.</li>
          </ol>
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

function RankingTable({ rows, error, compact, filter }: { rows: Character[] | null; error: string | null; compact?: boolean; filter?: (c: Character) => boolean }) {
  if (error) return <p className="error">{error}</p>;
  if (!rows) return <p className="muted">Cargando ranking…</p>;
  const shown = filter ? rows.filter(filter) : rows;
  if (shown.length === 0) return <p className="muted">{rows.length === 0 ? 'Todavía no hay personajes en el ranking.' : 'Ningún personaje coincide con el filtro.'}</p>;
  return (
    <div className="table-wrap">
      <table className="rank">
        <thead>
          <tr>
            <th scope="col" className="n">#</th>
            <th scope="col">Personaje</th>
            {!compact && <th scope="col">Clase</th>}
            <th scope="col" className="n">Resets</th>
            <th scope="col" className="n">Nivel</th>
            {!compact && <th scope="col" className="n">Master</th>}
          </tr>
        </thead>
        <tbody>
          {shown.map((c) => {
            const pos = rows.indexOf(c) + 1;
            return (
              <tr key={c.name} className={pos <= 3 ? 'podium' : ''}>
                <td className="n pos">{String(pos).padStart(2, '0')}</td>
                <td>
                  <span className="who">{c.name}</span>
                  {c.isOnline && <span className="online">en línea</span>}
                  {compact && <span className="cls">{c.className}</span>}
                </td>
                {!compact && <td className="cls-col">{c.className}</td>}
                <td className="n">{c.resets}</td>
                <td className="n">{c.level}</td>
                {!compact && <td className="n">{c.masterLevel || '–'}</td>}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function RankingPage() {
  const { rows, error } = useRanking(100);
  const [query, setQuery] = useState('');
  const [cls, setCls] = useState('');
  const classes = useMemo(() => Array.from(new Set((rows ?? []).map((r) => r.className))).sort(), [rows]);
  const q = query.trim().toLowerCase();
  return (
    <section className="page">
      <h1>Ranking</h1>
      <p className="lead">Ordenado por resets, después nivel master y nivel. Se actualiza cada minuto.</p>
      <div className="filters">
        <label>
          <span>Buscar personaje</span>
          <input id="rank-q" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Nombre" autoComplete="off" />
        </label>
        <label>
          <span>Clase</span>
          <select id="rank-cls" value={cls} onChange={(e) => setCls(e.target.value)}>
            <option value="">Todas</option>
            {classes.map((c) => (
              <option key={c} value={c}>{c}</option>
            ))}
          </select>
        </label>
      </div>
      <RankingTable rows={rows} error={error} filter={(c) => (!q || c.name.toLowerCase().includes(q)) && (!cls || c.className === cls)} />
    </section>
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
                <span className="who">{c.name}</span>
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
