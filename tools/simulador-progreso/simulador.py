"""Simulador de progreso de jugadores de PeroniaMU.

Simula personajes que suben de nivel y resetean como lo haría un jugador normal, con las fórmulas
del servidor (experiencia, daño, chance de pegar, drops) y los datos leídos del código del fork.
Ver README.md.

Uso:
    python simulador.py --src RUTA_DEL_FORK [--config config.json] [--jugadores 50] [--resets 20]
"""

import argparse
import json
import math
import random
import statistics
from pathlib import Path

import datos

AQUI = Path(__file__).resolve().parent

# Bonus de daño/defensa por nivel de ítem (_weaponDamageIncreaseTable, _defenseIncreaseTable):
# 3 por nivel hasta +9, y 1 más por cada nivel después.
BONUS_NIVEL = [0, 3, 6, 9, 12, 15, 18, 21, 24, 27, 31, 36, 42, 49, 57, 66]

# Chances de los grupos de drop (GameConfigurationInitializerBase.AddItemDropGroups y MapLevelLadder).
CHANCE_DINERO = 0.5
CHANCE_ITEM_ZONA = 0.3
CHANCE_EXC_ZONA = 0.0001
CHANCE_JOYAS_COMUN = 0.001
CHANCE_JOYAS_ZONA = 0.01
CHANCE_SOCKET_ZONA = 0.01
NIVEL_MAXIMO_ITEM_ZONA = 3

# Stats de la clase (CharacterClasses/ClassDarkKnight.cs). Agregar más clases acá.
CLASES = {
    "DK": {
        "familia": "DK",
        "base": {"fuerza": 28, "agilidad": 20, "vitalidad": 25, "energia": 10},
        "puntos_por_nivel": 5,
        # Twisting Slash: se aprende con la orbe (nivel 80) y pega a todos los del área.
        "skill_area_nivel": 80,
    },
}


def experiencia_para_nivel(nivel):
    """Fórmula por defecto de GameContext.DefaultExperienceFormula (experiencia total para llegar al nivel)."""
    if nivel == 0:
        return 0
    exp = 10 * (nivel + 8) * (nivel - 1) * (nivel - 1)
    if nivel >= 256:
        exp += 1000 * (nivel - 247) * (nivel - 256) * (nivel - 256)
    return exp


def experiencia_base(nivel_mob, nivel_jugador):
    """AttackableExtensions.CalculateBaseExperience."""
    exp = (nivel_mob + 25) * nivel_mob / 3.0
    if nivel_jugador > nivel_mob + 10:
        exp *= (nivel_mob + 10) / nivel_jugador
    if nivel_mob >= 65:
        exp += (nivel_mob - 64) * (nivel_mob // 4)
    return max(exp, 0) * 1.25


def chance_de_pegar(attack_rate, defense_rate):
    """AttackableExtensions.GetHitChanceTo."""
    if defense_rate < attack_rate:
        return 1.0 - defense_rate / attack_rate
    return 0.03


def poisson(rng, media):
    if media <= 0:
        return 0
    if media > 50:
        return max(0, round(rng.gauss(media, math.sqrt(media))))
    limite, k, p = math.exp(-media), 0, 1.0
    while True:
        p *= rng.random()
        if p <= limite:
            return k
        k += 1


# Cachés de una corrida (la configuración no cambia durante la corrida).
_EVALUACIONES = {}


class Personaje:
    def __init__(self, clase, config, rng):
        self.c = CLASES[clase]
        self.config = config
        self.rng = rng
        self.nivel = 1
        self.resets = 0
        self.exp = 0
        self.stats = dict(self.c["base"])
        self.puntos = 0
        self.equipo = {}  # ranura -> (Item, nivel del ítem)

    # --- Stats derivados (ClassDarkKnight.cs) ---
    def _arma(self):
        return self.equipo.get("arma")

    def danio(self):
        arma = self._arma()
        dmin = self.stats["fuerza"] / 6
        dmax = self.stats["fuerza"] / 4
        if arma:
            it, lv = arma
            dmin += it.dmin + BONUS_NIVEL[lv]
            dmax += it.dmax + BONUS_NIVEL[lv]
        return dmin, dmax

    def defensa(self):
        d = self.stats["agilidad"] / 3
        for ranura, (it, lv) in self.equipo.items():
            if ranura != "arma":
                d += it.defensa + BONUS_NIVEL[lv]
        return d

    def defense_rate(self):
        return self.stats["agilidad"] / 3

    def attack_rate(self):
        return 5 * self.nivel + 1.5 * self.stats["agilidad"] + self.stats["fuerza"] / 4

    def vida(self):
        return 35 + 2 * self.nivel + 3 * self.stats["vitalidad"]

    def multiplicador_skill(self):
        return 2 + 0.001 * self.stats["energia"]

    # --- Pelea en un spot ---
    # Cada spot tiene un solo tipo de monstruo (MonsterSpotGrouping), así que el jugador elige un monstruo.
    def evaluar(self, zona, m):
        clave = (zona.indice, m.numero, self.nivel, tuple(self.stats.values()),
                 tuple(sorted((r, it.numero, it.grupo, lv) for r, (it, lv) in self.equipo.items())))
        if clave not in _EVALUACIONES:
            _EVALUACIONES[clave] = self._evaluar(zona, m)
        return _EVALUACIONES[clave]

    def _evaluar(self, zona, m):
        cfg = self.config["combate"]
        esc = self.config["servidor"]["escalado_mobs_pct"]
        area = self.nivel >= self.c["skill_area_nivel"]
        dmin, dmax = self.danio()

        defensa_mob = m.defensa * (1 + esc["defensa"] / 100)
        defense_rate_mob = m.defense_rate * (1 + esc["defense_rate"] / 100)
        vida_mob = m.vida * (1 + esc["vida"] / 100)
        pegar = chance_de_pegar(self.attack_rate(), defense_rate_mob)
        d = (dmin + dmax) / 2 - defensa_mob
        if defense_rate_mob > self.attack_rate():
            d *= 0.3
        d = max(d, max(1, self.nivel // 10))
        if area:
            d = int(d * self.multiplicador_skill())
        golpes = math.ceil(vida_mob / d) / pegar

        danio_mob = (m.dmin + m.dmax) / 2 * (1 + esc["danio"] / 100) - self.defensa()
        danio_mob = max(danio_mob, max(1, m.nivel // 10))
        pega_mob = chance_de_pegar(m.attack_rate * (1 + esc["attack_rate"] / 100), self.defense_rate())
        dps_mob = pega_mob * danio_mob / m.demora_ataque

        por_spot = cfg["mobs_por_spot"]
        if area:
            # El área pega a todos los que están cerca a la vez.
            golpes_spot = golpes / cfg["fraccion_en_rango"]
            atacando = por_spot * 0.6
        else:
            golpes_spot = golpes * por_spot
            atacando = por_spot * 0.5
        segundos_pelea = golpes_spot * cfg["segundos_por_golpe"]
        segundos_spot = segundos_pelea + cfg["segundos_entre_spots"]

        neto = dps_mob * atacando - cfg["curacion_pct_vida_por_segundo"] * self.vida()
        segundos_para_morir = math.inf if neto <= 0 else self.vida() / neto
        sobrevive = segundos_para_morir > cfg["margen_supervivencia"] * segundos_pelea

        kills_hora = por_spot * 3600 / segundos_spot
        exp = exp_kill_actual(zona, m, self.nivel, self.config)
        return {
            "sobrevive": sobrevive,
            "segundos_spot": segundos_spot,
            "kills_hora": kills_hora,
            "exp_kill": exp,
            "exp_hora": exp * kills_hora,
        }

    def elegir(self, zonas):
        """Devuelve (zona, monstruo, evaluación) donde va a cazar."""
        cfg = self.config["comportamiento"]
        opciones = []
        for z in zonas:
            if z.nivel_requerido > self.nivel:
                continue
            for m in z.monstruos:
                if m.jefe:
                    continue
                ev = self.evaluar(z, m)
                if ev["sobrevive"]:
                    opciones.append((z, m, ev))
        if not opciones:
            z = zonas[0]
            m = min(z.monstruos, key=lambda m: m.nivel)
            return z, m, self.evaluar(z, m)
        if cfg["politica_zona"] == "mejor_exp":
            return max(opciones, key=lambda o: o[2]["exp_hora"])
        # "mayor_viable": el monstruo más fuerte que no lo mata y que no tarda demasiado en limpiar el spot.
        viables = [o for o in opciones if o[2]["segundos_spot"] <= cfg["segundos_maximos_por_spot"]] or opciones
        return max(viables, key=lambda o: (o[1].nivel, o[0].indice))

    # --- Puntos y equipo ---
    def repartir(self):
        if self.puntos <= 0:
            return
        maximo = self.config["servidor"]["stat_maximo"]
        # Primero lo que pide el mejor equipo que ya tiene (o el de la zona).
        for clave, req in (("fuerza", self._requisito("req_fuerza")), ("agilidad", self._requisito("req_agilidad"))):
            falta = min(max(0, req - self.stats[clave]), self.puntos)
            self.stats[clave] += falta
            self.puntos -= falta
        reparto = self.config["comportamiento"]["reparto"]
        while self.puntos > 0:
            abiertos = {k: v for k, v in reparto.items() if self.stats[k] < maximo}
            if not abiertos:
                break
            total = sum(abiertos.values())
            disponibles = self.puntos
            for k, v in abiertos.items():
                n = min(int(disponibles * v / total), maximo - self.stats[k])
                self.stats[k] += n
                self.puntos -= n
            if self.puntos == disponibles:
                k = min(abiertos, key=lambda k: self.stats[k])
                self.stats[k] += 1
                self.puntos -= 1

    def _requisito(self, campo):
        return max([getattr(it, campo) for it, _ in self.equipo.values()] + [0])

    def puede_usar(self, it):
        return (self.stats["fuerza"] + self.puntos >= it.req_fuerza
                and self.stats["agilidad"] + self.puntos >= it.req_agilidad
                and self.nivel >= it.req_nivel)

    @staticmethod
    def ranura(it):
        if it.tipo == "arma":
            return "arma"
        if it.tipo == "escudo":
            return "escudo"
        return datos.RANURAS_SET[it.grupo]

    @staticmethod
    def valor(it, lv):
        if it.tipo == "arma":
            return (it.dmin + it.dmax) / 2 + BONUS_NIVEL[lv]
        return it.defensa + BONUS_NIVEL[lv]

    def mejora(self, it, lv):
        ranura = self.ranura(it)
        if ranura == "escudo" and self._arma() and self._arma()[0].dos_manos:
            return False
        actual = self.equipo.get(ranura)
        return actual is None or self.valor(it, lv) > self.valor(*actual)

    def equipar(self, it, lv):
        ranura = self.ranura(it)
        self.equipo[ranura] = (it, lv)
        if ranura == "arma" and it.dos_manos:
            self.equipo.pop("escudo", None)


def puntos_de_reset(req, reset):
    """ResetProgressionCalculator.GetPointsForReset."""
    tramos = [t for t in req.get("tramos_puntos", []) if t[0] <= reset]
    if tramos:
        return max(0, max(tramos)[1])
    return max(0, req["puntos_por_reset"] * (reset if req["multiplicar_por_resets"] else 1))


def puntos_despues_de_reset(req, reset):
    """ResetProgressionCalculator.GetTotalPointsAfterReset: con tramos se suman los de todos los resets."""
    if not req.get("tramos_puntos"):
        return puntos_de_reset(req, reset)
    return sum(puntos_de_reset(req, r) for r in range(1, reset + 1))


def cargar_config_servidor(config, archivo):
    """Pisa la config con la que exportó exportar-config.ps1 de la base del server."""
    datos_server = json.loads(Path(archivo).read_text(encoding="utf-8-sig"))

    def valor(d, clave, defecto=None):
        return next((v for k, v in (d or {}).items() if k.lower() == clave.lower()), defecto)

    servidor = config["servidor"]
    tasas = [s["exp"] for s in datos_server.get("servidores") or [] if s.get("exp")]
    servidor["exp_rate"] = datos_server["exp_config"] * (max(tasas) if tasas else 1)
    for plugin in datos_server.get("plugins") or []:
        cfg = plugin.get("config")
        cfg = json.loads(cfg) if isinstance(cfg, str) and cfg else (cfg or {})
        tipo = plugin["tipo"].upper()
        if tipo == "6A9D585D-79D7-4674-B6EA-7E87392FA501":  # ResetFeaturePlugIn
            r = servidor["reset"]
            r["nivel_requerido"] = valor(cfg, "RequiredLevel", r["nivel_requerido"])
            r["nivel_despues"] = valor(cfg, "LevelAfterReset", r["nivel_despues"])
            r["puntos_por_reset"] = valor(cfg, "PointsPerReset", r["puntos_por_reset"])
            r["multiplicar_por_resets"] = valor(cfg, "MultiplyPointsByResetCount", r["multiplicar_por_resets"])
            r["reemplazar_puntos"] = valor(cfg, "ReplacePointsPerReset", r["reemplazar_puntos"])
            r["resetear_stats"] = valor(cfg, "ResetStats", r["resetear_stats"])
            tramos = valor(cfg, "PointsTiers", []) or []
            if isinstance(tramos, dict):  # serializado con referencias: {"$id": ..., "$values": [...]}
                tramos = tramos.get("$values", [])
            r["tramos_puntos"] = sorted([valor(t, "MinimumResetCount", 0), valor(t, "PointsGranted", 0)] for t in tramos)
            r["limite"] = valor(cfg, "ResetLimit")
        elif tipo == "59E5B45F-0A3D-4BAF-8B6C-9E1D2F3A4B5C":  # Monster Attribute Scaler
            esc = servidor["escalado_mobs_pct"]
            if not plugin.get("activo"):
                for k in esc:
                    esc[k] = 0
                continue
            todo = valor(cfg, "ScaleAllPercentage", 0) or 0
            for k, nombre in (("danio", "Damage"), ("defensa", "Defense"), ("vida", "Health"),
                              ("attack_rate", "AttackRate"), ("defense_rate", "DefenseRate")):
                esc[k] = todo if todo > 0 else valor(cfg, nombre + "Percentage", 25.0)


def zona_multiplicador(zona):
    return 2.0 if zona.drops == "Jewels" else 1.0


def nivel_item_zona(nivel_mob, zona):
    """DefaultDropGenerator.GetZoneItemLevel (el máximo de nivel se alcanza en el monstruo más fuerte)."""
    fuerte = max(m.nivel for m in zona.monstruos)
    if fuerte <= zona.nivel_min:
        return NIVEL_MAXIMO_ITEM_ZONA
    lv = (nivel_mob - zona.nivel_min) * (NIVEL_MAXIMO_ITEM_ZONA + 1) // (fuerte - zona.nivel_min + 1)
    return max(0, min(lv, NIVEL_MAXIMO_ITEM_ZONA))


class Simulacion:
    def __init__(self, config, zonas, equipo_zona, clase, semilla, traza=False):
        self.traza = traza
        self._ultimo = None
        self.config = config
        self.zonas = zonas
        self.equipo_zona = equipo_zona
        self.clase = clase
        self.rng = random.Random(semilla)
        self.pj = Personaje(clase, config, self.rng)
        self.horas = 0.0
        self.kills = 0
        self.totales = {"exc": 0, "exc_propios": 0, "joyas": {}, "socket": 0, "zen": 0, "muertes": 0}
        self.resets = []  # por reset: horas, kills, zona más usada, exc, joyas

    def correr(self, resets_objetivo):
        cfg = self.config
        req = cfg["servidor"]["reset"]
        puntos_por_nivel = self.pj.c["puntos_por_nivel"]
        cada = cfg["comportamiento"]["repartir_cada_niveles"]
        while self.pj.resets < resets_objetivo:
            inicio_horas, inicio_kills = self.horas, self.kills
            inicio_exc, inicio_joyas = self.totales["exc"], sum(self.totales["joyas"].values())
            horas_zona = {}
            zona = mob = ev = None
            while self.pj.nivel < req["nivel_requerido"]:
                if zona is None or self.pj.nivel % 5 == 0:
                    zona, mob, ev = self.pj.elegir(self.zonas)
                    if self.traza and (zona.nombre, mob.nombre) != self._ultimo:
                        self._ultimo = (zona.nombre, mob.nombre)
                        print(f"  reset {self.pj.resets} nivel {self.pj.nivel}: {mob.nombre} ({zona.nombre}),"
                              f" {ev['segundos_spot']:.0f} s por spot, {ev['kills_hora']:.0f} kills/h, stats {self.pj.stats}")
                # La exp por kill baja cuando el jugador pasa por más de 10 niveles al monstruo.
                exp_kill = exp_kill_actual(zona, mob, self.pj.nivel, cfg)
                faltan = experiencia_para_nivel(self.pj.nivel + 1) - self.pj.exp
                kills = max(1, math.ceil(faltan / max(exp_kill, 1)))
                horas = kills / ev["kills_hora"]
                self.horas += horas
                self.kills += kills
                donde = f"{mob.nombre} ({zona.nombre.split(',')[0].split(' and')[0]})"
                horas_zona[donde] = horas_zona.get(donde, 0) + horas
                self.drops(zona, mob, kills, exp_kill)
                self.pj.exp += kills * exp_kill
                while self.pj.nivel < req["nivel_requerido"] and self.pj.exp >= experiencia_para_nivel(self.pj.nivel + 1):
                    self.pj.nivel += 1
                    self.pj.puntos += puntos_por_nivel
                    if self.pj.nivel % cada == 0 or self.pj.resets == 0 and self.pj.nivel < cfg["comportamiento"]["repartir_siempre_hasta_nivel"]:
                        self.pj.repartir()
                        zona = None
            self.resetear()
            self.horas += cfg["combate"]["segundos_por_reset"] / 3600
            self.resets.append({
                "reset": self.pj.resets,
                "horas": self.horas - inicio_horas,
                "kills": self.kills - inicio_kills,
                "zona": max(horas_zona, key=horas_zona.get) if horas_zona else "",
                "exc": self.totales["exc"] - inicio_exc,
                "joyas": sum(self.totales["joyas"].values()) - inicio_joyas,
                "stats": dict(self.pj.stats),
            })
        return self

    def resetear(self):
        req = self.config["servidor"]["reset"]
        self.pj.resets += 1
        self.pj.nivel = req["nivel_despues"]
        self.pj.exp = 0
        puntos = puntos_despues_de_reset(req, self.pj.resets)
        if req["resetear_stats"]:
            self.pj.stats = dict(self.pj.c["base"])
        if req["reemplazar_puntos"]:
            self.pj.puntos = puntos
        else:
            self.pj.puntos += puntos
        self.pj.repartir()

    def drops(self, zona, mob, kills, exp_kill):
        rng = self.rng
        t = self.totales
        t["zen"] += kills * CHANCE_DINERO * (exp_kill + 7)
        nivel_mob = mob.nivel
        if zona.drops == "Jewels":
            for _ in range(poisson(rng, kills * CHANCE_JOYAS_ZONA)):
                j = rng.choice(datos.JOYAS_ZONA)
                t["joyas"][j] = t["joyas"].get(j, 0) + 1
            return
        validas = [j for j, nivel, maximo in datos.GRUPO_JOYAS_COMUN if nivel <= nivel_mob and (maximo is None or nivel_mob <= maximo)]
        for _ in range(poisson(rng, kills * CHANCE_JOYAS_COMUN)):
            j = rng.choice(validas)
            if j.startswith("Jewel"):
                t["joyas"][j] = t["joyas"].get(j, 0) + 1
        if zona.drops == "SocketItems":
            t["socket"] += poisson(rng, kills * CHANCE_SOCKET_ZONA)
            return

        pool = self.equipo_zona[zona.indice]
        familia = self.pj.c["familia"]
        propios = [it for it in pool if familia in it.familias]
        n_exc = poisson(rng, kills * CHANCE_EXC_ZONA)
        t["exc"] += n_exc
        for _ in range(n_exc):
            if rng.random() < len(propios) / len(pool):
                t["exc_propios"] += 1
        # Cada pieza propia cae con 30 % / tamaño del pool por kill; se queda con la que mejora su equipo.
        p = CHANCE_ITEM_ZONA / len(pool)
        for it in propios:
            if rng.random() < 1 - math.exp(-kills * p):
                lv = nivel_item_zona(nivel_mob, zona)
                if self.pj.puede_usar(it) and self.pj.mejora(it, lv):
                    self.pj.equipar(it, lv)


_EXP_KILL = {}


def exp_kill_actual(zona, mob, nivel, config):
    clave = (zona.indice, mob.numero, nivel, config["servidor"]["exp_rate"])
    if clave not in _EXP_KILL:
        _EXP_KILL[clave] = experiencia_base(mob.nivel, nivel) * config["servidor"]["exp_rate"] * zona_multiplicador(zona)
    return _EXP_KILL[clave]


def percentiles(valores):
    v = sorted(valores)
    def p(q):
        return v[min(len(v) - 1, int(q * len(v)))]
    return statistics.mean(v), p(0.1), p(0.5), p(0.9)


def formatear_horas(h):
    if h < 1:
        return f"{h * 60:.0f} min"
    if h < 48:
        return f"{h:.1f} h"
    return f"{h / 24:.1f} días"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", default=str(AQUI.parent.parent), help="carpeta raíz del fork (donde está src/)")
    ap.add_argument("--config", default=str(AQUI / "config.json"))
    ap.add_argument("--clase", default="DK", choices=sorted(CLASES))
    ap.add_argument("--jugadores", type=int, default=None)
    ap.add_argument("--resets", type=int, default=None)
    ap.add_argument("--exp-rate", type=float, default=None, help="pisa servidor.exp_rate de la config")
    ap.add_argument("--server", help="config real exportada con exportar-config.ps1 (exp, reset, escalado de mobs)")
    ap.add_argument("--json", help="guarda el detalle por reset en este archivo")
    ap.add_argument("--traza", action="store_true", help="muestra los cambios de zona del primer jugador")
    ap.add_argument("--evaluar", metavar="NIVEL,FUE,AGI,VIT,ENE",
                    help="solo evalúa cada zona para un personaje sin equipo con esos stats (para calibrar contra el juego)")
    args = ap.parse_args()

    config = json.loads(Path(args.config).read_text(encoding="utf-8"))
    if args.server:
        cargar_config_servidor(config, args.server)
    if args.exp_rate is not None:
        config["servidor"]["exp_rate"] = args.exp_rate
    sim_cfg = config["simulacion"]
    jugadores = args.jugadores or sim_cfg["jugadores"]
    resets = args.resets or sim_cfg["resets"]
    if config["servidor"]["reset"].get("limite"):
        resets = min(resets, config["servidor"]["reset"]["limite"])

    zonas, _, equipo_zona = datos.cargar(args.src)
    if args.evaluar:
        nivel, *stats = (int(x) for x in args.evaluar.split(","))
        pj = Personaje(args.clase, config, random.Random(0))
        pj.nivel = nivel
        pj.stats = dict(zip(("fuerza", "agilidad", "vitalidad", "energia"), stats))
        print(f"{'Monstruo':<26} {'Zona':<22} {'Req':>4} {'¿Sobrevive?':>11} {'s/spot':>7} {'kills/h':>8} {'exp/kill':>9} {'exp/h':>10}")
        for z in zonas:
            for m in z.monstruos:
                if m.jefe:
                    continue
                ev = pj.evaluar(z, m)
                print(f"{m.nombre[:26]:<26} {z.nombre[:22]:<22} {z.nivel_requerido:>4} {'sí' if ev['sobrevive'] else 'no':>11}"
                      f" {ev['segundos_spot']:>7.0f} {ev['kills_hora']:>8.0f} {ev['exp_kill']:>9.0f} {ev['exp_hora']:>10.0f}")
        return
    sims = [Simulacion(config, zonas, equipo_zona, args.clase, sim_cfg["semilla"] + i, args.traza and i == 0).correr(resets) for i in range(jugadores)]

    r = config["servidor"]["reset"]
    print(f"Clase {args.clase}, exp x{config['servidor']['exp_rate']:g}, {jugadores} jugadores, {resets} resets."
          f" Reset a nivel {r['nivel_requerido']}, {r['puntos_por_reset']} puntos"
          f"{' x resets' if r['multiplicar_por_resets'] else ''}{', reemplaza los puntos' if r['reemplazar_puntos'] else ''}")
    print("(promedio / p10 / mediana / p90; horas de juego efectivas, cazando sin parar)\n")
    print(f"{'Reset':>5} {'Horas del reset':>18} {'Acumulado':>12} {'Kills':>9} {'Exc':>5} {'Joyas':>6}  Dónde caza más")
    marcas = sorted({1, 2, 3, 5, 10, 20, 50, 100, 200, 300, 350, 360, resets} & set(range(1, resets + 1)))
    for r in marcas:
        filas = [s.resets[r - 1] for s in sims]
        horas = [f["horas"] for f in filas]
        acumulado = [sum(x["horas"] for x in s.resets[:r]) for s in sims]
        zonas_usadas = statistics.mode(f["zona"] for f in filas)
        print(f"{r:>5} {formatear_horas(statistics.mean(horas)):>18} {formatear_horas(statistics.mean(acumulado)):>12}"
              f" {statistics.mean(f['kills'] for f in filas):>9.0f} {statistics.mean(f['exc'] for f in filas):>5.1f}"
              f" {statistics.mean(f['joyas'] for f in filas):>6.1f}  {zonas_usadas}")

    def tramo(a, b):
        if b > resets:
            return None
        return [sum(x["horas"] for x in s.resets[a - 1:b]) for s in sims]

    print()
    por_dia = sim_cfg["horas_de_juego_por_dia"]
    for a, b in ((1, 10), (350, 360)):
        if (t := tramo(a, b)) is not None:
            m, p10, p50, p90 = percentiles(t)
            print(f"Resets {a}-{b}: {formatear_horas(m)} de juego en promedio (p10 {formatear_horas(p10)}, p90 {formatear_horas(p90)});"
                  f" jugando {por_dia:g} h por día son {m / por_dia:.0f} días")
    total_horas = statistics.mean(s.horas for s in sims)
    exc = statistics.mean(s.totales["exc"] for s in sims)
    exc_propios = statistics.mean(s.totales["exc_propios"] for s in sims)
    joyas = {}
    for s in sims:
        for j, n in s.totales["joyas"].items():
            joyas[j] = joyas.get(j, 0) + n / len(sims)
    print(f"Total {resets} resets: {formatear_horas(total_horas)}, {statistics.mean(s.kills for s in sims):,.0f} kills")
    print(f"Excelentes: {exc:.1f} en total ({exc / max(total_horas, 1e-9):.3f} por hora), {exc_propios:.1f} de su clase")
    print("Joyas: " + ", ".join(f"{j.replace('Jewel of ', '')} {n:.0f}" for j, n in sorted(joyas.items(), key=lambda x: -x[1])))
    print(f"Items socket (Raklion): {statistics.mean(s.totales['socket'] for s in sims):.1f}")
    final = sims[0].pj
    print(f"Stats al final (jugador 1): {final.stats}, equipo: " + ", ".join(f"{k} {it.nombre}+{lv}" for k, (it, lv) in final.equipo.items()))

    if args.json:
        Path(args.json).write_text(json.dumps([s.resets for s in sims], indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
