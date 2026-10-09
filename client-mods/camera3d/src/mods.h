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
