// Carga de las escenas de dialogo con los espectros (cinematicas.dll).
//
// Si cinematicas.dll esta junto a camera3d.dll y camera3d.ini no la desactiva ([Cinematicas] Enabled=0), se carga
// despues de espectros.dll y se le pasa el log, la ruta de camera3d.ini, la camara y el teclado. cinematicas.dll hace
// que un click sobre un espectro del refugio muestre una escena: fundido a negro, camara en primera persona mirando
// al espectro y su dialogo con los botones Duelo y Volver mas tarde.
//
// Sin cinematicas.dll el cliente funciona igual que antes (en el refugio se pelea con /espectro duelo).

#include "mods.h"

namespace
{
    CameraOverride g_cameraOverride = nullptr;

    void SetCameraOverride(CameraOverride callback)
    {
        g_cameraOverride = callback;
    }
}

void LoadCinematicas(LogFunction log)
{
    HMODULE module = nullptr;
    static char iniPath[MAX_PATH];
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&LoadCinematicas), &module)
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
    strcpy_s(iniPath, path);
    if (GetPrivateProfileIntA("Cinematicas", "Enabled", 1, iniPath) == 0)
    {
        log("Cinematicas: desactivado en camera3d.ini.");
        return;
    }

    strcpy_s(slash + 1, room, "cinematicas.dll");
    if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES)
    {
        return; // cliente sin cinematicas.dll
    }

    const HMODULE library = LoadLibraryA(path);
    const auto start = library != nullptr
        ? reinterpret_cast<StartCinematicasFunction>(GetProcAddress(library, StartCinematicasName))
        : nullptr;
    if (start == nullptr)
    {
        log("Cinematicas: no se pudo cargar %s.", path);
        return;
    }

    static CinematicasHost host{};
    host.version = CinematicasHostVersion;
    host.log = log;
    host.iniPath = iniPath;
    host.setCameraOverride = &SetCameraOverride;
    host.setKeyTick = &SetCinematicasKeyTick;
    start(&host);
}

bool CinematicasCamera(float* position, float* angle)
{
    return g_cameraOverride != nullptr && g_cameraOverride(position, angle);
}
