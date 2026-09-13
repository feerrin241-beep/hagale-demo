$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

Write-Host "Verificando HÁGALE..." -ForegroundColor Yellow

dotnet build "Hagale.sln" --no-restore /p:UseAppHost=false
dotnet test "Hagale.sln" --no-restore
node --check "src\Hagale.Api\wwwroot\app.js"

Write-Host "Verificación correcta: compilación, pruebas y JavaScript OK." -ForegroundColor Green
