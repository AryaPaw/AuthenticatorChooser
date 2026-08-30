#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Write-Step([string]$Name) {
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
}

function Fail([string]$Message) {
    Write-Host "RELEASE GATE FAIL: $Message" -ForegroundColor Red
    exit 1
}

Write-Step "Full unit tests"
& dotnet test AuthenticatorChooser.Tests -c Release /p:CollectCoverage=true /p:Threshold=90 /p:ThresholdType=line
if ($LASTEXITCODE -ne 0) {
    Fail "dotnet test failed or coverage below 90%"
}

Write-Step "Publish win-x64"
$publishDir = Join-Path $RepoRoot "artifacts\publish-win-x64"
New-Item -ItemType Directory -Force $publishDir | Out-Null
& dotnet publish AuthenticatorChooser -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish failed"
}

$publishedExe = Join-Path $publishDir "AuthenticatorChooser.exe"
if (-not (Test-Path $publishedExe)) {
    Fail "published exe missing"
}

Write-Step "Compile Inno Setup"
$isccCandidates = @(
    (Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Fail "ISCC.exe not found"
}

$csproj = Join-Path $RepoRoot "AuthenticatorChooser\AuthenticatorChooser.csproj"
$csprojText = Get-Content $csproj -Raw
if ($csprojText -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') {
    Fail "Version missing from AuthenticatorChooser.csproj"
}
$appVersion = $Matches[1]

$iss = Join-Path $RepoRoot "installer\AuthenticatorChooser.iss"
& $iscc $iss `
    "/DMyAppVersion=$appVersion" `
    "/DSourceExe=$publishedExe" `
    "/DOutputBase=AuthenticatorChooser-Setup-win-x64" `
    "/DInstallArch=x64compatible"
if ($LASTEXITCODE -ne 0) {
    Fail "ISCC failed"
}

$setup = Join-Path $RepoRoot "artifacts\AuthenticatorChooser-Setup-win-x64.exe"
if (-not (Test-Path $setup)) {
    Fail "setup exe missing"
}

Write-Step "Windows Sandbox"
$sandboxExe = Join-Path $env:WINDIR "System32\WindowsSandbox.exe"
if (-not (Test-Path $sandboxExe)) {
    Write-Host "BLOCKED: WindowsSandbox.exe not found" -ForegroundColor Yellow
    exit 2
}

$inDir = Join-Path $RepoRoot "artifacts\sandbox-in"
$outDir = Join-Path $RepoRoot "artifacts\sandbox-out"
New-Item -ItemType Directory -Force $inDir | Out-Null
New-Item -ItemType Directory -Force $outDir | Out-Null
Get-ChildItem $outDir -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
Get-ChildItem $inDir -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

Copy-Item (Join-Path $RepoRoot "scripts\sandbox\test.ps1") (Join-Path $inDir "test.ps1") -Force
Copy-Item $setup (Join-Path $inDir "AuthenticatorChooser-Setup-win-x64.exe") -Force

$runtimeDest = Join-Path $inDir "windowsdesktop-runtime-win-x64.exe"
if (-not (Test-Path $runtimeDest)) {
    Write-Host "Downloading .NET 8 Desktop Runtime for Sandbox..."
    Invoke-WebRequest -Uri "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" -OutFile $runtimeDest -UseBasicParsing
}

$wsbPath = Join-Path $outDir "release-gate.wsb"
$wsb = @"
<Configuration>
  <VGpu>Disable</VGpu>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$inDir</HostFolder>
      <SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\in</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$outDir</HostFolder>
      <SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\out</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -ExecutionPolicy Bypass -File C:\Users\WDAGUtilityAccount\Desktop\in\test.ps1</Command>
  </LogonCommand>
</Configuration>
"@
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($wsbPath, $wsb, $utf8)

$result = Join-Path $outDir "result.txt"
if (Test-Path $result) {
    Remove-Item $result -Force
}

Start-Process $sandboxExe -ArgumentList $wsbPath | Out-Null

$deadline = (Get-Date).AddMinutes(20)
Write-Host "Waiting for Sandbox result (up to 20 minutes)..."
while ((Get-Date) -lt $deadline) {
    if (Test-Path $result) {
        $text = Get-Content $result -Raw -ErrorAction SilentlyContinue
        if ($text -match "script-end") {
            Write-Host $text
            if ($text -match "verdict=PASS") {
                Write-Host "RELEASE GATE PASS" -ForegroundColor Green
                Write-Host "Manual installer: $setup"
                exit 0
            }
            Fail "Windows Sandbox verdict is not PASS"
        }
    }
    Start-Sleep -Seconds 5
}

Fail "Windows Sandbox timed out waiting for result.txt"
