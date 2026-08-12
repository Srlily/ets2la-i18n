@echo off
setlocal
set SCRIPT_DIR=%~dp0

rem Build ETS2LA
dotnet build "%SCRIPT_DIR%ETS2LA\ETS2LA.sln" -p:Platform=x64

rem Discover all other project files and build them
call "%SCRIPT_DIR%BuildYourPlugins.bat"

rem Finally run ETS2LA
cd /d "%SCRIPT_DIR%ETS2LA\ETS2LA\bin\x64\Debug\net10.0"
ETS2LA.exe