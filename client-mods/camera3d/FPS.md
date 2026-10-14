# Subir los FPS del cliente 1.04d: análisis

## Cómo funciona hoy (main.exe 1.04d)

El bucle de cada cuadro es `FUN_004d9cf0`:

1. **Lógica a paso fijo de 40 ms (25 por segundo).** El bucle
   `for (acc = [0x00E8CB50]; acc >= 40; acc -= 40)` hace por cada paso:
   - lee teclas (`0x00791020`);
   - corre la lógica de la escena (`0x004D8FD0` en la escena principal): mover personajes, monstruos,
     objetos, efectos, partículas e interfaz;
   - incrementa contadores (`0x00E8CB20` y otros).
2. **Dibujo**, una vez por cuadro: `0x004D95E0` en la escena principal, que incluye la cámara
   (`0x004D84E0`). Después `SwapBuffers`.
3. **Límite de 25 FPS** en `0x004DA3A1..0x004DA3E4`. Mide cuánto duró el cuadro con `GetTickCount`;
   si fueron menos de 40 ms, hace `Sleep(40 - duración)` y cuenta el cuadro como de 40 ms.
   Después suma esa duración al acumulador de la lógica.

O sea que el juego **ya separa la lógica (25 Hz fijos) del dibujo**: si se dibuja más seguido, la
lógica sigue corriendo 25 veces por segundo. Quitar el límite es fácil, porque son tres constantes
`0x28`. Ese cambio solo no alcanza, por estos dos problemas:

### Problema 1: entre dos pasos de lógica no se mueve nada

Las posiciones, los ángulos y los cuadros de animación cambian solo en los pasos de lógica. A 60 FPS
se dibujaría 2 o 3 veces seguidas exactamente la misma escena: el mundo seguiría viéndose a 25 Hz.
Lo que sí mejora sin más trabajo:
- el cursor;
- la interfaz;
- girar y hacer zoom con la cámara (el mod de cámara corre en el dibujo).

### Problema 2: algunas funciones de dibujo cambian el estado del juego

Ejemplo concreto, en `0x00506260`, que dibuja los items tirados en el piso:
- en cada dibujo suma un valor fijo a su ángulo (`+0x110`), y por eso giran;
- en cada dibujo crea partículas y efectos con `rand()` (`0x007711C0`, `0x0074CBE0`).

A 60 FPS los items girarían 2,4 veces más rápido y habría 2,4 veces más partículas. El cliente
abierto MuMain (más abajo) se encontró con lo mismo, y lo resolvió multiplicando cada incremento por
`FPS_ANIMATION_FACTOR = 25 / FPS`, en cientos de lugares del código fuente. Hacer eso parcheando el
binario no es viable.

## Opciones

### A. Mod sobre el main.exe actual (mantiene cámara 3D y click derecho)

Por etapas, todo dentro de `camera3d.dll` y configurable (por ejemplo `fps=60`, `fps=144` o
`fps=0` para usar el V-Sync):

1. **Limitador nuevo.** Se reemplaza el bloque `0x004DA3A1..0x004DA3E4` por una llamada a la DLL:
   - mide el tiempo con `QueryPerformanceCounter`, porque `GetTickCount` tiene 15,6 ms de
     resolución y daría tirones;
   - espera hasta el FPS elegido, con `timeBeginPeriod(1)` más una espera activa corta;
   - devuelve los milisegundos al acumulador, guardando las fracciones de milisegundo.

   Resultado: cursor, interfaz y cámara a 60+ FPS.
2. **Cuadros "extra" sin efectos secundarios.** Cuando en un cuadro no corrió ningún paso de
   lógica, se marca como cuadro extra. En esos cuadros:
   - las funciones que crean efectos, partículas, uniones, sprites y punteros no hacen nada;
   - los giros que se suman en el dibujo (como el de los items del piso) se escalan.

   Cada caso se encuentra y se prueba uno por uno. Es la parte con más riesgo de detalles visuales
   raros.
3. **Interpolación (la que da la sensación de 60 FPS de verdad).** Antes de cada paso de lógica se
   guarda el estado anterior. En el dibujo se mezclan el estado anterior y el actual según el tiempo
   que pasó (`acc / 40`), y después se deja todo como estaba. Se interpolan:
   - posición;
   - ángulo;
   - cuadro de animación;
   - la cámara, que sigue al personaje.

   Datos encontrados:
   - personajes y monstruos: 400 × `0x598` bytes, puntero en `[administrador + 8]` (`0x0096A340`);
   - el OBJECT de cada personaje está en `+0x308`, la posición en `+0xFC` y el ángulo en `+0x108`;
   - items del piso: 40 × `0x288` en `0x087896C0`.

   Los efectos y partículas seguirían a 25 Hz, pero son cortos y se nota mucho menos. La imagen
   queda un paso de lógica atrás: hasta 40 ms de retraso visual, lo normal en esta técnica.

Esfuerzo:
- **Etapa 1:** chica (un par de horas más pruebas).
- **Etapas 2 y 3:** varios días de análisis y pruebas en el juego, como pasó con la cámara.

Riesgos: cosas que se dibujan raro en mapas o eventos puntuales. Todo se puede apagar desde la
configuración.

### B. Cambiar al cliente MuMain (sven-n/MuMain, el cliente abierto ligado a OpenMU)

- Ya funciona a 60+ FPS con V-Sync, con todo el código escalado por `FPS_ANIMATION_FACTOR`.
- Trae equipar con click derecho, MU Helper, extensiones de inventario y baúl, y reconexión
  automática.
- Usa el protocolo "extendido" de OpenMU (el server ya tiene esos plugins: `*ExtendedPlugIn`).

En contra:
- está basado en Season 5.2 y lo van llevando a Season 6 E3, así que todavía le falta;
- hay que compilarlo uno mismo (no publican binarios);
- nuestros mods (cámara 3D, click derecho) no aplican, y habría que rehacerlos sobre su código
  fuente, que ahí es mucho más fácil.

Es un cambio de cliente para todos.

## Recomendación

Hacer la **etapa 1 de A** (rápida, segura, y ya mejora cursor, interfaz y cámara), y en paralelo
**probar MuMain** con el server de prueba para ver qué tan completo está para Season 6. Si MuMain
sirve, es el camino a largo plazo (FPS altos de verdad y mods con código fuente). Si no, seguir con
las etapas 2 y 3 de A.

## Mediciones (2026-10-09, cliente de prueba a 60 FPS)

- Lógica: 25,0 pasos por segundo, exactos, con 58 a 60 cuadros por segundo. El limitador nuevo no
  acelera el juego.
- Diagnóstico `src/fpsdiag.cpp` (`[Debug] RenderDiff=1` o `2`):
  - **Personajes:** al dibujar solo cambian `+0x214` (float, +0,25 en cada cuadro, va de 0 a 8) y
    `+0x218` (entero de 0 a 7), probablemente el índice de la estela del arma. Los demás campos
    que toca el dibujo cambian una vez por paso de lógica, así que están bien.
  - **Memoria completa** (en cuadros sin paso de lógica):
    - lo demás son búferes temporales del dibujo (huesos y vértices), que se recalculan en cada
      cuadro;
    - y el arreglo de 1000 objetos de `0x288` bytes en `0x09778748`: sprites de un solo cuadro,
      creados por `0x007711C0` y borrados por `0x00771560`. Hay que crearlos siempre.
- El código que dibuja personajes (`0x0056F210`) llama 76 veces a `0x007711C0` (sprites), 43 a
  `0x0074CBE0` (uniones o estelas), 34 a `0x006D8F20` (efectos) y, a través de otras funciones, a
  `0x0072D060` (partículas). Uniones, partículas y efectos duran varios pasos y la lógica los mueve:
  esos son los que se multiplicaban.

## Etapa 2 (`src/frames.cpp`, compilada en `camera3d_fps4.dll`, sin probar en el juego)

Envuelve el dibujo de la escena principal (`0x004DA2DC`). Si desde el cuadro anterior no corrió
ningún paso de lógica, mientras se dibuja `0x0074CBE0`, `0x0072D060` y `0x006D8F20` vuelven
enseguida, sin crear nada. Lo hacen con un trampolín que verifica los primeros bytes de cada función.

Pendiente: encontrar quién escribe `+0x214`. Estáticamente no aparece; hay que poner un punto de
parada sobre esa dirección con el juego abierto.
