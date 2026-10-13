$ErrorActionPreference = "Stop"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "Install the .NET 8 SDK first." }
$project = Join-Path $PSScriptRoot "OverlayDesk\OverlayDesk.csproj"
$output = Join-Path $PSScriptRoot "publish"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Published application: $output"
