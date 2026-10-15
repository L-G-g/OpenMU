# Levanta el servidor de prueba en modo demo (datos en memoria, no toca ninguna base de datos) en
# 127.127.127.127:45405, si no esta corriendo. -resolveIP hace que anuncie el game server en esa misma IP.
param([string]$Dir = 'D:\camera3d\testserver-build-repro')

$listening = Get-NetTCPConnection -LocalPort 45405 -State Listen -ErrorAction SilentlyContinue
if ($listening) { "Servidor de prueba ya corriendo (pid $($listening[0].OwningProcess))"; return }

$p = Start-Process -FilePath "$Dir\MUnique.OpenMU.Startup.exe" -ArgumentList '-demo', '-autostart', '-resolveIP:127.127.127.127' `
    -WorkingDirectory $Dir -WindowStyle Hidden -RedirectStandardOutput 'D:\camera3d\testserver-muauto.log' `
    -RedirectStandardError 'D:\camera3d\testserver-muauto.err' -PassThru
Set-Content D:\camera3d\testserver-muauto.pid $p.Id
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep 1
    if (Get-NetTCPConnection -LocalPort 45405 -State Listen -ErrorAction SilentlyContinue) { break }
}
"Servidor de prueba: pid $($p.Id), log D:\camera3d\testserver-muauto.log"
