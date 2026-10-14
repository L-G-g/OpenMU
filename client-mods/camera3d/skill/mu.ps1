# Utilidades para probar el cliente de prueba (D:\camera3d\client): capturas y mouse/teclado.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class GameCtl {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int data, IntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr extra);
    [DllImport("user32.dll")] public static extern short VkKeyScan(char c);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
}
"@

# Cliente de prueba controlado por el skill: el que abrio launch-test.ps1 (pid en testpid.txt).
function Get-GameWindow {
    $testPid = [int](Get-Content D:\camera3d\testpid.txt)
    $p = Get-Process -Id $testPid -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like "main*" -and $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
    if ($null -eq $p) { throw "El cliente de prueba no esta abierto" }
    return $p.MainWindowHandle
}

# Antes de mandar teclas: el cliente tiene que estar conectado solo a direcciones locales.
function Assert-TestConnection {
    $testPid = [int](Get-Content D:\camera3d\testpid.txt)
    $bad = Get-NetTCPConnection -OwningProcess $testPid -ErrorAction SilentlyContinue |
        Where-Object { $_.RemotePort -ne 0 -and -not $_.RemoteAddress.StartsWith("127.") }
    if ($bad) {
        Stop-Process -Id $testPid -Force
        throw "Conexion no local ($($bad[0].RemoteAddress):$($bad[0].RemotePort)); cliente cerrado"
    }
}
function Get-ClientOrigin([IntPtr]$h) {
    $pt = New-Object GameCtl+POINT
    [void][GameCtl]::ClientToScreen($h, [ref]$pt)
    $r = New-Object GameCtl+RECT
    [void][GameCtl]::GetClientRect($h, [ref]$r)
    return @{ X = $pt.X; Y = $pt.Y; W = $r.R; H = $r.B }
}

function Focus-Game {
    Assert-TestConnection
    $h = Get-GameWindow
    if ([GameCtl]::IsIconic($h)) { [void][GameCtl]::ShowWindow($h, 9) }
    [void][GameCtl]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 300
}

function Save-GameShot([string]$path) {
    $h = Get-GameWindow
    $o = Get-ClientOrigin $h
    $bmp = New-Object System.Drawing.Bitmap $o.W, $o.H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($o.X, $o.Y, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return "$path ($($o.W)x$($o.H))"
}

function Move-GameMouse([int]$x, [int]$y) {
    $o = Get-ClientOrigin (Get-GameWindow)
    [void][GameCtl]::SetCursorPos($o.X + $x, $o.Y + $y)
}

function Click-Game([int]$x, [int]$y, [string]$button = "left") {
    Move-GameMouse $x $y; Start-Sleep -Milliseconds 120
    $down, $up = switch ($button) { "left" { 0x2, 0x4 } "right" { 0x8, 0x10 } "middle" { 0x20, 0x40 } }
    [GameCtl]::mouse_event($down, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80
    [GameCtl]::mouse_event($up, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 150
}

function Wheel-Game([int]$x, [int]$y, [int]$notches) {
    Move-GameMouse $x $y; Start-Sleep -Milliseconds 100
    [GameCtl]::mouse_event(0x800, 0, 0, 120 * $notches, [IntPtr]::Zero); Start-Sleep -Milliseconds 200
}

function Drag-GameMiddle([int]$x, [int]$y, [int]$dx, [int]$dy, [int]$steps = 20) {
    Move-GameMouse $x $y; Start-Sleep -Milliseconds 100
    [GameCtl]::mouse_event(0x20, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80
    for ($i = 1; $i -le $steps; $i++) {
        Move-GameMouse ($x + [int]($dx * $i / $steps)) ($y + [int]($dy * $i / $steps)); Start-Sleep -Milliseconds 30
    }
    [GameCtl]::mouse_event(0x40, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 200
}

function Send-GameKey([byte]$vk) {
    $scan = [byte][GameCtl]::MapVirtualKey($vk, 0)
    [GameCtl]::keybd_event($vk, $scan, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
    [GameCtl]::keybd_event($vk, $scan, 2, [IntPtr]::Zero); Start-Sleep -Milliseconds 80
}

function Send-GameText([string]$text) {
    foreach ($c in $text.ToCharArray()) {
        $vks = [GameCtl]::VkKeyScan($c)
        $vk = [byte]($vks -band 0xff); $shift = ($vks -band 0x100) -ne 0
        if ($shift) { [GameCtl]::keybd_event(0x10, 0x2a, 0, [IntPtr]::Zero) }
        Send-GameKey $vk
        if ($shift) { [GameCtl]::keybd_event(0x10, 0x2a, 2, [IntPtr]::Zero) }
    }
}
