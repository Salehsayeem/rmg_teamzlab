# Task 15: export the OpenAPI contract snapshot. Boots the Api in Development on the plan-mandated
# alt port 5199, GETs /openapi/v1.json, writes contracts/openapi/openapi.json (pretty, keys sorted
# = deterministic), stops the Api. Works from the repo root and from worktrees: the root is derived
# from $PSScriptRoot (script lives in <root>/scripts), and the Api itself resolves its .env by
# walking up from the working directory this script gives it. No machine-specific paths.
# Compatible with Windows PowerShell 5.1 and pwsh.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$port = 5199
$out = Join-Path $root 'contracts/openapi/openapi.json'
$url = "http://localhost:$port/openapi/v1.json"

# Canonical JSON: objects get their keys sorted recursively, 2-space indent, UTF-8 (no BOM).
function Format-CanonicalJson($value, [string]$indent) {
    $pad = "$indent  "
    if ($null -eq $value) { return 'null' }
    if ($value -is [bool]) { if ($value) { return 'true' } else { return 'false' } }
    if ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal] -or $value -is [int16]) { return "$value" }
    if ($value -is [string]) { return ConvertTo-CanonicalString $value }
    if ($value -is [System.Management.Automation.PSCustomObject]) {
        $lines = @($value.PSObject.Properties |
            Sort-Object Name |
            ForEach-Object { '{0}: {1}' -f (ConvertTo-CanonicalString $_.Name), (Format-CanonicalJson $_.Value $pad) })
        if ($lines.Count -eq 0) { return '{}' }
        return "{`n$pad" + ($lines -join ",`n$pad") + "`n$indent}"
    }
    if ($value -is [System.Collections.IEnumerable]) {
        $items = @($value | ForEach-Object { Format-CanonicalJson $_ $pad })
        if ($items.Count -eq 0) { return '[]' }
        return "[`n$pad" + ($items -join ",`n$pad") + "`n$indent]"
    }
    throw "unexpected JSON value of type $($value.GetType().FullName)"
}

function ConvertTo-CanonicalString([string]$s) {
    $s = $s.Replace('\', '\\').Replace('"', '\"').Replace("`b", '\b').Replace("`f", '\f')
    $s = $s.Replace("`n", '\n').Replace("`r", '\r').Replace("`t", '\t')
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append('"')
    foreach ($char in $s.ToCharArray()) {
        if ([int]$char -lt 0x20) { [void]$builder.Append('\u{0:x4}' -f [int]$char) }
        else { [void]$builder.Append($char) }
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

Write-Host "Building + booting the Api in Development on http://localhost:$port ..."
$log = Join-Path ([System.IO.Path]::GetTempPath()) "rmg-openapi-$PID.log"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$process = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--no-launch-profile', '--project', (Join-Path $root 'src\Api\Api.csproj'), '--urls', "http://localhost:$port") `
    -WorkingDirectory $root -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $log -RedirectStandardError "$log.err"

try {
    $document = $null
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if ($process.HasExited) {
            Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Last 20 | Write-Host
            throw "the Api exited early (code $($process.ExitCode)) before serving $url"
        }
        Start-Sleep -Seconds 1
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 5
            if ($response.StatusCode -eq 200) { $document = $response.Content; break }
        } catch { if ($attempt -eq 119) { throw "the Api never served $url`n$(Get-Content $log -ErrorAction SilentlyContinue | Select-Object -Last 20)" } }
    }
    if ($null -eq $document) { throw "the Api never served $url" }
    # Windows PowerShell's default handler hands back byte[]; pwsh hands back a string.
    if ($document -is [byte[]]) { $document = [System.Text.Encoding]::UTF8.GetString($document) }

    $canonical = Format-CanonicalJson (ConvertFrom-Json $document) ''
    $dir = Split-Path -Parent $out
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    # WriteAllText = UTF-8 without BOM; keeps the snapshot byte-stable across machines/shells.
    [System.IO.File]::WriteAllText($out, $canonical + [Environment]::NewLine)
    Write-Host "Wrote $out"
} finally {
    # /T so the dotnet-run child process (the actual Api) dies with its parent.
    & taskkill /PID $process.Id /T /F 2>$null | Out-Null
    if (Test-Path $log) { Remove-Item $log, "$log.err" -ErrorAction SilentlyContinue }
}
