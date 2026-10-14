# Abre el cliente de prueba SIN administrador, apuntando al servidor elegido.
# main.exe pide administrador en su manifiesto; con __COMPAT_LAYER=RunAsInvoker corre como usuario comun
# (asi se le puede mandar mouse/teclado sin elevar). La conexion se lee de HKLM, que para un proceso
# sin elevar y con virtualizacion se redirige a HKCU\Software\Classes\VirtualStore.
param([string]$Exe = "D:\camera3d\client\main.exe", [string]$Ip = "127.127.127.127", [int]$Port = 45405)

$offsets = 12, 7, 3, 19
$i = 0
$encoded = -join ($Ip.ToCharArray() | ForEach-Object { [char]([int]$_ + $offsets[$i++ % 4]) })
switch ($Ip.Length % 4) {
    0 { $encodedPort = $Port + 12 - (([math]::Floor($Port / 4) % 4) * 8) }
    1 { $encodedPort = $Port + 7 - (($Port % 8) * 2) }
    2 { $encodedPort = $Port + 3 - (($Port % 4) * 2) }
    3 { $encodedPort = $Port + (0x13 - (($Port % 4) * 2)) - (([math]::Floor($Port / 0x10) % 2) * 0x20) }
}

foreach ($path in 'Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\WebZen\Mu\Connection',
                  'Software\Classes\VirtualStore\MACHINE\SOFTWARE\WebZen\Mu\Connection') {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($path)
    $key.SetValue('Key', [Environment]::TickCount, 'DWord')
    $key.SetValue('ParameterA', $encoded, 'String')
    $key.SetValue('ParameterB', [int]$encodedPort, 'DWord')
    $key.Close()
}

$env:__COMPAT_LAYER = 'RunAsInvoker'
$p = Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
Set-Content D:\camera3d\testpid.txt $p.Id
"Cliente abierto: pid $($p.Id) -> $Ip`:$Port"
