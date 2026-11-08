import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Account, api, BuyOrderRequest, CatalogItem, DepotItem, errorText, MarketOrder, MarketTrade, MyMarket, PricePoint } from './api';
import { LedSign } from './LedSign';
import { A, Loading, num, useLoad } from './ui';

const families: { code: string; name: string }[] = [
  { code: 'DK', name: 'Dark Knight' },
  { code: 'DW', name: 'Dark Wizard' },
  { code: 'ELF', name: 'Elf' },
  { code: 'MG', name: 'Magic Gladiator' },
  { code: 'DL', name: 'Dark Lord' },
  { code: 'SUM', name: 'Summoner' },
  { code: 'RF', name: 'Rage Fighter' },
];
const categories = ['Armas', 'Sets', 'Escudos', 'Alas', 'Joyas', 'Accesorios', 'Pergaminos', 'Otros'];

const initials = (name: string) =>
  name
    .replace(/[^A-Za-z0-9 ]/g, '')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join('') || '?';

function Glyph({ name, exc, big }: { name: string; exc?: boolean; big?: boolean }) {
  return <span className={`glyph${exc ? ' exc' : ''}${big ? ' lg' : ''}`} aria-hidden="true">{initials(name)}</span>;
}

/** "Exc Lightning Sword +11", like the game names it. */
function itemTitle(o: { itemName: string; level: number; excellentOptionCount: number; isAncient?: boolean }) {
  const prefix = o.isAncient ? 'Ancient ' : o.excellentOptionCount > 0 ? 'Exc ' : '';
  return `${prefix}${o.itemName}${o.level > 0 ? ` +${o.level}` : ''}`;
}

function Status({ s, name }: { s: MarketOrder['ownerStatus']; name: string }) {
  const label = s === 'game' ? 'en el juego' : s === 'web' ? 'en la web' : 'desconectado';
  return (
    <span className={`st ${s}`} title={label}>
      <A to="personaje" param={name} className="who">{name}</A>
    </span>
  );
}

function Wc({ v }: { v: number }) {
  return <span className="price mono">{num(v)} WC</span>;
}

/** Requirements of a buy order, in words. */
function wants(o: MarketOrder) {
  const parts: string[] = [];
  if (o.level > 0) parts.push(`+${o.level} o más`);
  if (o.excellentOptionCount > 0) parts.push(`${o.excellentOptionCount}+ exc`);
  if (o.hasLuck) parts.push('luck');
  if (o.hasSkill) parts.push('skill');
  if (o.optionLevel > 0) parts.push(`opción +${o.optionLevel * 4} o más`);
  return parts.length ? parts.join(' · ') : 'cualquiera';
}

function useMyMarket(account: Account | null | undefined, version: number) {
  const [mine, setMine] = useState<MyMarket | null>(null);
  useEffect(() => {
    if (!account) {
      setMine(null);
      return;
    }
    let alive = true;
    api.market.me().then((m) => alive && setMine(m), () => alive && setMine(null));
    return () => {
      alive = false;
    };
  }, [account, version]);
  return mine;
}

function tickerMessages(trades: MarketTrade[] | null) {
  if (!trades || trades.length === 0) return ['MERCADO PERONIA MU', 'PUBLICA DESDE TU DEPOSITO DE LA MERCADER'];
  return trades.slice(0, 8).map((t) => `${t.buyerName.toUpperCase()} COMPRO ${itemTitle(t).toUpperCase()} · ${t.price} WC`);
}

/* ---------- Market home: the showcase (option B) ---------- */

export function MarketPage({ account }: { account: Account | null | undefined }) {
  const catalog = useLoad(() => api.market.catalog(), 'catalog');
  const [kind, setKind] = useState<'sell' | 'buy'>('sell');
  const orders = useLoad(() => api.market.orders(kind), `orders-${kind}`, 30000);
  const trades = useLoad(() => api.market.trades(12), 'trades', 60000);
  const mine = useMyMarket(account, 0);

  const [query, setQuery] = useState('');
  const [family, setFamily] = useState<string | null>(null);
  const [cats, setCats] = useState<string[]>([]);
  const [minLevel, setMinLevel] = useState(0);
  const [minExc, setMinExc] = useState(0);
  const [luck, setLuck] = useState(false);
  const [skill, setSkill] = useState(false);
  const [ancient, setAncient] = useState(false);
  const [sockets, setSockets] = useState(false);
  const [onlineOnly, setOnlineOnly] = useState(false);

  const byKey = useMemo(() => new Map((catalog.data ?? []).map((c) => [c.key, c])), [catalog.data]);
  const list = useMemo(() => {
    const q = query.trim().toLowerCase();
    return (orders.data ?? []).filter((o) => {
      const c = byKey.get(o.itemKey);
      if (q && !o.itemName.toLowerCase().includes(q) && !o.options.some((x) => x.toLowerCase().includes(q))) return false;
      if (family && c && c.classes.length > 0 && !c.classes.includes(family)) return false;
      if (cats.length > 0 && !cats.includes(o.category)) return false;
      if (o.level < minLevel || o.excellentOptionCount < minExc) return false;
      if ((luck && !o.hasLuck) || (skill && !o.hasSkill) || (ancient && !o.isAncient) || (sockets && o.socketCount === 0)) return false;
      if (onlineOnly && o.ownerStatus === 'offline') return false;
      return true;
    });
  }, [orders.data, byKey, query, family, cats, minLevel, minExc, luck, skill, ancient, sockets, onlineOnly]);

  const exact = catalog.data?.find((c) => c.name.toLowerCase() === query.trim().toLowerCase());
  const toggleCat = (c: string) => setCats((x) => (x.includes(c) ? x.filter((y) => y !== c) : [...x, c]));
  const clear = () => {
    setQuery('');
    setFamily(null);
    setCats([]);
    setMinLevel(0);
    setMinExc(0);
    setLuck(false);
    setSkill(false);
    setAncient(false);
    setSockets(false);
    setOnlineOnly(false);
  };

  return (
    <section className="page market">
      <div className="head-row">
        <h1>Mercado</h1>
        <div className="mk-acct">
          {mine && <span className="balance">Saldo <Wc v={mine.balance} /></span>}
          {account ? <A to="misordenes" className="btn small-btn">Mis órdenes y depósito</A> : <A to="entrar" className="btn ghost small-btn">Entrá para comprar o vender</A>}
        </div>
      </div>
      <LedSign messages={tickerMessages(trades.data)} label="Últimas ventas del mercado" />
      <p className="lead mk-lead">Lo que publicás sale de tu depósito de la Mercader, al lado de la Julia de Lorencia. Cuando alguien compra, el ítem le llega a su depósito y los WC a vos, aunque no estés conectado.</p>

      <div className="mk-search">
        <label className="sr" htmlFor="mk-q">Buscar ítem</label>
        <input id="mk-q" list="mk-catalog" placeholder="Buscar ítem u opción: Bless, Dragon, Exc…" value={query} onChange={(e) => setQuery(e.target.value)} />
        <datalist id="mk-catalog">{catalog.data?.map((c) => <option key={c.key} value={c.name} />)}</datalist>
        {exact && <A to="item" param={exact.key} className="btn ghost small-btn">Ver precios de {exact.name}</A>}
      </div>

      <div className="tabs" role="tablist">
        <button type="button" role="tab" aria-selected={kind === 'sell'} className={kind === 'sell' ? 'on' : ''} onClick={() => setKind('sell')}>Venden</button>
        <button type="button" role="tab" aria-selected={kind === 'buy'} className={kind === 'buy' ? 'on' : ''} onClick={() => setKind('buy')}>Compran</button>
      </div>

      <div className="mk-layout">
        <aside className="mk-filters" aria-label="Filtros">
          <div className="grp">
            <span className="eyebrow">Clase</span>
            <select id="mk-class" value={family ?? ''} onChange={(e) => setFamily(e.target.value || null)}>
              <option value="">Todas</option>
              {families.map((f) => <option key={f.code} value={f.code}>{f.name}</option>)}
            </select>
          </div>
          <div className="grp">
            <span className="eyebrow">Tipo</span>
            <div className="chipset">
              {categories.map((c) => (
                <button key={c} type="button" className={`chip ${cats.includes(c) ? 'on' : ''}`} aria-pressed={cats.includes(c)} onClick={() => toggleCat(c)}>{c}</button>
              ))}
            </div>
          </div>
          <div className="grp two">
            <label><span className="eyebrow">Nivel mínimo</span>
              <select id="mk-lvl" value={minLevel} onChange={(e) => setMinLevel(Number(e.target.value))}>
                {Array.from({ length: 16 }, (_, i) => <option key={i} value={i}>{i === 0 ? 'Todos' : `+${i}`}</option>)}
              </select>
            </label>
            <label><span className="eyebrow">Excelentes</span>
              <select id="mk-exc" value={minExc} onChange={(e) => setMinExc(Number(e.target.value))}>
                {Array.from({ length: 7 }, (_, i) => <option key={i} value={i}>{i === 0 ? 'Todas' : `${i}+`}</option>)}
              </select>
            </label>
          </div>
          <div className="grp">
            <span className="eyebrow">Opciones</span>
            <Check id="mk-luck" label="Luck" v={luck} set={setLuck} />
            <Check id="mk-skill" label="Skill" v={skill} set={setSkill} />
            <Check id="mk-anc" label="Ancient" v={ancient} set={setAncient} />
            <Check id="mk-sock" label="Con sockets" v={sockets} set={setSockets} />
            <Check id="mk-on" label="Solo gente en línea" v={onlineOnly} set={setOnlineOnly} />
          </div>
          <button type="button" className="link" onClick={clear}>Limpiar filtros</button>
        </aside>

        <div className="mk-results">
          {orders.error && <p className="error">{orders.error}</p>}
          {!orders.data && !orders.error && <Loading what="el mercado" />}
          {orders.data && list.length === 0 && (
            <div className="empty">
              <p><b>{orders.data.length === 0 ? (kind === 'sell' ? 'Todavía nadie publicó nada.' : 'Todavía nadie pidió nada.') : 'Nada coincide con esos filtros.'}</b></p>
              <p className="muted">{kind === 'sell' ? 'Dejá un ítem en la Mercader y publicalo desde Mis órdenes.' : 'Podés hacer un pedido de compra desde la página de cada ítem.'}</p>
            </div>
          )}
          <ul className="cards">
            {list.map((o) => (
              <li key={o.id}>
                <A to="item" param={o.itemKey} className="card">
                  <span className="card-top">
                    <Glyph name={o.itemName} exc={o.excellentOptionCount > 0} />
                    <span className="card-name">
                      <span className={`itemname ${o.excellentOptionCount > 0 ? 'exc' : ''} ${o.isAncient ? 'anc' : ''}`}>{itemTitle(o)}</span>
                      <span className="opts">{kind === 'buy' ? `Pide: ${wants(o)}` : o.options.slice(0, 3).join(' · ') || o.category}</span>
                    </span>
                  </span>
                  <span className="card-foot">
                    <span className={`st ${o.ownerStatus}`}>{o.ownerName}{kind === 'buy' && o.remainingQuantity > 1 ? ` · x${o.remainingQuantity}` : ''}</span>
                    <Wc v={o.price} />
                  </span>
                </A>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function Check({ id, label, v, set }: { id: string; label: string; v: boolean; set: (b: boolean) => void }) {
  return (
    <label className="check" htmlFor={id}>
      <input id={id} type="checkbox" checked={v} onChange={(e) => set(e.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

/* ---------- Item page: the order book (option A) with a small price chart ---------- */

export function ItemPage({ itemKey, account }: { itemKey: string; account: Account | null | undefined }) {
  const [version, setVersion] = useState(0);
  const page = useLoad(() => api.market.item(itemKey), `item-${itemKey}-${version}`, 30000);
  const mine = useMyMarket(account, version);
  const [onlineOnly, setOnlineOnly] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const refresh = () => setVersion((v) => v + 1);

  if (page.missing) return <section className="page"><h1>Ese ítem no está en el mercado</h1><A to="mercado">Volver al mercado</A></section>;
  if (!page.data) return <section className="page">{page.error ? <p className="error">{page.error}</p> : <Loading what="el ítem" />}</section>;
  const { item, stats } = page.data;
  const sell = page.data.sell.filter((o) => !onlineOnly || o.ownerStatus !== 'offline');
  const buy = page.data.buy.filter((o) => !onlineOnly || o.ownerStatus !== 'offline');

  const act = async (run: () => Promise<void>, ok: string) => {
    setMsg(null);
    try {
      await run();
      setMsg({ ok: true, text: ok });
      refresh();
    } catch (e) {
      setMsg({ ok: false, text: errorText(e) });
    }
  };

  return (
    <section className="page market">
      <p className="back-top"><A to="mercado">← Mercado</A></p>
      <div className="mk-item">
        <Glyph name={item.name} big />
        <div className="mk-item-name">
          <h1>{item.name}</h1>
          <p className="muted">{item.category}{item.classes.length > 0 && item.classes.length < 7 ? ` · ${item.classes.map((c) => families.find((f) => f.code === c)?.name ?? c).join(', ')}` : ''}</p>
        </div>
        <dl className="mk-stats">
          <div><dt>Mediana 48 h</dt><dd>{stats.median48h != null ? `${num(stats.median48h)} WC` : '—'}</dd></div>
          <div><dt>Más barato 48 h</dt><dd>{stats.lowest48h != null ? `${num(stats.lowest48h)} WC` : '—'}</dd></div>
          <div><dt>Vendidos 48 h</dt><dd>{stats.sold48h}</dd></div>
        </dl>
        <PriceChart days={stats.days} />
      </div>

      <div className="mk-toolbar">
        <Check id="it-on" label="Solo gente en línea" v={onlineOnly} set={setOnlineOnly} />
        {account ? (
          <button type="button" className="btn ghost small-btn" onClick={() => setShowForm((s) => !s)}>{showForm ? 'Cerrar pedido' : 'Hacer pedido de compra'}</button>
        ) : (
          <A to="entrar" className="btn ghost small-btn">Entrá para comprar o vender</A>
        )}
      </div>
      {msg && <p className={msg.ok ? 'ok' : 'error'} role="status">{msg.text}</p>}
      {showForm && mine && (
        <BuyOrderForm
          item={item}
          mine={mine}
          onDone={(text) => {
            setShowForm(false);
            setMsg({ ok: true, text });
            refresh();
          }}
        />
      )}

      <div className="book">
        <div className="side sell">
          <h2><span>Venden</span><span className="muted mono">{sell.length}</span></h2>
          {sell.length === 0 ? <p className="muted pad">Nadie lo vende ahora.{account ? ' Hacé un pedido de compra y te avisa cuando alguien te lo venda.' : ''}</p> : (
            <ul className="orders">
              {sell.map((o) => (
                <li key={o.id}>
                  <div className="o-main">
                    <span className={`itemname ${o.excellentOptionCount > 0 ? 'exc' : ''}`}>{itemTitle(o)}</span>
                    {o.options.length > 0 && <span className="opts">{o.options.join(' · ')}</span>}
                    <Status s={o.ownerStatus} name={o.ownerName} />
                  </div>
                  <div className="o-act">
                    <Wc v={o.price} />
                    {account && !o.isMine && <Confirm label="Comprar" confirm={`Pagar ${num(o.price)} WC`} onConfirm={() => act(() => api.market.buy(o.id), `Listo. ${itemTitle(o)} ya está en tu depósito de la Mercader.`)} />}
                    {o.isMine && <span className="badge">tuya</span>}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
        <div className="side buy">
          <h2><span>Compran</span><span className="muted mono">{buy.length}</span></h2>
          {buy.length === 0 ? <p className="muted pad">Nadie lo está pidiendo.</p> : (
            <ul className="orders">
              {buy.map((o) => (
                <li key={o.id}>
                  <div className="o-main">
                    <span>Pide: <b>{wants(o)}</b>{o.remainingQuantity > 1 && <span className="muted"> · faltan {o.remainingQuantity}</span>}</span>
                    <Status s={o.ownerStatus} name={o.ownerName} />
                  </div>
                  <div className="o-act">
                    <Wc v={o.price} />
                    {account && !o.isMine && mine && <FillPicker order={o} depot={mine.depot} onFill={(itemId) => act(() => api.market.fill(o.id, itemId), `Vendido. Te llegan ${num(o.price - Math.floor((o.price * mine.commissionPercent) / 100))} WC.`)} />}
                    {o.isMine && <span className="badge">tuyo</span>}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </section>
  );
}

function Confirm({ label, confirm, onConfirm }: { label: string; confirm: string; onConfirm: () => Promise<void> }) {
  const [armed, setArmed] = useState(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!armed) return;
    const t = setTimeout(() => setArmed(false), 5000);
    return () => clearTimeout(t);
  }, [armed]);
  if (!armed) return <button type="button" className="btn small-btn" onClick={() => setArmed(true)}>{label}</button>;
  return (
    <button
      type="button"
      className="btn small-btn armed"
      disabled={busy}
      onClick={async () => {
        setBusy(true);
        await onConfirm();
        setBusy(false);
        setArmed(false);
      }}
    >
      {busy ? 'Un momento…' : confirm}
    </button>
  );
}

function meets(i: DepotItem, o: MarketOrder) {
  return i.itemKey === o.itemKey && i.isTradable && !i.listedOrderId && i.level >= o.level && i.excellentOptionCount >= o.excellentOptionCount && (i.hasLuck || !o.hasLuck) && (i.hasSkill || !o.hasSkill) && i.optionLevel >= o.optionLevel;
}

function FillPicker({ order, depot, onFill }: { order: MarketOrder; depot: DepotItem[]; onFill: (itemId: string) => Promise<void> }) {
  const fits = depot.filter((i) => meets(i, order));
  const [pick, setPick] = useState(fits[0]?.id ?? '');
  if (fits.length === 0) return <span className="muted small" title="Dejá el ítem en tu depósito de la Mercader para venderlo acá.">No tenés uno en la Mercader</span>;
  return (
    <span className="fill">
      {fits.length > 1 && (
        <select aria-label="Ítem para vender" value={pick} onChange={(e) => setPick(e.target.value)}>
          {fits.map((i) => <option key={i.id} value={i.id}>{itemTitle(i)} {i.options.slice(0, 2).join(' ')}</option>)}
        </select>
      )}
      <Confirm label="Vender" confirm={`Vender por ${num(order.price)} WC`} onConfirm={() => onFill(pick || fits[0].id)} />
    </span>
  );
}

function PriceChart({ days }: { days: PricePoint[] }) {
  const W = 300;
  const H = 96;
  const L = 34;
  const B = 18;
  if (days.length < 2) {
    return <div className="mk-chart empty-chart"><span className="muted small">El gráfico aparece con las primeras ventas.</span></div>;
  }
  const max = Math.max(...days.map((d) => d.median));
  const min = Math.min(...days.map((d) => d.median));
  const top = max === min ? max + 1 : max;
  const bottom = max === min ? Math.max(0, min - 1) : min;
  const t0 = new Date(days[0].day).getTime();
  const t1 = new Date(days[days.length - 1].day).getTime();
  const x = (d: PricePoint) => L + ((new Date(d.day).getTime() - t0) / Math.max(1, t1 - t0)) * (W - L - 6);
  const y = (v: number) => 6 + (1 - (v - bottom) / (top - bottom)) * (H - B - 6);
  const line = days.map((d, i) => `${i ? 'L' : 'M'}${x(d).toFixed(1)},${y(d.median).toFixed(1)}`).join('');
  const last = days[days.length - 1];
  const fmt = (s: string) => new Date(s).toLocaleDateString('es-AR', { day: 'numeric', month: 'short', timeZone: 'UTC' });
  return (
    <figure className="mk-chart">
      <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`Mediana de precio por día, de ${num(min)} a ${num(max)} WC`}>
        {[top, bottom].map((v) => (
          <g key={v}>
            <line x1={L} x2={W - 6} y1={y(v)} y2={y(v)} className="grid" />
            <text x={L - 4} y={y(v) + 3} textAnchor="end" className="axis">{num(v)}</text>
          </g>
        ))}
        <text x={L} y={H - 4} className="axis">{fmt(days[0].day)}</text>
        <text x={W - 6} y={H - 4} textAnchor="end" className="axis">{fmt(last.day)}</text>
        <path d={`${line} L${x(last)},${y(bottom)} L${L},${y(bottom)} Z`} className="area" />
        <path d={line} className="line" />
        <circle cx={x(last)} cy={y(last.median)} r="3.5" className="dotp" />
      </svg>
      <figcaption className="muted small">Mediana por día, últimos 90 días</figcaption>
    </figure>
  );
}

function BuyOrderForm({ item, mine, onDone }: { item: CatalogItem; mine: MyMarket; onDone: (text: string) => void }) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [price, setPrice] = useState(0);
  const [qty, setQty] = useState(1);
  const submit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const d: BuyOrderRequest = {
      itemKey: item.key,
      minimumLevel: Number(f.get('bo-lvl') ?? 0),
      minimumExcellentOptions: Number(f.get('bo-exc') ?? 0),
      requiresLuck: f.get('bo-luck') === 'on',
      requiresSkill: f.get('bo-skill') === 'on',
      minimumOptionLevel: Number(f.get('bo-opt') ?? 0),
      price,
      quantity: qty,
      ownerName: String(f.get('bo-name') ?? '') || undefined,
    };
    setBusy(true);
    setError(null);
    try {
      await api.market.buyOrder(d);
      onDone(`Pedido publicado. Se reservaron ${num(price * qty)} WC de tu saldo.`);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <form className="mk-form" onSubmit={submit} noValidate>
      <h2>Pedido de compra: {item.name}</h2>
      <p className="muted small">Los WC se reservan ahora. Si nadie te lo vende en {14} días, o si cancelás, vuelven a tu saldo. Tenés {num(mine.balance)} WC disponibles.</p>
      <div className="mk-form-grid">
        <label><span>Nivel mínimo</span>
          <select name="bo-lvl" id="bo-lvl" defaultValue={0}>{Array.from({ length: 16 }, (_, i) => <option key={i} value={i}>{i === 0 ? 'Cualquiera' : `+${i}`}</option>)}</select>
        </label>
        {item.canHaveExcellent && (
          <label><span>Excelentes mínimas</span>
            <select name="bo-exc" id="bo-exc" defaultValue={0}>{Array.from({ length: 7 }, (_, i) => <option key={i} value={i}>{i === 0 ? 'Ninguna' : i}</option>)}</select>
          </label>
        )}
        <label><span>Opción mínima</span>
          <select name="bo-opt" id="bo-opt" defaultValue={0}>{Array.from({ length: 8 }, (_, i) => <option key={i} value={i}>{i === 0 ? 'Cualquiera' : `+${i * 4}`}</option>)}</select>
        </label>
        <label><span>Precio por unidad (WC)</span>
          <input id="bo-price" type="number" min={1} inputMode="numeric" required value={price || ''} onChange={(e) => setPrice(Math.max(0, Math.floor(Number(e.target.value))))} />
        </label>
        <label><span>Cantidad</span>
          <input id="bo-qty" type="number" min={1} max={50} inputMode="numeric" value={qty} onChange={(e) => setQty(Math.min(50, Math.max(1, Math.floor(Number(e.target.value)))))} />
        </label>
        {mine.characterNames.length > 1 && (
          <label><span>Aparecer como</span>
            <select name="bo-name" id="bo-name">{mine.characterNames.map((n) => <option key={n} value={n}>{n}</option>)}</select>
          </label>
        )}
      </div>
      <div className="mk-form-row">
        <label className="check" htmlFor="bo-luck"><input id="bo-luck" name="bo-luck" type="checkbox" /><span>Con luck</span></label>
        <label className="check" htmlFor="bo-skill"><input id="bo-skill" name="bo-skill" type="checkbox" /><span>Con skill</span></label>
      </div>
      {error && <p className="error" role="alert">{error}</p>}
      <button className="btn" type="submit" disabled={busy || price < 1}>{busy ? 'Publicando…' : price > 0 ? `Reservar ${num(price * qty)} WC y publicar` : 'Poné un precio'}</button>
    </form>
  );
}

/* ---------- My orders and my market depot ---------- */

export function MyMarketPage({ account }: { account: Account | null | undefined }) {
  const [version, setVersion] = useState(0);
  const mine = useMyMarket(account, version);
  const catalog = useLoad(() => api.market.catalog(), 'catalog');
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [wanted, setWanted] = useState('');
  const refresh = () => setVersion((v) => v + 1);

  if (account === undefined) return <section className="page"><Loading what="tu cuenta" /></section>;
  if (account === null) {
    return (
      <section className="page narrow">
        <h1>Mis órdenes</h1>
        <p className="lead">Entrá para ver tu depósito de la Mercader y tus órdenes.</p>
        <A to="entrar" className="btn">Entrar</A>
      </section>
    );
  }
  if (!mine) return <section className="page"><Loading what="tu mercado" /></section>;

  const act = async (run: () => Promise<void>, ok: string) => {
    setMsg(null);
    try {
      await run();
      setMsg({ ok: true, text: ok });
      refresh();
    } catch (e) {
      setMsg({ ok: false, text: errorText(e) });
    }
  };

  const open = mine.orders.filter((o) => o.state === 'active' || o.state === 'hidden');
  const closed = mine.orders.filter((o) => o.state !== 'active' && o.state !== 'hidden');
  const wantedItem = catalog.data?.find((c) => c.name.toLowerCase() === wanted.trim().toLowerCase());

  return (
    <section className="page market">
      <p className="back-top"><A to="mercado">← Mercado</A></p>
      <h1>Mis órdenes</h1>
      <dl className="bigstats mk-balance">
        <div><dt>Disponible</dt><dd>{num(mine.balance)}</dd></div>
        <div><dt>Reservado en pedidos</dt><dd>{num(mine.reserved)}</dd></div>
        <div><dt>Órdenes abiertas</dt><dd>{open.length}/{mine.maximumOrders}</dd></div>
      </dl>
      <p className="muted small">Los WC se usan también en el Cash Shop del juego. El mercado se queda con el {mine.commissionPercent}% de cada venta.</p>
      {msg && <p className={msg.ok ? 'ok' : 'error'} role="status">{msg.text}</p>}

      <h2>Mi depósito de la Mercader</h2>
      {mine.depot.length === 0 ? (
        <div className="empty">
          <p><b>Tu depósito está vacío.</b></p>
          <p className="muted">En el juego, andá a Lorencia y hablá con la Mercader (la segunda Julia, al lado de la original). Lo que dejes ahí lo podés publicar desde acá.</p>
        </div>
      ) : (
        <ul className="depot">
          {mine.depot.map((i) => <DepotCard key={i.id} item={i} names={mine.characterNames} commission={mine.commissionPercent} onSell={(price, name) => act(() => api.market.sell({ itemId: i.id, price, ownerName: name }), `${itemTitle(i)} publicado por ${num(price)} WC.`)} />)}
        </ul>
      )}

      <h2>Pedir un ítem</h2>
      <div className="mk-search">
        <label className="sr" htmlFor="my-want">Ítem que buscás</label>
        <input id="my-want" list="my-catalog" placeholder="Escribí el nombre del ítem" value={wanted} onChange={(e) => setWanted(e.target.value)} />
        <datalist id="my-catalog">{catalog.data?.map((c) => <option key={c.key} value={c.name} />)}</datalist>
      </div>
      {wantedItem && <BuyOrderForm key={wantedItem.key} item={wantedItem} mine={mine} onDone={(text) => { setWanted(''); setMsg({ ok: true, text }); refresh(); }} />}

      <h2>Órdenes abiertas</h2>
      {open.length === 0 ? <p className="muted">No tenés órdenes abiertas.</p> : (
        <div className="table-wrap">
          <table className="rank mk-table">
            <thead><tr><th>Ítem</th><th className="hide-s">Tipo</th><th className="n">Precio</th><th className="hide-s">Vence</th><th></th></tr></thead>
            <tbody>
              {open.map((o) => (
                <tr key={o.id} className={o.state === 'hidden' ? 'dim' : ''}>
                  <td><A to="item" param={o.itemKey}>{itemTitle(o)}</A>{o.kind === 'buy' && <span className="muted small"> · {wants(o)}{o.quantity > 1 ? ` · ${o.quantity - o.remainingQuantity}/${o.quantity}` : ''}</span>}{o.state === 'hidden' && <span className="badge">oculta</span>}</td>
                  <td className="hide-s">{o.kind === 'sell' ? 'Venta' : 'Pedido'}</td>
                  <td className="n"><PriceEdit order={o} onSave={(price) => act(() => api.market.change(o.id, { price }), 'Precio cambiado.')} /></td>
                  <td className="hide-s muted small">{new Date(o.expiresAt).toLocaleDateString('es-AR')}</td>
                  <td className="row-act">
                    <button type="button" className="link" onClick={() => act(() => api.market.change(o.id, { hidden: o.state !== 'hidden' }), o.state === 'hidden' ? 'La orden se ve de nuevo.' : 'Orden oculta. El ítem sigue trabado en el depósito.')}>{o.state === 'hidden' ? 'Mostrar' : 'Ocultar'}</button>
                    <Confirm label="Cancelar" confirm="Sí, cancelar" onConfirm={() => act(() => api.market.cancel(o.id), o.kind === 'sell' ? 'Cancelada. Ya podés sacar el ítem en la Mercader.' : 'Cancelado. Los WC volvieron a tu saldo.')} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {open.length > 0 && (
        <button type="button" className="link" onClick={async () => { for (const o of open.filter((x) => x.state === 'active')) await api.market.change(o.id, { hidden: true }).catch(() => undefined); refresh(); }}>Ocultar todas</button>
      )}

      <h2>Historial</h2>
      {mine.history.length === 0 && closed.length === 0 ? <p className="muted">Todavía no compraste ni vendiste nada.</p> : (
        <div className="table-wrap">
          <table className="rank">
            <thead><tr><th>Fecha</th><th>Ítem</th><th className="hide-s">Con</th><th className="n">WC</th></tr></thead>
            <tbody>
              {mine.history.map((t, i) => {
                const sold = mine.characterNames.includes(t.sellerName);
                return (
                  <tr key={i}>
                    <td className="muted small">{new Date(t.at).toLocaleDateString('es-AR')}</td>
                    <td><A to="item" param={t.itemKey}>{itemTitle(t)}</A> <span className="muted small">{sold ? 'vendido' : 'comprado'}</span></td>
                    <td className="hide-s">{sold ? t.buyerName : t.sellerName}</td>
                    <td className={`n ${sold ? 'up' : ''}`}>{sold ? '+' : '−'}{num(t.price)}</td>
                  </tr>
                );
              })}
              {closed.filter((o) => o.state !== 'completed').map((o) => (
                <tr key={o.id} className="dim">
                  <td className="muted small">{new Date(o.expiresAt).toLocaleDateString('es-AR')}</td>
                  <td>{itemTitle(o)} <span className="muted small">{o.state === 'expired' ? 'venció' : 'cancelada'}</span></td>
                  <td className="hide-s">—</td>
                  <td className="n muted">{num(o.price)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function DepotCard({ item, names, commission, onSell }: { item: DepotItem; names: string[]; commission: number; onSell: (price: number, name?: string) => Promise<void> }) {
  const [price, setPrice] = useState(0);
  const [name, setName] = useState(names[0] ?? '');
  const [busy, setBusy] = useState(false);
  const fee = Math.floor((price * commission) / 100);
  return (
    <li className="card depot-card">
      <span className="card-top">
        <Glyph name={item.itemName} exc={item.excellentOptionCount > 0} />
        <span className="card-name">
          <span className={`itemname ${item.excellentOptionCount > 0 ? 'exc' : ''}`}>{itemTitle(item)}</span>
          <span className="opts">{item.options.join(' · ')}</span>
        </span>
      </span>
      {item.listedOrderId ? (
        <span className="badge live">Publicado</span>
      ) : !item.isTradable ? (
        <span className="muted small">No se puede tradear</span>
      ) : (
        <form
          className="sell-row"
          onSubmit={async (e) => {
            e.preventDefault();
            if (price < 1) return;
            setBusy(true);
            await onSell(price, name || undefined);
            setBusy(false);
          }}
        >
          <label className="sr" htmlFor={`p-${item.id}`}>Precio en WC</label>
          <input id={`p-${item.id}`} type="number" min={1} inputMode="numeric" placeholder="Precio WC" value={price || ''} onChange={(e) => setPrice(Math.max(0, Math.floor(Number(e.target.value))))} />
          {names.length > 1 && (
            <select aria-label="Aparecer como" value={name} onChange={(e) => setName(e.target.value)}>{names.map((n) => <option key={n} value={n}>{n}</option>)}</select>
          )}
          <button className="btn small-btn" type="submit" disabled={busy || price < 1}>Publicar</button>
          {price > 0 && <span className="muted small fee">Te llegan {num(price - fee)} WC</span>}
        </form>
      )}
      <A to="item" param={item.itemKey} className="small">Ver precios</A>
    </li>
  );
}

function PriceEdit({ order, onSave }: { order: MarketOrder; onSave: (price: number) => Promise<void> }) {
  const [edit, setEdit] = useState(false);
  const [v, setV] = useState(order.price);
  if (!edit) return <button type="button" className="link price-btn" onClick={() => setEdit(true)} title="Cambiar precio"><Wc v={order.price} /></button>;
  return (
    <form className="price-edit" onSubmit={async (e) => { e.preventDefault(); await onSave(v); setEdit(false); }}>
      <label className="sr" htmlFor={`pe-${order.id}`}>Nuevo precio</label>
      <input id={`pe-${order.id}`} type="number" min={1} value={v} onChange={(e) => setV(Math.max(1, Math.floor(Number(e.target.value))))} autoFocus />
      <button className="btn small-btn" type="submit">OK</button>
    </form>
  );
}
