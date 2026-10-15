// Piezas compartidas entre camera3d.dll (la DLL de los jugadores) y muauto.dll (herramientas de
// prueba, opcional).
#pragma once

#include <windows.h>
#include <string.h>

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

// Envoltorio del dibujo de la escena principal: recibe el dibujo original y si es un cuadro extra
// (sin paso de logica desde el anterior); devuelve lo mismo que el dibujo.
using RenderFunctionType = char(__cdecl*)();
using RenderWrapper = char (*)(RenderFunctionType render, bool extraFrame);

// --- Herramientas de prueba (muauto.dll) ---
// camera3d.dll carga muauto.dll solo si el archivo esta junto a ella, y llama a su funcion exportada
// StartTestTools con lo que necesita de camera3d. Asi la DLL de los jugadores no lleva codigo de prueba
// y las dos no parchean el mismo lugar del juego.

constexpr int TestHostVersion = 1;

// Lo que camera3d.dll le ofrece a muauto.dll.
struct TestHost
{
    int version;
    LogFunction log;
    void (*setSmoothMotion)(bool smooth);
    void (*setSmoothTrails)(bool smooth);
    void (*setCreatorBlocked)(int index, bool blocked);
    void (*setRenderDiagnostics)(RenderWrapper wrapper);
};

// Lo que muauto.dll le devuelve a camera3d.dll.
struct TestTools
{
    // Se llama en cada paso de logica, despues de que el juego actualiza sus teclas (puede ser nullptr).
    void (*tick)(BYTE* keyStates);
};

// bool StartTestTools(const TestHost* host, TestTools* tools): se llama una vez, al cargar camera3d.
using StartTestToolsFunction = bool (*)(const TestHost* host, TestTools* tools);
constexpr const char* StartTestToolsName = "StartTestTools";
