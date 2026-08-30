#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$csproj = Join-Path (Split-Path -Parent $PSScriptRoot) "AuthenticatorChooser\AuthenticatorChooser.csproj"
[xml] $proj = Get-Content -LiteralPath $csproj
$version = @(
    $proj.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)[0]
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "AuthenticatorChooser.csproj is missing <Version>"
}

Write-Output $version
