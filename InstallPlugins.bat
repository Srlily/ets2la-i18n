@echo off
setlocal
rem One-command local install for testing:
rem   1. copies the built DLLs into <root>\Plugins\ and <root>\Libraries\
rem      (top level - the layout the plugin manager's manual scan expects)
rem   2. removes earlier catalogue-style manifest entries to avoid double loading
rem
rem Usage: InstallPlugins.bat [ETS2LA root]
rem   ETS2LA root = the folder containing 'Plugins' and 'Libraries',
rem                 i.e. the Velopack 'current' folder. Defaults to this
rem                 script's own directory.
rem
rem Run this batch from the ETS2LA 'current' folder after copying/updating this
rem repository, then restart ETS2LA.

set "ROOT=%~dp0"
if not "%~1"=="" set "ROOT=%~1"

if not exist "%ROOT%\Plugins"  ( echo Error: %ROOT% does not look like an ETS2LA root (no Plugins folder). & exit /b 1 )
if not exist "%ROOT%\Libraries" ( echo Error: %ROOT% does not look like an ETS2LA root (no Libraries folder). & exit /b 1 )

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0InstallPlugins.ps1" -Root "%ROOT%" -Dist "%~dp0dist"
exit /b %errorlevel%