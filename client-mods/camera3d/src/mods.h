// Piezas compartidas entre los mods del cliente (camara 3D, click derecho).
#pragma once

#include "shared.h"

// Click derecho: se instala una vez; true si quedo activo.
bool InstallRightClick(LogFunction log);

// Click derecho: se llama al empezar a dibujar cada cuadro (devuelve el cursor a su lugar real).
void RightClickBeforeRender();

// Click derecho: true si el mensaje de la ventana se tiene que ignorar (durante un arrastre simulado).
bool RightClickFilterMessage(UINT message);

// FPS: reemplaza el limite de 25 cuadros por segundo por el de camera3d.ini; true si quedo activo.
bool InstallFps(LogFunction log);

// camera3d.ini [Video] Smooth (1 por defecto): movimiento interpolado entre pasos de logica.
bool SmoothMotionEnabled();

// Cuadros extra: el dibujo no crea uniones, particulas ni efectos fuera de los pasos de logica.
bool InstallExtraFrames(LogFunction log, bool smooth);
void SetRenderDiagnostics(RenderWrapper wrapper);

// Pruebas: activa o desactiva el bloqueo en cuadros extra de una funcion (0 uniones, 1 particulas, 2 efectos).
void SetCreatorBlocked(int index, bool blocked);
void SetSmoothMotion(bool smooth);

// Mapa del TAB: los marcadores de hotspots (nombre terminado en "(Hotspot)") se dibujan en violeta.
bool InstallMiniMapHotspots(LogFunction log);

// MU Helper: con camera3d.ini [MuHelper] SeguirConInventario=1 (por defecto) el inventario no lo frena.
bool InstallMuHelperInventory(LogFunction log);

// Herramientas de prueba: carga muauto.dll si esta junto a camera3d.dll (los jugadores no la tienen).
// TestToolsTick se llama en cada paso de logica, despues de que el juego actualiza sus teclas.
void LoadTestTools(HMODULE module, LogFunction log);
void TestToolsTick(BYTE* keyStates);

// Barras de vida: carga healthbars.dll si esta junto a camera3d.dll y camera3d.ini no la desactiva.
void LoadHealthBars(LogFunction log);

// Monstruos elite: carga elites.dll si esta junto a camera3d.dll y camera3d.ini no la desactiva.
void LoadElites(LogFunction log);

// Nivel y resets en la ventana del personaje: carga resets.dll si esta junto a camera3d.dll y camera3d.ini no la
// desactiva.
void LoadResets(LogFunction log);

// Stacks de pociones: carga stacks.dll si esta junto a camera3d.dll y camera3d.ini no la desactiva.
void LoadStacks(LogFunction log);

// Ventana de botin de las dungeons: carga loot.dll si esta junto a camera3d.dll y camera3d.ini no la desactiva.
void LoadLoot(LogFunction log);

// Espectros: carga espectros.dll si esta junto a camera3d.dll y camera3d.ini no la desactiva.
void LoadEspectros(LogFunction log);

// Espectros: lo que se llama al empezar a dibujar cada cuadro (camera3d.cpp) y el filtro del click derecho sobre un
// item equipable del inventario (rightclick.cpp).
void SetEspectrosBeforeRender(BeforeRenderCallback callback);
void EspectrosBeforeRender();
void SetRightClickFilter(RightClickFilter filter);
void SetEspectrosKeyTick(KeyTickCallback callback);

// Mapa del TAB completo: carga tabmap.dll si esta junto a camera3d.dll y camera3d.ini no la desactiva.
void LoadTabMap(LogFunction log);
void SetTabMapKeyTick(KeyTickCallback callback);

// Movimiento fluido de personajes y monstruos: antes de dibujar pone posiciones intermedias entre pasos
// de logica (stepped: hubo paso desde el cuadro anterior; t: 0 a 1 del tiempo desde el ultimo paso) y
// despues de dibujar vuelve a poner las reales.
void InterpolationBeforeRender(bool stepped, float t);
void InterpolationAfterRender();

// Objetos sueltos (cuervo del Dark Lord, mascotas nuevas): se interpolan al dibujarlos (RenderObject).
// InterpolationSetFrame avisa si se esta dibujando la escena del juego, si hubo paso de logica y t.
void InterpolationSetFrame(bool rendering, bool stepped, float t);
bool InstallLooseObjectInterpolation(LogFunction log);

// Estelas (uniones): los puntos de la cinta se dibujan entre su lugar anterior y el actual.
void InterpolationSetSmoothTrails(bool smooth);
void TrailsBeforeRender(bool stepped, float t);
void TrailsAfterRender();
