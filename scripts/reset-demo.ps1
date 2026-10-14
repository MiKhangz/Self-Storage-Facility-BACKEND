# Drops the local database, re-applies every migration and loads the demo data (accounts use password Demo@123).
#   powershell -ExecutionPolicy Bypass -File scripts/reset-demo.ps1
# Stop the running API first: it holds the build output, and the drop needs no open connections.
$ErrorActionPreference = 'Stop'
$be = Join-Path (Split-Path -Parent $PSScriptRoot) 'BE'
Push-Location $be
try {
    dotnet ef database drop --force -p StorEase.Infrastructure -s StorEase.Api
    if ($LASTEXITCODE -ne 0) { throw 'Drop failed.' }
    dotnet ef database update -p StorEase.Infrastructure -s StorEase.Api
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed.' }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Billing__RunInBackground = 'false'
    dotnet run --project StorEase.Api --no-launch-profile -- --seed-only
    if ($LASTEXITCODE -ne 0) { throw 'Seeding failed.' }
    Write-Host 'Demo database ready.'
} finally {
    Pop-Location
    Remove-Item Env:ASPNETCORE_ENVIRONMENT, Env:Billing__RunInBackground -ErrorAction SilentlyContinue
}
