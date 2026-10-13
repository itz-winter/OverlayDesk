$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "OverlayDesk\OverlayDesk.csproj"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0"
}
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""
Write-Host "Build complete. Run: OverlayDesk\bin\Release\net8.0-windows\OverlayDesk.exe"
