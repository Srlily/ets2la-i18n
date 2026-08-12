param(
    # ETS2LA install root, i.e. the folder that contains 'Plugins' and 'Libraries'
    # (the Velopack 'current' folder). Defaults to the script's own directory.
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

$manifestDir = Join-Path $env:APPDATA 'ETS2LA'
$manifestFile = Join-Path $manifestDir 'InstalledPluginManifest.json'

$entries = @(
    [PSCustomObject]@{
        Id           = 'srlily.i18n.library'
        Version      = '1.1.2'
        DllPath      = (Join-Path $Root "Libraries\srlily.i18n.library\srlily.i18n.library.dll")
        Dependencies = @()
        Type         = 1
    },
    [PSCustomObject]@{
        Id           = 'srlily.i18n'
        Version      = '1.1.2'
        DllPath      = (Join-Path $Root "Plugins\srlily.i18n\srlily.i18n.dll")
        Dependencies = @('srlily.i18n.library')
        Type         = 0
    }
)

foreach ($e in $entries) {
    if (-not (Test-Path $e.DllPath)) {
        Write-Error "DLL not found: $($e.DllPath)"
        exit 1
    }
}

$manifest = $null
if (Test-Path $manifestFile) {
    try { $manifest = Get-Content $manifestFile -Raw | ConvertFrom-Json } catch { }
}

if ($null -eq $manifest -or $null -eq $manifest.InstalledPlugins) {
    $manifest = [PSCustomObject]@{ InstalledPlugins = @() }
}

foreach ($e in $entries) {
    $existing = @($manifest.InstalledPlugins | Where-Object { $_.Id -eq $e.Id })
    if ($existing.Count -gt 0) {
        $manifest.InstalledPlugins = @($manifest.InstalledPlugins | ForEach-Object {
            if ($_.Id -eq $e.Id) { $e } else { $_ }
        })
        Write-Host "Updated  $($e.Id)"
    } else {
        $manifest.InstalledPlugins += $e
        Write-Host "Added    $($e.Id)"
    }
}

if (-not (Test-Path $manifestDir)) { New-Item -ItemType Directory -Path $manifestDir | Out-Null }
$manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestFile -Encoding UTF8

Write-Host ""
Write-Host "Manifest updated: $manifestFile"
Write-Host "Restart ETS2LA for the plugins to be discovered."
