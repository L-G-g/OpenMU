// Carga de los efectos de habilidades (skillfx.dll).
//
// Si skillfx.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([SkillFx] Enabled=0), se carga
// despues de tabmap.dll y se le pasa el log. skillfx.dll completa el ataque anterior cuando uno nuevo lo
// corta, para que su efecto se dibuje aunque se castee seguido con mucha agilidad.
//
// Sin skillfx.dll el cliente funciona igual que antes.

#include "mods.h"

void LoadSkillFx(LogFunction log)
{
    HMODULE module = nullptr;
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadSkillFx), &module)
        || GetModuleFileNameA(module, path, MAX_PATH) == 0)
    {
        return;
    }

    char* slash = strrchr(path, '\\');
    if (slash == nullptr)
    {
        return;
    }

    const size_t room = MAX_PATH - (slash + 1 - path);
    strcpy_s(slash + 1, room, "camera3d.ini");
    if (GetPrivateProfileIntA("SkillFx", "Enabled", 1, path) == 0)
    {
        log("Efectos de habilidades: desactivados en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "skillfx.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin skillfx.dll
    }

    const HMODULE skillFx = LoadLibraryA(path);
    const auto start = skillFx != nullptr
        ? reinterpret_cast<StartSkillFxFunction>(GetProcAddress(skillFx, StartSkillFxName))
        : nullptr;
    if (start == nullptr)
    {
        log("Efectos de habilidades: no se pudo cargar %s.", path);
        return;
    }

    static SkillFxHost host{};
    host.version = SkillFxHostVersion;
    host.log = log;
    start(&host);
}
