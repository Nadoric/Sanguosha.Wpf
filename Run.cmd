@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0"
if /I "%~1"=="--published" goto published
if not exist "src\Sanguosha.Desktop\Sanguosha.Desktop.csproj" goto published

echo [Source] Building and running the current Release source. Old artifacts are NOT used.
dotnet run --project "src\Sanguosha.Desktop" --configuration Release
set "result=%errorlevel%"
if not "%result%"=="0" (
    echo Build or launch failed. Install the .NET 10 SDK and check the errors above.
    echo The old published executable was NOT launched.
    pause
)
popd
exit /b %result%

:published
if not exist "artifacts\app\山河弈.exe" (
    echo Published app not found. Run Publish.ps1 first, or use the complete source project.
    pause
    popd
    exit /b 1
)
echo [Published] Explicitly launching artifacts\app\山河弈.exe; run Publish.ps1 after source updates.
start "" "artifacts\app\山河弈.exe"
popd
exit /b 0
