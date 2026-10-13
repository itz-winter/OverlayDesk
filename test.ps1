$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "OverlayDesk.Tests\OverlayDesk.Tests.csproj"
dotnet run --project $project -c Release
exit $LASTEXITCODE
