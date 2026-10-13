@echo off
setlocal
where dotnet >nul 2>nul
if errorlevel 1 (echo .NET 8 SDK is required. & exit /b 1)
dotnet run --project "%~dp0OverlayDesk.Tests\OverlayDesk.Tests.csproj" -c Release
exit /b %errorlevel%
