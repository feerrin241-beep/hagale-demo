param(
    [string]$Stamp = (Get-Date -Format "yyyyMMdd-HHmm")
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "hagale-publish-$Stamp"
$zipPath = Join-Path $artifactsRoot "hagale-publish-$Stamp.zip"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Generando publicación Release de HÁGALE..." -ForegroundColor Yellow
dotnet publish "src\Hagale.Api\Hagale.Api.csproj" --configuration Release --output $publishDir /p:UseAppHost=false

if (Test-Path $zipPath) {
    throw "Ya existe el ZIP $zipPath. Usa otro Stamp para no sobrescribir una publicación anterior."
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $unsafeEntries = $zip.Entries | Where-Object {
        $_.FullName -match "(?i)(appsettings\.Development|private-documents|\.env|secret|App_Data|usersecrets)"
    }

    if ($unsafeEntries) {
        $names = ($unsafeEntries | Select-Object -ExpandProperty FullName) -join ", "
        throw "La publicación contiene archivos que no deben salir a producción: $names"
    }
}
finally {
    $zip.Dispose()
}

$hash = Get-FileHash $zipPath -Algorithm SHA256

Write-Host "Publicación lista." -ForegroundColor Green
Write-Host "ZIP:  $zipPath"
Write-Host "SHA:  $($hash.Hash)"
