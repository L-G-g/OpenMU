# Calibración del simulador de progreso con el cliente real

Medido el 19/10/2026 con el cliente real (`D:\MuPeronia\MU Client 1.04d - Season 6E3`, con `muauto.dll` solo
durante la medición) contra el servidor en vivo (docker), cuenta `calib01`, personajes Dark Knight `CalibDK1`
a `CalibDK3`. Sin mouse ni teclado reales: todo con los comandos internos de muauto.

Configuración del servidor en ese momento: experiencia ×500, escalera de mapas con sets por zona,
eventos activos (Happy Hour, invasión de White Wizard en Lorencia) y muchos bots farmeando en Lost Tower y
Tarkan, que compiten por los mismos spots.

## Cómo se midió

- Script de farmeo: ataca al monstruo vivo más cercano a un spot fijo (un área de spawn del servidor) con
  Twisting Slash en tandas de 3 casts, o con ataque normal en Lorencia, y guarda una línea por pelea.
- Antes de farmear, y después de cada muerte, toma el buff del Shadow Phantom Soldier en Lorencia
  (`/move lorencia`, ida y vuelta: unos 30 s).
- Pociones grandes de vida (Q) y de maná (W) cuando bajan del 45 % y del 35 %.
- Equipo: el pool de la zona que replica el simulador (`equipo_por_zona`), todo +3. Stats iniciales con el
  reparto del `config.json` (45 % fuerza, 25 % agilidad, 20 % vitalidad, 10 % energía) y después cada 10
  niveles con `/addstr`, `/addagi`, `/addvit` y `/addene`.
- Un tick de lógica del juego son 40 ms (25 por segundo).

## Resultados por zona

| Zona (spot) | Personaje | Niveles | Minutos | Kills | Kills/h | Muertes | % del tiempo peleando |
|---|---|---|---|---|---|---|---|
| Lorencia (spiders y budge dragons, ataque normal) | CalibDK1, Small Axe | 20 → 43 | 6,1 | 98 | **963** | 0 | 63 % |
| Dungeon 1 (skeletons y lich, 108,215) | CalibDK1, Scale +3, Falchion +3 | 43 → 65 | 8,0 | 45 | **337** | 0 | 74 % |
| Lost Tower 1 (Shadow, 228,109) | CalibDK2, Brass +3, Serpent Sword +3 | 64 → 98 | 11,2 | 37 | **198** | 0 | 84 % |
| Tarkan (Mutant, 204,102) | CalibDK3, Dragon +3, Legendary Sword +3 | 140 → 140 | 11,4 | ≈0 | **no viable** | 12 | 23 % |

Con Twisting Slash:

| Zona | Casts | Segundos por cast | Kills por cast | Mobs a ≤2 baldosas al castear | Segundos por pelea | Kills por pelea |
|---|---|---|---|---|---|---|
| Dungeon 1 | 121 | 1,18 (incluye acercarse) | 0,37 | 1,5 | 19,7 | 2,5 |
| Lost Tower 1 | 365 | **0,89** (parado al lado) | 0,10 | 1,7 | 18,8 | 1,2 |

Ataque normal en Lorencia: 1,3 s por kill, incluyendo acercarse a un mob que estaba en promedio a 4,1
baldosas; las spiders y los budge dragons mueren de un golpe.

Caminata: 0,36 s por baldosa (2,8 baldosas por segundo; 30 baldosas en 271 ticks). Volver al spot desde el
warp lleva unos 15 s, y la ida y vuelta a Lorencia por el buff, unos 30 s.

### Detalle de cada zona

- **Lorencia:** a partir del nivel 30 está sobrenivelado (la zona es de monstruos de nivel 1 a 19).
  Con experiencia ×500 sube un nivel cada ~15 s.
- **Dungeon 1:** sin muertes. CalibDK1 terminó con 60 puntos sin repartir (ver limitaciones).
- **Lost Tower 1:** casi siempre había solo 2 Shadows vivos en el spot de 5 (respawn y bots), y cada uno
  aguantó ~16 casts (~25 s). En total, 35 s sin mobs en 11 minutos. CalibDK2 terminó con 140 puntos sin
  repartir, así que el daño de la segunda mitad está subestimado.
- **Tarkan:** con nivel 140 y Dragon +3, los 7 Mutant del spot lo rodean y lo matan: 12 muertes en
  11 minutos, ningún nivel ganado. Casi todo el tiempo se fue en reaparecer, buscar el buff y volver. Coincide
  con el simulador, que marca Tarkan como "no sobrevive" para ese nivel.

## Primeras mediciones contra `config.json` (sección `combate`)

Valores del `config.json` antes de calibrar. Los que quedaron están en "Valores calibrados", más abajo.

| Parámetro | Valor de entonces | Medido | Comentario |
|---|---|---|---|
| `segundos_por_golpe` | 0,8 | **0,9** | Twisting Slash parado al lado del objetivo (Lost Tower). En Dungeon da 1,2 porque incluye acercarse. |
| `mobs_por_spot` | 5,4 | 5–7 en la base; **~2 vivos** en Lost Tower, 7 en Tarkan | En Lost Tower el respawn y los bots dejan pocos vivos. |
| `fraccion_en_rango` | 0,8 | **~0,3** parado (1,5–1,7 de 5) | Sube a ~1 cuando los mobs se le vienen encima (Tarkan), pero ahí muere. |
| `segundos_entre_spots` | 8 | spot fijo: 35 s sin mobs en 11 min (~1 s por pelea, Lost Tower) | Volver al spot desde el warp: ~15 s. |
| (no modelado) maná | — | Twisting Slash necesita pociones de maná | En la primera prueba en Dungeon, sin pociones, quedó con 17/99 de maná y las peleas se estiraban a 35–40 s con pocos casts. |
| (no modelado) buff | — | ~30 s por viaje a Lorencia, después de cada muerte | Solo hasta nivel 220. |

## DK de 160 resets: reset completo hasta Raklion

Combinación más débil de las pedidas: alas S2 (Wings of Dragon +13) y set de Karutan normal +13 (Dragon Knight,
Daybreak), sin opciones excelentes. Recién reseteado: nivel 1 en Lorencia, 32.000 puntos (200 × 160) repartidos
45/25/20/10, sin repartir los puntos de cada nivel (regla de más de 100 resets). Sube como un jugador: en cada
momento farmea en el warp más alto que le permite su nivel, sin Swamp of Calmness, hasta Raklion (280). Twisting
Slash; buff hasta nivel 220.

| Zona | Minutos | Kills | Kills/h reales |
|---|---|---|---|
| Lorencia | 2,8 | 37 | 798 |
| Devias | 1,6 | 7 | 255 |
| Dungeon | 3,5 | 18 | 309 |
| Lost Tower | 4,0 | 19 | 284 |
| Atlans | 6,4 | 97 | 906 |
| Tarkan | 1,1 | 17 | 903 |
| Aida | 3,3 | 33 | 605 |
| Icarus | 1,9 | 13 | 418 |
| Kanturu Ruins | 5,9 | 67 | 683 |
| Kanturu Relics | 0,9 | 8 | 558 |
| Karutan 1 | 1,1 | 8 | 419 |
| Karutan 2 | 3,5 | 44 | 764 |
| **Raklion** (nivel 308 → 361) | **15,1** | **369** | **1.469**, sin muertes |

- **Nivel 1 → 280: 36 minutos** de reloj. Restando lo que no corresponde (4,3 min repartiendo puntos, que con la
  regla nueva no se hace, y ~1,5 min de viajes por el buff tras muertes que el script detectó mal) quedan **~30 min**.
  Incluye los warps, caminar al spot de cada zona y el buff.
- **En Raklion aguanta de sobra**: 15 minutos sin morir, 89 % del tiempo peleando, ~3,2 mobs en rango por cast.
  Al ser la combinación más débil, no se corrieron S3 + excelente ni S2 + excelente.

Simulador con la config real del servidor (`exportar-config.ps1`), con las reglas nuevas (sin Swamp y sin repartir
puntos de nivel con más de 100 resets). Su equipo sale de los drops (+3, sin alas ni excelentes):

| | Simulador antes | Simulador calibrado | Juego |
|---|---|---|---|
| Reset completo | ~9 min (1 → 300) | **~19 min** (1 → 300) | ~30 min hasta 280, ~33 hasta 300 |
| Kills/h antes de Raklion | 1.600–2.150 | **943** | ~944 sin warps, buff ni puntos (250–900 con todo) |
| Raklion | 703 kills/h | 536 kills/h | 1.469 kills/h |
| ¿Sobrevive en Raklion? | sí | sí | sí, 15 min sin morir |

El simulador sin calibrar no contaba la espera del respawn en un spot fijo, los warps, caminar al spot ni el buff.
Lo que queda de diferencia:
- **Spot:** el script farmeó el spot más cercano al warp (en Lorencia, spiders), y el simulador va al monstruo
  más fuerte que puede matar, que da mucha más experiencia: en el juego hicieron falta 368 kills hasta el 280 y el
  simulador hace 187 en todo el reset. Un jugador que va al mejor spot de cada zona tarda menos que la medición.
- **Raklion:** el simulador no modela el nivel del ítem por encima de su +3 ni las alas, así que con el set +13 y
  alas S2 el juego pega mucho más.

### Valores calibrados (`config.json`, sección `combate`)

| Parámetro | Antes | Ahora | De dónde sale |
|---|---|---|---|
| `segundos_por_golpe` | 0,8 | 0,8 | 0,77 s por cast en Raklion, 0,89 en Lost Tower |
| `mobs_por_spot` | 5,4 | 5,5 | promedio de las 975 áreas de spawn de la escalera |
| `fraccion_en_rango` | 0,8 | 0,45 | 3,2 mobs en rango por cast en un spot de 7 (Raklion) |
| `segundos_respawn` | — | 19 | respawn de 10 s más lo que tardan en aparecer y acercarse: ~21 s por spot en las zonas que mata de un cast |
| `segundos_entre_spots` | 8 | 2 | en Raklion, con la pelea más larga que el respawn, ~2 s perdidos por spot |
| `segundos_por_cambio_de_zona` | — | 25 | warp ~11 s y caminar al spot ~10–15 s |
| `segundos_buff` | — | 30 | ida y vuelta a Lorencia por el buff (hasta nivel 220) |

## Limitaciones

- **Puntos sin repartir:** el reparto cada 10 niveles con chat se aplicó una sola vez por personaje (al final
  quedaron 60 puntos en CalibDK1 y 140 en CalibDK2). Los otros comandos no llegaron, probablemente porque el
  chat no se abre en medio de un combate. Hay que verificar que se apliquen antes de repetir Dungeon y Lost Tower.
- **Kills en Tarkan:** el script contaba como kills los monstruos que desaparecían al morir el personaje.
  Ya está corregido para las próximas corridas.
- **Duración:** cada medición es de 6 a 11 minutos, en una sola sesión y con un personaje; con experiencia ×500
  el nivel cambia mucho durante la corrida.
- Los bots y los eventos del servidor en vivo cambian la cantidad de mobs disponibles.

## Para repetir

Los scripts están en la carpeta temporal de la sesión de Claude Code que hizo la medición:
- `farm.ps1`: medición.
- `resumen.py`: estos números a partir de los CSV.
- `setup-real.ps1` y `restore-real.ps1`: preparar el cliente real y dejarlo como estaba.
- `setup-zonas.sql`: nivel, stats y equipo de los DK.

`muauto.dll` necesita el arreglo de `client-mods/camera3d/src/muauto/automation.cpp` (enganche de
`GetFocus` y `SetFocus`) para usar el chat con el juego en segundo plano.
