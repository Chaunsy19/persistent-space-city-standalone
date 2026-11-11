# Run after building SpaceStation14.slnx. Additional arguments go to the client.
Push-Location $PSScriptRoot
try {
    & dotnet run --project Content.Client --no-build --no-restore -- --self-contained --username CityDev --connect --connect-address ss14://127.0.0.1:1212 @args
    $launchExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $launchExitCode
