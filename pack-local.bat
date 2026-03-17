@echo off
setlocal
cd /d "%~dp0"

:: Extract version from Directory.Build.props
for /f "tokens=2 delims=<>" %%v in ('findstr "Version" Directory.Build.props') do set "VERSION=%%v"
echo Version: %VERSION%

echo === Cleaning ===
rmdir /s /q packed_nupkgs 2>nul
rmdir /s /q _test_consumer 2>nul

echo === Downloading content (if missing) ===
if not exist "voices" (
    if not exist "release-assets" mkdir release-assets
    curl -L -o release-assets/voices.zip https://github.com/Lyrcaxis/KokoroSharpBinaries/releases/download/v1.0.0/voices.zip
    powershell -Command "Expand-Archive -Path release-assets/voices.zip -DestinationPath . -Force"
)
if not exist "espeak" (
    if not exist "release-assets" mkdir release-assets
    curl -L -o release-assets/espeak.zip https://github.com/Lyrcaxis/KokoroSharpBinaries/releases/download/v1.0.0/espeak-ng-binaries-v1.52.zip
    powershell -Command "Expand-Archive -Path release-assets/espeak.zip -DestinationPath . -Force"
)

echo === Packing (reproducing CI) ===
dotnet pack KokoroSharp.csproj -c Release -o ./packed_nupkgs || goto :fail
for %%f in (Runtimes\*.csproj) do dotnet pack "%%f" -c Release -o ./packed_nupkgs || goto :fail

echo.
echo === Packages ===
for %%f in (packed_nupkgs\*.nupkg) do echo   %%~nxf

echo.
echo === Inspecting KokoroSharp nupkg ===
powershell -Command "Add-Type -A 'System.IO.Compression.FileSystem'; [IO.Compression.ZipFile]::OpenRead((Resolve-Path 'packed_nupkgs/KokoroSharp.%VERSION%.nupkg')).Entries.FullName | Where-Object { $_ -match 'content|buildTransitive|targets' }"

echo.
echo === Clearing cached KokoroSharp packages ===
for /f "tokens=2 delims= " %%p in ('dotnet nuget locals global-packages --list') do set "NUGET_CACHE=%%p"
echo NuGet cache: %NUGET_CACHE%
rmdir /s /q "%NUGET_CACHE%kokorosharp" 2>nul
rmdir /s /q "%NUGET_CACHE%kokorosharp.cpu" 2>nul

echo.
echo === Testing as consumer (KokoroSharp.CPU from local feed) ===
mkdir _test_consumer
dotnet new console -n TestConsumer -o _test_consumer --force >nul

(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<configuration^>
echo   ^<packageSources^>
echo     ^<clear /^>
echo     ^<add key="local" value="%cd%\packed_nupkgs" /^>
echo     ^<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /^>
echo   ^</packageSources^>
echo ^</configuration^>
)> _test_consumer\nuget.config

dotnet add _test_consumer/TestConsumer.csproj package KokoroSharp.CPU -v %VERSION% -s "%cd%\packed_nupkgs" || goto :fail
dotnet build _test_consumer/TestConsumer.csproj -c Release -v:n > _test_consumer\build.log 2>&1 || goto :fail

echo.
echo === Checking if CopyContent target ran ===
findstr /i "CopyContent" _test_consumer\build.log
if errorlevel 1 echo   [WARN] CopyContent target not found in build log

echo.
echo === Checking NuGet cache for content ===
if exist "%NUGET_CACHE%kokorosharp\%VERSION%\content\voices" ( echo   [OK] content/voices in cache ) else ( echo   [FAIL] content/voices not in cache )
if exist "%NUGET_CACHE%kokorosharp\%VERSION%\buildTransitive\KokoroSharp.targets" ( echo   [OK] buildTransitive/KokoroSharp.targets in cache ) else ( echo   [FAIL] targets not in cache )

echo.
echo === Checking consumer output ===
echo Looking in _test_consumer\bin\Release\:
dir /b /ad _test_consumer\bin\Release\ 2>nul
for /d %%d in (_test_consumer\bin\Release\*) do (
    if exist "%%d\voices" ( echo   [OK] voices/ in %%~nxd ) else ( echo   [FAIL] voices/ missing in %%~nxd )
    if exist "%%d\espeak" ( echo   [OK] espeak/ in %%~nxd ) else ( echo   [FAIL] espeak/ missing in %%~nxd )
)

echo.
echo === Done ===
goto :end

:fail
echo.
echo === FAILED (check _test_consumer\build.log) ===

:end
rmdir /s /q _test_consumer 2>nul
pause
