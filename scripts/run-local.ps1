param(
    [int]$Port = 5171
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$localIp = Get-NetIPAddress -AddressFamily IPv4 -PrefixOrigin Dhcp -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike "169.254*" -and $_.IPAddress -notlike "127.*" } |
    Select-Object -First 1 -ExpandProperty IPAddress

Write-Host "HÁGALE se iniciará en modo desarrollo." -ForegroundColor Yellow
Write-Host "PC:      http://localhost:$Port/"
if ($localIp) {
    Write-Host "Celular: http://$localIp`:$Port/"
} else {
    Write-Host "Celular: no se detectó IP local DHCP. Revisa la conexión Wi-Fi/Ethernet." -ForegroundColor DarkYellow
}
Write-Host "Deja esta ventana abierta mientras pruebes la app local." -ForegroundColor Yellow

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project "src\Hagale.Api\Hagale.Api.csproj" --no-launch-profile --urls "http://0.0.0.0:$Port"
