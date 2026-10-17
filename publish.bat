@echo off
setlocal
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the .NET 8 SDK first.
  exit /b 1
)
dotnet publish "%~dp0OverlayDesk\OverlayDesk.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%~dp0publish"
if errorlevel 1 exit /b %errorlevel%
echo Published application is in: 

cd %~dp0publish
7z a OverlayDesk.7z * -mx=9
move .\OverlayDesk.7z ..