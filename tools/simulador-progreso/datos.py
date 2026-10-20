"""Lee los datos del servidor directamente del código C# del fork.

Así el simulador usa los mismos números que el servidor (escalera de mapas, stats de los
monstruos, armas, sets, requisitos de los warps) y se actualiza solo cuando cambia el código.
"""

import re
from dataclasses import dataclass, field
from pathlib import Path

INIT = Path("src/Persistence/Initialization")
S6 = INIT / "VersionSeasonSix"

# Familias de clases, en el orden de los argumentos de los Create* del código C#.
FAMILIAS = ["DW", "DK", "ELF", "MG", "DL", "SUM", "RF"]

# Ranuras de equipo (InventoryConstants): 0 mano izq, 1 mano der, 2 casco, 3 armadura,
# 4 pantalón, 5 guantes, 6 botas. El grupo de una armadura es la ranura + 5.
RANURAS_SET = {7: "casco", 8: "armadura", 9: "pantalon", 10: "guantes", 11: "botas"}
GRUPO_ESCUDO = 6

# El grupo común de joyas (0,1 % por kill): ítems y nivel de drop (ver Version075/095d/097d Jewels,
# Potions y Pets). Hay 5 "no joyas" (alcohol, pergamino, mascotas) en el mismo grupo.
GRUPO_JOYAS_COMUN = [
    ("Jewel of Chaos", 12, 66),
    ("Jewel of Bless", 25, None),
    ("Jewel of Soul", 30, None),
    ("Jewel of Life", 72, None),
    ("Jewel of Creation", 72, None),
    ("Alcohol", 15, None),
    ("Town Portal Scroll", 30, None),
    ("Guardian Angel", 0, None),
    ("Imp", 0, None),
    ("Horn of Uniria", 0, None),
]

# Las joyas de la zona que solo tira joyas (MapLevelLadder.ZoneJewels).
JOYAS_ZONA = ["Jewel of Chaos", "Jewel of Bless", "Jewel of Soul", "Jewel of Life", "Jewel of Creation", "Jewel of Guardian"]


@dataclass
class Monstruo:
    zona: int
    numero: int
    nivel: int
    vida: float
    dmin: float
    dmax: float
    defensa: float
    attack_rate: float
    defense_rate: float
    nombre: str
    jefe: bool
    demora_ataque: float = 1.6


@dataclass
class Zona:
    indice: int
    nivel_min: int
    nivel_max: int
    mapas: list
    drops: str
    nombre: str
    nivel_requerido: int = 0
    monstruos: list = field(default_factory=list)


@dataclass
class Item:
    grupo: int
    numero: int
    nombre: str
    nivel_drop: int
    familias: set
    dmin: int = 0
    dmax: int = 0
    velocidad: int = 0
    defensa: int = 0
    req_fuerza: int = 0
    req_agilidad: int = 0
    req_nivel: int = 0
    sockets: int = 0
    cae: bool = True
    dos_manos: bool = False

    @property
    def es_armadura(self):
        return self.grupo in RANURAS_SET

    @property
    def clave_rango(self):
        # DefaultDropGenerator.GetRankKey: los sets se agrupan por número, las armas por grupo y número.
        return self.numero if self.es_armadura else (self.grupo << 9) + self.numero

    @property
    def tipo(self):
        if self.es_armadura:
            return "set"
        return "escudo" if self.grupo == GRUPO_ESCUDO else "arma"


def _args(texto):
    """Separa los argumentos de una llamada, ignorando las comas dentro de paréntesis."""
    partes, nivel, actual = [], 0, ""
    for c in texto:
        if c == "(":
            nivel += 1
        elif c == ")":
            nivel -= 1
        if c == "," and nivel == 0:
            partes.append(actual.strip())
            actual = ""
        else:
            actual += c
    partes.append(actual.strip())
    return partes


def _llamadas(texto, nombre):
    for m in re.finditer(r"^\s*(?:var \w+ = )?this\." + nombre + r"\((\d.*)\);", texto, re.M):
        yield _args(m.group(1))


def _nombre(arg):
    m = re.search(r"ItemNames\.(\w+)", arg)
    return m.group(1) if m else arg.strip('"')


def _familias(flags):
    return {FAMILIAS[i] for i, f in enumerate(flags) if int(f) > 0}


def leer_items(raiz):
    items = {}
    armas = (raiz / S6 / "Items/Weapons.cs").read_text(encoding="utf-8-sig")
    for a in _llamadas(armas, "CreateWeapon"):
        if len(a) != 26:
            continue
        it = Item(int(a[0]), int(a[1]), _nombre(a[7]), int(a[8]), _familias(a[19:26]),
                  dmin=int(a[9]), dmax=int(a[10]), velocidad=int(a[11]), req_nivel=int(a[14]),
                  req_fuerza=int(a[15]), req_agilidad=int(a[16]), cae=a[6] == "true", dos_manos=int(a[4]) > 1)
        # Flechas y virotes (alto 1) no son armas.
        if int(a[5]) > 1:
            items[(it.grupo, it.numero)] = it

    armaduras = (raiz / S6 / "Items/Armors.cs").read_text(encoding="utf-8-sig")
    for a in _llamadas(armaduras, "CreateArmor"):
        if len(a) < 21:
            continue
        slot = int(a[1])
        it = Item(slot + 5, int(a[0]), _nombre(a[4]), int(a[5]), _familias(a[14:21]),
                  defensa=int(a[6]), req_nivel=int(a[8]), req_fuerza=int(a[9]), req_agilidad=int(a[10]))
        items[(it.grupo, it.numero)] = it
    for a in _llamadas(armaduras, "CreateGloves"):
        if len(a) != 15:
            continue
        it = Item(10, int(a[0]), _nombre(a[1]), int(a[2]), _familias(a[9:15]),
                  defensa=int(a[3]), req_nivel=int(a[6]), req_fuerza=int(a[7]), req_agilidad=int(a[8]))
        items[(it.grupo, it.numero)] = it
    for a in _llamadas(armaduras, "CreateBoots"):
        if len(a) != 19:
            continue
        it = Item(11, int(a[0]), _nombre(a[1]), int(a[2]), _familias(a[12:19]),
                  defensa=int(a[3]), req_nivel=int(a[6]), req_fuerza=int(a[7]), req_agilidad=int(a[8]))
        items[(it.grupo, it.numero)] = it
    for a in _llamadas(armaduras, "CreateShield"):
        if len(a) != 23:
            continue
        it = Item(GRUPO_ESCUDO, int(a[0]), _nombre(a[5]), int(a[6]), _familias(a[16:23]),
                  defensa=int(a[7]), req_nivel=int(a[10]), req_fuerza=int(a[11]), req_agilidad=int(a[12]))
        items[(it.grupo, it.numero)] = it

    sockets = (raiz / S6 / "Items/SocketSystem.cs").read_text(encoding="utf-8-sig")
    for m in re.finditer(r"AddWeaponSockets\((\d+), (\d+), (\d+)\)", sockets):
        if (k := (int(m.group(1)), int(m.group(2)))) in items:
            items[k].sockets = int(m.group(3))
    for m in re.finditer(r"AddArmorSockets\((\d+), (\d+)\)", sockets):
        if (k := (int(m.group(1)), int(m.group(2)))) in items:
            items[k].sockets = 3
    return [it for it in items.values() if it.cae and it.familias]


def leer_demoras_ataque(raiz):
    """Demora entre ataques de cada monstruo (por número), de las definiciones de los mapas."""
    demoras = {}
    for archivo in sorted((raiz / INIT).rglob("Maps/*.cs")):
        texto = archivo.read_text(encoding="utf-8-sig")
        for m in re.finditer(r"monster\.Number = (\d+);(.*?)monster\.AttackDelay = new TimeSpan\((\d+) \* TimeSpan\.TicksPerMillisecond\)", texto, re.S):
            if "monster.Number" not in m.group(2):
                demoras.setdefault(int(m.group(1)), int(m.group(3)) / 1000)
    return demoras


def leer_zonas(raiz):
    texto = (raiz / S6 / "MapLevelLadder.cs").read_text(encoding="utf-8-sig")
    zonas = []
    bloque = texto[texto.index("Zones { get; } ="):texto.index("Monsters { get; } =")]
    for m in re.finditer(r"\((\d+), (\d+), \[([\d, ]+)\], ZoneDrops\.(\w+)\), // (.*)", bloque):
        mapas = [int(x) for x in m.group(3).split(",")]
        zonas.append(Zona(len(zonas), int(m.group(1)), int(m.group(2)), mapas, m.group(4), m.group(5).strip()))

    demoras = leer_demoras_ataque(raiz)
    bloque = texto[texto.index("Monsters { get; } ="):texto.index("SymbolOfKundunDropLevels")]
    for m in re.finditer(r"new\((\d+), (\d+), (\d+), ([\d.]+), ([\d.]+), ([\d.]+), ([\d.]+), ([\d.]+), ([\d.]+)\), // (.*)", bloque):
        v = m.groups()
        comentario = v[9]
        nombre = comentario.split(":")[-1].split(",")[0].split("(")[0].strip()
        mo = Monstruo(int(v[0]), int(v[1]), int(v[2]), float(v[3]), float(v[4]), float(v[5]), float(v[6]),
                      float(v[7]), float(v[8]), nombre, "(boss)" in comentario, demoras.get(int(v[1]), 1.6))
        zonas[mo.zona - 1].monstruos.append(mo)

    _leer_requisitos(raiz, texto, zonas)
    return zonas


def _leer_requisitos(raiz, ladder, zonas):
    """Nivel para entrar a cada zona: el menor nivel de los warps a sus mapas (Gates.cs + la escalera)."""
    gates = (raiz / S6 / "Gates.cs").read_text(encoding="utf-8-sig")
    warps = {int(m.group(1)): (m.group(2), int(m.group(3)))
             for m in re.finditer(r"CreateWarpInfo\((\d+), \"(\w+)\", \d+, (\d+),", gates)}
    bloque = ladder[ladder.index("WarpLevelRequirements"):]
    for m in re.finditer(r"\{ (\d+), (\d+) \}, // (.*)", bloque):
        nombre, _ = warps[int(m.group(1))]
        warps[int(m.group(1))] = (nombre, int(m.group(2)))

    # Nombre del warp -> número de mapa (los mapas de la escalera).
    prefijos = {"Lorencia": 0, "Dungeon": 1, "Devias": 2, "Noria": 3, "LostTower": 4, "Atlans": 7, "Tarkan": 8,
                "Icarus": 10, "Aida": 33, "KanturuRuins": 37, "KanturuRelics": 38, "Elveland": 51, "PeaceSwamp": 56,
                "Raklion": 57, "Karutan": 80}
    nivel_mapa = {}
    for nombre, nivel in warps.values():
        base = re.sub(r"\d+$", "", nombre)
        if base in prefijos:
            mapa = prefijos[base]
            nivel_mapa[mapa] = min(nivel, nivel_mapa.get(mapa, 10_000))
    nivel_mapa[81] = nivel_mapa.get(80, 0)
    nivel_mapa[58] = nivel_mapa.get(57, 0)
    for z in zonas:
        z.nivel_requerido = min(nivel_mapa.get(m, 0) for m in z.mapas)
    # A Lorencia, Noria y Elbeland se llega caminando desde el inicio.
    zonas[0].nivel_requerido = 1


def familia_de_items(items):
    """Replica DefaultDropGenerator.GetOwnWeapons: qué armas son "propias" de cada familia."""
    armas = [it for it in items if it.tipo == "arma" and it.sockets == 0]
    todas = {f for it in armas for f in it.familias}
    propias = set()
    for familia in todas:
        usables = [it for it in armas if familia in it.familias]
        grupos = {it.grupo for it in usables if len(it.familias) == 1}
        own = [it for it in usables if it.grupo in grupos and (len(todas) == 1 or len(it.familias) < len(todas))]
        for it in own or usables:
            propias.add((familia, it.grupo, it.numero))
    return propias


def _nivel_rango(rango):
    armadura = [it for it in rango if it.grupo == 8]
    return armadura[0].nivel_drop if armadura else min(it.nivel_drop for it in rango)


def _rangos(items):
    grupos = {}
    for it in items:
        grupos.setdefault(it.clave_rango, []).append(it)
    return sorted(grupos.values(), key=lambda r: (_nivel_rango(r), r[0].clave_rango))


def _div(a, b):
    return (2 * a + b) // (2 * b)


def _rangos_zona(tipo, rangos, indice, cantidad):
    if tipo != "set":
        primero, fin = _div(indice * len(rangos), cantidad), _div((indice + 1) * len(rangos), cantidad)
        if fin > primero:
            return rangos[primero:fin]
    i = _div(indice * (len(rangos) - 1), cantidad - 1) if cantidad > 1 else len(rangos) - 1
    return [rangos[i]]


def equipo_por_zona(items, zonas):
    """Replica DefaultDropGenerator.GetZoneEquipment para cada zona que tira equipo.

    Devuelve {indice_zona: [Item, ...]} con todo el equipo de la zona (de todas las familias).
    """
    propias = familia_de_items(items)
    equipables = [it for it in items if it.sockets == 0]
    por_familia_tipo = {}
    for it in equipables:
        for familia in it.familias:
            tipo = it.tipo
            if tipo == "arma" and (familia, it.grupo, it.numero) not in propias:
                tipo = "compartida"
            por_familia_tipo.setdefault((familia, tipo), []).append(it)
    rangos = {k: _rangos(v) for k, v in por_familia_tipo.items()}

    zonas_equipo = [z for z in zonas if z.drops == "Equipment"]
    maximos = [z.nivel_max for z in zonas_equipo]
    resultado = {}
    for zi, zona in enumerate(zonas_equipo):
        pool = []
        for (familia, tipo), rs in rangos.items():
            if tipo == "compartida":
                continue
            elegidos = None
            compartidas = rangos.get((familia, "compartida"))
            if tipo == "arma" and compartidas:
                primer_nivel = _nivel_rango(rs[0])
                inicio = sum(1 for nivel in maximos if nivel < primer_nivel)
                debiles = [r for r in compartidas if _nivel_rango(r) < primer_nivel]
                if 0 < inicio < len(maximos) and debiles:
                    elegidos = (_rangos_zona(tipo, debiles, zi, inicio) if zi < inicio
                                else _rangos_zona(tipo, rs, zi - inicio, len(maximos) - inicio))
            if elegidos is None:
                elegidos = _rangos_zona(tipo, rs, zi, len(maximos))
            for r in elegidos:
                for it in r:
                    if it not in pool:
                        pool.append(it)
        resultado[zona.indice] = pool
    return resultado


def cargar(raiz):
    raiz = Path(raiz)
    items = leer_items(raiz)
    zonas = leer_zonas(raiz)
    return zonas, items, equipo_por_zona(items, zonas)
