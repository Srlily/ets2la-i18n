param(
    # ETS2LA install root, i.e. the folder that contains 'Plugins' and 'Libraries'
    # (the Velopack 'current' folder). Defaults to the script's own directory.
    [string]$Root = $PSScriptRoot,

    # Folder containing the built plugin DLLs (the repo's dist folder).
    [string]$Dist = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

# --- 1. Copy DLLs to the top level of Plugins/ and Libraries/
# The manual scan (Directory.GetFiles(path,"*.dll")) is non-recursive, so the
# DLLs must sit directly in these folders. This is the flow used by the
# official example-plugin as well.
$targets = @(
    @{ From = (Join-Path $Dist 'Plugins\srlily.i18n\srlily.i18n.dll');              To = (Join-Path $Root 'Plugins\srlily.i18n.dll') }
    @{ From = (Join-Path $Dist 'Libraries\srlily.i18n.library\srlily.i18n.library.dll'); To = (Join-Path $Root 'Libraries\srlily.i18n.library.dll') }
)

foreach ($t in $targets) {
    if (-not (Test-Path $t.From)) {
        Write-Error "Built DLL not found: $($t.From)"
        exit 1
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $t.To -Parent) | Out-Null
    Copy-Item -Force $t.From $t.To
    Write-Host "Installed $($t.To)"
}

# --- 2. Remove any manifest entries from earlier catalogue-style registrations
# so the plugins are not loaded twice (once via scan, once via manifest).
$manifestDir = Join-Path $env:APPDATA 'ETS2LA'
$manifestFile = Join-Path $manifestDir 'InstalledPluginManifest.json'

if (Test-Path $manifestFile) {
    try {
        $manifest = Get-Content $manifestFile -Raw | ConvertFrom-Json
        $before = @($manifest.InstalledPlugins).Count
        $manifest.InstalledPlugins = @($manifest.InstalledPlugins | Where-Object {
            $_.Id -ne 'srlily.i18n' -and $_.Id -ne 'srlily.i18n.library'
        })
        $after = @($manifest.InstalledPlugins).Count
        if ($before -ne $after) {
            $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestFile -Encoding UTF8
            Write-Host "Removed $($before - $after) manifest entries (avoid duplicate loading)."
        }
    } catch {
        Write-Warning "Could not update manifest ($manifestFile): $_"
    }
}

Write-Host ""
Write-Host "Done. Restart ETS2LA now, the plugins will be discovered."
Write-Host "If they still do not appear, check the log next to ETS2LA.exe"
Write-Host "(ets2la.log) for 'Loaded plugin' / 'Failed to load plugin' lines."