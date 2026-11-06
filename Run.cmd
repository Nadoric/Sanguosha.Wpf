@echo off
chcp 65001 >nul
pushd "%~dp0"
if exist "artifacts\app\山河弈.exe" (
    start "" "artifacts\app\山河弈.exe"
) else (
    dotnet run --project "src\Sanguosha.Desktop" --configuration Release
)
popd
