// FPS: reemplaza el limite de 25 cuadros por segundo del juego por uno configurable.
//
// El juego corre la logica a paso fijo de 40 ms (25 veces por segundo) y dibuja una vez por cuadro.
// Al terminar cada cuadro mide cuanto tardo con GetTickCount y, si fueron menos de 40 ms, espera
// hasta completarlos (0x004DA3A1..0x004DA3E4). Lo que mide se suma al acumulador de la logica, asi
// que dibujar mas seguido no acelera el juego: la logica sigue a 25 pasos por segundo.
//
// Aca se reemplaza ese bloque por una llamada a FrameLimiter, que espera hasta el FPS configurado
// (QueryPerformanceCounter: GetTickCount tiene 15,6 ms de resolucion) y devuelve los milisegundos
// del cuadro, guardando las fracciones para no perder tiempo.
//
// Configuracion en camera3d.ini, junto a la DLL:
//   [Video]
//   FPS=60      ; cuadros por segundo; 25 o menos deja el juego como estaba
//   ShowFps=1   ; contador en el titulo de la ventana (0 para no mostrarlo)
//   Smooth=1    ; movimiento interpolado entre pasos de logica (0 para desactivarlo)

#include "mods.h"

#include <mmsystem.h>
#include <stdio.h>

#pragma comment(lib, "winmm.lib")

namespace
{
    constexpr DWORD FrameLimitStart = 0x004DA3A1; // MOV EAX,[0x00E8CB30] ... (medir y esperar)
    constexpr DWORD FrameLimitEnd = 0x004DA3E4;   // MOV ECX,[EBP-0x178] (suma al acumulador)
    constexpr int DefaultFps = 60;
    constexpr int OriginalFps = 25;

    LogFunction g_log = nullptr;
    LARGE_INTEGER g_frequency{};
    LARGE_INTEGER g_lastFrame{};
    double g_frameTicks = 0.0;     // duracion de un cuadro, en ticks de QueryPerformanceCounter
    double g_pendingMs = 0.0;      // fracciones de milisegundo todavia no entregadas a la logica
    int* const LogicTickCounter = reinterpret_cast<int*>(0x00E8CB20); // +1 por cada paso de logica

    // Estadisticas: contador en el titulo de la ventana (cada medio segundo) y detalle en el log (cada
    // 5 segundos). "Trabajo" es lo que tarda el juego en un cuadro (logica + dibujo), sin la espera.
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);
    LARGE_INTEGER g_statsStart{};
    LARGE_INTEGER g_titleStart{};
    int g_statsFrames = 0;
    int g_titleFrames = 0;
    int g_statsTicks = 0;
    double g_worstFrameMs = 0.0;
    double g_titleWorstMs = 0.0;
    double g_workTotalMs = 0.0;
    double g_worstWorkMs = 0.0;
    int g_hitches = 0;            // cuadros que tardaron mas de 1,5 veces lo esperado
    bool g_showFps = true;
    bool g_smooth = true;

    double ElapsedMs(const LARGE_INTEGER& from, const LARGE_INTEGER& to)
    {
        return static_cast<double>(to.QuadPart - from.QuadPart) * 1000.0 / g_frequency.QuadPart;
    }

    void UpdateStats(const LARGE_INTEGER& now, double frameMs, double workMs)
    {
        const double targetMs = g_frameTicks * 1000.0 / g_frequency.QuadPart;
        g_statsFrames++;
        g_titleFrames++;
        g_workTotalMs += workMs;
        g_worstWorkMs = max(g_worstWorkMs, workMs);
        g_worstFrameMs = max(g_worstFrameMs, frameMs);
        g_titleWorstMs = max(g_titleWorstMs, frameMs);
        if (frameMs > targetMs * 1.5)
        {
            g_hitches++;
        }

        const double titleMs = ElapsedMs(g_titleStart, now);
        if (g_showFps && titleMs >= 500.0 && *MainWindow != nullptr)
        {
            char title[96];
            sprintf_s(title, "MU - %.0f FPS (peor cuadro %.0f ms)", g_titleFrames * 1000.0 / titleMs, g_titleWorstMs);
            SetWindowTextA(*MainWindow, title);
            g_titleStart = now;
            g_titleFrames = 0;
            g_titleWorstMs = 0.0;
        }

        const double seconds = ElapsedMs(g_statsStart, now) / 1000.0;
        if (seconds >= 5.0)
        {
            const int ticks = *LogicTickCounter;
            g_log("FPS: %.1f cuadros/s, %.1f pasos de logica/s; peor cuadro %.1f ms, %d tirones (>%.0f ms); "
                  "trabajo promedio %.1f ms, peor %.1f ms.",
                g_statsFrames / seconds, (ticks - g_statsTicks) / seconds, g_worstFrameMs, g_hitches, targetMs * 1.5,
                g_workTotalMs / g_statsFrames, g_worstWorkMs);
            g_statsStart = now;
            g_statsFrames = 0;
            g_statsTicks = ticks;
            g_worstFrameMs = 0.0;
            g_workTotalMs = 0.0;
            g_worstWorkMs = 0.0;
            g_hitches = 0;
        }
    }

    // Devuelve los milisegundos que duro el cuadro (lo que antes quedaba en [EBP-0x68]).
    int __cdecl FrameLimiter()
    {
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        const double workMs = ElapsedMs(g_lastFrame, now);
        const double targetTicks = static_cast<double>(g_lastFrame.QuadPart) + g_frameTicks;

        // Dormir casi todo lo que falta y terminar con una espera activa corta, para ser preciso.
        while (static_cast<double>(now.QuadPart) < targetTicks)
        {
            const double remainingMs = (targetTicks - static_cast<double>(now.QuadPart)) * 1000.0 / g_frequency.QuadPart;
            if (remainingMs > 2.0)
            {
                Sleep(static_cast<DWORD>(remainingMs - 1.0));
            }
            else
            {
                YieldProcessor();
            }

            QueryPerformanceCounter(&now);
        }

        const double frameMs = ElapsedMs(g_lastFrame, now);
        g_pendingMs += frameMs;
        g_lastFrame = now;
        UpdateStats(now, frameMs, workMs);

        const int elapsedMs = static_cast<int>(g_pendingMs);
        g_pendingMs -= elapsedMs;
        return elapsedMs;
    }

    int ReadConfiguredFps()
    {
        HMODULE module = nullptr;
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&ReadConfiguredFps), &module);

        char path[MAX_PATH];
        GetModuleFileNameA(module, path, MAX_PATH);
        char* slash = strrchr(path, '\\');
        if (slash == nullptr)
        {
            return DefaultFps;
        }

        strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "camera3d.ini");
        g_showFps = GetPrivateProfileIntA("Video", "ShowFps", 1, path) != 0;
        g_smooth = GetPrivateProfileIntA("Video", "Smooth", 1, path) != 0;
        return GetPrivateProfileIntA("Video", "FPS", DefaultFps, path);
    }
}

bool InstallFps(LogFunction log)
{
    g_log = log;

    const int fps = ReadConfiguredFps();
    if (fps <= OriginalFps)
    {
        g_log("FPS: limite original de 25 (configurado %d).", fps);
        return false;
    }

    // MOV EAX,[0x00E8CB30]; SUB EAX,[EBP-0x74]; MOV [EBP-0x68],EAX; CMP [EBP-0x68],0x28; JGE +0x32
    const BYTE expected[] = { 0xA1, 0x30, 0xCB, 0xE8, 0x00, 0x2B, 0x45, 0x8C, 0x89, 0x45, 0x98,
        0x83, 0x7D, 0x98, 0x28, 0x7D, 0x32 };
    if (memcmp(reinterpret_cast<const void*>(FrameLimitStart), expected, sizeof(expected)) != 0)
    {
        g_log("FPS: el limite de cuadros no coincide; main.exe no compatible. No se cambia.");
        return false;
    }

    QueryPerformanceFrequency(&g_frequency);
    QueryPerformanceCounter(&g_lastFrame);
    g_statsStart = g_lastFrame;
    g_titleStart = g_lastFrame;
    g_frameTicks = static_cast<double>(g_frequency.QuadPart) / fps;
    timeBeginPeriod(1); // Sleep con resolucion de 1 ms

    // CALL FrameLimiter; MOV [EBP-0x68],EAX; JMP FrameLimitEnd
    BYTE patch[10];
    patch[0] = 0xE8;
    const DWORD call = reinterpret_cast<DWORD>(&FrameLimiter) - (FrameLimitStart + 5);
    memcpy(patch + 1, &call, 4);
    patch[5] = 0x89;
    patch[6] = 0x45;
    patch[7] = 0x98;
    patch[8] = 0xEB;
    patch[9] = static_cast<BYTE>(FrameLimitEnd - (FrameLimitStart + 10));
    if (!WriteCode(FrameLimitStart, patch, sizeof(patch)))
    {
        g_log("FPS: no se pudo parchear el limite de cuadros.");
        return false;
    }

    g_log("FPS: limite en %d cuadros por segundo.", fps);
    return true;
}

bool SmoothMotionEnabled()
{
    return g_smooth;
}
