@echo off
setlocal
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)
dotnet build "%~dp0OverlayDesk\OverlayDesk.csproj" -c Release
if errorlevel 1 exit /b %errorlevel%
echo.
echo Build complete. Run: OverlayDesk\bin\Release\net8.0-windows\OverlayDesk.exe
