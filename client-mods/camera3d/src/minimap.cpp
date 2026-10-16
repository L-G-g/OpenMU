// Mapa del TAB: los hotspots de monstruos se marcan con un punto violeta en vez de verde.
//
// El mapa (CNewUIMiniMap) lee sus marcadores de Data\Local\<idioma>\Minimap\Minimap_<mapa>_<idioma>.bmd
// (0x0082B2B0): 100 entradas de 0x74 bytes en el objeto + 300 (tipo, x, y, giro, nombre en +0x10).
// Al dibujar (0x0082AA50) los de tipo 1 (NPC) usan la textura 0x7EC4 (Interface\mini_map_ui_npc, un
// punto verde) y los de tipo 2 (entradas) la 0x7EC3. El nombre aparece al pasar el mouse.
// Los spots de monstruos del server son marcadores de tipo 1; los hotspots terminan en "(Hotspot)".
//
// Como funciona: la llamada que dibuja el punto de un NPC (0x0082AD70, CALL 0x00638400) pasa por
// MiniMapNpcThunk. Si el nombre de esa entrada termina en "(Hotspot)", se dibuja con una copia violeta
// del punto verde. La copia se arma una vez leyendo la textura del juego de OpenGL y cambiando su
// color, asi tiene el mismo tamano y forma. 0x00638400 enlaza la textura con 0x00635CF0, que acepta
// un numero negativo como textura de OpenGL directa.

#include <windows.h>
#include <GL/gl.h>
#include <string.h>
#include "mods.h"

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD DrawMarkerFunction = 0x00638400; // dibuja un marcador del mapa (14 parametros, cdecl)
    constexpr DWORD NpcMarkerCallSite = 0x0082AD70;  // CALL DrawMarkerFunction para los de tipo 1
    constexpr DWORD BindTextureFunction = 0x00635CF0; // void BindTexture(int textura)
    int* const BoundTexture = reinterpret_cast<int*>(0x00E61E60); // ultima textura enlazada
    constexpr int NpcMarkerTexture = 0x7EC4;

    // Marcadores en CNewUIMiniMap (el "this" del dibujo, guardado en [EBP-0x3C])
    constexpr DWORD MarkersOffset = 300;
    constexpr DWORD MarkerSize = 0x74;
    constexpr DWORD MarkerNameOffset = 0x10;
    constexpr size_t MarkerNameLength = 100;
    constexpr int MarkerCount = 100;

    constexpr char HotspotSuffix[] = "(Hotspot)";

    using DrawMarkerType = void(__cdecl*)(DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD,
                                          DWORD, DWORD, DWORD, DWORD, int);
    using BindTextureType = void(__cdecl*)(int);

    LogFunction g_log = nullptr;
    BYTE* g_miniMap = nullptr;  // CNewUIMiniMap del dibujo actual
    GLuint g_hotspotTexture = 0;
    bool g_textureFailed = false;

    bool IsHotspot(int index)
    {
        if (g_miniMap == nullptr || index < 0 || index >= MarkerCount)
        {
            return false;
        }

        const char* name = reinterpret_cast<const char*>(g_miniMap + MarkersOffset + index * MarkerSize + MarkerNameOffset);
        const size_t length = strnlen(name, MarkerNameLength);
        const size_t suffixLength = sizeof(HotspotSuffix) - 1;
        return length >= suffixLength && memcmp(name + length - suffixLength, HotspotSuffix, suffixLength) == 0;
    }

    // Copia violeta del punto verde de los NPC. Se arma en el hilo del dibujo (hace falta OpenGL).
    bool CreateHotspotTexture()
    {
        reinterpret_cast<BindTextureType>(BindTextureFunction)(NpcMarkerTexture);
        GLint width = 0;
        GLint height = 0;
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_WIDTH, &width);
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_HEIGHT, &height);
        if (width <= 0 || height <= 0 || width > 256 || height > 256)
        {
            g_log("Mapa: la textura del punto de NPC no se pudo leer (%dx%d).", width, height);
            return false;
        }

        BYTE* pixels = new BYTE[width * height * 4];
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels);

        // Verde -> violeta: el verde pasa a ser azul y, algo menos, rojo; el rojo (los brillos) pasa a verde.
        for (int i = 0; i < width * height; i++)
        {
            BYTE* p = pixels + i * 4;
            const BYTE r = p[0];
            const BYTE g = p[1];
            p[0] = static_cast<BYTE>((g * 7 + r * 3) / 10);
            p[1] = r;
            p[2] = g;
        }

        glGenTextures(1, &g_hotspotTexture);
        glBindTexture(GL_TEXTURE_2D, g_hotspotTexture);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels);
        delete[] pixels;

        // El juego recuerda la ultima textura enlazada: ahora es la nuestra (como numero negativo).
        *BoundTexture = -static_cast<int>(g_hotspotTexture);
        g_log("Mapa: punto violeta de hotspots listo (%dx%d).", width, height);
        return g_hotspotTexture != 0;
    }

    void __cdecl DrawNpcMarker(DWORD texture, DWORD x, DWORD y, DWORD width, DWORD height, DWORD a6, DWORD a7,
                               DWORD a8, DWORD a9, DWORD a10, DWORD rotation, DWORD a12, DWORD a13, int index)
    {
        if (IsHotspot(index))
        {
            if (g_hotspotTexture == 0 && !g_textureFailed)
            {
                g_textureFailed = !CreateHotspotTexture();
            }

            if (g_hotspotTexture != 0)
            {
                texture = static_cast<DWORD>(-static_cast<int>(g_hotspotTexture));
            }
        }

        reinterpret_cast<DrawMarkerType>(DrawMarkerFunction)(texture, x, y, width, height, a6, a7, a8, a9, a10,
                                                              rotation, a12, a13, index);
    }

    // Reemplaza a la llamada original: guarda el CNewUIMiniMap del dibujo ([EBP-0x3C] del llamador, que
    // sigue siendo su marco) y sigue en DrawNpcMarker con los mismos parametros y la misma vuelta.
    __declspec(naked) void MiniMapNpcThunk()
    {
        __asm
        {
            mov eax, dword ptr [ebp - 0x3C]
            mov g_miniMap, eax
            jmp DrawNpcMarker
        }
    }
}

bool InstallMiniMapHotspots(LogFunction log)
{
    g_log = log;

    // CALL rel32 al dibujo de marcadores, despues de PUSH 0x7EC4; si no coincide, el main.exe es otro.
    BYTE expected[10] = { 0x68, 0xC4, 0x7E, 0x00, 0x00, 0xE8 };
    *reinterpret_cast<DWORD*>(expected + 6) = DrawMarkerFunction - (NpcMarkerCallSite + 5);
    if (memcmp(reinterpret_cast<const void*>(NpcMarkerCallSite - 5), expected, sizeof(expected)) != 0)
    {
        g_log("Mapa: la llamada del punto de NPC no coincide; hotspots desactivados.");
        return false;
    }

    BYTE call[5] = { 0xE8 };
    *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(&MiniMapNpcThunk) - (NpcMarkerCallSite + 5);
    if (!WriteCode(NpcMarkerCallSite, call, sizeof(call)))
    {
        g_log("Mapa: no se pudo instalar.");
        return false;
    }

    g_log("Mapa: hotspots en violeta listos.");
    return true;
}
