param(
    [switch]$ResetDatabase
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$webRoot = Join-Path $repoRoot 'apps\web'

if (-not (Test-Path (Join-Path $repoRoot '.env'))) {
    throw 'Missing repository .env. Copy .env.example to .env and configure PostgreSQL and Jwt__Key first.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is required. Install .NET SDK 11.0.100 and try again.'
}

$dotnetVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $dotnetVersion -notmatch '^11\.') {
    throw 'The .NET SDK is required. Install .NET SDK 11.0.100 and try again.'
}
Write-Host "Using .NET SDK $dotnetVersion."

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'npm was not found. Install Node.js 20.9 or newer and try again.'
}

foreach ($port in @(3000, 5000)) {
    $listener = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($listener) {
        throw "Port $port is already in use (PID $($listener.OwningProcess)). Stop that service or free the port, then retry."
    }
}

if ($ResetDatabase) {
    $confirmation = Read-Host 'This will DROP the project schemas and all their data. Type RESET to continue'
    if ($confirmation -cne 'RESET') {
        throw 'Database reset cancelled.'
    }

    Push-Location $repoRoot
    try {
        & dotnet run --project src/Database -- reset
        if ($LASTEXITCODE -ne 0) {
            throw 'Database reset failed. Check PostgreSQL and the connection string in .env.'
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path (Join-Path $webRoot 'node_modules'))) {
    Push-Location $webRoot
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) {
            throw 'Frontend dependency installation failed.'
        }
    }
    finally {
        Pop-Location
    }
}

Push-Location $repoRoot
try {
    & dotnet build RMG.slnx
    if ($LASTEXITCODE -ne 0) {
        throw 'The .NET solution build failed.'
    }
}
finally {
    Pop-Location
}

$apiCommand = 'dotnet run --no-build --project src/Api --no-launch-profile --urls http://localhost:5000'
$workerCommand = 'dotnet run --no-build --project src/Workers'
$webCommand = 'npm run dev'

Start-Process -FilePath 'powershell.exe' -WorkingDirectory $repoRoot -ArgumentList @(
    '-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $apiCommand
)
Start-Process -FilePath 'powershell.exe' -WorkingDirectory $repoRoot -ArgumentList @(
    '-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $workerCommand
)
Start-Process -FilePath 'powershell.exe' -WorkingDirectory $webRoot -ArgumentList @(
    '-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $webCommand
)

Write-Host 'Started API (http://localhost:5000), worker, and frontend (http://localhost:3000).'
Start-Process 'http://localhost:3000'
