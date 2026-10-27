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
| `0x00D45094` | `CNewUIHotKey` vtable `[3]`: `UpdateKeyEvent` (`0x007D3320`), las teclas rápidas; el administrador de ventanas la llama solo si ninguna ventana usó la tecla (por ejemplo el chat) | Se reemplaza: Esc (recién apretada, `0x00790F20`) cierra la ventana. Que se llame indica que se está en el juego |
| `0x005CF310` | Dibujo 3D de un item (`x, y, ancho, alto, tipo, nivel, exc, ancient, agarrado`), el del item agarrado | Dibuja los items equipados, con la preparación de `0x004D5C60` (vista `0x006363D0`, perspectiva `0x006358A0`, giro de la cámara `0x00635830`) |
| OBJECT `+0x98` | Alfa del objeto (CHARACTER `+0x308`) | Al empezar cada cuadro (`camera3d.dll`) se baja al de `Alpha` para los espectros; después de la interfaz vuelve al que tenía |

`camera3d.dll` le avisa al empezar cada cuadro y le pregunta antes de equipar un item con click derecho
(`src/rightclick.cpp`): con la ventana abierta, el item va al espectro. También le pasa las teclas después
de que el juego las actualiza y antes de que las vean las ventanas: con Shift sostenido y S recién apretada
abre o cierra la ventana y borra la S, así el inventario abierto no abre la tienda personal (no mientras se
escribe en el chat, cuando el foco está en su cuadro de texto).

## Archivos

- `src/camera3d.cpp`: la DLL (cámara e inicio de los demás mods).
- `src/rightclick.cpp`: click derecho inteligente.
- `src/mods.h`: piezas compartidas entre los mods.
- `src/fps.cpp`, `src/frames.cpp`, `src/interpolation.cpp`: FPS y movimiento fluido.
- `src/minimap.cpp`: hotspots en violeta en el mapa del TAB.
- `src/muhelper.cpp`: el MU Helper sigue con el inventario abierto.
- `src/testtools.cpp`: carga de `muauto.dll` si está junto a la DLL.
- `src/loadhealthbars.cpp`: carga de `healthbars.dll` si está junto a la DLL.
- `src/loadelites.cpp`: carga de `elites.dll` si está junto a la DLL.
- `src/loadresets.cpp`: carga de `resets.dll` si está junto a la DLL.
- `src/loadstacks.cpp`: carga de `stacks.dll` si está junto a la DLL.
- `src/loadloot.cpp`: carga de `loot.dll` si está junto a la DLL.
- `src/loadespectros.cpp`: carga de `espectros.dll` si está junto a la DLL.
- `src/shared.h`: lo que comparten `camera3d.dll`, `muauto.dll`, `healthbars.dll`, `elites.dll`,
  `resets.dll`, `stacks.dll`, `loot.dll` y `espectros.dll`.
- `src/healthbars/`: `healthbars.dll`, barras de vida de los monstruos.
- `src/elites/`: `elites.dll`, nombre y brillo de los monstruos elite.
- `src/resets/`: `resets.dll`, nivel y resets en la ventana del personaje.
- `src/stacks/`: `stacks.dll`, pociones que se juntan arrastrando hasta 255.
- `src/loot/`: `loot.dll`, ventana de need, greed y pass de las dungeons.
- `src/espectros/`: `espectros.dll`, ventana de espectros (Shift+S) y espectros transparentes.
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
cl /nologo /O2 /EHsc /LD /MT src\camera3d.cpp src\rightclick.cpp src\fps.cpp src\frames.cpp src\interpolation.cpp src\testtools.cpp src\minimap.cpp src\muhelper.cpp src\loadhealthbars.cpp src\loadelites.cpp src\loadresets.cpp src\loadstacks.cpp src\loadloot.cpp src\loadespectros.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\healthbars\healthbars.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\elites\elites.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\resets\resets.cpp user32.lib
cl /nologo /O2 /EHsc /LD /MT src\stacks\stacks.cpp user32.lib
cl /nologo /O2 /EHsc /LD /MT src\loot\loot.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\espectros\espectros.cpp user32.lib opengl32.lib
cl /nologo /O2 /EHsc /LD /MT src\muauto\muauto.cpp src\muauto\automation.cpp src\muauto\gamecommands.cpp src\muauto\testmode.cpp src\muauto\fpsdiag.cpp user32.lib
cl /nologo /O2 /EHsc /MT src\addimport.cpp
cl /nologo /O2 /EHsc /MT src\injector.cpp /Fe:Camera3D.exe user32.lib
```

Integrarla en el cliente:

```
addimport.exe main.exe main_camera3d.exe
```

y distribuir `main_camera3d.exe` (renombrado a `main.exe`) junto con `camera3d.dll`, `healthbars.dll`, `elites.dll`, `resets.dll`, `stacks.dll`, `loot.dll` y `espectros.dll`. `muauto.dll`
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
