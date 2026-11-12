[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'BE/StorEase.sln'

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Solution not found: $solution"
}

Push-Location $repositoryRoot
try {
    if (-not $SkipRestore) {
        dotnet restore $solution
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dotnet build $solution --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test $solution --no-build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
