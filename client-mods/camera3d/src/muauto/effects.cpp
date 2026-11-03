// Contador de efectos (pruebas): cuenta los efectos que crea el juego, para saber sin mirar la pantalla si
// una habilidad dibujo su efecto. No parchea nada: en cada paso de logica recorre el arreglo de efectos del
// juego y anota los que aparecieron desde el paso anterior.
//
// Datos del main.exe 1.04d (CreateEffect en 0x006D8F20):
//   - arreglo de efectos en 0x08967FE8, 200 lugares de 0x288 bytes;
//   - en cada uno: Live (byte) en +0x04, Type (int) en +0x30, Owner (OBJECT*) en +0x1D8.
// Del personaje propio (CHARACTER) se sigue AttackTime (byte en +0x22): el juego crea el efecto principal
// de un ataque cuando llega a su limite (15). Si vuelve a 1 antes, el efecto de ese ataque se pierde.
//
// Comandos: "fx reset" pone todo en cero; "fx" devuelve lo contado desde el ultimo reset.

#include "muauto.h"

#include <map>

namespace
{
    constexpr DWORD EffectsStart = 0x08967FE8;
    constexpr int EffectCount = 200;
    constexpr DWORD EffectSize = 0x288;
    constexpr DWORD EffectLive = 0x04;
    constexpr DWORD EffectType = 0x30;
    constexpr DWORD EffectOwner = 0x1D8;

    DWORD* const Hero = reinterpret_cast<DWORD*>(0x07BC4F04);
    constexpr DWORD CharObject = 0x308;
    constexpr DWORD CharAttackTime = 0x22;
    constexpr DWORD CharSkill = 0x26;
    constexpr DWORD CharTargetCharacter = 0x80;

    bool g_wasLive[EffectCount] = {};
    int g_lastType[EffectCount] = {};
    std::map<int, int> g_all;   // tipo -> cantidad creada
    std::map<int, int> g_own;   // tipo -> cantidad creada con el personaje propio como dueno
    int g_ticks = 0;
    int g_attackStarts = 0;     // AttackTime paso de 0 a 1
    int g_attackRestarts = 0;   // AttackTime volvio a 1 sin llegar al limite (efecto perdido)
    int g_attackCompleted = 0;  // AttackTime llego al limite (el juego creo el efecto principal)
    int g_completedWithTarget = 0;
    int g_lastAttackTime = 0;
    int g_lastTarget = -1;

    template <typename T>
    T Read(DWORD address)
    {
        return *reinterpret_cast<const T*>(address);
    }
}

void EffectsTick()
{
    __try
    {
        const DWORD hero = *Hero;
        const DWORD heroObject = hero != 0 ? hero + CharObject : 0;
        for (int i = 0; i < EffectCount; i++)
        {
            const DWORD effect = EffectsStart + i * EffectSize;
            const bool live = Read<BYTE>(effect + EffectLive) != 0;
            const int type = Read<int>(effect + EffectType);
            if (live && (!g_wasLive[i] || type != g_lastType[i]))
            {
                g_all[type]++;
                if (heroObject != 0 && Read<DWORD>(effect + EffectOwner) == heroObject)
                {
                    g_own[type]++;
                }
            }

            g_wasLive[i] = live;
            g_lastType[i] = type;
        }

        if (hero != 0)
        {
            // El paquete que empieza el ataque puede llegar antes o despues de que el juego sume 1 en el mismo
            // paso, asi que un ataque nuevo se ve como 1 o como 2, y un reinicio como un valor menor al anterior.
            const int attackTime = Read<BYTE>(hero + CharAttackTime);
            const bool completed = g_lastAttackTime >= 14 && (attackTime == 0 || attackTime == 1);
            if (completed)
            {
                g_attackCompleted++;
                if (g_lastTarget >= 0)
                {
                    g_completedWithTarget++;
                }
            }

            if (attackTime != 0 && (g_lastAttackTime == 0 || (completed && attackTime == 1)))
            {
                g_attackStarts++;
            }
            else if (attackTime != 0 && attackTime < g_lastAttackTime && !completed)
            {
                g_attackRestarts++;
            }

            g_lastAttackTime = attackTime;
            g_lastTarget = Read<short>(hero + CharTargetCharacter);
        }

        g_ticks++;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
    }
}

std::string EffectsCommand(const char* arguments)
{
    if (arguments != nullptr && _strnicmp(arguments, "reset", 5) == 0)
    {
        g_all.clear();
        g_own.clear();
        g_ticks = g_attackStarts = g_attackRestarts = g_attackCompleted = g_completedWithTarget = 0;
        return "fx: reset";
    }

    std::string result = "fx: pasos=" + std::to_string(g_ticks)
        + " ataques: empezados=" + std::to_string(g_attackStarts)
        + " reiniciados=" + std::to_string(g_attackRestarts)
        + " completos=" + std::to_string(g_attackCompleted)
        + " completos_con_objetivo=" + std::to_string(g_completedWithTarget);
    const DWORD hero = *Hero;
    if (hero != 0)
    {
        result += " skill=" + std::to_string(Read<WORD>(hero + CharSkill))
            + " objetivo=" + std::to_string(Read<short>(hero + CharTargetCharacter));
    }

    result += "\npropios:";
    for (const auto& entry : g_own)
    {
        result += " " + std::to_string(entry.first) + "x" + std::to_string(entry.second);
    }

    result += "\ntodos:";
    for (const auto& entry : g_all)
    {
        result += " " + std::to_string(entry.first) + "x" + std::to_string(entry.second);
    }

    return result;
}

std::string SkillFxCommand(const char* arguments)
{
    using SetEnabledType = void (*)(bool enabled);
    using CompletedForType = int (*)(const void* character);
    const HMODULE skillFx = GetModuleHandleA("skillfx.dll");
    const auto setEnabled = skillFx != nullptr
        ? reinterpret_cast<SetEnabledType>(GetProcAddress(skillFx, "SkillFxSetEnabled"))
        : nullptr;
    const auto completedFor = skillFx != nullptr
        ? reinterpret_cast<CompletedForType>(GetProcAddress(skillFx, "SkillFxCompletedFor"))
        : nullptr;
    if (setEnabled == nullptr || completedFor == nullptr)
    {
        return "error: skillfx: skillfx.dll no esta cargada";
    }

    static int s_baseline = 0;
    const void* hero = reinterpret_cast<const void*>(*Hero);
    if (arguments != nullptr && _strnicmp(arguments, "on", 2) == 0)
    {
        setEnabled(true);
        return "skillfx: on";
    }

    if (arguments != nullptr && _strnicmp(arguments, "off", 3) == 0)
    {
        setEnabled(false);
        return "skillfx: off";
    }

    if (arguments != nullptr && _strnicmp(arguments, "reset", 5) == 0)
    {
        s_baseline = completedFor(hero);
        return "skillfx: reset";
    }

    return "skillfx: completados=" + std::to_string(completedFor(hero) - s_baseline);
}
