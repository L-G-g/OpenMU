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

// --- Barras de vida (healthbars.dll) ---
// camera3d.dll carga healthbars.dll si el archivo esta junto a ella y camera3d.ini no la desactiva
// ([HealthBars] Enabled=0), y llama a su funcion exportada StartHealthBars. healthbars.dll parchea sus
// propios lugares del juego, ninguno de los que parchean camera3d.dll o muauto.dll.

constexpr int HealthBarsHostVersion = 1;

// Lo que camera3d.dll le ofrece a healthbars.dll.
struct HealthBarsHost
{
    int version;
    LogFunction log;
};

// bool StartHealthBars(const HealthBarsHost* host): se llama una vez, despues de que camera3d verifico
// que el main.exe es el esperado.
using StartHealthBarsFunction = bool (*)(const HealthBarsHost* host);
constexpr const char* StartHealthBarsName = "StartHealthBars";

// --- Monstruos elite (elites.dll) ---
// camera3d.dll carga elites.dll si el archivo esta junto a ella y camera3d.ini no la desactiva
// ([Elites] Enabled=0), y llama a su funcion exportada StartElites, despues de healthbars.dll. elites.dll
// encadena las mismas llamadas del juego que healthbars.dll: lee a donde apuntan y llama a eso.

constexpr int ElitesHostVersion = 1;

// Lo que camera3d.dll le ofrece a elites.dll.
struct ElitesHost
{
    int version;
    LogFunction log;
    const char* iniPath; // camera3d.ini, para leer la seccion [Elites]
};

// bool StartElites(const ElitesHost* host): se llama una vez, despues de StartHealthBars.
using StartElitesFunction = bool (*)(const ElitesHost* host);
constexpr const char* StartElitesName = "StartElites";

// --- Nivel y resets en la ventana del personaje (resets.dll) ---
// camera3d.dll carga resets.dll si el archivo esta junto a ella y camera3d.ini no la desactiva
// ([Resets] Enabled=0), y llama a su funcion exportada StartResets, despues de elites.dll. resets.dll
// encadena la llamada del reparto de paquetes igual que elites.dll.

constexpr int ResetsHostVersion = 1;

// Lo que camera3d.dll le ofrece a resets.dll.
struct ResetsHost
{
    int version;
    LogFunction log;
    const char* iniPath; // camera3d.ini, para leer la seccion [Resets]
};

// bool StartResets(const ResetsHost* host): se llama una vez, despues de StartElites.
using StartResetsFunction = bool (*)(const ResetsHost* host);
constexpr const char* StartResetsName = "StartResets";

// --- Stacks de pociones (stacks.dll) ---
// camera3d.dll carga stacks.dll si el archivo esta junto a ella y camera3d.ini no la desactiva
// ([Stacks] Enabled=0), y llama a su funcion exportada StartStacks, despues de elites.dll. stacks.dll
// parchea un lugar del juego que no parchea ninguna otra DLL (el chequeo de apilar del inventario).

constexpr int StacksHostVersion = 1;

// Lo que camera3d.dll le ofrece a stacks.dll.
struct StacksHost
{
    int version;
    LogFunction log;
};

// bool StartStacks(const StacksHost* host): se llama una vez, despues de StartElites.
using StartStacksFunction = bool (*)(const StacksHost* host);
constexpr const char* StartStacksName = "StartStacks";

// --- Ventana de botin de las dungeons (loot.dll) ---
// camera3d.dll carga loot.dll si el archivo esta junto a ella y camera3d.ini no la desactiva
// ([Loot] Enabled=0), y llama a su funcion exportada StartLoot, despues de stacks.dll. loot.dll encadena
// la llamada del reparto de paquetes igual que elites.dll, y parchea el dibujo despues de la interfaz y el
// mouse de las ventanas, que no parchea ninguna otra DLL.

constexpr int LootHostVersion = 1;

// Lo que camera3d.dll le ofrece a loot.dll.
struct LootHost
{
    int version;
    LogFunction log;
    const char* iniPath; // camera3d.ini, para leer la seccion [Loot]
};

// bool StartLoot(const LootHost* host): se llama una vez, despues de StartStacks.
using StartLootFunction = bool (*)(const LootHost* host);
constexpr const char* StartLootName = "StartLoot";

// --- Espectros (espectros.dll) ---
// camera3d.dll carga espectros.dll si el archivo esta junto a ella y camera3d.ini no la desactiva
// ([Espectros] Enabled=0), y llama a su funcion exportada StartEspectros, despues de loot.dll. espectros.dll
// encadena el reparto de paquetes, el dibujo despues de la interfaz y el mouse de las ventanas a lo que haya
// (por ejemplo loot.dll), y reemplaza la actualizacion de teclas de las teclas rapidas (vtable 0x00D45088),
// que no toca ninguna otra DLL.

constexpr int EspectrosHostVersion = 1;

// Se llama al empezar a dibujar cada cuadro de la escena principal.
using BeforeRenderCallback = void (*)();

// Click derecho sobre un item equipable del inventario (lugar del servidor: 12 + fila * 8 + columna).
// Devuelve true si lo usa (y el click derecho no hace nada mas).
using RightClickFilter = bool (*)(int inventorySlot);

// Lo que camera3d.dll le ofrece a espectros.dll.
struct EspectrosHost
{
    int version;
    LogFunction log;
    const char* iniPath; // camera3d.ini, para leer la seccion [Espectros]
    void (*setBeforeRender)(BeforeRenderCallback callback);
    void (*setRightClickFilter)(RightClickFilter filter);
};

// bool StartEspectros(const EspectrosHost* host): se llama una vez, despues de StartLoot.
using StartEspectrosFunction = bool (*)(const EspectrosHost* host);
constexpr const char* StartEspectrosName = "StartEspectros";
