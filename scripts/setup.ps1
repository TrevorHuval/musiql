#requires -Version 5.1
<#
    Bootstraps a local MusiQL environment: database, sample catalog, and
    frontend dependencies. Safe to re-run. After it finishes, start the two
    dev servers with the commands it prints.
#>
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Require-Command($name, $hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Host "Missing '$name'. $hint" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Checking prerequisites..." -ForegroundColor Cyan
Require-Command dotnet "Install the .NET 10 SDK: https://dotnet.microsoft.com/download"
Require-Command node   "Install Node 20+: https://nodejs.org"
Require-Command docker "Install Docker Desktop: https://www.docker.com/products/docker-desktop"

if (-not (Test-Path .env)) {
    Copy-Item .env.example .env
    Write-Host "Created .env from .env.example" -ForegroundColor Green
}

Write-Host "Starting PostgreSQL..." -ForegroundColor Cyan
docker compose up -d db
if ($LASTEXITCODE -ne 0) { throw "docker compose failed" }

Write-Host "Waiting for the database to be ready..." -ForegroundColor Cyan
for ($i = 0; $i -lt 30; $i++) {
    docker exec musiql-postgres pg_isready -U musiql -d musiql *> $null
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 1
}
if ($LASTEXITCODE -ne 0) { throw "database did not become ready" }

Write-Host "Loading the sample catalog..." -ForegroundColor Cyan
dotnet run --project src/MusiQL.Etl -- load --sample
if ($LASTEXITCODE -ne 0) { throw "sample load failed" }

if (-not $SkipTests) {
    Write-Host "Running backend tests..." -ForegroundColor Cyan
    dotnet test --nologo
    if ($LASTEXITCODE -ne 0) { throw "backend tests failed" }
}

Write-Host "Installing frontend dependencies..." -ForegroundColor Cyan
Push-Location frontend
npm install
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "npm install failed" }
Pop-Location

Write-Host ""
Write-Host "Setup complete. Start MusiQL with two terminals:" -ForegroundColor Green
Write-Host "  1) dotnet run --project src/MusiQL.Api"
Write-Host "  2) cd frontend; npm run dev"
Write-Host "Then open http://127.0.0.1:5173"
