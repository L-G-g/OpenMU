// addimport.exe <main.exe original> <main.exe de salida>
// Genera una copia de main.exe que, al arrancar, carga camera3d.dll (importa Camera3DVersion).
// Agrega una seccion nueva con la tabla de importaciones copiada + la entrada de camera3d.dll.
// El archivo original no se modifica.

#include <windows.h>
#include <stdio.h>
#include <vector>
#include <string.h>

static DWORD AlignUp(DWORD value, DWORD alignment)
{
    return (value + alignment - 1) / alignment * alignment;
}

int main(int argc, char** argv)
{
    if (argc != 3)
    {
        printf("Uso: addimport <entrada.exe> <salida.exe>\n");
        return 1;
    }

    FILE* in = nullptr;
    if (fopen_s(&in, argv[1], "rb") != 0)
    {
        printf("No se pudo abrir %s\n", argv[1]);
        return 1;
    }

    fseek(in, 0, SEEK_END);
    std::vector<BYTE> file(ftell(in));
    fseek(in, 0, SEEK_SET);
    fread(file.data(), 1, file.size(), in);
    fclose(in);

    auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(file.data());
    auto nt = reinterpret_cast<IMAGE_NT_HEADERS32*>(file.data() + dos->e_lfanew);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE || nt->Signature != IMAGE_NT_SIGNATURE || nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386)
    {
        printf("No es un ejecutable de 32 bits valido.\n");
        return 1;
    }

    auto sections = IMAGE_FIRST_SECTION(nt);
    const WORD sectionCount = nt->FileHeader.NumberOfSections;
    auto rvaToOffset = [&](DWORD rva) -> DWORD {
        for (WORD i = 0; i < sectionCount; i++)
        {
            const auto& s = sections[i];
            if (rva >= s.VirtualAddress && rva < s.VirtualAddress + max(s.Misc.VirtualSize, s.SizeOfRawData))
            {
                return rva - s.VirtualAddress + s.PointerToRawData;
            }
        }
        return 0;
    };

    // Lugar para un encabezado de seccion mas.
    const DWORD headersEnd = static_cast<DWORD>(reinterpret_cast<BYTE*>(&sections[sectionCount + 1]) - file.data());
    if (headersEnd > sections[0].PointerToRawData || headersEnd > nt->OptionalHeader.SizeOfHeaders)
    {
        printf("No hay espacio para una seccion nueva en los encabezados.\n");
        return 1;
    }

    // Descriptores de importacion actuales.
    auto& importDir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    const DWORD importOffset = rvaToOffset(importDir.VirtualAddress);
    std::vector<IMAGE_IMPORT_DESCRIPTOR> descriptors;
    for (auto d = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(file.data() + importOffset); d->Name != 0; d++)
    {
        const char* name = reinterpret_cast<const char*>(file.data() + rvaToOffset(d->Name));
        if (_stricmp(name, "camera3d.dll") == 0)
        {
            printf("El ejecutable ya carga camera3d.dll.\n");
            return 1;
        }

        descriptors.push_back(*d);
    }

    // Seccion nueva: descriptores (+1 nuevo +1 vacio), nombre, tabla de nombres e IAT.
    const auto& last = sections[sectionCount - 1];
    const DWORD sectionAlignment = nt->OptionalHeader.SectionAlignment;
    const DWORD fileAlignment = nt->OptionalHeader.FileAlignment;
    const DWORD newRva = AlignUp(last.VirtualAddress + max(last.Misc.VirtualSize, last.SizeOfRawData), sectionAlignment);
    const DWORD newOffset = AlignUp(static_cast<DWORD>(file.size()), fileAlignment);

    std::vector<BYTE> data;
    const DWORD descriptorsSize = static_cast<DWORD>((descriptors.size() + 2) * sizeof(IMAGE_IMPORT_DESCRIPTOR));
    data.resize(descriptorsSize);
    auto append = [&](const void* bytes, size_t length) -> DWORD {
        const DWORD rva = newRva + static_cast<DWORD>(data.size());
        data.insert(data.end(), static_cast<const BYTE*>(bytes), static_cast<const BYTE*>(bytes) + length);
        while (data.size() % 4 != 0) data.push_back(0);
        return rva;
    };

    const char dllName[] = "camera3d.dll";
    const DWORD nameRva = append(dllName, sizeof(dllName));
    BYTE hintName[2 + sizeof("Camera3DVersion")] = { 0, 0 };
    memcpy(hintName + 2, "Camera3DVersion", sizeof("Camera3DVersion"));
    const DWORD hintNameRva = append(hintName, sizeof(hintName));
    const DWORD thunks[2] = { hintNameRva, 0 };
    const DWORD intRva = append(thunks, sizeof(thunks));
    const DWORD iatRva = append(thunks, sizeof(thunks));

    IMAGE_IMPORT_DESCRIPTOR added{};
    added.OriginalFirstThunk = intRva;
    added.Name = nameRva;
    added.FirstThunk = iatRva;
    descriptors.push_back(added);
    descriptors.push_back(IMAGE_IMPORT_DESCRIPTOR{});
    memcpy(data.data(), descriptors.data(), descriptorsSize);

    const DWORD rawSize = AlignUp(static_cast<DWORD>(data.size()), fileAlignment);
    data.resize(rawSize);

    IMAGE_SECTION_HEADER section{};
    memcpy(section.Name, ".cam3d", 6);
    section.Misc.VirtualSize = static_cast<DWORD>(data.size());
    section.VirtualAddress = newRva;
    section.SizeOfRawData = rawSize;
    section.PointerToRawData = newOffset;
    // El cargador escribe la IAT, asi que tiene que poder escribirse.
    section.Characteristics = IMAGE_SCN_CNT_INITIALIZED_DATA | IMAGE_SCN_MEM_READ | IMAGE_SCN_MEM_WRITE;
    sections[sectionCount] = section;

    nt->FileHeader.NumberOfSections++;
    nt->OptionalHeader.SizeOfImage = AlignUp(newRva + section.Misc.VirtualSize, sectionAlignment);
    importDir.VirtualAddress = newRva;
    importDir.Size = descriptorsSize;
    // Las importaciones "pre-enlazadas" ya no coinciden con la tabla nueva: se descartan.
    nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT] = {};
    nt->OptionalHeader.CheckSum = 0;

    file.resize(newOffset, 0);
    file.insert(file.end(), data.begin(), data.end());

    FILE* out = nullptr;
    if (fopen_s(&out, argv[2], "wb") != 0)
    {
        printf("No se pudo escribir %s\n", argv[2]);
        return 1;
    }

    fwrite(file.data(), 1, file.size(), out);
    fclose(out);
    printf("Listo: %s ahora carga camera3d.dll al arrancar.\n", argv[2]);
    return 0;
}
