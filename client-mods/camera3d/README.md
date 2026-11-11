# Cámara 3D para el cliente MU 1.04d (Season 6 E3)

Mods del **cliente** (no del servidor) para el `main.exe` 1.04d que usamos con OpenMU, todos dentro
de `camera3d.dll`: cámara libre, click derecho inteligente y 60 FPS (o más) con movimiento fluido.

| Control | Acción |
|---|---|
| Rueda del mouse | Acercar / alejar (500 a 2000) |
| Botón del medio + mover | Girar e inclinar |
| Click del medio sin mover | Volver a la cámara original |

## Click derecho inteligente

Con el inventario abierto, click derecho sobre un item lo mueve sin arrastrar, y el cursor queda
donde estaba:

| Situación | Click derecho sobre un item del inventario |
|---|---|
| Sin otra ventana abierta | Lo equipa en su lugar (armas, escudos, set, alas, mascotas, anillos, collares) |
| Chaos Machine / mezclas, baúl, comercio, tienda personal | Lo pasa a esa ventana |
| Tienda de un NPC | Lo vende |

Click derecho sobre un item equipado lo desequipa, y sobre un item de la otra ventana lo devuelve al
inventario. Los consumibles (pociones, etc.) siguen usándose como siempre.

`src/rightclick.cpp` envuelve la actualización de teclas del juego (llamada en `0x004D9D59` a
`0x00791020`). En el tick en que detecta el click derecho simula, en ese mismo tick, el arrastre que
haría el jugador: para cada paso pone el mouse y el botón izquierdo, y procesa la interfaz
(`0x00815880` eventos de mouse y `0x00815B90` actualización, sobre `[0x09867090]`). Después deja el
mouse y los botones como estaban y borra el click derecho para el resto del juego (también las
banderas de la ventana `0x08793381..83`, como hace el inventario; si no, el personaje lanza su
habilidad y devuelve el item agarrado, por ejemplo mientras espera la respuesta de una venta).

Ventanas que reconoce por su vtable: inventario `0x00D46F6C`, mezclas `0x00D46AB8`, baúl
`0x00D489A0`, comercio `0x00D48BB4`, tienda de NPC `0x00D47750`, tienda personal `0x00D47314`.

## FPS: 60 o más, sin acelerar el juego

El juego original dibuja a 25 cuadros por segundo: su lógica corre a pasos fijos de 40 ms y al
terminar cada cuadro espera hasta completar los 40 ms. `src/fps.cpp` reemplaza esa espera por un
limitador configurable (con `QueryPerformanceCounter`). La lógica sigue a 25 pasos por segundo.

Dibujar más seguido aceleraba cosas que el juego avanza al dibujar, no en la lógica.
`src/frames.cpp` envuelve el dibujo de las tres escenas (login, selección de personaje y juego). En
los cuadros "extra" (sin paso de lógica desde el anterior):

- **Objetos que crea el dibujo:** no se crean uniones, partículas ni efectos (`0x0074CBE0`,
  `0x0072D060`, `0x006D8F20`).
- **Contadores:** se les devuelve el valor a los contadores que el dibujo usa como reloj (por
  ejemplo, el de los textos que parpadean).
- **Sprites:** se reactivan los sprites de un solo cuadro que creó la lógica (brillos como el del
  buff del DK).
- **Marcas de ventanas:** se repone la marca de "mostrar descripción" de la barra de habilidades.
- **Azar:** `rand` repite los valores del cuadro anterior para cada lugar que lo llama, así brillos y
  chispas cambian 25 veces por segundo.

Además, en todos los cuadros:

- **Animaciones del dibujo** (alas, partes del personaje, `0x00545180`): avanzan según la duración
  del cuadro.
- **Cámara del login:** su recorrido avanza con la lógica y se muestra interpolado.

**Movimiento fluido** (`src/interpolation.cpp`): en cada cuadro se dibuja un punto intermedio entre
el paso de lógica anterior y el actual, y después de dibujar se vuelven a poner los valores reales.
La imagen queda 40 ms atrasada. Lo que cambia entre cuadros por fuera de la lógica (paquetes del
server, como un cambio de mapa) se adopta sin interpolar. Se interpola:

- posición, ángulo y cuadro de animación de personajes, monstruos y NPCs;
- efectos y proyectiles;
- mascotas que vuelan y pájaros;
- objetos sueltos que pasan por `RenderObject` (`0x005EBBB0`), como el cuervo del Dark Lord y las
  mascotas nuevas;
- los puntos de las estelas de armas y skills.

Configuración en `camera3d.ini`, junto a la DLL:

```
[Video]
FPS=60          ; 25 o menos: como el juego original
ShowFps=1       ; contador en el título de la ventana
Smooth=1        ; movimiento fluido
SmoothTrails=1  ; estelas fluidas
```

`camera3d.log` anota cada 5 segundos los FPS, los pasos de lógica, el peor cuadro y los tirones.
El análisis completo y las mediciones están en `FPS.md`.

## Mapa del TAB: hotspots en violeta

En el mapa que se abre con TAB, los marcadores de los spots de monstruos que son hotspots (nombre que
termina en `(Hotspot)`, ver `client-mods/minimap-spots`) se dibujan con un punto violeta en vez del
verde de los NPC. El resto se ve igual que en el juego.

`src/minimap.cpp` envuelve la llamada que dibuja el punto de un NPC en el mapa (`0x0082AD70`, CALL
`0x00638400` con la textura `0x7EC4`, dentro del dibujo de `CNewUIMiniMap` en `0x0082AA50`). Los
marcadores se leen de `Minimap_<mapa>_<idioma>.bmd` (`0x0082B2B0`) en el objeto `+300`, 100 de `0x74`
bytes (tipo, x, y, giro, nombre en `+0x10`); el objeto está en `[EBP-0x3C]` del dibujo. Para un
hotspot se dibuja con una copia violeta del punto verde, armada una vez leyendo esa textura de OpenGL;
el enlace de texturas del juego (`0x00635CF0`) acepta un número negativo como textura de OpenGL directa.

## MU Helper con el inventario abierto

En el juego original el MU Helper se frena al abrir el inventario. Con `camera3d.dll` sigue atacando con
el inventario abierto; se frena igual que siempre con su botón, la tecla Inicio o Esc.

```
[MuHelper]
SeguirConInventario=1   ; 0: el inventario frena el helper, como el juego original
```

El que lo frena es el cliente, no el servidor: el paso del helper (`0x0095DC90`, cada 200 ms) le pide al
servidor que lo frene (`C1 BF 51 01`) si está visible el inventario (ventana `0x0D`) o la configuración del
helper (ventana `0x4F`, tecla Z), y el helper se apaga cuando el servidor contesta. `src/muhelper.cpp`
reemplaza por dos NOP el salto del inventario (`0x0095DE51`, `JNZ 0x0095DE68`); la configuración del
helper lo sigue frenando.

Otros lugares del cliente que también lo frenan y no cambian: la zona segura (el mismo paso del helper),
el cambio de mapa (`0x006408F0`), el menú de Esc (`0x007D3320`) y el botón del menú principal
(`0x00868470`).

## Skills de arma sin el arma

En PeroniaMU las armas no dan skills: las skills de arma (Defense, Falling Slash, Lunge, Uppercut, Cyclone,
Slash, Triple Shot, Power Slash, Force Wave, Killing Blow, Beast Uppercut, Phoenix Shot) se aprenden con las
quests de skills. El cliente original solo las deja usar si el arma equipada las trae; con `camera3d.dll` se
usan con cualquier arma o sin arma, siempre que estén en la lista de skills que manda el servidor.

```
[WeaponSkills]
Enabled=1   ; 0: las skills de arma necesitan el arma, como el juego original
```

`src/weaponskills.cpp` cambia tres lugares del cliente (una skill de arma tiene `+0x54 == 1` en la tabla de
skills `[0x08128ABC]`, `0x58` bytes por skill):

- `0x004EAC90` (`bool __thiscall (skill)`): devuelve true para una skill de arma solo si el item de una mano
  (`[0x08128AC4] +0x1240` o `+0x12AB`) la tiene. Lo usan el icono de la skill (gris), el lanzamiento y el
  árbol de maestría. Se reemplaza: true si la skill de arma está en la lista de skills del personaje, si no lo
  original.
- `0x005AD1A2` (`JNZ 0x005AFF59`): en el lanzamiento de los guerreros (`0x005AD040`), sin arma en la mano
  derecha o con un bastón solo siguen unas pocas skills. Las skills de arma aprendidas siguen.
- `0x005B554A` y `0x005B557B`: las llamadas que lanzan la skill con el item de cada mano (`0x0059B580`,
  `0x005A0A30`) solo la lanzan si está en la lista de skills del item (`+0x25` cantidad, `+0x26` números).
  Reciben una copia del item con la skill elegida en su lista.

Lo que no cambia: montado en Uniria, Dinorant o Fenrir, Defense a Slash y algunas skills del Rage Fighter
siguen sin poder usarse, como en el juego original.

## Barras de vida de los monstruos (healthbars.dll)

Sobre los monstruos dañados se dibuja una barra chica de tamaño fijo: fondo oscuro con borde y relleno
rojo según la vida que les queda. No aparece con la vida llena, al morir ni en un monstruo que
reaparece. Van en otra DLL, `healthbars.dll`, que `camera3d.dll` carga si está en la misma carpeta
(`src/loadhealthbars.cpp`); se desactivan con `camera3d.ini`:

```
[HealthBars]
Enabled=1       ; 0: no se carga healthbars.dll
```

El servidor (plugin "Show Object Health" de OpenMU) manda `C1 06 FA [número del objeto, 2 bytes big
endian] [vida en 1/250, de 1 a 250]` cuando un monstruo recibe daño sin morir y cuando uno dañado entra a
la vista. El `main.exe` original lo ignora: su reparto de paquetes (`0x006639D0`) manda los códigos
mayores a `0xF9` al caso por defecto, que devuelve 1 sin anotar nada ni desconectar. Así los clientes sin
`healthbars.dll` (o con un `camera3d.dll` anterior) no cambian.

`src/healthbars/healthbars.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x0065FD79` | CALL al reparto de paquetes `0x006639D0` (código, paquete, largo, cifrado) | `0xFA` se queda en la DLL (vida por número de objeto del servidor, `+0x7E` del personaje) y no llega al juego. Con `0x13` (entran a la vista), `0x14` (salen) y `0x17` (muere) se borra la vida de esos números y el paquete sigue al juego |
| `0x004D9C86` | CALL a `0x00637770` (empieza la interfaz: proyección 2D en píxeles) en la escena principal, después de elegir el objeto bajo el mouse | Se guardan las matrices 3D de OpenGL, se llama a la original y se dibujan las barras: quedan sobre el mundo y debajo de la interfaz |

Para cada monstruo (`+0x30C` vivo, `+0x24` no muerto, `+0x320` tipo 2) con vida conocida menor a la
llena se proyecta a la pantalla el punto `+0x404` (la posición, ya interpolada durante el dibujo) más la
altura de su caja (`+0x3E8`, máximo en z) y un margen. Los puntos detrás de la cámara no se dibujan. El
estado de OpenGL se guarda y se devuelve entero, porque el juego recuerda qué tiene activado.

## Monstruos elite (elites.dll)

Los monstruos elite de OpenMU (por ejemplo el "Shadow Elite" de Lost Tower, que suelta el Scroll of
Emperor de la primera quest) usan el modelo y el número de su original, así que el cliente original los
muestra igual que a los normales. Con `elites.dll` se llaman "<nombre> Elite" (al pasar el mouse dice
"Shadow Elite") y brillan en violeta transparente. `camera3d.dll` la carga si está en la misma carpeta,
después de `healthbars.dll` (`src/loadelites.cpp`):

```
[Elites]
Enabled=1               ; 0: no se carga elites.dll
Effect=1                ; número del efecto que el servidor le pone a los elites
Color=0.6,0.2,1.0,1.5   ; rojo, verde, azul (de 0 a 1) e intensidad del cromo
Tint=0.35               ; intensidad del tinte parejo (0: solo el cromo)
BossEffect=2            ; número del efecto que el servidor le pone a los jefes
BossColor=0.6,0.2,1.0,2.5 ; color de los jefes (violeta, con el cromo más fuerte)
BossTint=0.6            ; opacidad del teñido de los jefes (0: sin teñido, solo el brillo)
```

Los jefes (por ejemplo la Ice Queen de la primera dungeon, que usa el modelo de las Ice Queen normales)
se marcan igual con otro efecto: el plugin "Dungeon" le pone al jefe el efecto `BossEffectNumber` (2 por
defecto). Con `elites.dll` se llama "<nombre> Boss" ("Ice Queen Boss") y brilla con `BossColor`.
El brillo se suma a lo que hay detrás, y sobre un modelo claro como el de la Ice Queen queda casi
blanco. Por eso a los jefes antes del brillo se les dibuja la textura teñida del color de `BossColor`,
mezclada con alfa `BossTint` (`RenderBody` con `0x02` y alfa menor a 1: el juego activa la mezcla con
alfa, `0x00635FD0`, sin sumar luz). Los elites normales no cambian.

El servidor (plugin "Elite Monster Effect", `EffectNumber` 1 por defecto) le pone a cada elite un efecto
y lo manda con `C1 07 MagicEffectStatus [activo] [número del objeto, 2 bytes big endian] [efecto]` al
aparecer y cuando entra a la vista. El cliente S6 no dibuja nada para ese efecto en un monstruo (se
probaron 1, 2, 4, 56 y 81), así que sin `elites.dll` el elite se ve como uno normal.

`src/elites/elites.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x0065FD79` | CALL al reparto de paquetes (ya redirigido por `healthbars.dll` si está) | Se lee a dónde apunta y se encadena. Con `0x07` y el efecto de `Effect` se marca el número de objeto y al nombre del monstruo (`+0x38` del personaje) se le agrega " Elite"; con `0x13`, `0x14` y `0x17` se desmarca |
| `0x005DE260` | Dibujo del cuerpo de un objeto (OBJECT, ..., ..., pasada). `camera3d.dll` reemplaza a quien lo llama (`0x005EBBB0`) | Se salta al principio de la función (los primeros 10 bytes van a un trampolín). Después del dibujo original, si el objeto es el cuerpo de un elite vivo (`OBJECT` = personaje `+0x308`), se dibuja dos veces más con `RenderBody` (`0x005494C0`, `__thiscall` sobre el `BMD`) y el color de `Color` en la luz del modelo (`BMD +0x48`): color parejo y brillo (`0x41`, intensidad `Tint`) y textura, cromo y brillo (`0x46`) |

El modelo se busca igual que al empezar `0x005DE260` (`0x004CDA10` y `0x00969AD0` con el tipo `OBJECT
+0x30`). El brillo se suma a lo que hay detrás (se ve transparente) y el cromo se mueve con el tiempo.
`0x44` (cromo y brillo sin textura) no dibuja nada en este cliente. Con el mouse sobre el monstruo,
`0x005DDAF0` agranda el dibujo para el contorno de selección (escala `0x00E61638`); el brillo se dibuja
con la escala en 1.

## Stacks de pociones de 255 (stacks.dll)

Con la actualización "Potion Stacks Of 255" de OpenMU las pociones se apilan hasta 255 (la cantidad es
la durabilidad del item, un byte). Al comprar o levantar del piso el servidor ya las junta, pero el
cliente original decide por su cuenta si deja soltar una poción sobre otra igual: pide que cada una
tenga menos de 3 y si no, no manda el pedido de mover. Con `stacks.dll` se pueden juntar arrastrando
hasta 255. `camera3d.dll` la carga si está en la misma carpeta, después de `elites.dll`
(`src/loadstacks.cpp`):

```
[Stacks]
Enabled=1       ; 0: no se carga stacks.dll
```

`src/stacks/stacks.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x007DD490` | Chequeo de apilar: `bool __thiscall` (inventario, ITEM en el cursor, ITEM de abajo), `RET 8`. Lo usan el color de la casilla bajo el cursor (`0x007DB0E0`) y el inventario al soltar (`0x008380F0`, que con verdadero manda el pedido de mover con `0x005BD850`) | Se salta al principio de la función (los primeros 6 bytes van a un trampolín). Para dos pociones iguales del grupo 14 (números 0-8, 35-40, 46-50 y 85-87) del mismo nivel dice que sí si las dos tienen menos de 255; el resto va al chequeo original |

Campos del item (`ITEM`): `+0x00` tipo (grupo × 512 + número), `+0x02` opciones (nivel en los bits
3-6), `+0x16` durabilidad. El servidor junta lo que entra y deja el resto aparte (4 sobre 253 queda en
255 y 2). El chequeo original no deja apilar las pociones de escudo (35-37) y para las otras pide menos
de 3 (10, 30 o 50 en algunos items de eventos). Sin `stacks.dll` el cliente sigue igual; con un
servidor sin la actualización el pedido de mover se manda y el servidor lo rechaza (el item vuelve a su
lugar).

El servidor solo junta stacks dentro del inventario. Soltar una poción del baúl sobre otra del inventario
pasa por el mismo chequeo (como antes con menos de 3): el pedido se manda, el servidor lo rechaza y el
cliente deja de mostrar el item del inventario hasta volver a entrar. Sobre una poción del baúl el cliente
no deja soltar.

## Mapa del TAB completo (tabmap.dll)

Con `tabmap.dll` el TAB muestra el mapa entero de la zona en el centro de la pantalla, girado como la
cámara, con los NPC, las entradas, los spots de monstruos (hotspots en violeta) y el personaje. Al pasar el
mouse se ve el nombre del marcador y las coordenadas. Un click en el mapa hace caminar al personaje hasta ese
punto (la ruta y el destino se ven en el mapa) y sigue caminando aunque se cierre el mapa; click derecho en
el mapa frena la caminata. Fuera del mapa el mouse funciona como siempre. Sin `tabmap.dll` el TAB abre el
mapa original. `camera3d.dll` la carga si está en la misma carpeta (`src/loadtabmap.cpp`):

```
[TabMap]
Enabled=1       ; 0: el mapa original
Size=0.9        ; tamaño del mapa (parte del alto libre de la pantalla, 0.3 a 1)
Rotate=1        ; 1: girado como la cámara; 0: derecho (cuadrado, más grande)
Opacity=0.6     ; opacidad del mapa (se ve el juego por debajo)
MarkerOpacity=0.8 ; opacidad de los puntos de NPC y spots
```

`src/tabmap/tabmap.cpp` reemplaza el dibujo (`[0]`, `0x0082AA50`) y el mouse (`[2]`, `0x0082B5A0`) de la
vtable de la ventana del mapa (`CNewUIMiniMap`, `0x00D468B0`, ventana `0x48`); TAB y Esc la siguen abriendo
y cerrando como siempre. Usa la imagen que ya cargó el juego para la zona (`Data\World<n>\mini_map.ozt`,
textura `0x7EBF`; `+0x34BC` dice si se cargó) con la misma orientación que el dibujo original (`0x00638130`:
u = baldosa Y / 256, v = baldosa X / 256) y el giro de la cámara actual. Los marcadores son los de
`Minimap_<mapa>_<idioma>.bmd` que leyó el juego (100 de `0x74` bytes desde `+0x12C`).

Para caminar hace lo mismo que el click en el suelo (`0x005B741A` y siguientes): pone la baldosa destino
(`0x081C038C`, `0x081C0388`), busca el camino con `PathFinding2` (`0x00542D20`, en `+0x274` del personaje)
y llama a `SendMove` (`0x005A3C70`), que manda el mismo pedido de caminar que un click. Ese camino llega
hasta 15 baldosas, así que primero se busca la ruta entera sobre las paredes del mapa (`0x082C6AA0`: `0x04`
no se camina, `0x08` sin suelo) y se manda por tramos de 10 baldosas, uno cada vez que el personaje se
frena, en el paso de lógica (lo llama `camera3d.dll` después de actualizar las teclas). La caminata se corta
al llegar, al cambiar de mapa, al morir o si el jugador mueve al personaje de otra forma.

## Monturas voladoras (flight.dll)

Con el Dinorant o alas de tercer nivel equipadas (y las capas de Emperor y Overrule), Shift+V (o `/volar` en el
chat) despega: el personaje sube, vuela por encima de paredes, árboles y casas (no por encima del vacío) y va x1,5
más rápido. Las alas de primer y segundo nivel no vuelan (configurable en el plugin: factor 0).
Volando no se puede atacar ni usar skills, y los monstruos no lo atacan. Otra vez Shift+V aterriza, solo sobre
suelo caminable. Quien decide es el servidor (plugin "Flight" de OpenMU: Dinorant o alas, mapas comunes, no en
combate ni en la zona segura); `camera3d.dll` carga `flight.dll` si está en la misma carpeta (`src/loadflight.cpp`).

Con alas (sin el Dinorant) las poses se arman ajustando los huesos en cada cuadro, sin tocar archivos del juego:
al despegar se agacha y salta (sube rápido, estirado con los brazos en V), quieto en el aire queda parado,
moviéndose va inclinado (60°) con los brazos hacia atrás a lo largo del cuerpo y las piernas juntas, con un vaivén
lento, y al aterrizar baja planeando hasta la mitad de la altura, frena (cuerpo hacia atrás, piernas y brazos
adelante) y termina de bajar. Desde el despegue hasta el aterrizaje no se dibujan las armas ni el escudo (`+0x1C0`
y `+0x1E4` en -1 solo mientras se dibuja la escena). Con otra montura (Fenrir, Dark Horse,
Uniria) la montura no se dibuja mientras vuela y vuelve al aterrizar. Con el Dinorant queda la pose de jinete de
Icarus.

Huesos: `0x005440B0` es `BMD::Animation` (thiscall: `BoneTransform` (`0x07BC08A0`), cuadro (`+0x8C`), cuadro
anterior (`+0x90`), acción anterior (`+0x14`), `Angle` (`+0x108`), `HeadAngle` (`+0x114`), ...). Arma la matriz
3x4 de cada hueso (padre × local). Después de llamarla, para el personaje que vuela con alas (`Angle - 0x108 - 0x308`),
se gira cada miembro con todo lo que cuelga de él alrededor de su articulación para apuntarlo hacia una dirección
y se inclina el cuerpo desde la pelvis. Adelante es `(sen giro, -cos giro, 0)` (el paso `0x00564DD0` avanza por
`(0, -1, 0)` girado). El modelo del jugador es `0x48B` (`*(*(0x00E8C200) + 8) + 0x48B × 0x104`): 60 huesos
(`+0x22`), cada uno de `0x8C` bytes en `+0x2C` (nombre, `+0x20` padre, `+0x22` vacío): pelvis 2, muslos 3/10,
pantorrillas 4/11, pies 5/12, columna 17/18, cuello 19, cabeza 20, claviculas 25/34, brazos 26/35, antebrazos 27/36,
manos 28/37. La acción quieto la elige `0x0054EA80` (con Fenrir `0x7A`-`0x7D`); `0x005518F0` es la de atacar.

Las alas (el modelo de `+0x208`) se animan con la misma función justo después del cuerpo, con un `OBJECT` propio de su modelo: se atribuyen al último personaje animado y se elige su cuadro: un solo aletazo grande al despegar, aleteos lentos volando hacia adelante, dos aletazos al frenar y la animación normal quieto en el aire.

```
[Flight]
Enabled=1           ; 0: no se carga flight.dll
SpeedFactor=1.5     ; solo con servidores que no mandan el factor de cada uno
Height=350          ; altura de vuelo (una baldosa son 100)
ClimbPerSecond=250  ; velocidad al despegar y aterrizar
Key=V               ; letra, número o código de tecla en hexadecimal (0x56); 0 sin tecla
KeyShift=1          ; 1: Shift + la tecla
```

El servidor manda `C1 06 FE [número del objeto, 2 bytes big endian] [factor de velocidad en %, 0 no vuela]`
cuando alguien despega, aterriza o cambia de alas, y cuando uno que vuela entra a la vista; el `main.exe` original
lo ignora. Un 1 (servidores viejos) usa `SpeedFactor`.


`src/flight/flight.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x00564B20` | `float Velocidad(personaje)`: 12 caminando, 15 alas/Uniria/Dinorant, 16-19 caballo/Fenrir, x0,5 con hielo | Salto al principio (6 bytes al trampolín): x `SpeedFactor` si vuela. La usa el paso de movimiento (`0x00564DD0`) de todos los personajes |
| `0x00542D20` | Búsqueda de camino (x, y, destino, `PATH*` = personaje `+0x274`, distancia, pared) | Una baldosa se pisa si su atributo (`0x082C6AA0`) es menor que "pared" (2): volando se pasa 8, se cruza `0x04` y no `0x08`. También mueve a los otros jugadores |
| `0x0065FD79` | CALL al reparto de paquetes (encadenado) | `0xFE` marca quién vuela; `0x14` (salen de la vista) los desmarca |

La altura se suma solo para dibujar: el juego pone a los personajes sobre el terreno (+30 con Dinorant, +90 en
Tarkan e Icarus) en muchos lugares, así que `camera3d.dll` avisa antes y después del dibujo de la escena (después
del movimiento fluido, `src/frames.cpp`) y `flight.dll` sube la posición (`+0x404`) y después devuelve el valor
exacto. La cámara sigue al personaje subido. Necesita FPS mayor a 25 (con 25 no se envuelve el dibujo).

La montura y las mascotas son objetos aparte ("bugs": 10 `OBJECT` de `0x288` bytes en `0x08791550`, vivo en
`+0x04`, posición en `+0xFC`, dueño en `+0x1D8` = `OBJECT` del personaje, `+0x308`). Copian la posición del
dueño en el paso de lógica (`0x00501790`) y no al dibujar (`0x00503770`), así que se suben y se devuelven igual.

| Dirección | Qué es | Uso |
|---|---|---|
| `0x00501790` | Paso de un bug (`OBJECT*`, flag): el Dinorant (bug `0x143`) usa las acciones de vuelo (1, 3, 5, 7, alas abiertas) si el mundo (`0x00E61E18`) es 8 o 10 | Salto al principio (9 bytes al trampolín): si el dueño vuela, el mundo es 10 durante la llamada |
| `0x00542D20` (final) | Escribe el camino: `PATH+2` cantidad de puntos (con el de inicio), `+3` las x, `+0x12` las y (15 como máximo), `+0`/`+1` en 0 | Volando se escribe una línea recta (Bresenham, pasos de 8 direcciones) en vez del A*, que zigzaguea; si la línea pisa una baldosa `0x08` se usa el A* con pared 8 |

| `0x0054F710` | Acción del personaje al caminar: con el Dinorant, en los mundos 8 y 10 no pone la de correr montado (sube y baja); con alas (`+0x208`) fuera de la zona segura usa la de volar (`0x22`/`0x23`) cuando el contador de correr (`+0x25`) llega a 40 | Salto al principio: si vuela, el mundo es 10 durante la llamada y, con alas, el contador queda en 40. Al despegar y aterrizar con alas se la llama quieto (aletea) y al llegar arriba o al suelo se llama a `0x005518F0` (acción quieto) |
| `0x08967FE8` (200), `0x097622F0` (100) | Efectos (`OBJECT`, mismo dueño en `+0x1D8`) | Se suben y se devuelven como la montura |
| `0x09778748` (1000), `0x087C5708` (500, `0xA18` bytes: vivo `+0x00`, dueño `+0x54`) | Partículas con estela (`0x007711C0`) y uniones (`0x00735DC0` borra las de un dueño), por ejemplo el remolino del buff del Elf Soldier: se dibujan con las posiciones anteriores guardadas en el paso de lógica | Mientras el dueño vuela no se dibujan (vivo en 0 durante el dibujo) |

`flight.dll` exporta `float FlightHeroLift()` (la altura dibujada del personaje propio); `camera3d.dll` la usa, si
está, para agrandar el terreno visible y la distancia de dibujo (+1 vez por cada 500 de altura), así volando no
aparecen baldosas negras en los bordes.
| `0x005428B0` | Avance por el `PATH` (personaje, girar): curva por cada 4 puntos (`PATH+1` es el tramo, 0 a 3), gira (`+0x418`) hacia ese objetivo y llega a menos de 20 | Volando gira hacia el último punto (`0x00540C30`, suavizado con `0x005410A0`/`0x00540EA0` como el original), el punto actual pasa al más cercano y llega a menos de 20 del final: va derecho en vez de seguir la escalera |

Volando, el suelo dibujado sigue al terreno de a poco (3 por segundo), así que el personaje no sube y baja con
cada desnivel.

Volando no se puede despegar en la zona segura y al entrar a una se aterriza solo (lo decide el servidor).

## Herramientas de prueba (muauto.dll)

Las herramientas de prueba están en otra DLL, `muauto.dll`, que **no** forma parte del cliente de
los jugadores. `camera3d.dll` la carga solo si el archivo está en la misma carpeta (`src/testtools.cpp`),
al iniciar y antes de cualquier conexión, y le pasa lo que necesita (log, ajustes de prueba del
movimiento fluido, diagnóstico del dibujo). `muauto.dll` no parchea nada que parchee `camera3d.dll`:
el paso de lógica le llega por la actualización de teclas que ya envuelve el click derecho. Sin
`muauto.dll` el cliente funciona igual que v1.2 (las opciones `[Test]` y `[Debug]` no hacen nada).

Todo se activa además con `camera3d.ini`:

- **Modo de prueba** (`[Test] Server=127.127.127.127:45405`, `src/muauto/testmode.cpp`): redirige
  toda conexión no local al servidor de prueba.
- **Automatización** (`[Test] Automation=1`, `src/muauto/automation.cpp`): comandos desde
  `automation\commands.txt`, como mover, click, teclas, texto, capturas por OpenGL y ráfagas de
  cuadros.
- **Login sin mouse real** (con la automatización): las pantallas de login y de selección de
  personaje leen el mouse con su propio lector (`0x00E7C228`, actualizado por `0x00407C70` desde
  `0x004D9DCC`), que usa el cursor de Windows y no ve nada si la ventana no está activa. Después de su
  actualización se le pone el mouse de la automatización, así `click`, `text` y `key` funcionan también
  ahí sin robar el foco (`Invoke-MuLogin` en `skill/auto.ps1`).
- **Comandos de juego** (con la automatización, `src/muauto/gamecommands.cpp`): manejar al personaje
  por intención, sin coordenadas de pantalla. Cada resultado es una línea `nombre: clave=valor ...` en
  `done.txt`.

  | Comando | Qué hace | Resultado |
  |---|---|---|
  | `state` | Estado del personaje | `map x y hp mp sd ag level class skill selected target moving attacking dead safezone action` |
  | `objects [RADIO]` | Objetos vivos cerca (15 baldosas por defecto) | una línea `objects:` y una `object:` por objeto: `index kind type model key name x y dist live dead` (`type`: número de monstruo o NPC, como en el servidor) |
  | `walk X Y [wait]` | Caminar a la baldosa, como un click en el terreno | `result=sent`, o con `wait`: `arrived`, `stuck`, `nopath` o `timeout` y la posición final |
  | `attack N [untildead [SEGUNDOS]]` | Ataque normal al objeto `N`, como un click sostenido sobre el monstruo (se acerca y ataca) | `result=hit`, `killed`, `gone`, `timeout` o `herodead`, `hits` (ataques mandados) |
  | `skill S [N \| X Y]` | Habilidad número `S` sobre el objeto `N` (si está lejos, se acerca), una baldosa o uno mismo | `result=sent` con el paquete, o `timeout` |
  | `target N` | Estado del objeto `N` | como una línea de `objects` |
  | `peek DIRECCIÓN [BYTES]` | Bytes de la memoria del juego, para investigar | hexadecimal |
  | `enter N` | En la selección de personaje, entrar con el personaje `N` (lo mismo que el botón Connect, `0x004D6A60` con `0x00E60B4C`) | `slot name` |
  | `party N` | Invitar al jugador `N` a la party, como el botón Party de la ventana de comandos (`0x0078F100`, C1 40) | `key name` |

  El cliente no recibe la vida de los monstruos, así que `hp=na`.

  Cómo funciona: en cada paso de lógica, antes de la lógica del personaje (`0x005B5FC0`, la que
  atiende los clicks sobre el mundo), se deja el estado como si el jugador hiciera el click: el objeto
  elegido bajo el mouse, los botones apretados y, para caminar o lanzar sobre una baldosa, la baldosa
  "bajo el mouse". El juego hace el resto con su propia lógica (camino, acercarse, atacar, pedidos al
  servidor). Para saber qué pasó se cuentan los paquetes que arma el juego.

  | Dirección | Qué es | Uso |
  |---|---|---|
  | `0x005B5FC0` | Lógica del personaje propio (clicks sobre el mundo), llamada en `0x004D9577` | Corre después de la actualización de teclas; los comandos preparan su estado antes |
  | `0x005B81E0` | Elige el objeto bajo el mouse (al dibujar) | Por eso la selección se repone en cada paso |
  | `0x005B4940` | Uso de habilidades con click derecho | Habilidad elegida: personaje `+0x32` (índice en la lista) |
  | `0x00542D20` | Búsqueda de camino | (la usa el juego) |
  | `0x005A3C70` | Pedido de caminar | (la usa el juego) |
  | `0x005DA250` | Baldosa del terreno bajo el mouse | Las llamadas en `0x005B741A` (click en el terreno) y `0x005AAFDF` (destino de habilidad) pasan por un gancho que devuelve la baldosa del comando |
  | `0x00404B90` | Empieza cada paquete (tipo, código) | Gancho que cuenta los paquetes: `0xD4` caminar, `0x11` ataque, `0x19`/`0x1E`/`0xDB`/`0x1D` habilidades |
  | `0x00E61728` / `0x00E6172C` / `0x00E61730` / `0x00E61734` | Item, NPC, monstruo o jugador y objeto del mapa bajo el mouse | Selección del comando |
  | `0x00E61738` | Objetivo fijo (1 ataque, 2 habilidad) | Se borra al terminar, para que el ataque automático no siga |
  | `0x00E61790` | Objeto hacia el que va el personaje | `target` de `state` |
  | `0x081C03B4` / `0x00E616D0` | Pasos desde el último click atendido / mínimo entre clicks | Distinguir "sin camino" de "todavía no acepta clicks" |
  | `0x08793384` / `0x08793381` | Botones izquierdo / derecho: apretado recién, soltado, sostenido | Click sostenido |
  | `0x08793248` | Punto del terreno bajo el mouse | Centro de la baldosa del comando |
  | `0x08406AA0` | Alturas del terreno (256×256 floats) | Altura de ese punto |
  | `0x00E7C224` | Personajes: `[+8]`, 400 de `0x598` bytes | `objects` |
  | `0x08128AC8` | Atributos del personaje propio | `state` |
  | `0x08128ABC` | Tabla de habilidades (`0x58` bytes, nombre al principio) | Nombres |

  Campos del personaje (`CHARACTER`): `+0x0E` zona segura, `+0x13` clase, `+0x24` muerto, `+0x32`
  habilidad elegida, `+0x38` nombre, `+0x78` caminando, `+0x7E` número en el servidor, `+0x84` número de monstruo o NPC, `+0xAC`/`+0xB0`
  baldosa, `+0x308` objeto (`+0x30C` vivo, `+0x31A` acción, `+0x320` tipo: 1 jugador, 2 monstruo,
  4 NPC, `+0x338` modelo, `+0x404` posición). Atributos: `+0x0B` clase, `+0x0E` nivel, `+0x22`/`+0x26`
  vida, `+0x24`/`+0x28` mana, `+0x2A`/`+0x2C` SD, `+0x40`/`+0x42` AG, `+0x78` lista de habilidades.
- **Diagnóstico** (`[Debug] RenderDiff=1|2`, `src/muauto/fpsdiag.cpp`): anota qué cambia el dibujo.
- **Scripts** de `skill/` (ver `skill/SKILL.md`):
  - abrir el cliente de prueba sin administrador;
  - login automático;
  - control por automatización y comandos de juego (`Get-MuState`, `Get-MuObjects`, `Invoke-MuWalk`,
    `Invoke-MuAttack`, `Invoke-MuSkill`, ...);
  - lectura de memoria para medir velocidades;
  - grillas de capturas.

## Cómo funciona la cámara

`camera3d.dll` se carga dentro de `main.exe` y cambia tres cosas en memoria. Antes de tocar nada
verifica byte a byte que el ejecutable sea el esperado; con otro `main.exe` no hace nada y lo anota
en `camera3d.log`.

| Dirección | Qué es | Cambio |
|---|---|---|
| `0x004D84E0` | Función de cámara de la escena principal | La llamada en `0x004D960F` pasa por `CameraHook`: fija el giro antes, y después acerca/aleja e inclina la cámara alrededor del centro del personaje |
| `0x005DB8D7` | Recorte del terreno con ángulo fijo (-45°) | Usa el giro actual de la cámara |
| `0x005DB130` | Área de terreno visible (4 esquinas) | La llamada en `0x005DBFE3` pasa por `TerrainAreaHook`, que agranda el área según el zoom, el giro y la inclinación |

Variables del juego usadas: ángulos de cámara `0x087933D0`, posición `0x087933DC`, distancia
`0x00E8CB6C`, nivel de cámara `0x00E8CB1C`, distancia de dibujo `0x00E61E3C`, personaje
`0x07BC4F04` (+`0x404` posición), ventana `0x00E8C578`.

Las direcciones salen del análisis del `main.exe` con Ghidra (scripts en `ghidra-scripts/`).

## Nivel y resets en la ventana del personaje (resets.dll)

En la ventana del personaje (tecla C) el nivel se ve siempre como número, también después de la última
quest (el cliente original pone "Master" y una línea de guiones en la experiencia), y al lado van los
resets: `Level: 400 | Resets: 12`. Van en otra DLL, `resets.dll`, que `camera3d.dll` carga si está en la
misma carpeta, después de `elites.dll` (`src/loadresets.cpp`):

```
[Resets]
Enabled=1                 ; 0: no se carga resets.dll
Format=" | Resets: %d"    ; lo que va después del nivel (un solo %d; el texto entero entra en 31 letras)
```

El servidor (OpenMU con el reset activado) manda `C1 07 FB [resets, 4 bytes big endian]` justo después
de la información del personaje (`C3 F3 03`), al entrar al juego y después de un reset. El `main.exe`
original lo ignora (códigos mayores a `0xF9`). Sin el paquete (servidor sin resets) se ve solo el nivel.

`src/resets/resets.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x0065FD79` | CALL al reparto de paquetes (ya redirigido por `healthbars.dll` y `elites.dll` si están) | Se encadena. `0xFB` se queda en la DLL; con `C3 F3 03` se olvidan los resets hasta el `0xFB` siguiente |
| `0x0077F9B7` | `JNZ` en los textos de la ventana (`0x0077F990`): si la clase es de tercera clase (`0x005878E0`) escribe "Master" (texto 1745) | Pasa a `JMP`: siempre "Level: %d" (texto 200) y la experiencia (texto 201) |
| `0x0077FA10` | CALL a `sprintf` (`0x0050D100`) del texto del nivel, de 32 bytes en `[EBP-0x128]` | Escribe el nivel y le agrega `Format` con los resets |

## Botón de reset (resetbutton.dll) y estilo de PeroniaMU (src/ui)

Cuando el personaje puede resetear (nivel, zen, límite de resets e items: lo mismo que pide `/reset`), aparece
un botón **Reset** abajo en el centro de la pantalla, arriba de la barra de habilidades. Tiene el estilo nuevo
de PeroniaMU: marco de metal oscuro con bisel (como los botones de la entrada a Devil Square), letras doradas con
borde oscuro y un resplandor dorado detrás que late, con rayos que giran despacio y un reflejo que cruza el botón.
El click manda el chat `/reset`. `camera3d.dll` la carga si está en la misma carpeta, después de `tabmap.dll`
(`src/loadresetbutton.cpp`):

```
[ResetButton]
Enabled=1      ; 0: no se carga resetbutton.dll
Bottom=420     ; borde de abajo del botón, en coordenadas de 640x480 (la barra empieza en 429)
Width=120
Height=24
Text=RESET
FontSize=13.5  ; alto de las letras en coordenadas de 480 (0: la fuente negrita del juego, más chica)

[PeroniaUi]
; piedra de la cara de los botones (un .OZJ del cliente; sin comentario en la misma linea)
StoneTexture=Data\Interface\newui_msgbox_back.OZJ
; marco original de los botones (el de Devil Square, 3 estados uno debajo del otro); vacio: marco dibujado
ButtonTexture=Data\Interface\newui_btn_empty_big.OZT
ButtonStates=3
```

El servidor (plugins "Reset button", que revisa cada segundo, y "Show Reset Button (resetbutton.dll)") manda
`C1 05 FF 00 [0 o 1]` cuando cambia y, mientras se puede resetear, cada 3 segundos. El código `0xFF` queda para
las ventanas y botones de PeroniaMU (subcódigo `0x00`: el botón de reset). El `main.exe` original lo ignora. Con
`C3 F3 03` (entrar al juego, otro personaje, después de un reset) el botón se esconde hasta el próximo aviso, y
después de un click se esconde 4 segundos esperando la respuesta.

El estilo está en `src/ui/peroniaui.h` / `peroniaui.cpp`, que se compila dentro de cada DLL que lo usa:
`MetalButton` (el marco original de los botones de Devil Square, `newui_btn_empty_big.OZT`: un TGA con 4 bytes de cabecera, dibujado en tres partes para que las puntas no se deformen; sin ese archivo, un marco dibujado con la cara de piedra de la interfaz original: lee el `.OZJ` del cliente, que es un JPEG con una cabecera adelante, lo decodifica con WIC y lo repite a su tamaño real; sin el archivo queda metal liso), `Glow` (resplandor), `Shine` (reflejo) y `ButtonText` (texto dorado con la fuente del
juego). La idea es pasar después a este estilo las ventanas y botones de las otras DLL (`loot.dll`,
`espectros.dll`, etc.). Las llamadas que encadena son las mismas que `espectros.dll`: reparto de paquetes
(`0x0065FD79`), dibujo después de la interfaz (`0x004D9CCC`), mouse de las ventanas (`0x00860998`) y el salto en
"mouse sobre la interfaz" (`0x00860910`).

## Puntos libres de más de 65535 (points.dll)

El cliente original guarda los puntos libres en un número de 16 bits: no sabe de más de 65535, y al
repartir los descuenta él mismo, así que con más puntos llegaba a 0 aunque al personaje le quedaban.
`points.dll` muestra en la ventana del personaje (tecla C) los puntos reales (`Point: 72000`) y mantiene
en el cliente hasta 65535, así se pueden seguir repartiendo con el `+` hasta el último. `camera3d.dll` la
carga si está en la misma carpeta, después de `tabmap.dll` (`src/loadpoints.cpp`):

```
[Points]
Enabled=1                 ; 0: no se carga points.dll
```

El servidor (OpenMU) manda `C1 08 FF 20 [puntos, 4 bytes big endian]` cuando el personaje tiene más de
65535 puntos (o tenía, antes de repartir): después de la información del personaje (`C3 F3 03`), de subir
de nivel (`C3 F3 05`) y de repartir puntos. El `main.exe` original lo ignora (códigos mayores a `0xF9`). `0xFE` es de `flight.dll`.
Sin `points.dll` el cliente muestra hasta 65535 y, al llegar a 0, los puntos que quedan se reparten con
`/add` o se ven al volver a entrar.

`src/points/points.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x0065FD79` | CALL al reparto de paquetes (ya redirigido por las otras DLL si están) | Se encadena. `0xFF 0x20` se queda en la DLL (los otros subcódigos de `0xFF` siguen): pone `min(puntos, 65535)` en los puntos del cliente (`+0x74` de `[0x08128AC8]`) y recuerda los reales; con `C3 F3 03` se olvidan hasta el paquete siguiente |
| `0x0077FC06` | CALL a `sprintf` (`0x0050D100`) del texto "Point: %d" (texto 217), de 128 bytes en `[EBP-0x238]` | Escribe los puntos reales más lo que el cliente ya descontó desde el último paquete |

La experiencia de más de 65535 no necesita DLL: el `main.exe` 1.04d entiende `C3 9C` (experiencia
de 4 bytes) y el servidor la manda en lugar de varios `C3 16` de hasta 65535.
## Ventana de botín de las dungeons (loot.dll)

En una dungeon de OpenMU (plugin "Dungeon") los drops se reparten con una tirada: cada jugador de la
party elige need, greed o pass. Sin esta DLL se elige escribiendo `/need N`, `/greed N` o `/pass N`
(N es el número de la tirada que avisa el servidor). Con `loot.dll` las tiradas abiertas aparecen en
una ventana chica abajo en el centro de la pantalla, arriba de los avisos dorados, del chat y de la
barra de abajo, así no tapan al personaje (hasta 4, apiladas hacia arriba), con el item, el tiempo que
queda y los botones Need, Greed y Pass. Need está apagado si el item no es para la clase del personaje.
El botón manda el mismo comando de chat. `camera3d.dll` la carga si está en la misma carpeta, después
de `stacks.dll` (`src/loadloot.cpp`):

```
[Loot]
Enabled=1               ; 0: no se carga loot.dll
Bottom=300              ; borde de abajo de la ventana, en coordenadas de 640x480 (la barra empieza en 429)
```

El servidor (vista "Show Loot Roll View (loot.dll)", `ShowLootRollViewPlugIn`) manda paquetes que el
`main.exe` original ignora (el reparto devuelve 1 para los códigos mayores a `0xF9`):

- `C1 30 FC 00 [tirada, 2 bytes big endian] [segundos] [puede need: 0 o 1] [item, 40 bytes de texto]`:
  empezó una tirada para este jugador.
- `C1 07 FC 01 [tirada, 2 bytes big endian] [ganó: 0 o 1]`: el jugador eligió o la tirada terminó. Si
  ganó, el servidor le agrega el item al inventario enseguida (`C1 22 [lugar] [item]`).

`src/loot/loot.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x0065FD79` | CALL al reparto de paquetes (ya redirigido por `healthbars.dll`, `elites.dll` y `resets.dll` si están) | Se encadena. `0xFC` se queda en la DLL: abre o cierra la fila de la tirada |
| `0x004D9CCC` | CALL `0x004D5EC0` en la escena principal, después de dibujar la interfaz (`0x008609C0`), entre `BeginBitmap` (`0x00637770`) y `EndBitmap` (`0x00637870`) | Pasa por la DLL: dibuja la ventana (proyección 2D del juego, píxeles con el origen abajo) y después llama a la original |
| `0x00860998` | CALL `0x00815880` (eventos del mouse de las ventanas, `__thiscall` sobre el administrador `[0x09867090]`) en la actualización de la interfaz (`0x00860970`) | Con el mouse sobre la ventana no llama a la original (ninguna otra ventana lo recibe; pone en 0 la ventana con el mouse, `+0x38`) y procesa el click al soltar el botón (`0x00790F00`) |
| `0x00860910` | "Mouse sobre la interfaz" (`__thiscall` sobre el sistema de ventanas: el administrador tiene una ventana con el mouse en `+0x38`). Lo consultan la lógica del personaje (`0x005B5FC0`: caminar, atacar), la elección del objeto bajo el mouse (`0x005B81E0`) y el chat (`0x004D8FD0`) | Se reemplaza con un salto: devuelve 1 con el mouse sobre la ventana; si no, lo mismo que la original |
| `0x005BDE40` | Envío del chat (`void __cdecl(const char*)`, C1 00), el del Enter de la ventana de chat | Manda `/need N`, `/greed N` o `/pass N` |
| `0x08793728` | Espera del chat: `0x005BDE40` descarta el mensaje si es mayor a `0x32` (y uno igual al anterior si es mayor a 0); baja en cada paso | Los pedidos esperan en una cola hasta que el chat se puede mandar |

Los textos se dibujan con el texto del juego (`0x00E8203C`: `SetFont` `0x00420120`, `RenderText`
`0x00420150`; orden 1 izquierda, 2 derecha, 3 centro), como el mapa del comando M.

Item ganado: al recibir un item en el inventario (`0x22`, `0x0064B440`) el juego muestra "<item>
Obtained" con el nombre del item del piso que el jugador quiso levantar por última vez: el índice
`0x081C0420` en la tabla de items del piso (`0x081F6C00`, `0x350` bytes cada uno: tipo `short` en
`+0x02`, nivel en los bits 3 a 6 de `+0x04`). Un item ganado no se levantó del piso, así que el mensaje
nombraba otro (o el Kris, el item 0,0). Para los `0x22` que siguen a los `FC 01` ganados (uno por
tirada, hasta 10 s después del último; dos tiradas pueden terminar juntas), la DLL pone el índice en 0 y en ese lugar de la tabla el tipo del item recibido (`0x0058AA80`, la misma
función que usa el juego con los datos del item del paquete) y su nivel; llama al reparto y deja todo
como estaba. Así el mensaje dice el nombre correcto.

## Espectros (espectros.dll)

Con el plugin "Spectres" de OpenMU cada personaje puede invocar espectros de guerrero, mago y elfa (bots
que lo siguen, lo buffean y cazan con él). Sin esta DLL se manejan con el comando `/espectro`. Con
`espectros.dll` Shift+S abre una ventana a la izquierda de la pantalla con una pestaña por espectro:
invocar o retirar, resetear, los 12 items equipados (dibujados en 3D; un click lo quita y lo devuelve al
inventario), las estadísticas y los porcentajes con los que reparte sus puntos (+ y -, y Aplicar cuando
suman 100). Con la ventana abierta, un click derecho sobre un item equipable del inventario se lo equipa
al espectro de la pestaña elegida. Los espectros se ven transparentes. `camera3d.dll` la carga si está en
la misma carpeta, después de `loot.dll` (`src/loadespectros.cpp`):

```
[Espectros]
Enabled=1               ; 0: no se carga espectros.dll
Alpha=0.5               ; opacidad de los espectros (1: como un jugador)
Left=10                 ; esquina de la ventana, en coordenadas de 640x480
Top=40
```

El servidor (vista "Show Spectres View (espectros.dll)", `ShowSpectresViewPlugIn`) manda paquetes que el
`main.exe` original ignora:

- `C1 05 FD 01 [objeto, 2 bytes big endian]`: el objeto es un espectro (se repite cada 2 segundos).
- `C2 [largo] FD 00` y una entrada por espectro: invocado, nombre, nivel, resets, puntos, estadísticas,
  porcentajes y los 12 items (tipo, los 12 bytes del item y su descripción).

La ventana contesta con `/espectro <g|m|e>`, `/espectro reset`, `/espectro equipar <clase> <lugar del
inventario>`, `/espectro quitar <clase> <casillero>`, `/espectro puntos ...` y `/espectro panel` (al abrirla).

`src/espectros/espectros.cpp`:

| Dirección | Qué es | Uso |
|---|---|---|
| `0x0065FD79`, `0x004D9CCC`, `0x00860998` | Reparto de paquetes, dibujo después de la interfaz y eventos del mouse de las ventanas (ver `loot.dll`) | Se encadenan a lo que haya. `0xFD` se queda en la DLL |
| `0x00860910` | "Mouse sobre la interfaz" | Salto a la DLL: 1 con el mouse sobre la ventana; si no, salta a lo que había (`loot.dll`) o hace lo mismo que la original |
| `0x00E8C578` | Ventana principal del juego | Shift+S (estados de la tabla de teclas que `camera3d.dll` le pasa en cada paso, después de `0x00791020`) abre o cierra la ventana y Esc la cierra, solo si `GetFocus()` es la ventana principal (no escribiendo en el chat). La tecla se borra de la tabla mientras siga apretada: S sola con el inventario abierto abre la tienda personal |
| `0x005CF310` | Dibujo 3D de un item (`x, y, ancho, alto, tipo, nivel, exc, ancient, agarrado`), el del item agarrado | Dibuja los items equipados, con la preparación de `0x004D5C60` (vista `0x006363D0`, perspectiva `0x006358A0`, giro de la cámara `0x00635830`) |
| OBJECT `+0x98` | Alfa del objeto (CHARACTER `+0x308`) | Al empezar cada cuadro (`camera3d.dll`) se baja al de `Alpha` para los espectros; después de la interfaz vuelve al que tenía |

`camera3d.dll` le avisa al empezar cada cuadro, le pasa el teclado en cada paso de lógica y le pregunta antes de equipar un item con click derecho
(`src/rightclick.cpp`): con la ventana abierta, el item va al espectro.

## Escenas con los espectros del refugio (cinematicas.dll)

En el refugio de los espectros no hay duelo por cercanía: un click sobre un espectro rival empieza una
escena. La pantalla se va a negro, la cámara pasa a primera persona mirando al espectro (el personaje
propio no se dibuja), aparecen bandas de cine arriba y abajo y el espectro cuenta su parte de la historia
en una caja de diálogo, letra por letra (un click la muestra entera). Abajo, "Duelo" (solo si el servidor
dice que se puede ahora; si no, el espectro dice por qué) y "Volver más tarde" (o Esc). Al elegir, la
pantalla se va a negro y la cámara vuelve a la normal. `camera3d.dll` la carga si está en la misma
carpeta, después de `espectros.dll` (`src/loadcinematicas.cpp`):

```ini
[Cinematicas]
Enabled=1               ; 0: no se carga cinematicas.dll
Frontal=1               ; escenas de la historia: la cámara se pone de frente al NPC, sin importar desde dónde se habló
FrontAngle=0            ; grados que se suman al giro del NPC para esa cámara (por si un modelo mira torcido)
```

El servidor (la misma vista `ShowSpectresViewPlugIn`) marca a los rivales con `C1 06 FD 05 [objeto]` cada
pocos segundos; un click sobre uno manda `/espectro hablar [objeto]`. El diálogo llega en
`C2 [largo] FD 04 [objeto] [se puede pelear] [clase] [bit 0: con montura]` con tres textos Latin-1 terminados en 0 (nombre, lo
que dice y por qué no se puede pelear); "Duelo" manda `/espectro duelo [objeto]`. Los demás paquetes `FD`
siguen a `espectros.dll`.

La cámara: `camera3d.dll` llama a `cinematicas.dll` después de calcular la suya en cada cuadro; durante la
escena la DLL pone la posición (a 230 unidades del espectro, del lado del personaje, a la altura de los
ojos) y los ángulos (inclinación -90 = horizonte, giro hacia el espectro), y `camera3d.dll` no aplica su
zoom ni su inclinación y agranda el terreno visible. El click: en el paso de lógica (`camera3d.dll`), si
se aprieta el botón izquierdo con un rival marcado bajo el mouse (`0x00E61730`), se borran los botones del
juego (`0x08793384`) para que el personaje no camine ni ataque.
## Quests de skills (quests.dll)

Con `quests.dll` los NPC que enseñan skills (plugin "Skill quests" del servidor) muestran un signo flotando
sobre la cabeza: `!` dorado si hay una quest para aceptar, `?` dorado si la quest está lista para entregar y
`?` gris si está en curso. Un click en el signo (no en el NPC, que sigue abriendo su ventana de siempre) abre
la ventana de la quest con los objetivos, el progreso y la recompensa, y el botón Aceptar o Entregar. Las
quests activas se ven en un recuadro de seguimiento a la derecha. `camera3d.dll` la carga si está en la misma
carpeta (`src/loadquests.cpp`):

```
[Quests]
Enabled=1       ; 0: sin signos ni ventana
TrackerLeft=470 ; lugar del seguimiento (640x480)
TrackerTop=70
TrackerCollapsed=1 ; 1: el seguimiento queda plegado (solo el borde dorado) y se despliega con el mouse encima
MarkSize=1      ; tamaño de los signos (0.5 a 3)
HeadMargin=70   ; altura del signo sobre la cabeza del NPC (unidades del mundo)
```

El servidor manda `C2 FF 10` (signos de los NPC cercanos, cada 3 segundos), `C2 FF 11` (seguimiento) y
`C2 FF 12` (ventana de la quest); el código `0xFF` es el de la interfaz de PeroniaMU. La DLL contesta con el
comando de chat `/quest`. `src/quests/quests.cpp` encadena el reparto de paquetes, el comienzo de la interfaz
(`0x004D9C86`, como `healthbars.dll`: ahí proyecta los signos, sobre el mundo y debajo de las ventanas), el
dibujo después de la interfaz y el mouse de las ventanas. Los botones usan el estilo de `src/ui/peroniaui`.

## Archivos

- `src/camera3d.cpp`: la DLL (cámara e inicio de los demás mods).
- `src/rightclick.cpp`: click derecho inteligente.
- `src/mods.h`: piezas compartidas entre los mods.
- `src/fps.cpp`, `src/frames.cpp`, `src/interpolation.cpp`: FPS y movimiento fluido.
- `src/minimap.cpp`: hotspots en violeta en el mapa del TAB.
- `src/muhelper.cpp`: el MU Helper sigue con el inventario abierto.
- `src/weaponskills.cpp`: las skills de arma aprendidas se usan sin el arma.
- `src/testtools.cpp`: carga de `muauto.dll` si está junto a la DLL.
- `src/loadhealthbars.cpp`: carga de `healthbars.dll` si está junto a la DLL.
- `src/loadelites.cpp`: carga de `elites.dll` si está junto a la DLL.
- `src/loadresets.cpp`: carga de `resets.dll` si está junto a la DLL.
- `src/loadstacks.cpp`: carga de `stacks.dll` si está junto a la DLL.
- `src/loadloot.cpp`: carga de `loot.dll` si está junto a la DLL.
- `src/loadespectros.cpp`: carga de `espectros.dll` si está junto a la DLL.
- `src/loadcinematicas.cpp`: carga de `cinematicas.dll` si está junto a la DLL, y su cámara.
- `src/loadtabmap.cpp`: carga de `tabmap.dll` si está junto a la DLL.
- `src/loadflight.cpp`: carga de `flight.dll` si está junto a la DLL.
- `src/loadresetbutton.cpp`: carga de `resetbutton.dll` si está junto a la DLL.
- `src/loadquests.cpp`: carga de `quests.dll` si está junto a la DLL.
- `src/loadpoints.cpp`: carga de `points.dll` si está junto a la DLL.
- `src/shared.h`: lo que comparten `camera3d.dll`, `muauto.dll`, `healthbars.dll`, `elites.dll`,
  `resets.dll`, `stacks.dll`, `loot.dll`, `espectros.dll` y `cinematicas.dll`.
- `src/healthbars/`: `healthbars.dll`, barras de vida de los monstruos.
- `src/elites/`: `elites.dll`, nombre y brillo de los monstruos elite.
- `src/resets/`: `resets.dll`, nivel y resets en la ventana del personaje.
- `src/stacks/`: `stacks.dll`, pociones que se juntan arrastrando hasta 255.
- `src/loot/`: `loot.dll`, ventana de need, greed y pass de las dungeons.
- `src/espectros/`: `espectros.dll`, ventana de espectros (Shift+S) y espectros transparentes.
- `src/cinematicas/`: `cinematicas.dll`, escenas de diálogo con los espectros del refugio.
- `src/tabmap/`: `tabmap.dll`, mapa del TAB completo con click para caminar.
- `src/flight/`: `flight.dll`, monturas voladoras.
- `src/resetbutton/`: `resetbutton.dll`, botón Reset cuando el personaje puede resetear.
- `src/ui/`: estilo de PeroniaMU para ventanas y botones nuevos (marco de metal, letras doradas, resplandor).
- `src/quests/`: `quests.dll`, signos de quest sobre los NPC, ventana de la quest y seguimiento.
- `src/ui/`: `peroniaui`, estilo de los botones nuevos (se compila dentro de cada DLL que lo usa).
- `src/points/`: `points.dll`, puntos libres de más de 65535 en la ventana del personaje.
- `src/muauto/`: `muauto.dll`, herramientas de prueba (modo de prueba, automatización, comandos de
  juego, diagnóstico). No se distribuye a los jugadores.
- `src/addimport.cpp`: genera una copia de `main.exe` que importa `camera3d.dll` (sección nueva
  `.cam3d` con la tabla de importaciones ampliada). Así la cámara viene integrada y no hace falta
  ningún cargador. El `main.exe` original no se modifica.
- `src/injector.cpp`: cargador alternativo (`Camera3D.exe`) que inyecta la DLL en un `main.exe` en
  ejecución. Sirve para probar sin tocar el ejecutable; acepta `--only <ruta de main.exe>`.
- `ghidra-scripts/`: scripts de análisis (búsqueda de constantes, referencias, descompilación y
  desensamblado de rangos, vtables por nombre de clase).

## Compilar

Con las Build Tools de Visual Studio (C++ x86), desde una consola "x86 Native Tools":

```
cl /nologo /O2 /EHsc /LD /MT src\camera3d.cpp src\rightclick.cpp src\fps.cpp src\frames.cpp src\interpolation.cpp src\testtools.cpp src\minimap.cpp src\muhelper.cpp src\weaponskills.cpp src\loadhealthbars.cpp src\loadelites.cpp src\loadresets.cpp src\loadstacks.cpp src\loadloot.cpp src\loadespectros.cpp src\loadcinematicas.cpp src\loadtabmap.cpp src\loadflight.cpp src\loadresetbutton.cpp src\loadquests.cpp src\loadpoints.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\healthbars\healthbars.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\elites\elites.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\resets\resets.cpp user32.lib
cl /nologo /O2 /EHsc /LD /MT src\stacks\stacks.cpp user32.lib
cl /nologo /O2 /EHsc /LD /MT src\loot\loot.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\espectros\espectros.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\cinematicas\cinematicas.cpp src\ui\peroniaui.cpp user32.lib gdi32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\tabmap\tabmap.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\flight\flight.cpp user32.lib
cl /nologo /O2 /EHsc /LD /MT src\resetbutton\resetbutton.cpp src\ui\peroniaui.cpp user32.lib gdi32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\quests\quests.cpp src\ui\peroniaui.cpp user32.lib gdi32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\points\points.cpp user32.lib
cl /nologo /O2 /EHsc /LD /MT src\muauto\muauto.cpp src\muauto\automation.cpp src\muauto\gamecommands.cpp src\muauto\testmode.cpp src\muauto\fpsdiag.cpp user32.lib
cl /nologo /O2 /EHsc /MT src\addimport.cpp
cl /nologo /O2 /EHsc /MT src\injector.cpp /Fe:Camera3D.exe user32.lib
```

Integrarla en el cliente:

```
addimport.exe main.exe main_camera3d.exe
```

y distribuir `main_camera3d.exe` (renombrado a `main.exe`) junto con `camera3d.dll`, `healthbars.dll`, `elites.dll`, `resets.dll`, `stacks.dll`, `loot.dll`, `espectros.dll`, `cinematicas.dll`, `tabmap.dll`, `flight.dll`, `resetbutton.dll`, `quests.dll` y `points.dll`. `muauto.dll`
va solo en el cliente de prueba.

No se suben a este repositorio (público) los binarios compilados ni el `main.exe` (es de Webzen).
El cliente completo, con estos archivos ya instalados, está respaldado en el repositorio privado
`L-G-g/mu-cliente`.

## Versión en uso (v1.2)

v1.2 agrega 60 FPS con movimiento fluido; `main.exe` no cambia (sigue cargando `camera3d.dll`).
Huellas SHA-256 para verificar que un archivo es exactamente el de esta versión:

| Archivo | SHA-256 |
|---|---|
| `main.exe` original (1.04d, parche OpenMU) | `A942F2D77639C1E2138689EB69C22B05E62B7AC2DFA0B51253E57FAFA39930FE` |
| `main.exe` con cámara (`addimport` sobre el original) | `53500D16DD8DC6E1B5F222DBA8A1B2F1D40B1E15C72D075BD0344308C01A4A54` |
| `camera3d.dll` (v1.2) | `05B4352F5CCB25CBE55EE096ACFF25EC4AEF222D9E93AD9725B2D143C1A5AACC` |

`addimport` es determinista: aplicado al `main.exe` original produce siempre el mismo archivo.
