# Cámara 3D para el cliente MU 1.04d (Season 6 E3)

Mods del **cliente** (no del servidor) para el `main.exe` 1.04d que usamos con OpenMU, todos dentro
de `camera3d.dll`: cámara libre y click derecho inteligente.

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

## Archivos

- `src/camera3d.cpp`: la DLL (cámara e inicio de los demás mods).
- `src/rightclick.cpp`: click derecho inteligente.
- `src/mods.h`: piezas compartidas entre los mods.
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
cl /nologo /O2 /EHsc /LD /MT src\camera3d.cpp srcightclick.cpp user32.lib
cl /nologo /O2 /EHsc /MT src\addimport.cpp
cl /nologo /O2 /EHsc /MT src\injector.cpp /Fe:Camera3D.exe user32.lib
```

Integrarla en el cliente:

```
addimport.exe main.exe main_camera3d.exe
```

y distribuir `main_camera3d.exe` (renombrado a `main.exe`) junto con `camera3d.dll`.

No se suben a este repositorio (público) los binarios compilados ni el `main.exe` (es de Webzen).
El cliente completo, con estos archivos ya instalados, está respaldado en el repositorio privado
`L-G-g/mu-cliente`.

## Versión en uso (v1.1)

v1.1 agrega el click derecho inteligente; `main.exe` no cambia (sigue cargando `camera3d.dll`).
Huellas SHA-256 para verificar que un archivo es exactamente el de esta versión:

| Archivo | SHA-256 |
|---|---|
| `main.exe` original (1.04d, parche OpenMU) | `A942F2D77639C1E2138689EB69C22B05E62B7AC2DFA0B51253E57FAFA39930FE` |
| `main.exe` con cámara (`addimport` sobre el original) | `53500D16DD8DC6E1B5F222DBA8A1B2F1D40B1E15C72D075BD0344308C01A4A54` |
| `camera3d.dll` (v1.1) | `A6A328BDA3427E66EF55A82DE44FD75B3BF5AE93958516A6045EC6356A17AA42` |

`addimport` es determinista: aplicado al `main.exe` original produce siempre el mismo archivo.
