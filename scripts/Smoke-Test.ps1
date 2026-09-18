param([ValidateSet('Http', 'Grpc')][string]$Transport = 'Http')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$previous = $env:SMS_Api__Transport
Push-Location $root
try {
    $env:SMS_Api__Transport = $Transport
    dotnet build src/Sms.ConsoleApp/Sms.ConsoleApp.csproj -c Release --no-restore --nologo --disable-build-servers -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки консольного приложения.' }
    # The demo server must already be running. Bad input should be rejected, then the valid order succeeds.
    @('UNKNOWN:1', 'A1004292:0', 'A1004292:1;A1004293:0.408') |
        dotnet run --project src/Sms.ConsoleApp -c Release --no-build --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw "Консольный сценарий $Transport завершился с ошибкой." }
}
finally {
    $env:SMS_Api__Transport = $previous
    Pop-Location
}
