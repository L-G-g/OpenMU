// tabmap.dll - Mapa del TAB completo e interactivo para el cliente MU 1.04d con camera3d.dll.
//
// El mapa original del TAB (CNewUIMiniMap, vtable 0x00D468B0, ventana 0x48) dibuja una parte del mapa de la
// zona, centrada en el personaje y oscureciendo casi toda la pantalla. Con tabmap.dll se ve el mapa entero de
// la zona en el centro de la pantalla (girado como la camara), con los NPC, las entradas, los spots de
// monstruos (hotspots en violeta) y el personaje. Un click en el mapa hace caminar al personaje hasta ese
// punto; el camino sigue aunque se cierre el mapa. Click derecho en el mapa frena la caminata.
//
// Como funciona:
//   - Se reemplazan dos funciones de la vtable de la ventana: el dibujo ([0], 0x0082AA50) y los eventos del
//     mouse ([2], 0x0082B5A0). La apertura y el cierre (TAB, Esc: [3] 0x0082A9E0) siguen siendo los del juego.
//   - La imagen del mapa es la que ya cargo el juego para la zona (Data\World<n>\mini_map.ozt, textura
//     0x7EBF); +0x34BC de la ventana dice si se pudo cargar. El dibujo original (0x00638130) la pone con la
//     coordenada u de la textura = baldosa Y / 256 y la v = baldosa X / 256, girada 45 grados (el giro de la
//     camara del juego, -45). Aca se usa lo mismo, con el giro de la camara actual (0x087933D0 + 8), que
//     camera3d.dll deja cambiar.
//   - Los marcadores son los de Minimap_<mapa>_<idioma>.bmd que leyo el juego (0x0082B2B0): 100 de 0x74
//     bytes desde +0x12C de la ventana (tipo: 1 NPC o spot, 2 entrada; x, y, giro; nombre en +0x10). Iconos
//     del juego: 0x7EC4 NPC, 0x7EC3 entrada, 0x7EC2 personaje (las imagenes ocupan 35/64 de la textura).
//   - Caminar: lo mismo que hace el juego con un click en el suelo (0x005B741A y siguientes): pone la baldosa
//     destino (0x081C038C x, 0x081C0388 y), busca el camino con PathFinding2 (0x00542D20, en el camino del
//     personaje +0x274) y, si hay, pone +0x79 en 0 y llama a SendMove (0x005A3C70), que arma el mismo pedido
//     de caminar que un click. Como ese camino es corto (hasta 15 baldosas), para un destino lejano se busca
//     antes la ruta entera sobre las paredes del mapa (0x082C6AA0, 256x256: 0x04 no se camina, 0x08 sin
//     suelo) y se camina por tramos: cada vez que el personaje se frena se manda el tramo siguiente. Esto
//     corre en cada paso de logica (lo llama camera3d.dll despues de actualizar las teclas).
//   - La caminata se corta al llegar, al cambiar de mapa, al morir o si el jugador mueve al personaje de otra
//     forma (el destino del ultimo pedido no es el nuestro).
//   - Los textos se dibujan con el texto del juego (0x00E8203C: fuente, colores y RenderText 0x00420150).
//
// camera3d.ini:
//   [TabMap]
//   Enabled=1       ; 0: el mapa original
//   Size=0.9        ; tamano del mapa: parte del alto libre de la pantalla (0.3 a 1)
//   Rotate=1        ; 1: girado como la camara; 0: derecho (cuadrado, mas grande)
//   Opacity=0.92    ; opacidad de la imagen del mapa
//   MarkerOpacity=0.55 ; opacidad de los puntos de NPC y spots

#include "../shared.h"

#include <GL/gl.h>
#include <limits.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <algorithm>
#include <queue>
#include <vector>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD MiniMapVtable = 0x00D468B0;
    constexpr DWORD MiniMapRenderFunction = 0x0082AA50;  // bool __thiscall Render()
    constexpr DWORD MiniMapMouseFunction = 0x0082B5A0;   // bool __thiscall UpdateMouseEvent(): 0 si uso el mouse
    constexpr DWORD BindTextureFunction = 0x00635CF0;    // void __cdecl BindTexture(int): negativo = textura GL
    constexpr DWORD IsPressFunction = 0x00790F20;        // bool __cdecl(int tecla): recien apretada
    constexpr DWORD IsRepeatFunction = 0x00790F40;       // bool __cdecl(int tecla): sostenida
    constexpr DWORD PlaySoundFunction = 0x006D6AD0;      // __cdecl(int sonido, OBJECT*, int)
    constexpr DWORD PathFindingFunction = 0x00542D20;    // bool __cdecl PathFinding2(sx, sy, tx, ty, PATH*, float, int)
    constexpr DWORD SendMoveFunction = 0x005A3C70;       // void __cdecl SendMove(CHARACTER*, OBJECT*): pedido de caminar
    constexpr DWORD SetFontFunction = 0x00420120;        // void __thiscall CUIRenderText::SetFont(HFONT)
    constexpr DWORD RenderTextFunction = 0x00420150;     // void __thiscall (x, y, texto, ancho, alto, orden, SIZE*)
    constexpr DWORD EnableAlphaBlendFunction = 0x00635FD0; // void __cdecl(bool): mezcla con alfa (estado del juego)
    constexpr DWORD DisableAlphaBlendFunction = 0x00635F50; // void __cdecl(): sin mezcla (estado del juego)

    constexpr int MapTexture = 0x7EBF;
    constexpr int HeroTexture = 0x7EC2;
    constexpr int GateTexture = 0x7EC3;
    constexpr int NpcTexture = 0x7EC4;
    constexpr float IconUv = 0.546875f; // 35/64: parte usada de las texturas de los iconos
    constexpr int ClickSound = 0x19;
    constexpr int SortCenter = 3;       // RenderText: centrado en la caja
    constexpr int MainBarHeight = 51;   // barra de abajo de la interfaz, en 640x480

    BYTE* const RenderText = reinterpret_cast<BYTE*>(0x00E8203C); // CUIRenderText: +4 el que dibuja
    HFONT* const NormalFont = reinterpret_cast<HFONT*>(0x00E8C588);
    HFONT* const BoldFont = reinterpret_cast<HFONT*>(0x00E8C58C);
    int* const MouseX = reinterpret_cast<int*>(0x0879340C); // 640x480
    int* const MouseY = reinterpret_cast<int*>(0x08793410);
    int* const WindowWidth = reinterpret_cast<int*>(0x00E61E58);
    int* const WindowHeight = reinterpret_cast<int*>(0x00E61E5C);
    int* const CurrentMap = reinterpret_cast<int*>(0x00E61E18);
    int* const Scene = reinterpret_cast<int*>(0x00E609E8);      // 5: juego
    BYTE** const Hero = reinterpret_cast<BYTE**>(0x07BC4F04);
    const float* const CameraAngle = reinterpret_cast<const float*>(0x087933D0); // [2]: giro
    int* const TargetX = reinterpret_cast<int*>(0x081C038C);   // baldosa destino del ultimo pedido de caminar
    int* const TargetY = reinterpret_cast<int*>(0x081C0388);
    int* const ClickTicks = reinterpret_cast<int*>(0x081C03B4); // pasos desde el ultimo click atendido
    int* const ClickDelay = reinterpret_cast<int*>(0x00E616D0); // pasos minimos entre clicks
    const WORD* const TerrainWall = reinterpret_cast<const WORD*>(0x082C6AA0); // 256x256, indice y * 256 + x

    // Ventana del mapa (CNewUIMiniMap).
    constexpr DWORD MapLoaded = 0x34BC;
    constexpr DWORD Markers = 0x12C, MarkerSize = 0x74, MarkerX = 0x04, MarkerY = 0x08, MarkerName = 0x10;
    constexpr int MarkerCount = 100;
    constexpr size_t MarkerNameLength = 100;

    // Personaje (CHARACTER): +0x24 muerto, +0x78 caminando, +0x79 tipo de movimiento, +0xAC/+0xB0 baldosa,
    // +0x274 camino (PATH: +2 cantidad, +3 x[15], +0x12 y[15]), +0x308 objeto (+0x30C vivo, +0x404 posicion).
    constexpr DWORD CharDead = 0x24, CharMoving = 0x78, CharMovement = 0x79, CharTileX = 0xAC, CharTileY = 0xB0;
    constexpr DWORD CharPath = 0x274, CharObject = 0x308, CharLive = 0x30C, CharPosition = 0x404;
    constexpr DWORD PathCount = 2, PathXs = 3, PathYs = 0x12;

    constexpr WORD Blocked = 0x04 | 0x08; // no se camina / sin suelo
    constexpr int DefaultWall = 2;        // lo que pasa el click en el suelo a PathFinding2
    constexpr int SegmentLength = 10;     // baldosas por tramo (PathFinding2 llega hasta 15)
    constexpr int StuckTicks = 5 * 25;    // pasos sin avanzar antes de abandonar

    using RenderType = bool(__fastcall*)(BYTE* window, void* unused);
    using MouseType = bool(__fastcall*)(BYTE* window, void* unused);
    using BindTextureType = void(__cdecl*)(int texture);
    using IsPressType = bool(__cdecl*)(int key);
    using PlaySoundType = int(__cdecl*)(int sound, void* object, int flag);
    using PathFindingType = bool(__cdecl*)(int sx, int sy, int tx, int ty, BYTE* path, float distance, int wall);
    using SendMoveType = void(__cdecl*)(BYTE* character, BYTE* object);
    using SetFontType = void(__fastcall*)(BYTE* text, void* unused, HFONT font);
    using RenderTextType = void(__fastcall*)(BYTE* text, void* unused, int x, int y, const char* string, int width,
        int height, int sort, SIZE* size);

    LogFunction g_log = nullptr;
    float g_size = 0.9f;
    bool g_rotate = true;
    float g_opacity = 0.92f;
    float g_markerOpacity = 0.55f;

    // Copia violeta del punto de los NPC para los hotspots (como en el mapa original con camera3d.dll).
    GLuint g_hotspotTexture = 0;
    bool g_hotspotFailed = false;

    // --- Caminata en curso ---
    struct Walk
    {
        bool active = false;
        int map = -1;
        int destX = 0, destY = 0;
        std::vector<POINT> route; // de la baldosa de partida al destino
        int index = 0;            // ultimo punto de la ruta alcanzado
        bool sent = false;        // ya se mando un tramo
        int sentX = 0, sentY = 0; // destino del ultimo tramo mandado
        int lastX = -1, lastY = -1;
        int stillTicks = 0;
    };

    Walk g_walk;

    // Boton izquierdo visto en la llamada anterior del mouse del mapa (para tomar solo clicks nuevos).
    bool g_leftWasDown = true;
    DWORD g_lastMouseTick = 0;

    // --- Utilidades ---
    template <typename T>
    T& At(BYTE* base, DWORD offset)
    {
        return *reinterpret_cast<T*>(base + offset);
    }

    bool Free(int x, int y)
    {
        return x >= 0 && y >= 0 && x < 256 && y < 256 && (TerrainWall[y * 256 + x] & Blocked) == 0;
    }

    BYTE* PlayableHero()
    {
        BYTE* hero = *Hero;
        if (*Scene != 5 || hero == nullptr || At<BYTE>(hero, CharLive) == 0 || At<BYTE>(hero, CharDead) != 0)
        {
            return nullptr;
        }

        return hero;
    }

    void StopWalk(const char* reason)
    {
        if (g_walk.active)
        {
            g_log("Mapa del TAB: caminata terminada (%s).", reason);
        }

        g_walk = Walk{};
    }

    // Baldosa libre mas cercana a (x, y), hasta 20 de distancia; false si no hay.
    bool NearestFree(int& x, int& y)
    {
        for (int radius = 0; radius <= 20; radius++)
        {
            int bestX = -1, bestY = -1, best = INT_MAX;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if ((std::max)(abs(dx), abs(dy)) != radius || !Free(x + dx, y + dy))
                    {
                        continue;
                    }

                    const int distance = dx * dx + dy * dy;
                    if (distance < best)
                    {
                        best = distance;
                        bestX = x + dx;
                        bestY = y + dy;
                    }
                }
            }

            if (bestX >= 0)
            {
                x = bestX;
                y = bestY;
                return true;
            }
        }

        return false;
    }

    // Ruta de 8 direcciones (A*, sin cortar esquinas de paredes). Si el destino no se alcanza, la ruta llega a
    // la baldosa alcanzable mas cercana a el.
    std::vector<POINT> FindRoute(int sx, int sy, int tx, int ty)
    {
        constexpr int Size = 256 * 256;
        constexpr int Straight = 10, Diagonal = 14;
        static std::vector<int> cost(Size), parent(Size);
        static std::vector<BYTE> closed(Size);
        std::fill(cost.begin(), cost.end(), INT_MAX);
        std::fill(closed.begin(), closed.end(), 0);

        auto heuristic = [&](int x, int y) {
            const int dx = abs(x - tx);
            const int dy = abs(y - ty);
            return Straight * (dx + dy) + (Diagonal - 2 * Straight) * (std::min)(dx, dy);
        };

        using Entry = std::pair<int, int>; // (costo + estimado, indice)
        std::priority_queue<Entry, std::vector<Entry>, std::greater<Entry>> open;
        const int start = sy * 256 + sx;
        cost[start] = 0;
        parent[start] = -1;
        open.push({ heuristic(sx, sy), start });
        int best = start;
        int bestHeuristic = heuristic(sx, sy);
        while (!open.empty())
        {
            const int current = open.top().second;
            open.pop();
            if (closed[current])
            {
                continue;
            }

            closed[current] = 1;
            const int cx = current % 256;
            const int cy = current / 256;
            const int h = heuristic(cx, cy);
            if (h < bestHeuristic)
            {
                best = current;
                bestHeuristic = h;
            }

            if (cx == tx && cy == ty)
            {
                break;
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    const int nx = cx + dx;
                    const int ny = cy + dy;
                    if ((dx == 0 && dy == 0) || !Free(nx, ny) || (dx != 0 && dy != 0 && (!Free(cx + dx, cy) || !Free(cx, cy + dy))))
                    {
                        continue;
                    }

                    const int next = ny * 256 + nx;
                    const int nextCost = cost[current] + (dx != 0 && dy != 0 ? Diagonal : Straight);
                    if (!closed[next] && nextCost < cost[next])
                    {
                        cost[next] = nextCost;
                        parent[next] = current;
                        open.push({ nextCost + heuristic(nx, ny), next });
                    }
                }
            }
        }

        std::vector<POINT> route;
        for (int node = best; node != -1; node = parent[node])
        {
            route.push_back({ node % 256, node / 256 });
        }

        std::reverse(route.begin(), route.end());
        return route;
    }

    void StartWalk(int x, int y)
    {
        BYTE* hero = PlayableHero();
        if (hero == nullptr || !NearestFree(x, y))
        {
            return;
        }

        const int hx = At<int>(hero, CharTileX);
        const int hy = At<int>(hero, CharTileY);
        g_walk = Walk{};
        g_walk.map = *CurrentMap;
        g_walk.destX = x;
        g_walk.destY = y;
        g_walk.route = FindRoute(hx, hy, x, y);
        g_walk.active = g_walk.route.size() > 1;
        if (g_walk.active)
        {
            const POINT end = g_walk.route.back();
            g_walk.destX = end.x;
            g_walk.destY = end.y;
            g_log("Mapa del TAB: caminar de %d,%d a %d,%d (%d baldosas).", hx, hy, end.x, end.y,
                static_cast<int>(g_walk.route.size()) - 1);
        }
    }

    // Manda un tramo hasta (x, y) como un click en el suelo; false si PathFinding2 no encuentra camino.
    bool SendSegment(BYTE* hero, int x, int y)
    {
        const int hx = At<int>(hero, CharTileX);
        const int hy = At<int>(hero, CharTileY);
        *TargetX = x;
        *TargetY = y;
        if (!reinterpret_cast<PathFindingType>(PathFindingFunction)(hx, hy, x, y, hero + CharPath, 0.0f, DefaultWall))
        {
            return false;
        }

        At<BYTE>(hero, CharMovement) = 0;
        reinterpret_cast<SendMoveType>(SendMoveFunction)(hero, hero + CharObject);
        g_walk.sent = true;
        g_walk.sentX = x;
        g_walk.sentY = y;
        return true;
    }

    // Destino del camino que esta siguiendo el personaje.
    bool HeroPathEnd(BYTE* hero, int& x, int& y)
    {
        BYTE* path = hero + CharPath;
        const int count = path[PathCount];
        if (count < 1 || count > 15)
        {
            return false;
        }

        x = path[PathXs + count - 1];
        y = path[PathYs + count - 1];
        return true;
    }

    void WalkTick()
    {
        if (!g_walk.active)
        {
            return;
        }

        BYTE* hero = PlayableHero();
        if (hero == nullptr || *CurrentMap != g_walk.map)
        {
            StopWalk("sin personaje o cambio de mapa");
            return;
        }

        const int hx = At<int>(hero, CharTileX);
        const int hy = At<int>(hero, CharTileY);
        const bool moving = At<char>(hero, CharMoving) != 0;
        if (g_walk.sent)
        {
            int px, py;
            const bool ours = *TargetX == g_walk.sentX && *TargetY == g_walk.sentY
                && (!moving || (HeroPathEnd(hero, px, py) && px == g_walk.sentX && py == g_walk.sentY));
            if (!ours)
            {
                StopWalk("el jugador movio al personaje");
                return;
            }
        }

        if (hx == g_walk.destX && hy == g_walk.destY)
        {
            StopWalk("llego");
            return;
        }

        if (hx != g_walk.lastX || hy != g_walk.lastY)
        {
            g_walk.lastX = hx;
            g_walk.lastY = hy;
            g_walk.stillTicks = 0;
        }
        else if (++g_walk.stillTicks > StuckTicks)
        {
            StopWalk("trabado");
            return;
        }

        if (moving || *ClickTicks < *ClickDelay)
        {
            return;
        }

        // Punto de la ruta donde esta el personaje (si se salio de la ruta, se busca otra desde ahi).
        const int count = static_cast<int>(g_walk.route.size());
        int found = -1;
        for (int i = g_walk.index; i < count && i < g_walk.index + 3 * SegmentLength; i++)
        {
            if (abs(g_walk.route[i].x - hx) <= 1 && abs(g_walk.route[i].y - hy) <= 1)
            {
                found = i;
                if (g_walk.route[i].x == hx && g_walk.route[i].y == hy)
                {
                    break;
                }
            }
        }

        if (found < 0)
        {
            g_walk.route = FindRoute(hx, hy, g_walk.destX, g_walk.destY);
            g_walk.index = 0;
            if (g_walk.route.size() < 2)
            {
                StopWalk("sin ruta");
                return;
            }

            found = 0;
        }

        g_walk.index = found;
        for (int step = (std::min)(SegmentLength, count - 1 - found); step >= 1; step--)
        {
            const POINT& point = g_walk.route[found + step];
            if (SendSegment(hero, point.x, point.y))
            {
                return;
            }
        }

        StopWalk("sin camino");
    }

    void __cdecl KeyTick(BYTE*)
    {
        __try
        {
            WalkTick();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            g_walk.active = false;
        }
    }

    // --- Ubicacion en la pantalla ---
    struct Layout
    {
        float centerX, centerY; // pixeles de la ventana, origen arriba a la izquierda
        float side;             // lado del mapa
        float cosine, sine;     // giro (antihorario en la pantalla)
    };

    Layout ComputeLayout()
    {
        const float width = static_cast<float>(*WindowWidth);
        const float height = static_cast<float>(*WindowHeight);
        const float bar = height * MainBarHeight / 480;
        const float room = (std::min)(height - bar, width) * 0.96f;
        Layout layout{};
        layout.centerX = width / 2;
        layout.centerY = (height - bar) / 2;
        layout.side = room * g_size / (g_rotate ? 1.41421356f : 1.0f);
        const float degrees = g_rotate ? -CameraAngle[2] : 0.0f;
        const float radians = degrees * 3.14159265f / 180.0f;
        layout.cosine = cosf(radians);
        layout.sine = sinf(radians);
        return layout;
    }

    // Baldosa (puede tener decimales) a pixeles: u = y / 256 hacia la derecha, v = x / 256 hacia abajo.
    void TileToScreen(const Layout& layout, float x, float y, float& sx, float& sy)
    {
        const float localX = (y / 256.0f - 0.5f) * layout.side;
        const float localUp = (0.5f - x / 256.0f) * layout.side;
        sx = layout.centerX + localX * layout.cosine - localUp * layout.sine;
        sy = layout.centerY - (localX * layout.sine + localUp * layout.cosine);
    }

    // Pixeles a baldosa; false si el punto queda afuera del mapa.
    bool ScreenToTile(const Layout& layout, float sx, float sy, float& x, float& y)
    {
        const float dx = sx - layout.centerX;
        const float up = layout.centerY - sy;
        const float localX = dx * layout.cosine + up * layout.sine;
        const float localUp = -dx * layout.sine + up * layout.cosine;
        y = (localX / layout.side + 0.5f) * 256.0f;
        x = (0.5f - localUp / layout.side) * 256.0f;
        return x >= 0 && y >= 0 && x < 256 && y < 256;
    }

    POINT MousePixels()
    {
        return { *MouseX * *WindowWidth / 640, *MouseY * *WindowHeight / 480 };
    }

    // --- Marcadores ---
    struct Marker
    {
        int kind;
        float x, y;
        const char* name;
        bool hotspot;
    };

    std::vector<Marker> ReadMarkers(BYTE* window)
    {
        static const char HotspotSuffix[] = "(Hotspot)";
        constexpr size_t SuffixLength = sizeof(HotspotSuffix) - 1;
        std::vector<Marker> markers;
        for (int i = 0; i < MarkerCount; i++)
        {
            BYTE* entry = window + Markers + i * MarkerSize;
            const int kind = entry[0];
            if (kind != 1 && kind != 2)
            {
                continue;
            }

            const char* name = reinterpret_cast<const char*>(entry + MarkerName);
            const size_t length = strnlen(name, MarkerNameLength);
            const bool hotspot = length >= SuffixLength && memcmp(name + length - SuffixLength, HotspotSuffix,
                SuffixLength) == 0;
            markers.push_back({ kind, static_cast<float>(At<int>(entry, MarkerX)), static_cast<float>(At<int>(entry, MarkerY)),
                length < MarkerNameLength ? name : nullptr, hotspot });
        }

        return markers;
    }

    float IconSize(int kind)
    {
        const float height = static_cast<float>(*WindowHeight);
        return kind == 2 ? height * 22 / 480 : height * 9.6f / 480; // spots y NPC: chicos, para no tapar al personaje
    }

    // Marcador bajo el mouse (indice en markers) o -1.
    int HoveredMarker(const Layout& layout, const std::vector<Marker>& markers, const POINT& mouse)
    {
        int hovered = -1;
        float best = 1e9f;
        for (size_t i = 0; i < markers.size(); i++)
        {
            float sx, sy;
            TileToScreen(layout, markers[i].x, markers[i].y, sx, sy);
            const float radius = IconSize(markers[i].kind) * 0.5f;
            const float distance = (mouse.x - sx) * (mouse.x - sx) + (mouse.y - sy) * (mouse.y - sy);
            if (distance <= radius * radius && distance < best)
            {
                best = distance;
                hovered = static_cast<int>(i);
            }
        }

        return hovered;
    }

    // --- Dibujo ---
    void Bind(int texture)
    {
        reinterpret_cast<BindTextureType>(BindTextureFunction)(texture);
    }

    bool CreateHotspotTexture()
    {
        Bind(NpcTexture);
        GLint width = 0;
        GLint height = 0;
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_WIDTH, &width);
        glGetTexLevelParameteriv(GL_TEXTURE_2D, 0, GL_TEXTURE_HEIGHT, &height);
        if (width <= 0 || height <= 0 || width > 256 || height > 256)
        {
            return false;
        }

        std::vector<BYTE> pixels(static_cast<size_t>(width) * height * 4);
        glGetTexImage(GL_TEXTURE_2D, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
        for (int i = 0; i < width * height; i++)
        {
            BYTE* p = &pixels[static_cast<size_t>(i) * 4];
            const BYTE r = p[0];
            const BYTE g = p[1];
            p[0] = static_cast<BYTE>((g * 7 + r * 3) / 10);
            p[1] = r;
            p[2] = g;
        }

        glGenTextures(1, &g_hotspotTexture);
        Bind(-static_cast<int>(g_hotspotTexture));
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
        return g_hotspotTexture != 0;
    }

    // Cuadrado de la textura (u, v de 0 a uv) centrado en (x, y).
    void Icon(int texture, float x, float y, float size, float uv)
    {
        Bind(texture);
        const float half = size / 2;
        glBegin(GL_QUADS);
        glTexCoord2f(0, 0);
        glVertex2f(x - half, y - half);
        glTexCoord2f(uv, 0);
        glVertex2f(x + half, y - half);
        glTexCoord2f(uv, uv);
        glVertex2f(x + half, y + half);
        glTexCoord2f(0, uv);
        glVertex2f(x - half, y + half);
        glEnd();
    }

    // El cuadrado del mapa (u, v de 0 a 1), agrandado en grow pixeles por lado.
    void MapQuad(const Layout& layout, float grow, bool textured)
    {
        static const float corners[4][2] = { { 0, 0 }, { 1, 0 }, { 1, 1 }, { 0, 1 } }; // (u, v)
        for (const auto& corner : corners)
        {
            const float u = corner[0];
            const float v = corner[1];
            const float localX = (u - 0.5f) * layout.side + (u < 0.5f ? -grow : grow);
            const float localUp = (0.5f - v) * layout.side + (v < 0.5f ? grow : -grow);
            if (textured)
            {
                glTexCoord2f(u, v);
            }

            glVertex2f(layout.centerX + localX * layout.cosine - localUp * layout.sine,
                layout.centerY - (localX * layout.sine + localUp * layout.cosine));
        }
    }

    void Ring(float x, float y, float radius)
    {
        glBegin(GL_LINE_LOOP);
        for (int i = 0; i < 20; i++)
        {
            const float angle = i * 3.14159265f * 2 / 20;
            glVertex2f(x + radius * cosf(angle), y + radius * sinf(angle));
        }

        glEnd();
    }

    void DrawShapes(BYTE* window, const Layout& layout, const std::vector<Marker>& markers)
    {
        const float width = static_cast<float>(*WindowWidth);
        const float height = static_cast<float>(*WindowHeight);
        glMatrixMode(GL_PROJECTION);
        glPushMatrix();
        glLoadIdentity();
        glOrtho(0, width, height, 0, -1, 1);
        glMatrixMode(GL_MODELVIEW);
        glPushMatrix();
        glLoadIdentity();
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_TEXTURE_BIT
            | GL_LINE_BIT | GL_POLYGON_BIT | GL_POINT_BIT);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glDisable(GL_FOG);
        glDisable(GL_LIGHTING);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);

        // Fondo y marco.
        const float frame = height * 6 / 480;
        glDisable(GL_TEXTURE_2D);
        glColor4f(0.0f, 0.0f, 0.0f, 0.8f);
        glBegin(GL_QUADS);
        MapQuad(layout, frame, false);
        glEnd();
        glLineWidth(2.0f);
        glColor4f(0.8f, 0.65f, 0.35f, 0.9f);
        glBegin(GL_LINE_LOOP);
        MapQuad(layout, frame, false);
        glEnd();

        // La imagen del mapa.
        glEnable(GL_TEXTURE_2D);
        glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_MODULATE);
        Bind(MapTexture);
        glColor4f(1.0f, 1.0f, 1.0f, g_opacity);
        glBegin(GL_QUADS);
        MapQuad(layout, 0, true);
        glEnd();

        // Marcadores: entradas, NPC y spots.
        if (g_hotspotTexture == 0 && !g_hotspotFailed)
        {
            g_hotspotFailed = !CreateHotspotTexture();
        }

        for (const Marker& marker : markers)
        {
            float sx, sy;
            TileToScreen(layout, marker.x, marker.y, sx, sy);
            const int texture = marker.kind == 2 ? GateTexture
                : marker.hotspot && g_hotspotTexture != 0 ? -static_cast<int>(g_hotspotTexture) : NpcTexture;
            glColor4f(1.0f, 1.0f, 1.0f, marker.kind == 2 ? 1.0f : g_markerOpacity);
            Icon(texture, sx, sy, IconSize(marker.kind), IconUv);
        }

        // Ruta que falta y destino.
        BYTE* hero = PlayableHero();
        glDisable(GL_TEXTURE_2D);
        if (g_walk.active && hero != nullptr)
        {
            glPointSize((std::max)(2.0f, height / 300));
            glColor4f(1.0f, 0.85f, 0.3f, 0.9f);
            glBegin(GL_POINTS);
            for (size_t i = g_walk.index + 1; i < g_walk.route.size(); i += 2)
            {
                float sx, sy;
                TileToScreen(layout, g_walk.route[i].x + 0.5f, g_walk.route[i].y + 0.5f, sx, sy);
                glVertex2f(sx, sy);
            }

            glEnd();
            float dx, dy;
            TileToScreen(layout, g_walk.destX + 0.5f, g_walk.destY + 0.5f, dx, dy);
            const float pulse = 0.5f + 0.5f * sinf(GetTickCount() / 150.0f);
            glLineWidth(2.0f);
            glColor4f(1.0f, 0.3f, 0.2f, 0.9f);
            Ring(dx, dy, height * (5 + 2 * pulse) / 480);
        }

        // El personaje (con su posicion fluida).
        if (hero != nullptr)
        {
            const float* position = reinterpret_cast<const float*>(hero + CharPosition);
            float sx, sy;
            TileToScreen(layout, position[0] / 100.0f, position[1] / 100.0f, sx, sy);
            glEnable(GL_TEXTURE_2D);
            glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
            Icon(HeroTexture, sx, sy, height * 16 / 480, IconUv);
        }

        glPopAttrib();
        glMatrixMode(GL_PROJECTION);
        glPopMatrix();
        glMatrixMode(GL_MODELVIEW);
        glPopMatrix();
        Bind(MapTexture); // el juego vuelve a enlazar lo que necesite: que su "ultima textura" sea real
    }

    void Outline(float left, float top, float right, float bottom)
    {
        glBegin(GL_LINE_LOOP);
        glVertex2f(left + 0.5f, top + 0.5f);
        glVertex2f(right - 0.5f, top + 0.5f);
        glVertex2f(right - 0.5f, bottom - 0.5f);
        glVertex2f(left + 0.5f, bottom - 0.5f);
        glEnd();
    }

    // Recuadro del cartel (pixeles, origen arriba a la izquierda) con la proyeccion 2D del juego (origen abajo).
    void DrawBox(float left, float top, float right, float bottom)
    {
        const float height = static_cast<float>(*WindowHeight);
        glPushAttrib(GL_ENABLE_BIT | GL_CURRENT_BIT | GL_COLOR_BUFFER_BIT | GL_LINE_BIT);
        glDisable(GL_TEXTURE_2D);
        glDisable(GL_DEPTH_TEST);
        glDisable(GL_ALPHA_TEST);
        glDisable(GL_CULL_FACE);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glColor4f(0.0f, 0.0f, 0.0f, 0.85f);
        glBegin(GL_QUADS);
        glVertex2f(left, height - top);
        glVertex2f(right, height - top);
        glVertex2f(right, height - bottom);
        glVertex2f(left, height - bottom);
        glEnd();
        glLineWidth(1.0f);
        glColor4f(0.8f, 0.65f, 0.35f, 0.9f);
        Outline(left, height - bottom, right, height - top);
        glPopAttrib();
    }

    DWORD Color(BYTE red, BYTE green, BYTE blue)
    {
        return red | (green << 8) | (blue << 16) | 0xFF000000;
    }

    // Texto centrado en una caja, en coordenadas de 640x480.
    void Text(const char* text, float left, float top, float width, DWORD color, bool bold)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        *reinterpret_cast<DWORD*>(renderer + 0x10) = color;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = 0; // sin fondo
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, bold ? *BoldFont : *NormalFont);
        reinterpret_cast<RenderTextType>(RenderTextFunction)(RenderText, nullptr, static_cast<int>(left),
            static_cast<int>(top), text, static_cast<int>(width), 0, SortCenter, nullptr);
    }

    // Cartel junto al mouse: el nombre del marcador o la baldosa bajo el mouse.
    void DrawTexts(const Layout& layout, const std::vector<Marker>& markers)
    {
        BYTE* const renderer = *reinterpret_cast<BYTE**>(RenderText + 4);
        if (renderer == nullptr)
        {
            return;
        }

        const POINT mouse = MousePixels();
        float tileX, tileY;
        if (!ScreenToTile(layout, static_cast<float>(mouse.x), static_cast<float>(mouse.y), tileX, tileY))
        {
            return;
        }

        char coordinates[32];
        sprintf_s(coordinates, "%d, %d", static_cast<int>(tileX), static_cast<int>(tileY));
        const int hovered = HoveredMarker(layout, markers, mouse);
        const char* name = hovered >= 0 ? markers[hovered].name : nullptr;

        reinterpret_cast<void(__cdecl*)(bool)>(EnableAlphaBlendFunction)(true);
        glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
        const DWORD savedText = *reinterpret_cast<DWORD*>(renderer + 0x10);
        const DWORD savedBackground = *reinterpret_cast<DWORD*>(renderer + 0x14);
        const float toX = 640.0f / *WindowWidth;
        const float toY = 480.0f / *WindowHeight;
        const float boxWidth = name != nullptr ? 130.0f : 50.0f; // 640x480
        const float lineHeight = 12.0f;
        const int lines = name != nullptr ? 2 : 1;
        const float boxHeight = lines * lineHeight + 6.0f;
        float left = mouse.x * toX + 12.0f;
        float top = mouse.y * toY + 14.0f;
        if (left + boxWidth > 636.0f)
        {
            left = mouse.x * toX - 12.0f - boxWidth;
        }

        if (top + boxHeight > 480.0f - MainBarHeight)
        {
            top = mouse.y * toY - 6.0f - boxHeight;
        }

        DrawBox(left / toX, top / toY, (left + boxWidth) / toX, (top + boxHeight) / toY);
        float y = top + 3.0f;
        if (name != nullptr)
        {
            Text(name, left, y, boxWidth, markers[hovered].hotspot ? Color(220, 150, 255) : Color(255, 204, 26), true);
            y += lineHeight;
        }

        Text(coordinates, left, y, boxWidth, Color(230, 230, 230), false);

        *reinterpret_cast<DWORD*>(renderer + 0x10) = savedText;
        *reinterpret_cast<DWORD*>(renderer + 0x14) = savedBackground;
        reinterpret_cast<SetFontType>(SetFontFunction)(RenderText, nullptr, *NormalFont);
        reinterpret_cast<void(__cdecl*)()>(DisableAlphaBlendFunction)();
    }

    // Sin objetos con destructor: puede usar __try.
    void DrawAll(BYTE* window, const Layout& layout, const std::vector<Marker>& markers)
    {
        __try
        {
            DrawShapes(window, layout, markers);
            DrawTexts(layout, markers);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }
    }

    bool __fastcall RenderHook(BYTE* window, void* unused)
    {
        if (At<BYTE>(window, MapLoaded) == 0)
        {
            return reinterpret_cast<RenderType>(MiniMapRenderFunction)(window, unused);
        }

        DrawAll(window, ComputeLayout(), ReadMarkers(window));
        return true;
    }

    bool __fastcall MouseHook(BYTE* window, void* unused)
    {
        if (At<BYTE>(window, MapLoaded) == 0)
        {
            return reinterpret_cast<MouseType>(MiniMapMouseFunction)(window, unused);
        }

        const Layout layout = ComputeLayout();
        const POINT mouse = MousePixels();
        float x, y;
        if (!ScreenToTile(layout, static_cast<float>(mouse.x), static_cast<float>(mouse.y), x, y))
        {
            return true; // afuera del mapa: el mouse sigue al mundo y a las otras ventanas
        }

        // Solo un click que empieza con el mapa abierto: al abrirlo (o si el mouse vuelve al mapa) se ignora
        // el estado del boton que venia de antes.
        const DWORD now = GetTickCount();
        const bool fresh = now - g_lastMouseTick > 250;
        g_lastMouseTick = now;
        const bool leftDown = reinterpret_cast<IsPressType>(IsPressFunction)(VK_LBUTTON)
            || reinterpret_cast<IsPressType>(IsRepeatFunction)(VK_LBUTTON);
        const bool clicked = leftDown && !g_leftWasDown && !fresh;
        g_leftWasDown = leftDown;
        if (clicked)
        {
            StartWalk(static_cast<int>(x), static_cast<int>(y));
            reinterpret_cast<PlaySoundType>(PlaySoundFunction)(ClickSound, nullptr, 0);
        }
        else if (reinterpret_cast<IsPressType>(IsPressFunction)(VK_RBUTTON))
        {
            StopWalk("click derecho en el mapa");
        }

        return false; // el mouse es del mapa: no llega al mundo ni a otras ventanas
    }

    bool SlotMatches(int slot, DWORD function)
    {
        return reinterpret_cast<const DWORD*>(MiniMapVtable)[slot] == function;
    }

    bool SetSlot(int slot, const void* function)
    {
        const DWORD value = reinterpret_cast<DWORD>(function);
        return WriteCode(MiniMapVtable + slot * 4, &value, sizeof(value));
    }

    float ReadFloat(const char* ini, const char* key, float fallback, float low, float high)
    {
        char value[32];
        GetPrivateProfileStringA("TabMap", key, "", value, sizeof(value), ini);
        const float number = value[0] != '\0' ? static_cast<float>(atof(value)) : fallback;
        return (std::min)(high, (std::max)(low, number));
    }
}

extern "C" __declspec(dllexport) bool StartTabMap(const TabMapHost* host)
{
    if (host == nullptr || host->version != TabMapHostVersion)
    {
        return false;
    }

    g_log = host->log;
    if (!SlotMatches(0, MiniMapRenderFunction) || !SlotMatches(2, MiniMapMouseFunction))
    {
        g_log("Mapa del TAB: la ventana del juego no coincide; desactivado.");
        return false;
    }

    g_size = ReadFloat(host->iniPath, "Size", 0.9f, 0.3f, 1.0f);
    g_rotate = GetPrivateProfileIntA("TabMap", "Rotate", 1, host->iniPath) != 0;
    g_opacity = ReadFloat(host->iniPath, "Opacity", 0.92f, 0.2f, 1.0f);
    g_markerOpacity = ReadFloat(host->iniPath, "MarkerOpacity", 0.55f, 0.2f, 1.0f);
    if (!SetSlot(0, &RenderHook) || !SetSlot(2, &MouseHook))
    {
        g_log("Mapa del TAB: no se pudo instalar.");
        return false;
    }

    host->setKeyTick(&KeyTick);
    g_log("Mapa del TAB completo listo (tamano %.2f, %s).", g_size, g_rotate ? "girado como la camara" : "derecho");
    return true;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}
