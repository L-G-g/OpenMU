// camera3d.dll - Camara 3D para el cliente MU Online 1.04d (main.exe S6E3, parche OpenMU).
//
// Controles (dentro del juego):
//   - Rueda del mouse:            acercar / alejar.
//   - Boton del medio + arrastrar: girar (izquierda/derecha) e inclinar (arriba/abajo).
//   - Click del medio sin mover:   volver a la camara original.
//
// Funcionamiento:
//   - Se reemplaza la llamada a la funcion de camara (0x004D84E0) que hace la escena principal
//     (0x004D960F) por una funcion propia, que ajusta los angulos y la distancia antes y despues
//     de llamar a la original.
//   - El recorte del terreno (0x005DB130) usaba un angulo fijo de -45 grados; se lo cambia para
//     que use el angulo actual de la camara, si no al girar desaparecerian partes del suelo.
//   - Antes de parchear se verifica que los bytes coincidan con los esperados. Si el main.exe es
//     otro, la DLL no toca nada y lo anota en camera3d.log.

#include <windows.h>
#include <math.h>
#include <stdio.h>
#include <share.h>

namespace
{
    // --- Direcciones del main.exe 1.04d ---
    constexpr DWORD CameraFunction = 0x004D84E0;      // char __cdecl MoveMainCamera()
    constexpr DWORD CameraCallSite = 0x004D960F;      // CALL CameraFunction en la escena principal
    constexpr DWORD TerrainCullYawInstr = 0x005DB8D7; // FLD dword ptr [0x00D27AE4] (-45.0)
    constexpr DWORD TerrainAreaFunction = 0x005DB130; // calcula el area de terreno visible
    constexpr DWORD TerrainAreaCallSite = 0x005DBFE3; // CALL TerrainAreaFunction

    float* const CameraAngle = reinterpret_cast<float*>(0x087933D0);    // [0]=inclinacion, [1]=Y, [2]=giro
    float* const CameraPosition = reinterpret_cast<float*>(0x087933DC); // x, y, z
    float* const CameraDistance = reinterpret_cast<float*>(0x00E8CB6C); // distancia actual (suavizada)
    short* const CameraLevel = reinterpret_cast<short*>(0x00E8CB1C);    // 5 = modo especial
    float* const ViewFar = reinterpret_cast<float*>(0x00E61E3C);        // distancia maxima de dibujo
    float* const TerrainAreaX = reinterpret_cast<float*>(0x082C64B8);   // 4 esquinas, en baldosas
    float* const TerrainAreaY = reinterpret_cast<float*>(0x082C64A8);
    const double* const TileScale = reinterpret_cast<const double*>(0x00D23AC0); // posicion -> baldosa
    DWORD* const HeroPointer = reinterpret_cast<DWORD*>(0x07BC4F04);    // personaje propio
    HWND* const MainWindow = reinterpret_cast<HWND*>(0x00E8C578);
    constexpr DWORD HeroPositionOffset = 0x404;                         // float x, y, z

    // --- Limites ---
    constexpr float DefaultDistance = 1000.0f;
    constexpr float MinDistance = 500.0f;
    constexpr float MaxDistance = 2000.0f;
    constexpr float ZoomStep = 100.0f;
    constexpr float MinPitchDelta = -25.0f; // mas horizontal
    constexpr float MaxPitchDelta = 30.0f;  // mas desde arriba
    constexpr float DegreesPerPixel = 0.3f;
    constexpr float PivotHeight = 100.0f;   // altura del punto alrededor del cual se inclina
    constexpr float CloseZoomLift = 0.12f;  // cuanto sube la camara por cada unidad que se acerca

    // --- Estado (todo se usa desde el hilo del juego) ---
    volatile float g_yawDelta = 0.0f;
    volatile float g_pitchDelta = 0.0f;
    volatile float g_distance = DefaultDistance;
    bool g_dragging = false;
    bool g_dragMoved = false;
    POINT g_lastMouse{};
    float g_lastYawWeSet = 0.0f;
    float g_baseYaw = -45.0f;
    bool g_yawInitialized = false;
    bool g_gameControlsYaw = false;
    float g_baseViewFar = 0.0f;
    float g_lastViewFarWeSet = -1.0f;
    WNDPROC g_originalWndProc = nullptr;
    FILE* g_log = nullptr;

    void Log(const char* format, ...)
    {
        if (g_log == nullptr)
        {
            return;
        }

        va_list args;
        va_start(args, format);
        vfprintf(g_log, format, args);
        va_end(args);
        fputc('\n', g_log);
        fflush(g_log);
    }

    bool BytesMatch(DWORD address, const BYTE* expected, size_t length)
    {
        __try
        {
            return memcmp(reinterpret_cast<const void*>(address), expected, length) == 0;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return false;
        }
    }

    bool WriteCode(DWORD address, const void* data, size_t length)
    {
        DWORD oldProtect;
        if (!VirtualProtect(reinterpret_cast<void*>(address), length, PAGE_EXECUTE_READWRITE, &oldProtect))
        {
            return false;
        }

        memcpy(reinterpret_cast<void*>(address), data, length);
        VirtualProtect(reinterpret_cast<void*>(address), length, oldProtect, &oldProtect);
        FlushInstructionCache(GetCurrentProcess(), reinterpret_cast<void*>(address), length);
        return true;
    }

    // Cuanto hay que agrandar el area de terreno que se dibuja (y la distancia de vision) para la
    // vista actual: alejando se ve mas terreno, y mirando mas horizontal se ve mas lejos.
    float ViewScale()
    {
        // El area del juego es justa para su camara (incluso sin tocarla aparecen baldosas negras en
        // algunas esquinas), asi que siempre se le da margen extra.
        float scale = g_distance / DefaultDistance;
        if (scale < 1.0f)
        {
            // De cerca la camara se sube (ver PivotHeightFor) sin cambiar su angulo, y eso hace que
            // la parte de arriba de la pantalla mire mas lejos: hace falta mas terreno, no menos.
            scale = 1.0f + (DefaultDistance - g_distance) * CloseZoomLift / 150.0f;
        }

        scale *= 1.8f;
        if (g_yawDelta != 0.0f)
        {
            scale *= 1.15f; // girada, las esquinas de la pantalla quedan fuera del area original
        }

        if (g_pitchDelta < 0.0f)
        {
            scale *= 1.0f - g_pitchDelta / 25.0f; // hasta el doble con la inclinacion minima
        }

        return scale;
    }

    // Altura del punto al que mira la camara: al acercarse se lo sube, para que entren las alas.
    float PivotHeightFor(float distance)
    {
        if (distance >= DefaultDistance)
        {
            return PivotHeight;
        }

        return PivotHeight + (DefaultDistance - distance) * CloseZoomLift; // hasta +60 con el zoom minimo
    }

    // Se llama en lugar del calculo del area de terreno visible (0x005DB130). Despues de calcularla,
    // se agrandan sus cuatro esquinas alrededor del punto de referencia (el personaje).
    void __cdecl TerrainAreaHook(float* center, char flag)
    {
        using TerrainAreaType = void(__cdecl*)(float*, char);
        reinterpret_cast<TerrainAreaType>(TerrainAreaFunction)(center, flag);

        const float scale = ViewScale();
        if (scale <= 1.0f || center == nullptr)
        {
            return;
        }

        // Las esquinas estan en unidades de baldosa: posicion * TileScale.
        const float tileScale = static_cast<float>(*TileScale);
        const float cx = center[0] * tileScale;
        const float cy = center[1] * tileScale;
        for (int i = 0; i < 4; i++)
        {
            TerrainAreaX[i] = cx + (TerrainAreaX[i] - cx) * scale;
            TerrainAreaY[i] = cy + (TerrainAreaY[i] - cy) * scale;
        }
    }

    // Se llama en lugar de la funcion de camara original, en cada cuadro de la escena principal.
    char __cdecl CameraHook()
    {
        using CameraFunctionType = char(__cdecl*)();

        // 1) Giro: la funcion original calcula la posicion de la camara a partir del angulo de giro,
        //    asi que alcanza con fijarlo antes. Si el juego cambio el angulo por su cuenta (escenas
        //    especiales), se toma ese valor como nueva base.
        if (!g_yawInitialized || CameraAngle[2] != g_lastYawWeSet)
        {
            g_baseYaw = CameraAngle[2];
            g_yawInitialized = true;
        }

        if (!g_gameControlsYaw)
        {
            CameraAngle[2] = g_baseYaw + g_yawDelta;
        }

        g_lastYawWeSet = CameraAngle[2];

        // La original calcula la camara con la distancia que tiene guardada al empezar.
        const float originalDistance = *CameraDistance;
        const float yawBeforeCall = CameraAngle[2];
        const char result = reinterpret_cast<CameraFunctionType>(CameraFunction)();

        // En algunas escenas el juego fija el giro el mismo en cada cuadro. Mientras lo haga, se
        // deja el giro en sus manos, para que la posicion y la vista no queden desparejas.
        g_gameControlsYaw = CameraAngle[2] != yawBeforeCall;
        if (g_gameControlsYaw)
        {
            g_lastYawWeSet = CameraAngle[2];
        }

        // Si la camara esta en un modo especial (nivel 5, por ejemplo montado/eventos), no se toca.
        const DWORD hero = *HeroPointer;
        if (*CameraLevel == 5 || hero == 0 || originalDistance < 1.0f)
        {
            return result;
        }

        // 2) Zoom e inclinacion alrededor del centro del personaje. La camara original mira hacia
        //    el personaje; se acerca/aleja la camara sobre esa misma linea (asi el personaje queda en
        //    el mismo lugar de la pantalla a cualquier distancia) y se la gira hacia arriba/abajo
        //    alrededor de ese punto, inclinando la vista lo mismo.
        if (g_distance != DefaultDistance || g_pitchDelta != 0.0f)
        {
            const float* heroPosition = reinterpret_cast<const float*>(hero + HeroPositionOffset);
            // El acercamiento se hace sobre la linea hacia el centro del personaje (PivotHeight), y
            // despues se sube todo un poco si se esta muy cerca, para que entren las alas.
            const float pivot[3] = { heroPosition[0], heroPosition[1], heroPosition[2] + PivotHeight };
            const float zoom = g_distance / originalDistance;
            const float lift = PivotHeightFor(g_distance) - PivotHeight;
            float offset[3] = {
                (CameraPosition[0] - pivot[0]) * zoom,
                (CameraPosition[1] - pivot[1]) * zoom,
                (CameraPosition[2] - pivot[2]) * zoom + lift,
            };

            const float horizontal = sqrtf(offset[0] * offset[0] + offset[1] * offset[1]);
            if (g_pitchDelta != 0.0f && horizontal > 1.0f)
            {
                const float radius = sqrtf(horizontal * horizontal + offset[2] * offset[2]);
                const float elevation = atan2f(offset[2], horizontal) + g_pitchDelta * 3.14159265f / 180.0f;
                const float newHorizontal = radius * cosf(elevation);
                offset[0] = offset[0] / horizontal * newHorizontal;
                offset[1] = offset[1] / horizontal * newHorizontal;
                offset[2] = radius * sinf(elevation);
                CameraAngle[0] += g_pitchDelta;
            }

            CameraPosition[0] = pivot[0] + offset[0];
            CameraPosition[1] = pivot[1] + offset[1];
            CameraPosition[2] = pivot[2] + offset[2];
        }

        // 3) Distancia de vision: si se ve mas terreno, hay que dibujar mas lejos. El juego no siempre
        //    la vuelve a fijar en cada cuadro, asi que se parte de su ultimo valor propio.
        if (*ViewFar != g_lastViewFarWeSet)
        {
            g_baseViewFar = *ViewFar;
        }

        *ViewFar = g_baseViewFar * ViewScale();
        g_lastViewFarWeSet = *ViewFar;

        return result;
    }

    LRESULT CALLBACK WndProcHook(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
        case WM_MOUSEWHEEL:
        {
            const short delta = GET_WHEEL_DELTA_WPARAM(wParam);
            float distance = g_distance - (delta / WHEEL_DELTA) * ZoomStep;
            if (distance < MinDistance) distance = MinDistance;
            if (distance > MaxDistance) distance = MaxDistance;
            g_distance = distance;
            break; // el juego tambien recibe la rueda (por ejemplo para listas)
        }

        case WM_MBUTTONDOWN:
            g_dragging = true;
            g_dragMoved = false;
            g_lastMouse = { static_cast<short>(LOWORD(lParam)), static_cast<short>(HIWORD(lParam)) };
            SetCapture(window);
            return 0;

        case WM_MBUTTONUP:
            if (g_dragging)
            {
                g_dragging = false;
                ReleaseCapture();
                if (!g_dragMoved)
                {
                    g_yawDelta = 0.0f;
                    g_pitchDelta = 0.0f;
                    g_distance = DefaultDistance;
                }
            }

            return 0;

        case WM_MOUSEMOVE:
            if (g_dragging)
            {
                const POINT current = { static_cast<short>(LOWORD(lParam)), static_cast<short>(HIWORD(lParam)) };
                const int dx = current.x - g_lastMouse.x;
                const int dy = current.y - g_lastMouse.y;
                if (dx != 0 || dy != 0)
                {
                    g_dragMoved = true;
                    float yaw = g_yawDelta + dx * DegreesPerPixel;
                    while (yaw > 180.0f) yaw -= 360.0f;
                    while (yaw < -180.0f) yaw += 360.0f;
                    g_yawDelta = yaw;

                    float pitch = g_pitchDelta + dy * DegreesPerPixel;
                    if (pitch < MinPitchDelta) pitch = MinPitchDelta;
                    if (pitch > MaxPitchDelta) pitch = MaxPitchDelta;
                    g_pitchDelta = pitch;
                }

                g_lastMouse = current;
            }

            break;
        }

        return CallWindowProcA(g_originalWndProc, window, message, wParam, lParam);
    }

    bool InstallPatches()
    {
        // CALL rel32 a la funcion de camara
        BYTE expectedCall[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(expectedCall + 1) = CameraFunction - (CameraCallSite + 5);
        // FLD dword ptr [0x00D27AE4]
        const BYTE expectedFld[6] = { 0xD9, 0x05, 0xE4, 0x7A, 0xD2, 0x00 };

        if (!BytesMatch(CameraCallSite, expectedCall, sizeof(expectedCall)))
        {
            Log("ERROR: la llamada a la camara no coincide; main.exe no compatible. No se aplica nada.");
            return false;
        }

        if (!BytesMatch(TerrainCullYawInstr, expectedFld, sizeof(expectedFld)))
        {
            Log("ERROR: el recorte del terreno no coincide; main.exe no compatible. No se aplica nada.");
            return false;
        }

        BYTE expectedAreaCall[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(expectedAreaCall + 1) = TerrainAreaFunction - (TerrainAreaCallSite + 5);
        if (!BytesMatch(TerrainAreaCallSite, expectedAreaCall, sizeof(expectedAreaCall)))
        {
            Log("ERROR: la llamada del area de terreno no coincide; main.exe no compatible. No se aplica nada.");
            return false;
        }

        BYTE areaCall[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(areaCall + 1) = reinterpret_cast<DWORD>(&TerrainAreaHook) - (TerrainAreaCallSite + 5);
        if (!WriteCode(TerrainAreaCallSite, areaCall, sizeof(areaCall)))
        {
            Log("ERROR: no se pudo parchear el area de terreno.");
            return false;
        }

        BYTE call[5] = { 0xE8 };
        *reinterpret_cast<DWORD*>(call + 1) = reinterpret_cast<DWORD>(&CameraHook) - (CameraCallSite + 5);
        if (!WriteCode(CameraCallSite, call, sizeof(call)))
        {
            Log("ERROR: no se pudo parchear la llamada a la camara.");
            return false;
        }

        // FLD dword ptr [CameraAngle + 8] (angulo de giro actual)
        BYTE fld[6] = { 0xD9, 0x05 };
        *reinterpret_cast<DWORD*>(fld + 2) = reinterpret_cast<DWORD>(&CameraAngle[2]);
        if (!WriteCode(TerrainCullYawInstr, fld, sizeof(fld)))
        {
            Log("ERROR: no se pudo parchear el recorte del terreno.");
            return false;
        }

        Log("Parches aplicados.");
        return true;
    }

    DWORD WINAPI InitThread(LPVOID)
    {
        if (!InstallPatches())
        {
            return 0;
        }

        // Esperar a que exista la ventana del juego y engancharse a sus mensajes.
        for (int i = 0; i < 600 && *MainWindow == nullptr; i++)
        {
            Sleep(100);
        }

        const HWND window = *MainWindow;
        if (window == nullptr)
        {
            Log("ERROR: no se encontro la ventana del juego; la camara no respondera al mouse.");
            return 0;
        }

        g_originalWndProc = reinterpret_cast<WNDPROC>(
            SetWindowLongA(window, GWL_WNDPROC, reinterpret_cast<LONG>(&WndProcHook)));
        Log(g_originalWndProc != nullptr ? "Mouse enganchado. Camara 3D lista." : "ERROR: no se pudo enganchar el mouse.");
        return 0;
    }
}

// Funcion exportada para que main.exe pueda "importar" esta DLL y asi cargarla al arrancar. No hace
// nada: todo se inicializa en DllMain.
extern "C" __declspec(dllexport) void Camera3DVersion()
{
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);

        char path[MAX_PATH];
        GetModuleFileNameA(module, path, MAX_PATH);
        char* slash = strrchr(path, '\\');
        if (slash != nullptr)
        {
            strcpy_s(slash + 1, MAX_PATH - (slash + 1 - path), "camera3d.log");
            g_log = _fsopen(path, "w", _SH_DENYNO); // compartido: se puede leer con el juego abierto
        }

        Log("camera3d cargada.");
        const HANDLE thread = CreateThread(nullptr, 0, &InitThread, nullptr, 0, nullptr);
        if (thread != nullptr)
        {
            CloseHandle(thread);
        }
    }

    return TRUE;
}
