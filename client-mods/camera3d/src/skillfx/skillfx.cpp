// skillfx.dll - Efectos de habilidades que no se pierden al castear seguido, para el cliente MU 1.04d con
// camera3d.dll.
//
// El juego crea el efecto principal de un ataque o habilidad cuando el contador AttackTime del personaje
// llega a su limite (15 casi siempre), unos 14 pasos de logica (560 ms) despues de empezar. Cada paquete
// de ataque nuevo (0x1E, 0x19, ...) vuelve a poner AttackTime en 1: si el personaje castea de nuevo antes
// de esos 560 ms (agilidad alta), el ataque anterior nunca llega al limite y su efecto no se dibuja,
// aunque el dano si entra. Con la Summoner y Drain Life se pierden 8 de cada 10 efectos desde ~3000 de
// agilidad (MagicSpeed ~190).
//
// Esta DLL completa el ataque pendiente en el mismo paso de logica en que empieza el nuevo: ejecuta el
// bloque de "fin de ataque" de MoveCharacter con la habilidad, el objetivo y el lugar del ataque anterior,
// y despues sigue con el ataque nuevo como si nada.
//
// Como funciona (MoveCharacter = 0x0055AF20, cdecl (CHARACTER*, OBJECT*)):
//   - 0x0055B3B4..0x0055B3E7: si AttackTime > 0, llama a 0x00556C10 (fija el limite g_iLimitAttackTime en
//     0x00E616C4 segun la habilidad y crea los efectos intermedios) y suma 1.
//   - 0x0055B3F1: CMP ECX(AttackTime),[0x00E616C4]; JL 0x00560478. Si llego al limite sigue en 0x0055B3FD:
//     AttackTime = 0 y un switch por la habilidad que crea el efecto; todos los casos terminan en
//     0x0056045C (Skill = 0, +0x86 = 0) y 0x00560471 (AttackFlag = 0), y siguen en 0x00560478.
//   - Gancho 1 en 0x0055B3F1 (6 bytes, el CMP): Decide() guarda por personaje el AttackTime de cada paso, el
//     limite, la habilidad, el objetivo y el lugar. Si AttackTime bajo (un paquete nuevo lo puso en 1 y el
//     juego le sumo 1) mientras el ataque anterior todavia no habia llegado al limite, guarda el ataque nuevo,
//     pone los datos del anterior y salta a 0x0055B3FD: el juego crea el efecto del ataque anterior.
//   - Gancho 2 en 0x00560471 (7 bytes, MOV ECX,[EBP+8]; MOV byte [ECX+0x21],0): Finish() vuelve a poner el
//     ataque nuevo y salta a 0x0055B3EA, donde el juego compara de nuevo (AttackTime 2, no llega) y sigue
//     con el resto de MoveCharacter. Sin ataque pendiente sigue en 0x00560478.
//   - Como todo pasa dentro de la misma llamada a MoveCharacter, las variables locales que usan los casos del
//     switch ya estan calculadas para ese personaje, y la animacion y la posicion no avanzan de mas.
//   - No se completan los ataques cuyo fin cambia la animacion del personaje (switch caso 0x4E: habilidades
//     78 y 518): pisaria la animacion del ataque nuevo. Lo demas que el fin de ataque toca del personaje
//     (el angulo y OBJECT +0x208 de las habilidades de Rage Fighter) se guarda y se vuelve a poner. Tampoco
//     Dark Side (263, 559) ni Pollution (225), que reinician AttackTime dentro del mismo casteo.
//   - Los efectos intermedios de 0x00556C10 (los que salen en un AttackTime fijo antes del limite) no se
//     repiten: del ataque anterior solo se crea el efecto final.
//
// CHARACTER: AttackFlag +0x21 (byte), AttackTime +0x22 (byte), Skill +0x26 (ushort), SkillX +0x2F,
// SkillY +0x30 (bytes), TargetCharacter +0x80 (short, -1 sin objetivo), +0x86 (ushort, se borra al terminar).

#include "../shared.h"

#include <unordered_map>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    // Los saltos de vuelta estan escritos como numeros en CompareHook y FinishHook:
    //   0x0055B3F7 JL 0x00560478 (despues del CMP); 0x0055B3FD AttackTime = 0 y switch por habilidad;
    //   0x0055B3EA MOV EAX,[EBP+8]; MOVZX ECX,byte [EAX+0x22]; CMP ...; 0x00560478 resto de MoveCharacter.
    constexpr DWORD CompareAddress = 0x0055B3F1;     // CMP ECX,dword ptr [0x00E616C4]
    const BYTE CompareBytes[] = { 0x3B, 0x0D, 0xC4, 0x16, 0xE6, 0x00 };
    constexpr DWORD FinishAddress = 0x00560471;      // MOV ECX,[EBP+8]; MOV byte ptr [ECX+0x21],0
    const BYTE FinishBytes[] = { 0x8B, 0x4D, 0x08, 0xC6, 0x41, 0x21, 0x00 };
    int* const LimitAttackTime = reinterpret_cast<int*>(0x00E616C4);

    constexpr DWORD AttackFlag = 0x21, AttackTime = 0x22, Skill = 0x26, SkillX = 0x2F, SkillY = 0x30;
    constexpr DWORD TargetCharacter = 0x80, AttackExtra = 0x86;
    // +0x06: el bit alto del objetivo de 0x19 (algunos casos llaman a 0x004C8AB0 con el); +0x418 (OBJECT +0x110)
    // el angulo, que el fin de las habilidades de Rage Fighter (260, 551, 552, 554, 555) vuelve a calcular
    // hacia el objetivo; +0x510 (OBJECT +0x208): esas mismas habilidades copian ahi el objetivo.
    constexpr DWORD SkillHitFlag = 0x06, Angle = 0x418, ObjectTarget = 0x510;

    // Un dato guardado vale si es del paso anterior (con margen por tirones).
    constexpr DWORD MaxAgeMs = 300;

    struct AttackState
    {
        BYTE attackFlag;
        BYTE attackTime;
        WORD skill;
        BYTE skillX;
        BYTE skillY;
        short target;
        WORD extra;
        BYTE hitFlag;
        float angle;
        WORD objectTarget;
    };

    struct Tracked
    {
        DWORD time;      // GetTickCount del ultimo paso
        int limit;       // limite de AttackTime para su habilidad
        AttackState last; // como quedo en el ultimo paso, despues de sumar 1
    };

    LogFunction g_log = nullptr;
    std::unordered_map<DWORD, Tracked> g_tracked;
    std::unordered_map<DWORD, int> g_completed; // ataques completados por personaje (para pruebas)
    DWORD g_pendingCharacter = 0; // personaje cuyo ataque anterior se esta completando
    AttackState g_newAttack{};
    bool g_enabled = true;

    AttackState Read(const BYTE* c)
    {
        AttackState s;
        s.attackFlag = c[AttackFlag];
        s.attackTime = c[AttackTime];
        s.skill = *reinterpret_cast<const WORD*>(c + Skill);
        s.skillX = c[SkillX];
        s.skillY = c[SkillY];
        s.target = *reinterpret_cast<const short*>(c + TargetCharacter);
        s.extra = *reinterpret_cast<const WORD*>(c + AttackExtra);
        s.hitFlag = c[SkillHitFlag];
        s.angle = *reinterpret_cast<const float*>(c + Angle);
        s.objectTarget = *reinterpret_cast<const WORD*>(c + ObjectTarget);
        return s;
    }

    void Write(BYTE* c, const AttackState& s)
    {
        c[AttackFlag] = s.attackFlag;
        c[AttackTime] = s.attackTime;
        *reinterpret_cast<WORD*>(c + Skill) = s.skill;
        c[SkillX] = s.skillX;
        c[SkillY] = s.skillY;
        *reinterpret_cast<short*>(c + TargetCharacter) = s.target;
        *reinterpret_cast<WORD*>(c + AttackExtra) = s.extra;
        c[SkillHitFlag] = s.hitFlag;
        *reinterpret_cast<float*>(c + Angle) = s.angle;
        *reinterpret_cast<WORD*>(c + ObjectTarget) = s.objectTarget;
    }

    // Habilidades que no se completan:
    //   - 78 y 518 (Fire Scream): su fin cambia la animacion del personaje (switch caso 0x4E).
    //   - 263 y 559 (Dark Side): cada golpe llega con su propio paquete y vuelve a poner AttackTime en 1 dentro
    //     del mismo casteo (medido: 35 reinicios en 9 casteos sin skillfx); completarlos agregaba efectos de mas.
    //   - 225 (Pollution): el juego ya termina dos ataques por casteo; completar el que se corta agregaba efectos.
    bool IsExcluded(WORD skill)
    {
        return skill == 78 || skill == 518 || skill == 263 || skill == 559 || skill == 225;
    }

    // Gancho 1: true para crear ahora el efecto del ataque anterior (salta al bloque de fin de ataque).
    bool __cdecl Decide(BYTE* c)
    {
        const DWORD now = GetTickCount();
        const AttackState current = Read(c);
        Tracked& tracked = g_tracked[reinterpret_cast<DWORD>(c)];
        const AttackState& last = tracked.last;
        const bool recent = now - tracked.time <= MaxAgeMs;
        // Un ataque cortado: AttackTime bajo de verdad (el paquete nuevo lo puso en 1 y el juego le sumo 1) y el
        // anterior ya llevaba al menos dos pasos. Con "igual" o con el anterior en 2 se confundian con un ataque
        // que no se corto: habilidades que dejan AttackTime quieto unos pasos (Death Stab, Dark Side) o 0x1E y
        // 0x19 del mismo ataque llegando en pasos seguidos.
        if (g_enabled && g_pendingCharacter == 0 && recent && current.attackTime != 0
            && current.attackTime < last.attackTime && last.attackTime >= 3 && last.attackTime < tracked.limit
            && !IsExcluded(last.skill))
        {
            // El ataque anterior se corto antes de llegar al limite: completarlo primero.
            g_newAttack = current;
            AttackState previous = last;
            previous.attackTime = static_cast<BYTE>(tracked.limit);
            Write(c, previous);
            g_pendingCharacter = reinterpret_cast<DWORD>(c);
            g_completed[reinterpret_cast<DWORD>(c)]++;
            return true;
        }

        tracked.time = now;
        tracked.limit = *LimitAttackTime;
        tracked.last = current;
        return false;
    }

    // Gancho 2: true si se completo un ataque pendiente y el juego tiene que seguir con el nuevo.
    bool __cdecl Finish(BYTE* c)
    {
        if (g_pendingCharacter == 0 || g_pendingCharacter != reinterpret_cast<DWORD>(c))
        {
            return false;
        }

        g_pendingCharacter = 0;
        Write(c, g_newAttack);
        Tracked& tracked = g_tracked[reinterpret_cast<DWORD>(c)];
        tracked.time = GetTickCount();
        tracked.last = g_newAttack;
        tracked.last.attackTime = 1; // la proxima comparacion ve 2: el ataque nuevo sigue normal
        return true;
    }

    __declspec(naked) void CompareHook()
    {
        __asm
        {
            pushad
            push dword ptr [ebp + 8]
            call Decide
            add esp, 4
            test al, al
            popad
            jnz fire
            cmp ecx, dword ptr ds:[0x00E616C4]
            push 0x0055B3F7 // CompareNext
            ret
        fire:
            push 0x0055B3FD // FirePath
            ret
        }
    }

    __declspec(naked) void FinishHook()
    {
        __asm
        {
            mov ecx, dword ptr [ebp + 8]
            mov byte ptr [ecx + 0x21], 0
            pushad
            push dword ptr [ebp + 8]
            call Finish
            add esp, 4
            test al, al
            popad
            jnz again
            push 0x00560478 // FinishNext
            ret
        again:
            push 0x0055B3EA // ReloadAttackTime
            ret
        }
    }

    bool Hook(DWORD address, const BYTE* expected, size_t length, void* target)
    {
        if (memcmp(reinterpret_cast<const void*>(address), expected, length) != 0)
        {
            return false;
        }

        BYTE code[8];
        memset(code, 0x90, sizeof(code));
        code[0] = 0xE9;
        *reinterpret_cast<DWORD*>(code + 1) = reinterpret_cast<DWORD>(target) - (address + 5);
        return WriteCode(address, code, length);
    }
}

extern "C" __declspec(dllexport) bool StartSkillFx(const SkillFxHost* host)
{
    if (host == nullptr || host->version != SkillFxHostVersion)
    {
        return false;
    }

    g_log = host->log;
    if (memcmp(reinterpret_cast<const void*>(CompareAddress), CompareBytes, sizeof(CompareBytes)) != 0
        || memcmp(reinterpret_cast<const void*>(FinishAddress), FinishBytes, sizeof(FinishBytes)) != 0)
    {
        g_log("Efectos de habilidades: MoveCharacter no coincide; desactivados.");
        return false;
    }

    if (!Hook(CompareAddress, CompareBytes, sizeof(CompareBytes), &CompareHook)
        || !Hook(FinishAddress, FinishBytes, sizeof(FinishBytes), &FinishHook))
    {
        g_log("Efectos de habilidades: no se pudieron instalar.");
        return false;
    }

    g_log("Efectos de habilidades: el efecto de un ataque cortado por otro se dibuja igual.");
    return true;
}

// Para pruebas (comando "skillfx" de muauto): cuantos ataques cortados de un personaje se completaron.
extern "C" __declspec(dllexport) int SkillFxCompletedFor(const void* character)
{
    const auto found = g_completed.find(reinterpret_cast<DWORD>(character));
    return found != g_completed.end() ? found->second : 0;
}

// Para pruebas: prende o apaga la DLL sin sacar los ganchos (apagada, el juego hace lo de siempre).
extern "C" __declspec(dllexport) void SkillFxSetEnabled(bool enabled)
{
    g_enabled = enabled;
}
