# Regenerates docs/openapi.json from the API's Swagger definition.
# Run after any change to a controller, route, request/response DTO or enum:
#   powershell -ExecutionPolicy Bypass -File scripts/export-openapi.ps1
# Builds into a temp folder so it works while the API is running (bin/ is locked then).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path ([IO.Path]::GetTempPath()) 'storease-openapi-build'

dotnet tool restore --tool-manifest (Join-Path $root '.config/dotnet-tools.json') | Out-Null
dotnet build (Join-Path $root 'BE/StorEase.Api') -o $out --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# Program.cs refuses to start without a JWT key; generating the spec never signs a token, so a placeholder is enough.
$env:Jwt__Key = 'openapi-export-only-placeholder-key-not-a-secret'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Push-Location $root
try {
    dotnet swagger tofile --output docs/openapi.json (Join-Path $out 'StorEase.Api.dll') v1
    if ($LASTEXITCODE -ne 0) { throw 'Swagger export failed.' }
} finally {
    Pop-Location
    Remove-Item Env:Jwt__Key, Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
}
