# Lectura de memoria del cliente de prueba (solo lectura, proceso del mismo usuario).
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class MuMem {
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
    public static IntPtr Handle;
    public static byte[] Read(long addr, int size) { var b = new byte[size]; int r; ReadProcessMemory(Handle, new IntPtr(addr), b, size, out r); return b; }
    public static int ReadInt(long addr) { return BitConverter.ToInt32(Read(addr, 4), 0); }
    public static float ReadFloat(long addr) { return BitConverter.ToSingle(Read(addr, 4), 0); }
}
"@
[MuMem]::Handle = [MuMem]::OpenProcess(0x0010, $false, [int](Get-Content D:\camera3d\testpid.txt)) # PROCESS_VM_READ

# Avance por segundo de un float (por ejemplo un cuadro de animacion), contando las vueltas.
function Measure-MuRate([long]$Address, [double]$Seconds = 4) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $last = [MuMem]::ReadFloat($Address); $total = 0.0; $samples = 0
    while ($sw.Elapsed.TotalSeconds -lt $Seconds) {
        $v = [MuMem]::ReadFloat($Address)
        if ($v -ge $last) { $total += $v - $last } else { $total += $v } # dio la vuelta (vuelve cerca de 0)
        $last = $v; $samples++
        Start-Sleep -Milliseconds 2
    }
    "{0:N2} por segundo ({1} muestras)" -f ($total / $sw.Elapsed.TotalSeconds), $samples
}

# Distancia por segundo que recorre un punto (x,y floats), por ejemplo la camara.
function Measure-MuSpeed([long]$Address, [double]$Seconds = 5) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $x = [MuMem]::ReadFloat($Address); $y = [MuMem]::ReadFloat($Address + 4); $total = 0.0
    while ($sw.Elapsed.TotalSeconds -lt $Seconds) {
        Start-Sleep -Milliseconds 5
        $nx = [MuMem]::ReadFloat($Address); $ny = [MuMem]::ReadFloat($Address + 4)
        $total += [Math]::Sqrt(($nx - $x) * ($nx - $x) + ($ny - $y) * ($ny - $y)); $x = $nx; $y = $ny
    }
    "{0:N1} unidades por segundo" -f ($total / $sw.Elapsed.TotalSeconds)
}

# Fluidez de un punto que se mueve: cuantas lecturas (cada ~2 ms) lo encuentran quieto respecto de la
# anterior distinta, y el paso promedio entre cambios. Con saltos a 25 por segundo hay ~40 ms entre
# cambios; fluido a 60 FPS, ~17 ms.
function Measure-MuSmoothness([long]$Address, [double]$Seconds = 5) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $x = [MuMem]::ReadFloat($Address); $changes = 0
    while ($sw.Elapsed.TotalSeconds -lt $Seconds) {
        $nx = [MuMem]::ReadFloat($Address)
        if ($nx -ne $x) { $changes++; $x = $nx }
        Start-Sleep -Milliseconds 1
    }
    "{0:N1} cambios por segundo ({1:N1} ms entre cambios)" -f ($changes / $Seconds), (1000 * $Seconds / [Math]::Max($changes, 1))
}
