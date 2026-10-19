# Simulador de progreso de jugadores

Estima cuánto tardan los jugadores en subir y resetear en PeroniaMU, y qué les cae en el camino
(excelentes, joyas, items socket, zen). Es un modelo matemático: no abre el juego. Usa las fórmulas
del servidor y lee los datos directamente del código del fork, así que si cambia la escalera de mapas,
un monstruo o un ítem, el simulador lo toma solo.

Necesita Python 3.10 o más nuevo, sin librerías extra.

```
python tools/simulador-progreso/simulador.py --exp-rate 100 --resets 360 --jugadores 20
```

`--src` es la carpeta del fork (por defecto, la del repo donde está el script). Los datos de la escalera
de mapas salen de `MapLevelLadder.cs`, que hoy está en la rama del PR #25.

Otras opciones:

- `--server server.json`: usa la config real del servidor (tasa de exp, plugin de reset, Monster Attribute
  Scaler). El archivo se genera en la PC del servidor con `exportar-config.ps1`, que solo lee la base.
- `--config archivo.json`: la configuración (ver abajo). Por defecto `config.json` de esta carpeta.
- `--traza`: muestra en qué nivel el primer jugador cambia de zona, con sus stats.
- `--evaluar NIVEL,FUE,AGI,VIT,ENE`: no simula; muestra para un personaje sin equipo cuánto tarda en
  limpiar un spot, kills por hora y exp por hora en cada zona. Sirve para comparar con una medición en
  el juego y calibrar.
- `--json archivo`: guarda el detalle de cada reset de cada jugador.

## Qué hace el personaje

1. Sube de nivel en la zona más alta donde **no muere** y donde limpiar un spot no lleva más de
   `segundos_maximos_por_spot` (`politica_zona: "mayor_viable"`). Con `"mejor_exp"` elige en cambio la
   zona que da más exp por hora. No caza en las zonas de `zonas_excluidas` (hoy Swamp of Calmness).
2. Reparte los puntos en cada nivel durante el primer reset y después cada `repartir_cada_niveles`
   niveles (y apenas resetea), primero lo que pide su equipo y el resto según `reparto`. Con más de
   `muchos_resets` resets (100) no reparte los puntos de cada nivel, solo los del reset.
3. Se pone las piezas de su clase que caen en su zona si son mejores que las que tiene
   (cada zona tira sus propios sets y armas, igual que `DefaultDropGenerator`).
4. Resetea al llegar al nivel pedido, con los puntos que da el plugin de reset.

## Qué sale del código (exacto)

- Experiencia por kill (`CalculateBaseExperience`), la tabla de experiencia, `ExpMultiplier` 2 de Swamp.
- Chance de pegar (`1 − defense rate / attack rate`, 3 % si no alcanza, y daño ×0,3).
- Daño y defensa del Dark Knight (`ClassDarkKnight.cs`), multiplicador de skill 2 + energía/1000.
- Stats de los monstruos de cada zona y nivel requerido de los warps (escalera de mapas).
- Grupos de drop: zen 50 %, ítem de la zona 30 %, excelente de la zona 0,01 %, joyas 0,1 %,
  Swamp joyas 1 %, Raklion socket 1 %. El pool de ítems de cada zona replica `GetZoneEquipment`.
- Puntos por reset (`ResetProgressionCalculator`) y el tope de 32.767 por stat del cliente.

## Qué es supuesto (calibrar)

Todo lo de la sección `combate` de `config.json`: segundos entre golpes, mobs por spot y cuántos entran
en el área, segundos para caminar al spot siguiente, cuánto cura el jugador con pociones por segundo y
el margen para decir "no muere". También la tasa de exp y la configuración del reset del servidor real.

No modela todavía: otras clases, alas, mascotas, subir ítems con joyas, opciones excelentes en el daño,
fiestas, MU Helper, eventos (Blood Castle, Devil Square), muertes y pérdida de exp, ni que dos jugadores
compitan por los mismos mobs. Los ítems que no son equipo (anillos, orbes, pociones) no entran en el pool
de la zona, así que la chance de cada pieza está un poco sobreestimada.

## Calibrar con el cliente de prueba

Con la skill `mu-test-client` (muauto) contra el servidor de prueba:

1. Poner un DK con stats conocidos (panel), ir a una zona con `/move`, atacar spots con Twisting Slash
   durante unos minutos y contar kills y tiempo.
2. Correr `--evaluar` con esos mismos stats y comparar kills por hora.
3. Ajustar `segundos_por_golpe` y `segundos_entre_spots` hasta que coincidan.

Las mediciones hechas con el cliente real contra el servidor en vivo están en `calibracion.md`.
