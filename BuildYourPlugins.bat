@echo off
setlocal enabledelayedexpansion

rem Libraries and Plugins are in separate folders, so we need to loop through them both
for %%R in (Libraries Plugins) do (
  for /d %%D in (%%R\*) do (
    set "name=%%~nxD"
    if exist "%%D\!name!.csproj" (
      dotnet build "%%D\!name!.csproj"
      rem AssemblyName matches the plugin id (e.g. srlily.i18n), which is also the
      rem install folder name. Fall back to the project folder name if unset.
      set "id=!name!"
      for /f "usebackq tokens=2 delims=<>" %%B in (`findstr /i "<AssemblyName>" "%%D\!name!.csproj"`) do (
        set "id=%%B"
      )
      rem Installed layout matches the plugin catalogue:
      rem   <ETS2LA data>/Plugins/<pluginId>/<pluginId>.dll
      rem   <ETS2LA data>/Libraries/<pluginId>/<pluginId>.dll
      set "out_dir=%LOCALAPPDATA%\ETS2LA\%%R\!id!"
      if not exist "!out_dir!" mkdir "!out_dir!"
      del /q "!out_dir!\!id!.dll" 2>nul
      copy /y "%%D\bin\Debug\net10.0\!id!.dll" "!out_dir!\!id!.dll" >nul
    )
  )
)