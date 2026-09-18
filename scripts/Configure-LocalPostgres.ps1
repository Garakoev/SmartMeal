param(
    [string]$Server = 'localhost',
    [int]$Port = 5432,
    [string]$Username = 'postgres'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$password = Read-Host "Пароль PostgreSQL для $Username" -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
try {
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection['Host'] = $Server
    $connection['Port'] = $Port
    $connection['Database'] = 'sms_test'
    $connection['Username'] = $Username
    $connection['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $connection['Timeout'] = 5
    $connection['Command Timeout'] = 15
    $settings = @{ ConnectionStrings = @{ Postgres = $connection.get_ConnectionString() } }
    $path = Join-Path $root 'src/Sms.ConsoleApp/appsettings.local.json'
    [IO.File]::WriteAllText($path, ($settings | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Write-Host 'Локальные настройки сохранены. appsettings.local.json исключён из Git.'
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $password.Dispose()
}
