---
name: mu-test-client
description: Manejar el cliente MU de prueba (S6 1.04d con camera3d.dll + muauto.dll) contra el servidor de prueba local, por intención (estado, objetos cercanos, caminar, atacar, habilidades) en lugar de clicks a ciegas y capturas. Usar para probar cambios del servidor OpenMU (balance de monstruos, habilidades, drops) o de los mods del cliente.
---

# Cliente MU de prueba

Herramientas para abrir el cliente de prueba, entrar al juego y manejar al personaje con comandos de
alto nivel. Los comandos corren dentro del juego (`muauto.dll`), funcionan aunque la ventana no tenga
el foco y devuelven una línea `nombre: clave=valor ...` por resultado.

## Reglas de seguridad (siempre)

- Solo el **cliente de prueba** `D:\camera3d\client` y el **servidor de prueba**
  (`127.127.127.127:45405`). Nunca el cliente real de Gabi (`D:\MuPeronia\...`), nunca el servidor en
  vivo (`C:\Users\Gabi\OpenMU-master\deploy\all-in-one`, docker) ni cuentas de amigos.
- `muauto.dll` va solo en `D:\camera3d\client`. El cliente de los jugadores lleva únicamente
  `camera3d.dll`.
- `camera3d.ini` del cliente de prueba tiene que tener `[Test] Server=127.127.127.127:45405`: el modo
  de prueba redirige al servidor de prueba toda conexión no local. Antes de mandar comandos, verificar
  con `Assert-MuLocal` que el cliente solo tiene conexiones locales (si no, lo cierra).
- No manejar el cliente mientras Gabi está usando la PC. Con la PC bloqueada no hay input real ni
  capturas de pantalla; los comandos internos y `Get-MuShot` sí funcionan.
- Si otro hilo está usando el cliente o el servidor de prueba, esperar a que estén libres.

## Abrir y entrar

```powershell
. D:\camera3d\skill\auto.ps1
& D:\camera3d\skill\launch-test.ps1                    # abre main.exe sin administrador; pid en D:\camera3d\testpid.txt
& D:\camera3d\skill\login.ps1 -Account testgm          # input real: necesita la PC desbloqueada
Assert-MuLocal
```

Cuentas del servidor de prueba: la contraseña es el nombre (`testgm`, `test400`, ...). `login.ps1`
usa coordenadas para 1024x768.

## Comandos de juego

| PowerShell | Comando | Resultado |
|---|---|---|
| `Get-MuState` | `state` | `name map x y hp mp sd ag level class classraw skill skillname selected target moving attacking dead safezone action` |
| `Get-MuObjects [-Radius 15] [-Kind monster]` | `objects [RADIO]` | un objeto por línea: `index kind(monster/npc/player) kindraw model key name x y dist live dead` |
| `Get-MuNearestMonster` | | el monstruo vivo más cercano |
| `Get-MuTarget N` | `target N` | como un objeto |
| `Invoke-MuWalk X Y [-Wait]` | `walk X Y [wait]` | `result=sent`; con `-Wait`: `arrived`, `stuck`, `nopath` o `timeout`, y `x y` finales |
| `Invoke-MuAttack N [-UntilDead] [-Seconds 60]` | `attack N [untildead [SEGUNDOS]]` | `result=hit/killed/gone/timeout/herodead hits ticks x y` |
| `Invoke-MuSkill S [-Target N \| -X x -Y y]` | `skill S [N \| X Y]` | `result=sent packet=0x19.. ` o `timeout` |
| `Invoke-Mu @("peek 0x07BC4F04 4")` | `peek DIRECCION [BYTES]` | bytes en hexadecimal |

- `N` es el índice del objeto (`index` de `objects`), `S` el número de habilidad (por ejemplo 48 =
  Greater Fortitude del DK, 41 = Twisting Slash). El personaje tiene que tenerla aprendida.
- `walk` usa la búsqueda de camino del juego; si el destino está lejos, `-Wait` sigue haciendo
  clicks hasta llegar.
- `attack` hace lo mismo que mantener el click sobre el monstruo: se acerca y ataca. No funciona en
  zona segura (`safezone=1`).
- `hits` cuenta los pedidos de ataque que mandó el cliente. El cliente no recibe la vida de los
  monstruos (`hp=na`): para saber si murió, `result=killed` o `target`.
- Comandos de bajo nivel (`move`, `click`, `key`, `text`, `wait`, `shot`, ...) siguen disponibles con
  `Invoke-Mu`; ver `src/muauto/automation.cpp`.

## Ejemplo: matar el monstruo más cercano

```powershell
. D:\camera3d\skill\auto.ps1
Get-MuState
$m = Get-MuNearestMonster
if ($m) { Invoke-MuAttack $m.index -UntilDead -Seconds 60 }
Get-MuShot D:\camera3d\automation-tests\despues.png
```

## Si algo no responde

- `camera3d.log` (junto a `main.exe`) dice si se cargó `muauto.dll`, si el modo de prueba está activo
  y si los comandos de juego se instalaron ("Comandos de juego listos").
- `Invoke-Mu` falla si el cliente se cerró o no contestó en el tiempo dado.
