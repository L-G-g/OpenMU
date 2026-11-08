export type GameServer = { id: number; name: string; isOnline: boolean; players: number; maximumPlayers: number };
export type ServerInfo = { serverName: string; playersOnline: number; gameServers: GameServer[]; registrationOpen: boolean; clientDownloadUrl: string | null };
export type Character = { name: string; className: string; level: number; masterLevel: number; resets: number; isOnline: boolean; mapName: string | null };
export type Account = { loginName: string; eMail: string; registeredAt: string; wCoin: number; isOnline: boolean; characters: Character[] };

export type CharacterProfile = { name: string; className: string; level: number; masterLevel: number; resets: number; isOnline: boolean; serverName: string | null; mapName: string | null; guildName: string | null; guildPosition: string | null };
export type OnlinePlayer = { name: string; className: string; level: number; masterLevel: number; resets: number; mapName: string | null; serverName: string };
export type Guild = { name: string; masterName: string | null; memberCount: number; score: number; logo: string | null; allianceName: string | null };
export type GuildMember = { name: string; position: string; className: string; level: number; masterLevel: number; resets: number; isOnline: boolean };
export type GuildDetail = { guild: Guild; members: GuildMember[] };
export type GameEvent = { key: string; name: string; startsAtUtc: string };
export type Stats = { accounts: number; newAccounts: number; characters: number; guilds: number; playersOnline: number; experienceRate: number; masterExperienceRate: number; maximumLevel: number; maximumMasterLevel: number };
export type NewsItem = { date: string; title: string; body: string[] };

export type CastleSiege = { isActive: boolean; ownerGuildName: string | null; ownerGuildLogo: string | null; state: string | null; stateName: string | null; stateEndsAtUtc: string | null; nextBattleUtc: string | null; guilds: { name: string; marks: number }[] };

export type CatalogItem = { key: string; group: number; number: number; name: string; category: string; width: number; height: number; classes: string[]; canHaveExcellent: boolean };
export type MarketOrder = {
  id: string; kind: 'sell' | 'buy'; state: string; ownerName: string; ownerStatus: 'game' | 'web' | 'offline'; isMine: boolean;
  itemKey: string; itemName: string; category: string; level: number; excellentOptionCount: number; hasLuck: boolean; hasSkill: boolean;
  optionLevel: number; isAncient: boolean; socketCount: number; options: string[]; price: number; quantity: number; remainingQuantity: number;
  createdAt: string; expiresAt: string;
};
export type PricePoint = { day: string; median: number; minimum: number; maximum: number; count: number };
export type MarketStats = { median48h: number | null; lowest48h: number | null; sold48h: number; days: PricePoint[] };
export type MarketItemPage = { item: CatalogItem; sell: MarketOrder[]; buy: MarketOrder[]; stats: MarketStats };
export type MarketTrade = { at: string; itemKey: string; itemName: string; level: number; excellentOptionCount: number; price: number; sellerName: string; buyerName: string };
export type DepotItem = { id: string; itemKey: string; itemName: string; level: number; excellentOptionCount: number; hasLuck: boolean; hasSkill: boolean; optionLevel: number; isAncient: boolean; socketCount: number; options: string[]; isTradable: boolean; listedOrderId: string | null };
export type MyMarket = { balance: number; reserved: number; commissionPercent: number; maximumOrders: number; characterNames: string[]; depot: DepotItem[]; depotReadable: boolean; orders: MarketOrder[]; history: MarketTrade[] };
export type BuyOrderRequest = { itemKey: string; minimumLevel: number; minimumExcellentOptions: number; requiresLuck: boolean; requiresSkill: boolean; minimumOptionLevel: number; price: number; quantity: number; ownerName?: string };

export class ApiError extends Error {
  constructor(public code: string, public status: number) {
    super(code);
  }
}

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api${path}`, {
    credentials: 'same-origin',
    headers: init?.body ? { 'Content-Type': 'application/json' } : undefined,
    ...init,
  });
  if (res.status === 204 || res.status === 201) return undefined as T;
  if (res.status === 429) throw new ApiError('too_many_requests', 429);
  if (res.status === 401) throw new ApiError('unauthorized', 401);
  const body = await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(body?.error ?? 'unknown', res.status);
  return body as T;
}

const post = <T>(path: string, data?: unknown) => call<T>(path, { method: 'POST', body: data === undefined ? undefined : JSON.stringify(data) });

export const api = {
  server: () => call<ServerInfo>('/server'),
  ranking: (count = 100) => call<Character[]>(`/ranking?count=${count}`),
  register: (d: { loginName: string; password: string; eMail?: string; inviteCode: string }) => post<void>('/register', d),
  login: (d: { loginName: string; password: string }) => post<void>('/login', d),
  logout: () => post<void>('/logout'),
  me: () => call<Account>('/me'),
  online: () => call<OnlinePlayer[]>('/online'),
  character: (name: string) => call<CharacterProfile>(`/characters/${encodeURIComponent(name)}`),
  guilds: () => call<Guild[]>('/guilds'),
  guild: (name: string) => call<GuildDetail>(`/guilds/${encodeURIComponent(name)}`),
  events: () => call<GameEvent[]>('/events'),
  castleSiege: () => call<CastleSiege>('/castle-siege'),
  stats: () => call<Stats>('/stats'),
  news: () => call<NewsItem[]>('/news'),
  changePassword: (d: { currentPassword: string; newPassword: string }) => post<void>('/me/password', d),
  market: {
    catalog: () => call<CatalogItem[]>('/market/catalog'),
    orders: (kind: 'sell' | 'buy') => call<MarketOrder[]>(`/market/orders?kind=${kind}`),
    item: (key: string) => call<MarketItemPage>(`/market/items/${encodeURIComponent(key)}`),
    trades: (count = 20) => call<MarketTrade[]>(`/market/trades?count=${count}`),
    me: () => call<MyMarket>('/market/me'),
    sell: (d: { itemId: string; price: number; ownerName?: string }) => post<void>('/market/sell', d),
    buyOrder: (d: BuyOrderRequest) => post<void>('/market/buy-orders', d),
    buy: (id: string) => post<void>(`/market/orders/${id}/buy`),
    fill: (id: string, itemId: string) => post<void>(`/market/orders/${id}/fill`, { itemId }),
    cancel: (id: string) => post<void>(`/market/orders/${id}/cancel`),
    change: (id: string, d: { price?: number; hidden?: boolean }) => post<void>(`/market/orders/${id}`, d),
  },
};

const messages: Record<string, string> = {
  registration_closed: 'El registro está cerrado por ahora. Pedile a Gabi que lo abra.',
  invalid_invite_code: 'Ese código de invitación no existe. Revisalo o pedile uno nuevo a Gabi.',
  invalid_login_name: 'El usuario tiene que tener de 3 a 10 caracteres, sin espacios ni acentos.',
  invalid_password: 'La contraseña tiene que tener de 3 a 20 caracteres, sin acentos ni ñ.',
  invalid_email: 'Ese e-mail no parece válido. Podés dejarlo vacío.',
  login_name_taken: 'Ese usuario ya existe. Probá con otro.',
  wrong_credentials: 'Usuario o contraseña incorrectos.',
  wrong_current_password: 'La contraseña actual no es correcta.',
  banned: 'Esta cuenta está bloqueada. Hablá con Gabi.',
  account_online: 'Salí del juego para cambiar la contraseña y probá de nuevo.',
  too_many_requests: 'Demasiados intentos seguidos. Esperá un minuto y probá de nuevo.',
  save_failed: 'No se pudo guardar. Probá de nuevo en un rato.',
  unauthorized: 'Tu sesión venció. Entrá de nuevo.',
  market_closed: 'El mercado está cerrado en este momento. Avisale a Gabi.',
  invalid_price: 'El precio tiene que ser de 1 WC o más.',
  invalid_quantity: 'La cantidad tiene que ser de 1 a 50.',
  unknown_item: 'Ese ítem no existe en el catálogo.',
  too_many_orders: 'Llegaste al máximo de órdenes abiertas. Cancelá alguna para publicar otra.',
  item_already_listed: 'Ese ítem ya está publicado.',
  item_not_in_depot: 'Ese ítem ya no está en tu depósito de la Mercader.',
  item_not_tradable: 'Ese ítem no se puede tradear.',
  item_does_not_meet_order: 'Ese ítem no cumple lo que pide el comprador.',
  no_character: 'Necesitás al menos un personaje para usar el mercado.',
  not_enough_coins: 'No te alcanzan los WC.',
  account_busy: 'Tu cuenta está ocupada en este momento. Probá de nuevo en unos segundos.',
  seller_busy: 'La cuenta del vendedor está ocupada. Probá de nuevo en unos segundos.',
  buyer_busy: 'La cuenta del comprador está ocupada. Probá de nuevo en unos segundos.',
  depot_open: 'Cerrá el depósito de la Mercader en el juego y probá de nuevo.',
  seller_depot_open: 'El vendedor tiene su depósito abierto en el juego. Probá de nuevo en un rato.',
  buyer_depot_open: 'El comprador tiene su depósito abierto en el juego. Probá de nuevo en un rato.',
  depot_full: 'Tu depósito de la Mercader está lleno. Hacé lugar en el juego y probá de nuevo.',
  buyer_depot_full: 'El depósito del comprador está lleno. Probá más tarde.',
  order_not_found: 'Esa orden ya no está disponible.',
  own_order: 'Esa orden es tuya.',
  trade_failed: 'No se pudo completar. No se movió nada y los WC se devolvieron.',
};

export function errorText(e: unknown): string {
  if (e instanceof ApiError) return messages[e.code] ?? 'Algo falló del lado del server. Probá de nuevo en un rato.';
  return 'No hay conexión con el server. Revisá tu internet y probá de nuevo.';
}
