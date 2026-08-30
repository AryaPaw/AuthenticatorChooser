#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$identity
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $process = Start-Process -FilePath "powershell.exe" -Verb RunAs -Wait -PassThru -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $PSCommandPath
    )
    exit $process.ExitCode
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Write-Step([string]$Name) {
    Write-Host $Name -ForegroundColor Cyan
}

Write-Step "Stopping running AuthenticatorChooser"
Get-Process AuthenticatorChooser -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force
}

$deadline = (Get-Date).AddSeconds(12)
while (Get-Process AuthenticatorChooser -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) {
        throw "Could not stop AuthenticatorChooser. Exit it from the tray and run this script again."
    }
    Start-Sleep -Milliseconds 200
}

$out = Join-Path $RepoRoot "artifacts\local"
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

Write-Step "Publishing to artifacts\local"
& dotnet publish AuthenticatorChooser -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $out --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $out "AuthenticatorChooser.exe"
if (-not (Test-Path $exe)) {
    throw "Publish succeeded but $exe is missing"
}

Write-Step "Launching $exe --show-window"
Start-Process -FilePath $exe -ArgumentList "--show-window" -WorkingDirectory $out
Write-Host "Ready. The window should open; the tray copy is this same process."
