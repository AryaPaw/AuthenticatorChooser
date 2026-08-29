$ErrorActionPreference = "Continue"
$outDir = "C:\Users\WDAGUtilityAccount\Desktop\out"
$inDir = "C:\Users\WDAGUtilityAccount\Desktop\in"
New-Item -ItemType Directory -Force $outDir | Out-Null
$log = Join-Path $outDir "result.txt"

function Log([string]$m) {
    Add-Content -Path $log -Value ("{0} {1}" -f (Get-Date -Format "HH:mm:ss"), $m) -Encoding utf8
}

Set-Content -Path $log -Value "test-id=release-gate-sandbox" -Encoding utf8

try {
    & reg.exe add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" /v ConsentPromptBehaviorAdmin /t REG_DWORD /d 0 /f | Out-Null
    Log "uac-consent-disabled"
} catch {
    Log "uac-fail"
}

function Wait-LoggedProcess([string]$file, [string[]]$arguments, [int]$timeoutMs, [string]$name) {
    Log ($name + "-start")
    $proc = Start-Process $file -ArgumentList $arguments -PassThru
    if ($null -eq $proc) {
        Log ($name + "-start-null")
        return -1
    }

    Log ($name + "-pid=" + $proc.Id)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if ($proc.HasExited) {
            return $proc.ExitCode
        }

        Start-Sleep -Seconds 2
    }

    Log ($name + "-timeout")
    try {
        & "$env:SystemRoot\System32\taskkill.exe" /F /T /PID $proc.Id | Out-Null
    } catch {}
    return -1
}

function Wait-SetupInstaller([string]$file, [string[]]$arguments, [int]$timeoutMs, [string]$appPath, [string]$uninsPath) {
    Log "setup-start"
    $proc = Start-Process $file -ArgumentList $arguments -PassThru
    if ($null -eq $proc) {
        Log "setup-start-null"
        return -1
    }

    Log ("setup-pid=" + $proc.Id)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if ($proc.HasExited) {
            return $proc.ExitCode
        }

        if ((Test-Path $appPath) -and (Test-Path $uninsPath)) {
            Log "setup-files-present"
            Stop-AppImage
            $until = $sw.ElapsedMilliseconds + 15000
            while (-not $proc.HasExited -and $sw.ElapsedMilliseconds -lt $until) {
                Start-Sleep -Seconds 1
            }

            if ($proc.HasExited) {
                return $proc.ExitCode
            }

            try {
                & "$env:SystemRoot\System32\taskkill.exe" /F /T /PID $proc.Id | Out-Null
            } catch {}
            Log "setup-killed-after-files"
            return 0
        }

        Start-Sleep -Seconds 2
    }

    Log "setup-timeout"
    try {
        & "$env:SystemRoot\System32\taskkill.exe" /F /T /PID $proc.Id | Out-Null
    } catch {}
    return -1
}

function Stop-AppImage {
    try {
        & "$env:SystemRoot\System32\taskkill.exe" /F /IM AuthenticatorChooser.exe /T | Out-Null
    } catch {}
}

$runtime = Join-Path $inDir "windowsdesktop-runtime-win-x64.exe"
if (Test-Path $runtime) {
    Log "runtime-install-begin"
    $runtimeExit = Wait-LoggedProcess $runtime @("/install","/quiet","/norestart") 600000 "runtime"
    Log ("runtime-exit=" + $runtimeExit)
} else {
    Log "runtime-missing"
    $runtimeExit = -1
}

$setup = Join-Path $inDir "AuthenticatorChooser-Setup-win-x64.exe"
Log ("setup-exists=" + (Test-Path $setup))
if (-not (Test-Path $setup)) {
    Log "verdict=FAIL"
    Log "script-end"
    exit 1
}

Log "setup-install-begin"
$appDir = "C:\Program Files\AuthenticatorChooser"
$app = Join-Path $appDir "AuthenticatorChooser.exe"
$unins = Join-Path $appDir "unins000.exe"
$setupLog = Join-Path $outDir "setup.log"
$setupExit = Wait-SetupInstaller $setup @(
    "/VERYSILENT",
    "/SUPPRESSMSGBOXES",
    "/NORESTART",
    "/SP-",
    "/LOG=$setupLog"
) 180000 $app $unins
Stop-AppImage
Log ("setup-exit=" + $setupExit)

$exeInstalled = Test-Path $app
$uninsPresent = Test-Path $unins
Log ("exe_installed=" + $exeInstalled)
Log ("unins_present=" + $uninsPresent)

New-Item -ItemType Directory -Force "$env:APPDATA\AuthenticatorChooser" | Out-Null
Set-Content "$env:APPDATA\AuthenticatorChooser\settings.json" "{}" -Encoding utf8

$uninstallOk = $false
if ($uninsPresent) {
    Log "uninstall-begin"
    Stop-AppImage
    $uninstallExit = Wait-LoggedProcess $unins @("/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART") 180000 "uninstall"
    Log ("uninstall-exit=" + $uninstallExit)
    Start-Sleep -Seconds 5
    $uninstallOk = ($uninstallExit -eq 0)
} else {
    Log "uninstall-skipped"
}

$exeAfter = Test-Path $app
$dirAfter = Test-Path $appDir
Log ("exe_after=" + $exeAfter)
Log ("dir_after=" + $dirAfter)

$installOk = ($setupExit -eq 0) -and $exeInstalled -and $uninsPresent
$removed = (-not $exeAfter) -and (-not $dirAfter)
if ($installOk -and $uninstallOk -and $removed) {
    Log "verdict=PASS"
} else {
    Log "verdict=FAIL"
}
Log "script-end"
