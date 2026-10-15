// Diagnostico (solo pruebas): que cambia el dibujo de la escena principal en los personajes.
//
// Envuelve la llamada al dibujo de la escena principal (0x004DA2DC -> 0x004D95E0). Antes de dibujar
// copia los 400 personajes y despues compara: cada campo (de 4 bytes) que cambio durante el dibujo se
// cuenta. Cada 10 segundos anota en el log los campos que mas cambian, con un ejemplo del cambio.
//
// Se activa con camera3d.ini:
//   [Debug]
//   RenderDiff=1

#include "muauto.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

namespace
{
    DWORD* const CharacterManager = reinterpret_cast<DWORD*>(0x00E7C224);
    constexpr int CharacterCount = 400;
    constexpr int CharacterSize = 0x598;
    constexpr int FieldCount = CharacterSize / 4;

    // Modo 2: toda la seccion .data de main.exe, una vez por segundo, en cuadros sin paso de logica.
    constexpr DWORD DataStart = 0x00E5F000;
    constexpr DWORD DataSize = 0x08A1B104 & ~3u;
    BYTE* g_dataBefore = nullptr;
    BYTE* g_dataChanged = nullptr; // 1 byte por palabra: cuantas veces cambio (hasta 255)
    int g_mode = 0;
    int g_samples = 0;
    DWORD g_lastSample = 0;

    LogFunction g_log = nullptr;
    BYTE g_before[CharacterCount * CharacterSize];
    int g_changes[FieldCount];
    float g_sampleBefore[FieldCount];
    float g_sampleAfter[FieldCount];
    int g_frames = 0;
    DWORD g_lastReport = 0;

    BYTE* GetCharacters()
    {
        const DWORD manager = *CharacterManager;
        return manager != 0 ? *reinterpret_cast<BYTE**>(manager + 8) : nullptr;
    }

    void Report()
    {
        int order[FieldCount];
        for (int i = 0; i < FieldCount; i++)
        {
            order[i] = i;
        }

        qsort_s(order, FieldCount, sizeof(int), [](void*, const void* a, const void* b) {
            return g_changes[*static_cast<const int*>(b)] - g_changes[*static_cast<const int*>(a)];
        }, nullptr);

        g_log("Diagnostico: %d cuadros. Campos de personaje que cambian al dibujar:", g_frames);
        for (int i = 0; i < 25 && g_changes[order[i]] > 0; i++)
        {
            const int field = order[i];
            g_log("  +0x%03X: %d veces (ej. %g -> %g)", field * 4, g_changes[field], g_sampleBefore[field], g_sampleAfter[field]);
        }

        memset(g_changes, 0, sizeof(g_changes));
        g_frames = 0;
    }

    void ReportData()
    {
        g_log("Diagnostico de memoria: %d muestras (cuadros sin paso de logica). Zonas que cambia el dibujo:", g_samples);
        const DWORD words = DataSize / 4;
        int reported = 0;
        for (DWORD w = 0; w < words && reported < 60;)
        {
            if (g_dataChanged[w] == 0)
            {
                w++;
                continue;
            }

            // Zona: palabras cambiadas separadas por menos de 64 bytes.
            DWORD end = w;
            DWORD count = 0;
            int maxHits = 0;
            for (DWORD k = w; k < words && k <= end + 16; k++)
            {
                if (g_dataChanged[k] != 0)
                {
                    end = k;
                    count++;
                    maxHits = max(maxHits, static_cast<int>(g_dataChanged[k]));
                }
            }

            if (maxHits * 2 >= g_samples) // cambia en al menos la mitad de las muestras
            {
                const DWORD first = DataStart + w * 4;
                g_log("  0x%08X-0x%08X: %u palabras, hasta %d/%d veces (ej. 0x%08X = %g)", first, DataStart + end * 4 + 3,
                    count, maxHits, g_samples, first, *reinterpret_cast<const float*>(first));
                reported++;
            }

            w = end + 1;
        }

        memset(g_dataChanged, 0, DataSize / 4);
        g_samples = 0;
    }

    char RenderDataHook(RenderFunctionType render, bool extraFrame)
    {
        const bool sample = extraFrame && GetTickCount() - g_lastSample > 1000;
        if (!sample)
        {
            return render();
        }

        g_lastSample = GetTickCount();
        memcpy(g_dataBefore, reinterpret_cast<const void*>(DataStart), DataSize);
        const char result = render();
        const DWORD* before = reinterpret_cast<const DWORD*>(g_dataBefore);
        const DWORD* after = reinterpret_cast<const DWORD*>(DataStart);
        for (DWORD w = 0; w < DataSize / 4; w++)
        {
            if (before[w] != after[w] && g_dataChanged[w] < 255)
            {
                g_dataChanged[w]++;
            }
        }

        if (++g_samples >= 20)
        {
            ReportData();
        }

        return result;
    }

    char RenderHook(RenderFunctionType render, bool)
    {
        BYTE* characters = nullptr;
        __try
        {
            characters = GetCharacters();
            if (characters != nullptr)
            {
                memcpy(g_before, characters, sizeof(g_before));
            }
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            characters = nullptr;
        }

        const char result = render();

        if (characters != nullptr)
        {
            g_frames++;
            for (int c = 0; c < CharacterCount; c++)
            {
                const DWORD* before = reinterpret_cast<const DWORD*>(g_before + c * CharacterSize);
                const DWORD* after = reinterpret_cast<const DWORD*>(characters + c * CharacterSize);
                for (int f = 0; f < FieldCount; f++)
                {
                    if (before[f] != after[f])
                    {
                        g_changes[f]++;
                        g_sampleBefore[f] = *reinterpret_cast<const float*>(&before[f]);
                        g_sampleAfter[f] = *reinterpret_cast<const float*>(&after[f]);
                    }
                }
            }
        }

        if (GetTickCount() - g_lastReport > 10000)
        {
            g_lastReport = GetTickCount();
            Report();
        }

        return result;
    }

    bool IsEnabled()
    {
        HMODULE module = nullptr;
        GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&IsEnabled), &module);

        char path[MAX_PATH];
        GetModuleFileNameA(module, path, MAX_PATH);
        char* slash = strrchr(path, '\\');
        if (slash == nullptr)
        {
            return false;
        }

        strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "camera3d.ini");
        g_mode = GetPrivateProfileIntA("Debug", "RenderDiff", 0, path);
        return g_mode != 0;
    }
}

bool InstallRenderDiagnostics(LogFunction log)
{
    g_log = log;
    if (!IsEnabled())
    {
        return false;
    }

    if (g_mode == 2)
    {
        g_dataBefore = static_cast<BYTE*>(VirtualAlloc(nullptr, DataSize, MEM_COMMIT, PAGE_READWRITE));
        g_dataChanged = static_cast<BYTE*>(VirtualAlloc(nullptr, DataSize / 4, MEM_COMMIT, PAGE_READWRITE));
        if (g_dataBefore == nullptr || g_dataChanged == nullptr)
        {
            g_log("Diagnostico: sin memoria para copiar .data.");
            return false;
        }
    }

    Host().setRenderDiagnostics(g_mode == 2 ? &RenderDataHook : &RenderHook);
    g_log("Diagnostico del dibujo activo.");
    return true;
}
