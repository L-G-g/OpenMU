import { useMemo, useState } from 'react';
import { api, Character, GameEvent, Guild, NewsItem } from './api';
import { A, clock, GuildMark, Loading, num, untilText, useLoad, useNow, Who } from './ui';

/* ---------- Ranking ---------- */

type RankTab = 'resets' | 'nivel' | 'master' | 'guilds';
const rankTabs: { id: RankTab; label: string }[] = [
  { id: 'resets', label: 'Resets' },
  { id: 'nivel', label: 'Nivel' },
  { id: 'master', label: 'Master' },
  { id: 'guilds', label: 'Guilds' },
];

const sorters: Record<Exclude<RankTab, 'guilds'>, (a: Character, b: Character) => number> = {
  resets: () => 0,
  nivel: (a, b) => b.level - a.level || b.resets - a.resets || b.masterLevel - a.masterLevel,
  master: (a, b) => b.masterLevel - a.masterLevel || b.resets - a.resets || b.level - a.level,
};

export function RankingTable({ rows, error, compact, highlight }: { rows: Character[] | null; error: string | null; compact?: boolean; highlight?: RankTab }) {
  if (error) return <p className="error">{error}</p>;
  if (!rows) return <Loading what="el ranking" />;
  if (rows.length === 0) return <p className="muted">Ningún personaje coincide.</p>;
  const hl = (t: RankTab) => (highlight === t ? 'n hl' : 'n');
  return (
    <div className="table-wrap">
      <table className="rank">
        <thead>
          <tr>
            <th scope="col" className="n pos">#</th>
            <th scope="col">Personaje</th>
            {!compact && <th scope="col">Clase</th>}
            <th scope="col" className={hl('resets')}>Resets</th>
            <th scope="col" className={hl('nivel')}>Nivel</th>
            {!compact && <th scope="col" className={hl('master')}>Master</th>}
          </tr>
        </thead>
        <tbody>
          {rows.map((c, i) => (
            <tr key={c.name} className={i < 3 ? 'podium' : ''}>
              <td className="n pos">{String(i + 1).padStart(2, '0')}</td>
              <td>
                <Who name={c.name} online={c.isOnline} />
                {compact && <span className="cls">{c.className}</span>}
              </td>
              {!compact && <td className="cls-col">{c.className}</td>}
              <td className={hl('resets')}>{c.resets}</td>
              <td className={hl('nivel')}>{c.level}</td>
              {!compact && <td className={hl('master')}>{c.masterLevel || '–'}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function RankingPage() {
  const ranking = useLoad(() => api.ranking(100), 'ranking');
  const guilds = useLoad(() => api.guilds(), 'guilds');
  const [tab, setTab] = useState<RankTab>('resets');
  const [query, setQuery] = useState('');
  const [cls, setCls] = useState('');
  const classes = useMemo(() => Array.from(new Set((ranking.data ?? []).map((r) => r.className))).sort(), [ranking.data]);
  const q = query.trim().toLowerCase();
  const rows = useMemo(() => {
    if (!ranking.data || tab === 'guilds') return null;
    return ranking.data
      .filter((c) => (!q || c.name.toLowerCase().includes(q)) && (!cls || c.className === cls))
      .slice()
      .sort(sorters[tab]);
  }, [ranking.data, tab, q, cls]);

  return (
    <section className="page">
      <h1>Ranking</h1>
      <p className="lead">Los 100 mejores personajes, sin GMs. Se actualiza cada minuto.</p>
      <div className="tabs" role="tablist" aria-label="Tipo de ranking">
        {rankTabs.map((t) => (
          <button key={t.id} type="button" role="tab" aria-selected={tab === t.id} className={tab === t.id ? 'on' : ''} onClick={() => setTab(t.id)}>
            {t.label}
          </button>
        ))}
      </div>
      {tab === 'guilds' ? (
        <GuildTable guilds={guilds.data} error={guilds.error} />
      ) : (
        <>
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
          <RankingTable rows={rows} error={ranking.error} highlight={tab} />
        </>
      )}
    </section>
  );
}

/* ---------- Character ---------- */

export function CharacterPage({ name }: { name: string }) {
  const { data: c, error, missing } = useLoad(() => api.character(name), `char:${name}`);
  if (missing) return <NotFound what={`No hay ningún personaje llamado «${name}».`} />;
  if (error) return <p className="error page">{error}</p>;
  if (!c) return <div className="page"><Loading what="el personaje" /></div>;
  return (
    <section className="page">
      <p className="kicker">{c.className}</p>
      <div className="head-row">
        <h1>{c.name}</h1>
        {c.isOnline ? <span className="badge live">en línea</span> : <span className="badge">desconectado</span>}
      </div>
      <dl className="bigstats">
        <div><dt>Resets</dt><dd>{c.resets}</dd></div>
        <div><dt>Nivel</dt><dd>{c.level}</dd></div>
        <div><dt>Master</dt><dd>{c.masterLevel || '–'}</dd></div>
      </dl>
      <dl className="facts">
        <div>
          <dt>Guild</dt>
          <dd>{c.guildName ? <><A to="guild" param={c.guildName}>{c.guildName}</A> <span className="muted small">{c.guildPosition}</span></> : 'sin guild'}</dd>
        </div>
        {c.isOnline && (
          <div><dt>Dónde está</dt><dd>{c.mapName ?? '–'} <span className="muted small">{c.serverName}</span></dd></div>
        )}
      </dl>
      <p className="back"><A to="ranking">Volver al ranking</A></p>
    </section>
  );
}

/* ---------- Online ---------- */

export function OnlinePage() {
  const { data, error } = useLoad(() => api.online(), 'online', 20000);
  const byMap = useMemo(() => {
    const m = new Map<string, number>();
    (data ?? []).forEach((p) => m.set(p.mapName ?? '–', (m.get(p.mapName ?? '–') ?? 0) + 1));
    return Array.from(m.entries()).sort((a, b) => b[1] - a[1]);
  }, [data]);
  return (
    <section className="page">
      <h1>Jugando ahora</h1>
      <p className="lead">Quién está conectado y en qué mapa. Se actualiza solo cada 20 segundos.</p>
      {error && <p className="error">{error}</p>}
      {!data && !error && <Loading what="los jugadores" />}
      {data && data.length === 0 && <p className="muted">No hay nadie conectado en este momento.</p>}
      {data && data.length > 0 && (
        <>
          <ul className="chips" aria-label="Jugadores por mapa">
            {byMap.map(([map, n]) => (
              <li key={map}><span>{map}</span> <b className="mono">{n}</b></li>
            ))}
          </ul>
          <div className="table-wrap">
            <table className="rank">
              <thead>
                <tr>
                  <th scope="col">Personaje</th>
                  <th scope="col" className="hide-s">Clase</th>
                  <th scope="col" className="n">Resets</th>
                  <th scope="col" className="n">Nivel</th>
                  <th scope="col">Mapa</th>
                </tr>
              </thead>
              <tbody>
                {data.map((p) => (
                  <tr key={p.name}>
                    <td><Who name={p.name} /><span className="cls show-s">{p.className}</span></td>
                    <td className="cls-col hide-s">{p.className}</td>
                    <td className="n">{p.resets}</td>
                    <td className="n">{p.level}</td>
                    <td className="map-col">{p.mapName ?? '–'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}

/* ---------- Guilds ---------- */

export function GuildTable({ guilds, error }: { guilds: Guild[] | null; error: string | null }) {
  if (error) return <p className="error">{error}</p>;
  if (!guilds) return <Loading what="las guilds" />;
  if (guilds.length === 0) return <p className="muted">Todavía no hay guilds. Se crean en el juego hablando con el Guild Master de Devias.</p>;
  return (
    <div className="table-wrap">
      <table className="rank">
        <thead>
          <tr>
            <th scope="col" className="n pos">#</th>
            <th scope="col">Guild</th>
            <th scope="col" className="hide-s">Master</th>
            <th scope="col" className="n">Miembros</th>
            <th scope="col" className="n hide-s">Puntos</th>
          </tr>
        </thead>
        <tbody>
          {guilds.map((g, i) => (
            <tr key={g.name} className={i < 3 ? 'podium' : ''}>
              <td className="n pos">{String(i + 1).padStart(2, '0')}</td>
              <td>
                <span className="guild-cell">
                  <GuildMark logo={g.logo} size={24} label={`Escudo de ${g.name}`} />
                  <A to="guild" param={g.name} className="who">{g.name}</A>
                </span>
                {g.masterName && <span className="cls show-s">Master: {g.masterName}</span>}
              </td>
              <td className="cls-col hide-s">{g.masterName ?? '–'}</td>
              <td className="n">{g.memberCount}</td>
              <td className="n hide-s">{g.score}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function GuildsPage() {
  const { data, error } = useLoad(() => api.guilds(), 'guilds');
  return (
    <section className="page">
      <h1>Guilds</h1>
      <p className="lead">Ordenadas por cantidad de miembros. Tocá una para ver quiénes la forman.</p>
      <CastleCard full />
      <GuildTable guilds={data} error={error} />
    </section>
  );
}

export function GuildPage({ name }: { name: string }) {
  const { data, error, missing } = useLoad(() => api.guild(name), `guild:${name}`);
  if (missing) return <NotFound what={`No hay ninguna guild llamada «${name}».`} />;
  if (error) return <p className="error page">{error}</p>;
  if (!data) return <div className="page"><Loading what="la guild" /></div>;
  const g = data.guild;
  const online = data.members.filter((m) => m.isOnline).length;
  return (
    <section className="page">
      <div className="guild-head">
        <GuildMark logo={g.logo} size={96} label={`Escudo de ${g.name}`} />
        <div>
          <p className="kicker">Guild</p>
          <h1>{g.name}</h1>
        </div>
      </div>
      <dl className="facts">
        <div><dt>Master</dt><dd>{g.masterName ? <A to="personaje" param={g.masterName}>{g.masterName}</A> : '–'}</dd></div>
        <div><dt>Miembros</dt><dd>{g.memberCount} <span className="muted small">{online} en línea</span></dd></div>
        <div><dt>Puntos</dt><dd className="mono">{g.score}</dd></div>
        {g.allianceName && g.allianceName !== g.name && <div><dt>Alianza con</dt><dd><A to="guild" param={g.allianceName}>{g.allianceName}</A></dd></div>}
      </dl>
      <h2>Miembros</h2>
      <div className="table-wrap">
        <table className="rank">
          <thead>
            <tr>
              <th scope="col">Personaje</th>
              <th scope="col" className="hide-s">Cargo</th>
              <th scope="col" className="hide-s">Clase</th>
              <th scope="col" className="n">Resets</th>
              <th scope="col" className="n">Nivel</th>
            </tr>
          </thead>
          <tbody>
            {data.members.map((m) => (
              <tr key={m.name}>
                <td><Who name={m.name} online={m.isOnline} /><span className="cls show-s">{m.position} · {m.className}</span></td>
                <td className="cls-col hide-s">{m.position}</td>
                <td className="cls-col hide-s">{m.className}</td>
                <td className="n">{m.resets}</td>
                <td className="n">{m.level}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="back"><A to="guilds">Todas las guilds</A></p>
    </section>
  );
}

/* ---------- Castle Siege ---------- */

const when = (iso: string) => new Date(iso).toLocaleString('es-AR', { weekday: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });

export function CastleCard({ full }: { full?: boolean }) {
  const { data: cs } = useLoad(() => api.castleSiege(), 'siege', 60000);
  const now = useNow(30000);
  if (!cs || !cs.isActive) return null;
  const battle = cs.nextBattleUtc ? new Date(cs.nextBattleUtc).getTime() : null;
  const fighting = cs.state === 'Start';
  return (
    <div className={`castle ${fighting ? 'fighting' : ''}`}>
      <div className="castle-top">
        {cs.ownerGuildName ? <GuildMark logo={cs.ownerGuildLogo} size={44} label={`Escudo de ${cs.ownerGuildName}`} /> : <span className="castle-empty" aria-hidden="true">?</span>}
        <div>
          <p className="kicker">Castle Siege · dueño del castillo</p>
          <p className="castle-owner">{cs.ownerGuildName ? <A to="guild" param={cs.ownerGuildName}>{cs.ownerGuildName}</A> : 'Nadie todavía'}</p>
        </div>
      </div>
      <dl className="castle-facts">
        <div><dt>Ahora</dt><dd>{cs.stateName}{cs.stateEndsAtUtc && !fighting && <span className="muted small"> hasta el {when(cs.stateEndsAtUtc)}</span>}</dd></div>
        {battle && !fighting && <div><dt>Próxima batalla</dt><dd>{when(cs.nextBattleUtc!)} <span className="muted small">({untilText(battle - now).replace(/ \d+ s$/, '')})</span></dd></div>}
      </dl>
      {full && cs.guilds.length > 0 && (
        <>
          <p className="kicker castle-reg">Guilds anotadas</p>
          <ul className="chips">
            {cs.guilds.map((g) => (
              <li key={g.name}><A to="guild" param={g.name}>{g.name}</A> <b className="mono">{g.marks}</b></li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

/* ---------- Events ---------- */

function dayLabel(d: Date) {
  const today = new Date();
  const tomorrow = new Date();
  tomorrow.setDate(today.getDate() + 1);
  if (d.toDateString() === today.toDateString()) return 'Hoy';
  if (d.toDateString() === tomorrow.toDateString()) return 'Mañana';
  return d.toLocaleDateString('es-AR', { weekday: 'long', day: 'numeric' });
}

export function NextEvents({ events, limit }: { events: GameEvent[]; limit?: number }) {
  const now = useNow();
  const upcoming = events.filter((e) => new Date(e.startsAtUtc).getTime() > now - 60000).slice(0, limit);
  if (upcoming.length === 0) return <p className="muted">No hay eventos programados en las próximas 24 horas.</p>;
  return (
    <ul className="events">
      {upcoming.map((e, i) => {
        const at = new Date(e.startsAtUtc).getTime();
        return (
          <li key={`${e.key}-${e.startsAtUtc}`} className={i === 0 ? 'next' : ''}>
            <span className="ev-time mono">{clock(e.startsAtUtc)}</span>
            <span className="ev-name">{e.name}</span>
            <span className="ev-left mono">{untilText(at - now)}</span>
          </li>
        );
      })}
    </ul>
  );
}

export function EventsPage() {
  const { data, error } = useLoad(() => api.events(), 'events', 300000);
  const [kind, setKind] = useState('');
  const kinds = useMemo(() => Array.from(new Map((data ?? []).map((e) => [e.key, e.name])).entries()), [data]);
  const shown = (data ?? []).filter((e) => !kind || e.key === kind);
  const days = useMemo(() => {
    const m = new Map<string, GameEvent[]>();
    shown.forEach((e) => {
      const label = dayLabel(new Date(e.startsAtUtc));
      m.set(label, [...(m.get(label) ?? []), e]);
    });
    return Array.from(m.entries());
  }, [shown]);
  return (
    <section className="page">
      <h1>Eventos</h1>
      <p className="lead">Lo que arranca en las próximas 24 horas, en la hora de tu celu o compu.</p>
      <CastleCard />
      {error && <p className="error">{error}</p>}
      {!data && !error && <Loading what="los eventos" />}
      {data && (
        <>
          {kinds.length > 1 && (
            <div className="tabs" role="group" aria-label="Filtrar por evento">
              <button type="button" className={kind === '' ? 'on' : ''} aria-pressed={kind === ''} onClick={() => setKind('')}>Todos</button>
              {kinds.map(([k, n]) => (
                <button key={k} type="button" className={kind === k ? 'on' : ''} aria-pressed={kind === k} onClick={() => setKind(k)}>{n}</button>
              ))}
            </div>
          )}
          {days.length === 0 && <p className="muted">No hay eventos programados en las próximas 24 horas.</p>}
          {days.map(([label, list]) => (
            <div key={label}>
              <h2>{label}</h2>
              <NextEvents events={list} />
            </div>
          ))}
        </>
      )}
    </section>
  );
}

/* ---------- News ---------- */

export function NewsList({ items, limit }: { items: NewsItem[]; limit?: number }) {
  return (
    <div className="news">
      {items.slice(0, limit).map((n) => (
        <article key={`${n.date}-${n.title}`}>
          <p className="kicker">{n.date}</p>
          <h3>{n.title}</h3>
          {n.body.map((p, i) => (
            <p key={i}>{p}</p>
          ))}
        </article>
      ))}
    </div>
  );
}

export function NewsPage() {
  const { data, error } = useLoad(() => api.news(), 'news');
  return (
    <section className="page narrow-wide">
      <h1>Novedades</h1>
      <p className="lead">Lo que cambió en el server.</p>
      {error && <p className="error">{error}</p>}
      {!data && !error && <Loading what="las novedades" />}
      {data && data.length === 0 && <p className="muted">Todavía no hay novedades publicadas.</p>}
      {data && <NewsList items={data} />}
    </section>
  );
}

/* ---------- Info ---------- */

const ladder: [string, string, string][] = [
  ['Lorencia, Noria, Elbeland', '1–19', 'Equipo de la zona'],
  ['Devias', '20–29', 'Equipo de la zona'],
  ['Dungeon', '30–39', 'Equipo de la zona'],
  ['Lost Tower', '40–49', 'Equipo de la zona'],
  ['Atlans', '50–59', 'Equipo de la zona'],
  ['Tarkan', '60–69', 'Equipo de la zona'],
  ['Aida', '70–79', 'Equipo de la zona'],
  ['Icarus', '80–89', 'Equipo de la zona'],
  ['Kanturu Ruins', '90–99', 'Equipo de la zona'],
  ['Kanturu Relics', '100–109', 'Equipo de la zona'],
  ['Karutan 1 y 2', '110–119', 'Equipo de la zona (el último)'],
  ['Swamp of Calmness, Crywolf', '130–139', 'Solo joyas (Crywolf también materiales de Fenrir)'],
  ['Raklion', '140–148', 'Items socket; Selupan siempre tira uno de 3 sockets'],
];

export function InfoPage() {
  const { data: s } = useLoad(() => api.stats(), 'stats');
  return (
    <section className="page">
      <h1>Cómo es el server</h1>
      <p className="lead">MU Online Season 6 con cambios propios. Esto es lo que tenés que saber antes de arrancar.</p>

      <dl className="bigstats">
        <div><dt>Experiencia</dt><dd>{s ? `x${num(s.experienceRate)}` : '…'}</dd></div>
        <div><dt>Nivel máximo</dt><dd>{s ? s.maximumLevel : '…'}</dd></div>
        <div><dt>Master máximo</dt><dd>{s ? s.maximumMasterLevel : '…'}</dd></div>
        <div><dt>Clases</dt><dd>7</dd></div>
      </dl>

      <div className="info-grid">
        <article>
          <h2>Resets</h2>
          <ul className="plain">
            <li>Del reset 1 al 10 se resetea en <b>nivel 300</b>.</li>
            <li>Cada 10 resets se piden <b>10 niveles más</b>, hasta <b>nivel 400</b> desde el reset 101.</li>
            <li>Entre el nivel 300 y el 399 la experiencia es <b>x5</b>.</li>
            <li>No hay penalidad de experiencia por pegarle a monstruos de mucho más nivel.</li>
            <li>Con menos de 10 resets tenés siempre puesto el buff del Elf Soldier.</li>
          </ul>
        </article>
        <article>
          <h2>Lo propio de PeroniaMU</h2>
          <ul className="plain">
            <li>Las 7 clases se pueden crear desde el principio, también MG, DL, Summoner y Rage Fighter.</li>
            <li>Dungeon en Devias con la Ice Queen al final, y el loot se reparte con need, greed o pass.</li>
            <li>Espectros: rivales que te cruzás en los mapas.</li>
            <li>Cada monstruo dorado de la invasión dorada tira su Box of Kundun (+1 a +5), y la caja siempre da un excelente.</li>
            <li>Monstruos en grupos (spots) de 5, y de 7 en los puntos violetas del mapa.</li>
            <li>Los élites de la primera quest (Shadow, Death Knight, Lizard King, Mutant) tiran los items de quest.</li>
            <li>Pociones apiladas de a 255.</li>
          </ul>
        </article>
        <article>
          <h2>El cliente</h2>
          <ul className="plain">
            <li>Cámara 3D que se gira y se aleja.</li>
            <li>60 FPS.</li>
            <li>Mapa completo con TAB, y caminás tocando en el mapa.</li>
            <li>Barras de vida sobre los monstruos.</li>
            <li>Nivel y resets en la ventana de personaje (C).</li>
          </ul>
        </article>
      </div>

      <h2>Escalera de mapas</h2>
      <p className="muted">Cada zona tiene su rango de niveles de monstruos y tira su propio equipo, así que conviene ir subiendo en orden. Los warps del menú M son gratis y piden el nivel de la zona. Vulcanus queda afuera de la escalera porque es para PvP.</p>
      <div className="table-wrap">
        <table className="rank">
          <thead>
            <tr>
              <th scope="col" className="n pos">Peligro</th>
              <th scope="col">Mapa</th>
              <th scope="col" className="n">Monstruos</th>
              <th scope="col" className="hide-s">Qué tira</th>
            </tr>
          </thead>
          <tbody>
            {ladder.map(([maps, levels, drops], i) => (
              <tr key={maps}>
                <td className="n pos">{String(i + 1).padStart(2, '0')}</td>
                <td>{maps}<span className="cls show-s">{drops}</span></td>
                <td className="n">{levels}</td>
                <td className="cls-col hide-s">{drops}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {s && (
        <>
          <h2>En números</h2>
          <dl className="bigstats">
            <div><dt>Cuentas</dt><dd>{num(s.accounts)}</dd></div>
            <div><dt>Personajes</dt><dd>{num(s.characters)}</dd></div>
            <div><dt>Guilds</dt><dd>{num(s.guilds)}</dd></div>
            <div><dt>Jugando ahora</dt><dd>{num(s.playersOnline)}</dd></div>
          </dl>
        </>
      )}
    </section>
  );
}

export function NotFound({ what }: { what: string }) {
  return (
    <section className="page narrow">
      <h1>No está</h1>
      <p className="lead">{what}</p>
      <A to="inicio" className="btn ghost">Ir al inicio</A>
    </section>
  );
}
