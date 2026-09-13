param(
    [int]$Port = 5172
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$keyBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($keyBytes)

$env:ASPNETCORE_ENVIRONMENT = "Demo"
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
$env:Database__Provider = "InMemory"
$env:ConnectionStrings__HagaleDatabase = "HagaleDemo"
$env:Jwt__Issuer = "Hagale.Demo"
$env:Jwt__Audience = "Hagale.Demo"
$env:Jwt__SigningKey = [Convert]::ToBase64String($keyBytes)

Write-Host "HÁGALE se iniciará en modo DEMO local." -ForegroundColor Yellow
Write-Host "PC: http://localhost:$Port/"
Write-Host "Base de datos: memoria temporal (se borra al detener la ventana)." -ForegroundColor DarkYellow
Write-Host "Deja esta ventana abierta mientras pruebes la demo." -ForegroundColor Yellow

dotnet run --project "src\Hagale.Api\Hagale.Api.csproj" --no-launch-profile --urls "http://127.0.0.1:$Port"
