# Control del cliente de prueba por automatizacion interna (camera3d.dll en modo de prueba).
# Funciona con la PC bloqueada y sin robar el foco. Ver automation.cpp para la lista de comandos.

$script:AutoFolder = "D:\camera3d\client\automation"

# Ejecuta comandos (uno por linea) y devuelve el resultado de cada uno.
function Invoke-Mu([string[]]$Commands, [int]$TimeoutSeconds = 60) {
    $done = Join-Path $script:AutoFolder "done.txt"
    Remove-Item $done -ErrorAction SilentlyContinue
    $tmp = Join-Path $script:AutoFolder "commands.tmp"
    Set-Content -Path $tmp -Value $Commands -Encoding ASCII
    Move-Item $tmp (Join-Path $script:AutoFolder "commands.txt") -Force
    $limit = (Get-Date).AddSeconds($TimeoutSeconds)
    while (-not (Test-Path $done)) {
        if ((Get-Date) -gt $limit) { throw "El cliente no respondio a los comandos" }
        if (-not (Get-Process -Id ([int](Get-Content D:\camera3d\testpid.txt)) -ErrorAction SilentlyContinue)) { throw "El cliente se cerro" }
        Start-Sleep -Milliseconds 100
    }
    Start-Sleep -Milliseconds 50
    $result = Get-Content $done
    Remove-Item $done -ErrorAction SilentlyContinue
    return $result
}

# Captura del juego en PNG (se lee directo de OpenGL).
function Get-MuShot([string]$Png) {
    $bmp = [IO.Path]::ChangeExtension($Png, ".bmp")
    Remove-Item $bmp -ErrorAction SilentlyContinue
    Invoke-Mu @("shot $bmp", "wait 2") | Out-Null
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($bmp)
    $image.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png)
    $image.Dispose()
    Remove-Item $bmp
    return $Png
}

# Conexiones del cliente: todas tienen que ser locales.
function Assert-MuLocal {
    $testPid = [int](Get-Content D:\camera3d\testpid.txt)
    $bad = Get-NetTCPConnection -OwningProcess $testPid -ErrorAction SilentlyContinue |
        Where-Object { $_.RemotePort -ne 0 -and -not $_.RemoteAddress.StartsWith("127.") }
    if ($bad) { Stop-Process -Id $testPid -Force; throw "Conexion no local; cliente cerrado" }
}
