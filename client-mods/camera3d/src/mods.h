// Piezas compartidas entre los mods del cliente (camara 3D, click derecho).
#pragma once

#include <windows.h>

using LogFunction = void (*)(const char* format, ...);

// Escribe bytes en el codigo o datos del juego (cambiando la proteccion de memoria por un momento).
inline bool WriteCode(DWORD address, const void* data, size_t length)
{
    DWORD oldProtect;
    if (!VirtualProtect(reinterpret_cast<void*>(address), length, PAGE_EXECUTE_READWRITE, &oldProtect))
    {
        return false;
    }

    memcpy(reinterpret_cast<void*>(address), data, length);
    VirtualProtect(reinterpret_cast<void*>(address), length, oldProtect, &oldProtect);
    FlushInstructionCache(GetCurrentProcess(), reinterpret_cast<void*>(address), length);
    return true;
}

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

// Modo de prueba: si camera3d.ini lo pide, redirige toda conexion externa al servidor de prueba.
bool InstallTestMode(LogFunction log);

// Envoltorio del dibujo de la escena principal: recibe el dibujo original y si es un cuadro extra
// (sin paso de logica desde el anterior); devuelve lo mismo que el dibujo.
using RenderFunctionType = char(__cdecl*)();
using RenderWrapper = char (*)(RenderFunctionType render, bool extraFrame);

// Cuadros extra: el dibujo no crea uniones, particulas ni efectos fuera de los pasos de logica.
bool InstallExtraFrames(LogFunction log, bool smooth);
void SetRenderDiagnostics(RenderWrapper wrapper);

// Pruebas: activa o desactiva el bloqueo en cuadros extra de una funcion (0 uniones, 1 particulas, 2 efectos).
void SetCreatorBlocked(int index, bool blocked);
void SetSmoothMotion(bool smooth);

// Diagnostico (pruebas): anota que cambia el dibujo. Necesita InstallExtraFrames.
bool InstallRenderDiagnostics(LogFunction log);

// Automatizacion (modo de prueba): comandos desde automation\commands.txt. AutomationTick se llama en
// cada paso de logica, despues de que el juego actualiza sus teclas.
bool InstallAutomation(LogFunction log);
void AutomationTick(BYTE* keyStates);

// Movimiento fluido de personajes y monstruos: antes de dibujar pone posiciones intermedias entre pasos
// de logica (stepped: hubo paso desde el cuadro anterior; t: 0 a 1 del tiempo desde el ultimo paso) y
// despues de dibujar vuelve a poner las reales.
void InterpolationBeforeRender(bool stepped, float t);
void InterpolationAfterRender();
