# Builds BrowserFork, copies it to %LOCALAPPDATA%\Programs\BrowserFork and registers it as a browser.
# Then pick "BrowserFork" for HTTP/HTTPS in Settings › Apps › Default apps (Windows won't let apps do that themselves).
$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'Programs\BrowserFork'
$exe = Join-Path $target 'BrowserFork.exe'

Get-Process BrowserFork -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish "$PSScriptRoot\src\BrowserFork.csproj" -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:PublishReadyToRun=true -o $target
if ($LASTEXITCODE) { throw "publish failed" }

# Settings › Default apps only lists BrowserFork once it is also registered machine-wide (HKLM), which needs admin.
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrators')
if ($admin) { & $exe --register-machine | Out-Null }
else { Start-Process $exe -ArgumentList '--register-machine' -Verb RunAs -Wait }

& $exe --register
