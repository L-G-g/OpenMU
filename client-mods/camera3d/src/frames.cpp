// Cuadros extra: con mas FPS, que el dibujo no acelere el juego.
//
// La logica corre 25 veces por segundo, pero el dibujo de cada escena tambien cambia cosas, una vez
// por cuadro dibujado. A 60 FPS eso pasaba 2,4 veces mas seguido y todo parecia acelerado:
//
// 1. Crea uniones (estelas), particulas y efectos: brillo de armas y alas, chispas, auras, etc. Si
//    desde el cuadro anterior no corrio ningun paso de logica, es un cuadro "extra": mientras se
//    dibuja, esas funciones no hacen nada, asi que se crean tantos como a 25 FPS. Los sprites de
//    0x007711C0 no se tocan: duran un solo cuadro (se borran despues de dibujarlos).
// 2. Avanza la animacion de partes del personaje, como las alas: llama a la funcion que reproduce la
//    animacion de un modelo (0x00545180, *cuadro += velocidad). Mientras se dibuja, la velocidad se
//    multiplica por lo que duro el cuadro dividido 40 ms: avanza lo mismo por segundo que a 25 FPS, y
//    ademas mas suave. Fuera del dibujo (la logica) no se cambia.
//
// 3. En el login, avanza el recorrido de la camara un paso por cuadro (0x004DE1E0). Ahora avanza solo
//    en los cuadros con paso de logica, y en cada cuadro se muestra un punto intermedio entre el paso
//    anterior y el actual segun el tiempo que paso (con 40 ms de retraso), asi se mueve fluido.
// 4. Algunas ventanas marcan "mostrar descripcion" al procesar el mouse (en el paso de logica) y la
//    borran al dibujarla, como la barra de habilidades (+0x41). En los cuadros extra la descripcion ya
//    estaba borrada y parpadeaba: se guarda la marca al empezar cada cuadro con paso de logica y se
//    vuelve a poner antes de dibujar los cuadros extra.
// 5. Muchos efectos eligen posiciones, brillos o chispas al azar (rand) cada vez que se dibujan: a 60 FPS
//    cambiaban 2,4 veces mas seguido y se veian acelerados. En el cuadro con paso de logica se anotan
//    los valores que devuelve rand junto con quien lo llamo; en los cuadros extra cada llamada recibe
//    el mismo valor que recibio esa misma llamada en el cuadro anterior, asi el azar cambia 25 veces
//    por segundo como en el original. (Las llamadas desde las funciones que no se ejecutan en los
//    cuadros extra se saltean buscando por quien llama.)
// 6. La logica crea sprites (brillos, auras como la del buff del DK) en el arreglo de 0x09778748, y el
//    dibujo los borra despues de mostrarlos (0x00771560): asi solo se veian en el cuadro que sigue a
//    cada paso de logica y en los extra faltaban (se veian cortados). Se anotan los sprites vivos al
//    empezar cada cuadro con paso de logica y se vuelven a activar antes de dibujar los cuadros extra.
// 7. Suma contadores que se usan como reloj de efectos visuales (por ejemplo, textos que parpadean).
//    En los cuadros extra se les devuelve su valor despues de dibujar, asi avanzan como a 25 FPS.
//
// Se envuelven las llamadas al dibujo de las tres escenas en 0x004D9CF0: login (0x004D7970),
// seleccion de personaje (0x004D70E0) y juego (0x004D95E0).

#include "mods.h"

#include <intrin.h>
#include <math.h>
#include <string.h>

namespace
{
    int* const LogicTickCounter = reinterpret_cast<int*>(0x00E8CB20); // +1 por cada paso de logica
    constexpr double TickMs = 40.0;

    LogFunction g_log = nullptr;
    volatile BYTE g_extraFrame = 0;
    // Por funcion: si en este cuadro se saltea (cuadro extra y bloqueo activo). Hasta 8 funciones.
    volatile BYTE g_skipCreator[8] = {};
    bool g_creatorBlocked[8] = { true, true, true, true, true, true, true, true };
    volatile BYTE g_rendering = 0;
    float g_animationFactor = 1.0f;
    int g_lastRenderTicks = -1;
    LARGE_INTEGER g_frequency{};
    LARGE_INTEGER g_lastRender{};
    RenderWrapper g_diagnostics = nullptr;

    struct Scene
    {
        DWORD callSite; // CALL al dibujo de la escena
        DWORD render;
        bool takesDc;   // login y seleccion de personaje reciben el HDC (cdecl)
    };

    const Scene Scenes[] = {
        { 0x004DA2AC, 0x004D7970, true },  // login
        { 0x004DA2C6, 0x004D70E0, true },  // seleccion de personaje
        { 0x004DA2DC, 0x004D95E0, false }, // juego
    };

    // Funciones a las que se les pone un trampolin. Se copian sus primeras instrucciones (enteras, sin
    // saltos relativos).
    struct Patched
    {
        const char* name;
        DWORD address;
        BYTE prologue[9];
        int length;
    };

    // En los cuadros extra vuelven enseguida (devuelven 0). Crean objetos que despues mueve la logica
    // (cdecl: limpia quien llama).
    const Patched Creators[] = {
        { "uniones", 0x0074CBE0, { 0x55, 0x8B, 0xEC, 0xB8, 0x28, 0x13, 0x00, 0x00 }, 8 },       // MOV EAX,0x1328
        { "particulas", 0x0072D060, { 0x55, 0x8B, 0xEC, 0x81, 0xEC, 0x38, 0x03, 0x00, 0x00 }, 9 }, // SUB ESP,0x338
        { "efectos", 0x006D8F20, { 0x55, 0x8B, 0xEC, 0xB8, 0xCC, 0x12, 0x00, 0x00 }, 8 },      // MOV EAX,0x12CC
    };

    // Contadores que el dibujo suma en cada cuadro (encontrados con el diagnostico de memoria).
    int* const FrameCounters[] = {
        reinterpret_cast<int*>(0x081C0418), // textos que parpadean (0x005977E0: contador % 10 < 5)
        reinterpret_cast<int*>(0x00E8C56C), // de 0 a 40 (0x005EDFE0, 0x005EEA70)
        reinterpret_cast<int*>(0x08793130), // de 0 a 15 (0x005FA710)
    };
    constexpr int FrameCounterCount = sizeof(FrameCounters) / sizeof(FrameCounters[0]);
    int g_savedCounters[FrameCounterCount];

    // Marcas de ventanas que se ponen en el paso de logica y se borran al dibujar.
    struct StickyFlag
    {
        const char* name;
        DWORD vtable; // clase de la ventana
        DWORD offset;
    };

    const StickyFlag StickyFlags[] = {
        { "descripcion de habilidad", 0x00D461C4, 0x41 }, // CNewUISkillList
    };
    constexpr int StickyFlagCount = sizeof(StickyFlags) / sizeof(StickyFlags[0]);
    BYTE* const UiManager = reinterpret_cast<BYTE*>(0x09867090);
    BYTE* g_stickyAddress[StickyFlagCount] = {};
    BYTE g_stickyValue[StickyFlagCount] = {};

    // La ventana se busca por su clase entre las del administrador de ventanas (y se vuelve a buscar si
    // cambio, por ejemplo despues de cambiar de personaje).
    BYTE* FindWindowField(const StickyFlag& flag)
    {
        __try
        {
            for (DWORD offset = 4; offset < 0x400; offset += 4)
            {
                const DWORD window = *reinterpret_cast<const DWORD*>(UiManager + offset);
                if (window > 0x10000 && *reinterpret_cast<const DWORD*>(window) == flag.vtable)
                {
                    return reinterpret_cast<BYTE*>(window + flag.offset);
                }
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
        }

        return nullptr;
    }

    void UpdateStickyFlags(bool extraFrame)
    {
        for (int i = 0; i < StickyFlagCount; i++)
        {
            if (!extraFrame)
            {
                g_stickyAddress[i] = FindWindowField(StickyFlags[i]);
                g_stickyValue[i] = g_stickyAddress[i] != nullptr ? *g_stickyAddress[i] : 0;
            }
            else if (g_stickyAddress[i] != nullptr)
            {
                *g_stickyAddress[i] = g_stickyValue[i];
            }
        }
    }

    // Recorrido de la camara del login (objeto en 0x00E8CBA8): posicion, altura, angulo y direccion.
    const Patched CameraTour = { "recorrido de camara", 0x004DE1E0, { 0x55, 0x8B, 0xEC, 0x81, 0xEC, 0xA4, 0x00, 0x00, 0x00 }, 9 }; // SUB ESP,0xA4
    const DWORD TourFields[] = { 0x30, 0x34, 0x3C, 0x54, 0x5C, 0x60 };
    constexpr DWORD TourAngleField = 0x54; // grados de 0 a 360
    constexpr int TourFieldCount = sizeof(TourFields) / sizeof(TourFields[0]);
    using TourFunction = void(__fastcall*)(BYTE*);
    TourFunction g_tourOriginal = nullptr;
    BYTE* g_tour = nullptr;
    float g_tourPrevious[TourFieldCount];
    float g_tourCurrent[TourFieldCount];
    bool g_tourValid = false;
    bool g_tourOverridden = false;
    LARGE_INTEGER g_tourStepTime{};

    float& TourField(BYTE* tour, int index)
    {
        return *reinterpret_cast<float*>(tour + TourFields[index]);
    }

    bool g_smooth = true; // camera3d.ini [Video] Smooth=0: sin interpolacion (solo avanza con la logica)

    void __fastcall TourHook(BYTE* tour)
    {
        if (!g_rendering)
        {
            g_tourOriginal(tour);
            return;
        }

        if (!g_smooth)
        {
            if (!g_extraFrame)
            {
                g_tourOriginal(tour);
            }

            return;
        }

        if (!g_extraFrame)
        {
            for (int i = 0; i < TourFieldCount; i++)
            {
                g_tourPrevious[i] = TourField(tour, i);
            }

            g_tourOriginal(tour);
            for (int i = 0; i < TourFieldCount; i++)
            {
                g_tourCurrent[i] = TourField(tour, i);
            }

            g_tour = tour;
            g_tourValid = true;
            QueryPerformanceCounter(&g_tourStepTime);
        }

        if (!g_tourValid || g_tour != tour)
        {
            return;
        }

        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        double t = static_cast<double>(now.QuadPart - g_tourStepTime.QuadPart) * 1000.0 / g_frequency.QuadPart / TickMs;
        t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
        for (int i = 0; i < TourFieldCount; i++)
        {
            float from = g_tourPrevious[i];
            float to = g_tourCurrent[i];
            if (TourFields[i] == TourAngleField)
            {
                // por el camino mas corto (de 350 a 10 son 20 grados, no 340)
                if (to - from > 180.0f) from += 360.0f;
                else if (from - to > 180.0f) to += 360.0f;
            }

            float value = from + (to - from) * static_cast<float>(t);
            if (TourFields[i] == TourAngleField && value >= 360.0f)
            {
                value -= 360.0f;
            }

            TourField(tour, i) = value;
        }

        g_tourOverridden = true;
    }

    // rand de la biblioteca de C dentro de main.exe: semilla por hilo en _getptd()+0x14.
    constexpr DWORD RandFunction = 0x009CFA0D;
    constexpr DWORD GetPtdFunction = 0x009E11D0;
    const BYTE RandPrologue[] = { 0xE8, 0xBE, 0x17, 0x01, 0x00, 0x8B, 0x48, 0x14 }; // CALL _getptd; MOV ECX,[EAX+0x14]
    struct RandEntry
    {
        void* caller;
        int value;
    };
    constexpr int RandTapeSize = 65536;
    RandEntry g_randTape[RandTapeSize];
    int g_randCount = 0;
    int g_randCursor = 0;
    int g_randReplayed = 0; // para el log: llamadas en cuadros extra servidas desde lo anotado
    int g_randFallback = 0; //              y las que no coincidieron

    int RealRand()
    {
        BYTE* ptd = reinterpret_cast<BYTE*(__cdecl*)()>(GetPtdFunction)();
        DWORD& seed = *reinterpret_cast<DWORD*>(ptd + 0x14);
        seed = seed * 0x343FD + 0x269EC3;
        return static_cast<int>((seed >> 16) & 0x7FFF);
    }

    int __cdecl RandHook()
    {
        void* const caller = _ReturnAddress();
        if (g_rendering && !g_extraFrame)
        {
            const int value = RealRand();
            if (g_randCount < RandTapeSize)
            {
                g_randTape[g_randCount++] = { caller, value };
            }

            return value;
        }

        if (g_rendering && g_extraFrame)
        {
            // Se busca la proxima llamada de ese mismo lugar (con un limite, para no recorrer todo).
            const int end = min(g_randCount, g_randCursor + 4096);
            for (int i = g_randCursor; i < end; i++)
            {
                if (g_randTape[i].caller == caller)
                {
                    g_randCursor = i + 1;
                    g_randReplayed++;
                    return g_randTape[i].value;
                }
            }

            g_randFallback++; // esa llamada no estaba en el cuadro anterior: valor nuevo, y se sigue
        }

        return RealRand();
    }

    // Sprites (1000 de 0x288 bytes; +4: vivo).
    BYTE* const Sprites = reinterpret_cast<BYTE*>(0x09778748);
    constexpr int SpriteCount = 1000;
    constexpr int SpriteSize = 0x288;
    short g_logicSprites[SpriteCount];
    int g_logicSpriteCount = 0;

    void UpdateLogicSprites(bool extraFrame)
    {
        if (!extraFrame)
        {
            g_logicSpriteCount = 0;
            for (int i = 0; i < SpriteCount; i++)
            {
                if (Sprites[i * SpriteSize + 4] != 0)
                {
                    g_logicSprites[g_logicSpriteCount++] = static_cast<short>(i);
                }
            }

            return;
        }

        for (int i = 0; i < g_logicSpriteCount; i++)
        {
            Sprites[g_logicSprites[i] * SpriteSize + 4] = 1;
        }
    }

    // bool __thiscall PlayAnimation(float* frame, float* prior, ushort* priorAction, float speed, ...)
    const Patched PlayAnimation = { "animacion", 0x00545180, { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x28 }, 6 }; // SUB ESP,0x28


    void BeginRender()
    {
        const int ticks = *LogicTickCounter;
        g_extraFrame = ticks == g_lastRenderTicks ? 1 : 0;
        g_lastRenderTicks = ticks;

        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        const double ms = static_cast<double>(now.QuadPart - g_lastRender.QuadPart) * 1000.0 / g_frequency.QuadPart;
        g_lastRender = now;
        g_animationFactor = ms >= TickMs ? 1.0f : static_cast<float>(ms / TickMs);
        g_rendering = 1;
        for (int i = 0; i < 8; i++)
        {
            g_skipCreator[i] = g_extraFrame && g_creatorBlocked[i] ? 1 : 0;
        }

        UpdateStickyFlags(g_extraFrame != 0);
        UpdateLogicSprites(g_extraFrame != 0);
        if (g_extraFrame)
        {
            g_randCursor = 0;
        }
        else
        {
            g_randCount = 0;
        }

        for (int i = 0; i < FrameCounterCount; i++)
        {
            g_savedCounters[i] = *FrameCounters[i];
        }
    }

    // Fluidez de la camara (para el log): cuanto se mueve por cuadro y que tan parejo. Con saltos de 25
    // por segundo a 60 FPS hay cuadros quietos y cuadros con saltos (variacion alta); fluido, todos
    // los cuadros se mueven parecido (variacion baja).
    float* const CameraPosition = reinterpret_cast<float*>(0x087933DC);
    float g_lastCamera[2] = {};
    double g_moveSum = 0.0, g_moveSquares = 0.0;
    int g_moveFrames = 0, g_stillFrames = 0;
    int g_holes = 0;              // cuadros quietos entre dos cuadros con movimiento (saltos)
    int g_movingFrames = 0;
    bool g_movedBefore = false;    // el cuadro anterior se movio
    bool g_stillAfterMove = false; // hay cuadros quietos despues de un movimiento
    DWORD g_moveReport = 0;

    void UpdateCameraStats()
    {
        const float dx = CameraPosition[0] - g_lastCamera[0];
        const float dy = CameraPosition[1] - g_lastCamera[1];
        g_lastCamera[0] = CameraPosition[0];
        g_lastCamera[1] = CameraPosition[1];
        const double move = sqrt(dx * dx + dy * dy);
        if (move < 500.0)
        {
            g_moveSum += move;
            g_moveSquares += move * move;
            g_moveFrames++;
            g_stillFrames += move < 0.01 ? 1 : 0;
            // salto: un cuadro quieto entre dos cuadros con movimiento
            const bool moved = move >= 0.01;
            g_movingFrames += moved ? 1 : 0;
            g_holes += moved && !g_movedBefore && g_stillAfterMove ? 1 : 0;
            g_stillAfterMove = moved ? false : g_movedBefore || g_stillAfterMove;
            g_movedBefore = moved;
        }

        if (GetTickCount() - g_moveReport >= 5000 && g_moveFrames > 0)
        {
            const double mean = g_moveSum / g_moveFrames;
            const double variance = g_moveSquares / g_moveFrames - mean * mean;
            if (mean > 0.01)
            {
                g_log("Camara: %.2f por cuadro, variacion %.0f%%, %d de %d cuadros quieta; %d saltos en %d cuadros con movimiento.",
                    mean, 100.0 * sqrt(variance > 0.0 ? variance : 0.0) / mean, g_stillFrames, g_moveFrames, g_holes,
                    g_movingFrames);
                g_holes = g_movingFrames = 0;
            }

            g_log("Azar en cuadros extra: %d repetidos, %d sin coincidencia.", g_randReplayed, g_randFallback);
            g_randReplayed = g_randFallback = 0;

            g_moveReport = GetTickCount();
            g_moveSum = g_moveSquares = 0.0;
            g_moveFrames = g_stillFrames = 0;
        }
    }

    void EndRender()
    {
        UpdateCameraStats();
        if (g_tourOverridden)
        {
            // el recorrido sigue desde el paso real, no desde el punto intermedio que se mostro
            for (int i = 0; i < TourFieldCount; i++)
            {
                TourField(g_tour, i) = g_tourCurrent[i];
            }

            g_tourOverridden = false;
        }

        if (g_extraFrame)
        {
            for (int i = 0; i < FrameCounterCount; i++)
            {
                *FrameCounters[i] = g_savedCounters[i];
            }
        }

        g_rendering = 0;
        g_extraFrame = 0;
        for (int i = 0; i < 8; i++)
        {
            g_skipCreator[i] = 0;
        }
    }

    // Momento del ultimo cuadro con paso de logica (para la interpolacion de personajes).
    LARGE_INTEGER g_lastStep{};

    char __cdecl RenderGame()
    {
        BeginRender();
        if (g_smooth)
        {
            LARGE_INTEGER now;
            QueryPerformanceCounter(&now);
            if (!g_extraFrame)
            {
                g_lastStep = now;
            }

            double t = static_cast<double>(now.QuadPart - g_lastStep.QuadPart) * 1000.0 / g_frequency.QuadPart / TickMs;
            InterpolationBeforeRender(!g_extraFrame, static_cast<float>(t > 1.0 ? 1.0 : t));
            InterpolationSetFrame(true, !g_extraFrame, static_cast<float>(t > 1.0 ? 1.0 : t));
            TrailsBeforeRender(!g_extraFrame, static_cast<float>(t > 1.0 ? 1.0 : t));
        }

        const auto render = reinterpret_cast<RenderFunctionType>(Scenes[2].render);
        const char result = g_diagnostics != nullptr ? g_diagnostics(render, g_extraFrame != 0) : render();
        InterpolationAfterRender();
        TrailsAfterRender();
        InterpolationSetFrame(false, false, 0.0f);
        EndRender();
        return result;
    }

    // Login y seleccion de personaje reciben el HDC; para el diagnostico se los llama sin argumentos.
    HDC g_dc = nullptr;

    char __cdecl CallLogin()
    {
        return reinterpret_cast<char(__cdecl*)(HDC)>(Scenes[0].render)(g_dc);
    }

    char __cdecl CallCharacterSelect()
    {
        return reinterpret_cast<char(__cdecl*)(HDC)>(Scenes[1].render)(g_dc);
    }

    char RenderWithDc(HDC dc, RenderFunctionType render)
    {
        BeginRender();
        g_dc = dc;
        const char result = g_diagnostics != nullptr ? g_diagnostics(render, g_extraFrame != 0) : render();
        EndRender();
        return result;
    }

    char __cdecl RenderLogin(HDC dc)
    {
        return RenderWithDc(dc, &CallLogin);
    }

    char __cdecl RenderCharacterSelect(HDC dc)
    {
        return RenderWithDc(dc, &CallCharacterSelect);
    }

    // Copia el prologo y salta a la funcion original despues de el.
    BYTE* WriteContinuation(BYTE* p, const Patched& function)
    {
        memcpy(p, function.prologue, function.length);
        p += function.length;
        *p++ = 0xE9; // JMP original + prologo
        const DWORD back = function.address + function.length - (reinterpret_cast<DWORD>(p) + 4);
        memcpy(p, &back, 4);
        return p + 4;
    }

    void JumpTo(const Patched& function, BYTE* stub)
    {
        BYTE jump[5] = { 0xE9 };
        const DWORD to = reinterpret_cast<DWORD>(stub) - (function.address + 5);
        memcpy(jump + 1, &to, 4);
        WriteCode(function.address, jump, sizeof(jump));
    }

    BYTE* WriteAddress(BYTE* p, const volatile void* address)
    {
        const DWORD value = reinterpret_cast<DWORD>(address);
        memcpy(p, &value, 4);
        return p + 4;
    }

    // Si es un cuadro extra devuelve 0 sin crear nada; si no, sigue en la funcion original.
    void HookCreator(const Patched& creator, int index, BYTE* stub)
    {
        BYTE* p = stub;
        *p++ = 0x80; // CMP BYTE PTR [g_skipCreator[index]],0
        *p++ = 0x3D;
        p = WriteAddress(p, &g_skipCreator[index]);
        *p++ = 0x00;
        *p++ = 0x74; // JZ continuar
        *p++ = 0x03;
        *p++ = 0x31; // XOR EAX,EAX
        *p++ = 0xC0;
        *p++ = 0xC3; // RET
        WriteContinuation(p, creator);
        JumpTo(creator, stub);
    }

    // Mientras se dibuja, multiplica la velocidad (4to argumento, [ESP+0x10]) por g_animationFactor.
    void HookPlayAnimation(BYTE* stub)
    {
        BYTE* p = stub;
        *p++ = 0x80; // CMP BYTE PTR [g_rendering],0
        *p++ = 0x3D;
        p = WriteAddress(p, &g_rendering);
        *p++ = 0x00;
        *p++ = 0x74; // JZ continuar
        *p++ = 0x0E;
        *p++ = 0xD9; // FLD DWORD PTR [ESP+0x10]
        *p++ = 0x44;
        *p++ = 0x24;
        *p++ = 0x10;
        *p++ = 0xD8; // FMUL DWORD PTR [g_animationFactor]
        *p++ = 0x0D;
        p = WriteAddress(p, &g_animationFactor);
        *p++ = 0xD9; // FSTP DWORD PTR [ESP+0x10]
        *p++ = 0x5C;
        *p++ = 0x24;
        *p++ = 0x10;
        WriteContinuation(p, PlayAnimation);
        JumpTo(PlayAnimation, stub);
    }

    bool Matches(const Patched& function)
    {
        if (memcmp(reinterpret_cast<const void*>(function.address), function.prologue, function.length) == 0)
        {
            return true;
        }

        g_log("Cuadros extra: el inicio de la funcion de %s no coincide. No se aplica.", function.name);
        return false;
    }

    void PatchCall(DWORD site, const void* target)
    {
        BYTE patch[5] = { 0xE8 };
        const DWORD call = reinterpret_cast<DWORD>(target) - (site + 5);
        memcpy(patch + 1, &call, 4);
        WriteCode(site, patch, sizeof(patch));
    }
}

void SetSmoothMotion(bool smooth)
{
    g_smooth = smooth;
    g_log("Movimiento fluido %s.", smooth ? "activado" : "desactivado");
}

void SetCreatorBlocked(int index, bool blocked)
{
    if (index >= 0 && index < 8)
    {
        g_creatorBlocked[index] = blocked;
        g_log("Cuadros extra: bloqueo de la funcion %d %s.", index, blocked ? "activo" : "desactivado");
    }
}

void SetRenderDiagnostics(RenderWrapper wrapper)
{
    g_diagnostics = wrapper;
}

bool InstallExtraFrames(LogFunction log, bool smooth)
{
    g_log = log;
    g_smooth = smooth;

    for (const Scene& scene : Scenes)
    {
        const BYTE* site = reinterpret_cast<const BYTE*>(scene.callSite);
        if (site[0] != 0xE8 || scene.callSite + 5 + *reinterpret_cast<const int*>(site + 1) != scene.render)
        {
            g_log("Cuadros extra: la llamada al dibujo en 0x%08X no coincide. No se aplica.", scene.callSite);
            return false;
        }
    }

    for (const Patched& creator : Creators)
    {
        if (!Matches(creator))
        {
            return false;
        }
    }

    if (!Matches(PlayAnimation) || !Matches(CameraTour))
    {
        return false;
    }

    if (memcmp(reinterpret_cast<const void*>(RandFunction), RandPrologue, sizeof(RandPrologue)) != 0)
    {
        g_log("Cuadros extra: rand no coincide. No se aplica.");
        return false;
    }

    BYTE* stubs = static_cast<BYTE*>(VirtualAlloc(nullptr, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
    if (stubs == nullptr)
    {
        return false;
    }

    QueryPerformanceFrequency(&g_frequency);
    QueryPerformanceCounter(&g_lastRender);

    for (int i = 0; i < static_cast<int>(sizeof(Creators) / sizeof(Creators[0])); i++)
    {
        HookCreator(Creators[i], i, stubs + i * 64);
    }

    HookPlayAnimation(stubs + 256);

    // Trampolin del recorrido: prologo copiado y salto a la funcion original despues de el.
    WriteContinuation(stubs + 320, CameraTour);
    g_tourOriginal = reinterpret_cast<TourFunction>(stubs + 320);
    JumpTo(CameraTour, reinterpret_cast<BYTE*>(&TourHook));

    // rand se reemplaza entera (RealRand hace lo mismo que la original).
    const Patched rand = { "rand", RandFunction, {}, 0 };
    JumpTo(rand, reinterpret_cast<BYTE*>(&RandHook));

    PatchCall(Scenes[0].callSite, &RenderLogin);
    PatchCall(Scenes[1].callSite, &RenderCharacterSelect);
    PatchCall(Scenes[2].callSite, &RenderGame);

    g_log("Cuadros extra: el dibujo no crea uniones, particulas ni efectos fuera de los pasos de logica, "
          "y las animaciones del dibujo avanzan segun el tiempo del cuadro.");
    return true;
}
