// MU Helper: sigue corriendo con el inventario abierto.
//
// El paso del MU Helper (0x0095DC90, cada 200 ms) le pide al servidor que lo frene (C1 BF 51 01) si
// esta visible el inventario (ventana 0x0D) o la configuracion del helper (ventana 0x4F, tecla Z):
//
//   0x0095DE3E  PUSH 0x0D / CALL IsVisible
//   0x0095DE51  JNZ 0x0095DE68      ; inventario visible: mandar el pedido de frenar
//   0x0095DE53  PUSH 0x4F / CALL IsVisible
//   0x0095DE66  JZ 0x0095DECB       ; ninguna de las dos: seguir con el helper
//
// Con [MuHelper] SeguirConInventario=1 (por defecto) el salto del inventario se reemplaza por NOPs: el
// inventario ya no lo frena y la configuracion del helper sigue frenandolo.

#include "mods.h"

namespace
{
    constexpr DWORD InventoryCheckJump = 0x0095DE51;
    const BYTE OriginalJump[] = { 0x75, 0x15 }; // JNZ 0x0095DE68
    const BYTE PushInventory[] = { 0x6A, 0x0D }; // 0x0095DE3E PUSH 0x0D
    const BYTE PushHelperSetup[] = { 0x6A, 0x4F }; // 0x0095DE53 PUSH 0x4F

    bool BytesAre(DWORD address, const BYTE* expected, size_t length)
    {
        return memcmp(reinterpret_cast<const void*>(address), expected, length) == 0;
    }
}

bool InstallMuHelperInventory(LogFunction log)
{
    HMODULE module = nullptr;
    char path[MAX_PATH];
    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&InstallMuHelperInventory), &module)
        || GetModuleFileNameA(module, path, MAX_PATH) == 0)
    {
        return false;
    }

    char* slash = strrchr(path, '\\');
    if (slash == nullptr)
    {
        return false;
    }

    strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "camera3d.ini");
    if (GetPrivateProfileIntA("MuHelper", "SeguirConInventario", 1, path) == 0)
    {
        log("MU Helper: el inventario lo frena (SeguirConInventario=0).");
        return false;
    }

    if (!BytesAre(InventoryCheckJump - 0x13, PushInventory, sizeof(PushInventory))
        || !BytesAre(InventoryCheckJump, OriginalJump, sizeof(OriginalJump))
        || !BytesAre(InventoryCheckJump + 2, PushHelperSetup, sizeof(PushHelperSetup)))
    {
        log("MU Helper: el chequeo del inventario no coincide; no se cambia.");
        return false;
    }

    const BYTE nops[] = { 0x90, 0x90 };
    if (!WriteCode(InventoryCheckJump, nops, sizeof(nops)))
    {
        log("MU Helper: no se pudo parchear el chequeo del inventario.");
        return false;
    }

    log("MU Helper: sigue con el inventario abierto.");
    return true;
}
