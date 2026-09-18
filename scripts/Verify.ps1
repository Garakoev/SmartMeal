param([switch]$WithPostgres, [switch]$WithEnvironment)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$previous = $env:SMS_TEST_POSTGRES
$previousEnvironment = $env:SMS_TEST_ENVIRONMENT
Push-Location $root
try {
    if ($WithEnvironment) { $env:SMS_TEST_ENVIRONMENT = '1' }
    if ($WithPostgres) {
        $local = Join-Path $root 'src/Sms.ConsoleApp/appsettings.local.json'
        $configPath = if (Test-Path -LiteralPath $local) { $local } else { Join-Path $root 'src/Sms.ConsoleApp/appsettings.json' }
        $settings = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
        # DbConnectionStringBuilder is a dictionary: use the setter explicitly so PowerShell
        # does not create a dictionary entry named "ConnectionString" instead of parsing it.
        $connection.set_ConnectionString($settings.ConnectionStrings.Postgres)
        $connection['Database'] = 'postgres'
        $env:SMS_TEST_POSTGRES = $connection.get_ConnectionString()
    }
    dotnet restore SmartMealService.sln --disable-parallel --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось восстановить зависимости.' }
    dotnet build SmartMealService.sln -c Release --no-restore --nologo --disable-build-servers -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки.' }
    dotnet test tests/Sms.Tests/Sms.Tests.csproj -c Release --no-build --no-restore --nologo --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/TestResults
    if ($LASTEXITCODE -ne 0) { throw 'Обнаружены ошибки в тестах.' }
}
finally {
    $env:SMS_TEST_POSTGRES = $previous
    $env:SMS_TEST_ENVIRONMENT = $previousEnvironment
    Pop-Location
}
