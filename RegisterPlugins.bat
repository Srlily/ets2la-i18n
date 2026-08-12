@echo off
setlocal
rem Registers the srlily.i18n plugins in ETS2LA's InstalledPluginManifest.json.
rem This is required for the subfolder layout (Plugins\<pluginId>\<pluginId>.dll),
rem which is otherwise only used by the plugin catalogue.
rem
rem Usage: RegisterPlugins.bat [ETS2LA root]
rem   ETS2LA root = the folder containing 'Plugins' and 'Libraries',
rem                 i.e. the Velopack 'current' folder. Defaults to this
rem                 script's own directory.

set "ROOT=%~dp0"
if not "%~1"=="" set "ROOT=%~1"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RegisterPlugins.ps1" -Root "%ROOT%"
exit /b %errorlevel%