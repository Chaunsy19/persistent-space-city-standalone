# Run after building SpaceStation14.slnx. Additional arguments go to the server.
Push-Location $PSScriptRoot
try {
    & dotnet run --project Content.Server --no-build --no-restore -- --config-file (Join-Path $PSScriptRoot 'space-city.server.toml') --data-dir (Join-Path $PSScriptRoot '.space-city-local/server') @args
    $launchExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $launchExitCode
