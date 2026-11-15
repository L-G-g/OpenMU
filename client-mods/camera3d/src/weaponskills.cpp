// Skills de arma aprendidas: se usan sin el arma que las trae.
//
// En PeroniaMU las armas ya no dan skills: las skills de arma (Defense, Falling Slash, Lunge, Uppercut, Cyclone,
// Slash, Triple Shot, Power Slash, Force Wave, Killing Blow, Beast Uppercut, Phoenix Shot) se aprenden con las quests
// y llegan en la lista de skills del personaje. El cliente las sigue atando al arma:
//
//   0x004EAC90  bool __thiscall CheckWeaponSkill(int skill): si la skill es de arma (tabla de skills [0x08128ABC],
//               0x58 bytes por skill, +0x54 == 1), solo devuelve true si el item de una mano ([0x08128AC4] +0x1240
//               o +0x12AB) es de un tipo que trae la skill y ese item la tiene. Lo usan el icono de la skill (gris si
//               es false, 0x00813C70), el lanzamiento de los guerreros (0x005AD040) y el arbol de maestria.
//   0x005AD1A2  JNZ 0x005AFF59 en el lanzamiento de los guerreros: sin arma en la mano derecha o con un baston en
//               alguna mano, solo siguen unas pocas skills (Swell Life, las del Dark Lord...); las demas no se lanzan.
//   0x005B554A  CALL 0x0059B580 y 0x005B557B CALL 0x005A0A30 (char __cdecl (personaje, item de una mano), una vez
//               por mano): lanzan la skill elegida solo si esta en la lista de skills del item (+0x25 cantidad,
//               +0x26 los numeros, WORD). Asi se lanzan las skills de arma; si ninguna mano la tiene, no pasa nada.
//
// Con camera3d.ini [WeaponSkills] Enabled=1 (por defecto), una skill de arma que esta en la lista de skills del
// personaje (la que manda el servidor) se puede usar con cualquier arma o sin arma: CheckWeaponSkill devuelve true
// para ella, el salto del lanzamiento la deja seguir y las dos llamadas reciben una copia del item de la mano con esa
// skill en su lista. El servidor decide quien tiene la skill.

#include "mods.h"

namespace
{
    constexpr DWORD CheckFunction = 0x004EAC90;
    constexpr DWORD NoWeaponJump = 0x005AD1A2;
    const BYTE CheckPrologue[] = { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x24 };         // PUSH EBP / MOV EBP,ESP / SUB ESP,0x24
    const BYTE OriginalNoWeaponJump[] = { 0x0F, 0x85, 0xB1, 0x2D, 0x00, 0x00 }; // JNZ 0x005AFF59
    constexpr DWORD WarriorHandCall = 0x005B554A;
    constexpr DWORD WarriorHandFunction = 0x0059B580;
    constexpr DWORD OtherHandCall = 0x005B557B;
    constexpr DWORD OtherHandFunction = 0x005A0A30;

    DWORD* const Hero = reinterpret_cast<DWORD*>(0x07BC4F04);
    DWORD* const SkillTable = reinterpret_cast<DWORD*>(0x08128ABC);
    DWORD* const CharacterAttribute = reinterpret_cast<DWORD*>(0x08128AC8);
    constexpr DWORD HeroCurrentSkill = 0x32;
    constexpr DWORD SkillSize = 0x58;
    constexpr DWORD SkillWeaponFlag = 0x54;
    constexpr DWORD AttrSkills = 0x78;
    constexpr int MaxSkills = 150;
    constexpr unsigned SkillCount = 0x28A;
    constexpr size_t ItemSize = 0x6B;
    constexpr DWORD ItemSkillCount = 0x25;
    constexpr DWORD ItemSkills = 0x26;

    using CheckType = bool(__thiscall*)(void* self, unsigned skill);
    CheckType g_originalCheck = nullptr;

    using HandSkillType = char(__cdecl*)(int character, BYTE* item);

    bool IsLearnedWeaponSkill(unsigned skill)
    {
        if (skill == 0 || skill >= SkillCount || *SkillTable == 0 || *CharacterAttribute == 0
            || *reinterpret_cast<const BYTE*>(*SkillTable + skill * SkillSize + SkillWeaponFlag) != 1)
        {
            return false;
        }

        const WORD* skills = reinterpret_cast<const WORD*>(*CharacterAttribute + AttrSkills);
        for (int i = 0; i < MaxSkills; i++)
        {
            if (skills[i] == skill)
            {
                return true;
            }
        }

        return false;
    }

    // Reemplazo de CheckWeaponSkill (__thiscall: this en ECX, la skill en la pila).
    bool __fastcall CheckWeaponSkill(void* self, void*, unsigned skill)
    {
        return IsLearnedWeaponSkill(skill) || g_originalCheck(self, skill);
    }

    // Llama al lanzamiento de una mano con una copia del item que tiene en su lista la skill elegida, si es una skill
    // de arma aprendida que el item no tiene.
    char CallWithSkill(HandSkillType original, int character, BYTE* item)
    {
        if (*Hero == 0 || *CharacterAttribute == 0)
        {
            return original(character, item);
        }

        const BYTE index = *reinterpret_cast<const BYTE*>(*Hero + HeroCurrentSkill);
        const WORD skill = index < MaxSkills
            ? *reinterpret_cast<const WORD*>(*CharacterAttribute + AttrSkills + index * 2)
            : 0;
        if (!IsLearnedWeaponSkill(skill))
        {
            return original(character, item);
        }

        const WORD* itemSkills = reinterpret_cast<const WORD*>(item + ItemSkills);
        for (int i = 0; i < item[ItemSkillCount]; i++)
        {
            if (itemSkills[i] == skill)
            {
                return original(character, item);
            }
        }

        BYTE copy[ItemSize];
        memcpy(copy, item, ItemSize);
        copy[ItemSkillCount] = 1;
        memcpy(copy + ItemSkills, &skill, sizeof(skill));
        return original(character, copy);
    }

    char __cdecl WarriorHandSkill(int character, BYTE* item)
    {
        return CallWithSkill(reinterpret_cast<HandSkillType>(WarriorHandFunction), character, item);
    }

    char __cdecl OtherHandSkill(int character, BYTE* item)
    {
        return CallWithSkill(reinterpret_cast<HandSkillType>(OtherHandFunction), character, item);
    }

    bool __cdecl AllowWithoutWeapon(unsigned skill)
    {
        return IsLearnedWeaponSkill(skill);
    }

    // En lugar del JNZ 0x005AFF59: la ultima comparacion (skill == 0xEE) dejo ZF; la skill esta en [EBP+0x0C].
    __declspec(naked) void NoWeaponCheck()
    {
        __asm
        {
            jz allow
            pushad
            push dword ptr [ebp + 0x0C]
            call AllowWithoutWeapon
            add esp, 4
            test al, al
            popad
            jnz allow
            push 0x005AFF59 // no se lanza
            ret
        allow:
            push 0x005AD1A8 // sigue el lanzamiento
            ret
        }
    }

    bool BytesAre(DWORD address, const BYTE* expected, size_t length)
    {
        return memcmp(reinterpret_cast<const void*>(address), expected, length) == 0;
    }

    bool WriteJump(DWORD address, const void* target, size_t length)
    {
        BYTE code[8] = { 0xE9 };
        const DWORD relative = reinterpret_cast<DWORD>(target) - (address + 5);
        memcpy(code + 1, &relative, sizeof(relative));
        for (size_t i = 5; i < length; i++)
        {
            code[i] = 0x90;
        }

        return WriteCode(address, code, length);
    }

    bool CallTargets(DWORD address, DWORD target)
    {
        DWORD relative;
        memcpy(&relative, reinterpret_cast<const void*>(address + 1), sizeof(relative));
        return *reinterpret_cast<const BYTE*>(address) == 0xE8 && address + 5 + relative == target;
    }

    bool WriteCall(DWORD address, const void* target)
    {
        BYTE code[5] = { 0xE8 };
        const DWORD relative = reinterpret_cast<DWORD>(target) - (address + 5);
        memcpy(code + 1, &relative, sizeof(relative));
        return WriteCode(address, code, sizeof(code));
    }

    bool ReadIniEnabled()
    {
        HMODULE module = nullptr;
        char path[MAX_PATH];
        if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                reinterpret_cast<LPCSTR>(&ReadIniEnabled), &module)
            || GetModuleFileNameA(module, path, MAX_PATH) == 0)
        {
            return true;
        }

        char* slash = strrchr(path, '\\');
        if (slash == nullptr)
        {
            return true;
        }

        strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "camera3d.ini");
        return GetPrivateProfileIntA("WeaponSkills", "Enabled", 1, path) != 0;
    }
}

bool InstallWeaponSkills(LogFunction log)
{
    if (!ReadIniEnabled())
    {
        log("Skills de arma: necesitan el arma (WeaponSkills Enabled=0).");
        return false;
    }

    if (!BytesAre(CheckFunction, CheckPrologue, sizeof(CheckPrologue))
        || !BytesAre(NoWeaponJump, OriginalNoWeaponJump, sizeof(OriginalNoWeaponJump))
        || !CallTargets(WarriorHandCall, WarriorHandFunction)
        || !CallTargets(OtherHandCall, OtherHandFunction))
    {
        log("Skills de arma: el codigo del juego no coincide; no se cambia.");
        return false;
    }

    // El comienzo original de CheckWeaponSkill y un salto al resto, para llamarla desde el reemplazo.
    BYTE* trampoline = static_cast<BYTE*>(VirtualAlloc(nullptr, 16, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
    if (trampoline == nullptr)
    {
        log("Skills de arma: no se pudo reservar memoria.");
        return false;
    }

    memcpy(trampoline, CheckPrologue, sizeof(CheckPrologue));
    trampoline[6] = 0xE9;
    const DWORD back = (CheckFunction + sizeof(CheckPrologue)) - (reinterpret_cast<DWORD>(trampoline) + 11);
    memcpy(trampoline + 7, &back, sizeof(back));
    FlushInstructionCache(GetCurrentProcess(), trampoline, 16);
    g_originalCheck = reinterpret_cast<CheckType>(trampoline);

    if (!WriteJump(CheckFunction, reinterpret_cast<const void*>(&CheckWeaponSkill), sizeof(CheckPrologue))
        || !WriteJump(NoWeaponJump, reinterpret_cast<const void*>(&NoWeaponCheck), sizeof(OriginalNoWeaponJump))
        || !WriteCall(WarriorHandCall, reinterpret_cast<const void*>(&WarriorHandSkill))
        || !WriteCall(OtherHandCall, reinterpret_cast<const void*>(&OtherHandSkill)))
    {
        log("Skills de arma: no se pudo parchear el juego.");
        return false;
    }

    log("Skills de arma: las aprendidas se usan sin el arma.");
    return true;
}
