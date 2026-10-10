@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_CMD="
where dotnet >nul 2>&1
if not errorlevel 1 (
    dotnet --list-sdks | findstr /r /c:"^10\." >nul 2>&1
    if not errorlevel 1 set "DOTNET_CMD=dotnet"
)

if not defined DOTNET_CMD if exist "%TEMP%\lazycat-dotnet-sdk\dotnet.exe" (
    set "DOTNET_CMD=%TEMP%\lazycat-dotnet-sdk\dotnet.exe"
)

if not defined DOTNET_CMD (
    echo .NET 10 SDK is required to run the prototype.
    echo Install it from https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

echo Building and launching Lazy Cat Companion...
"%DOTNET_CMD%" run --project "%~dp0LazyCatCompanion.csproj" --configuration Debug
set "RUN_EXIT_CODE=%errorlevel%"

if not "%RUN_EXIT_CODE%"=="0" (
    echo.
    echo The prototype closed with an error. Review the message above.
    pause
)

exit /b %RUN_EXIT_CODE%
