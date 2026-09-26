param([switch]$Test)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$dotnetExe = 'dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location -LiteralPath $workspacePath
try {
    & $dotnetExe build (Join-Path $workspacePath 'src/RcCar.sln') -c Release -m:1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Test) {
        & $dotnetExe test (Join-Path $workspacePath 'src/RcCar.sln') -c Release --no-build -m:1
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
