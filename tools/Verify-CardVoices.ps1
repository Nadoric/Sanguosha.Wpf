param
(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$voiceRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $ProjectRoot 'src/Sanguosha.Desktop/Assets/CardVoices'))
$manifest = Get-Content -LiteralPath (Join-Path $voiceRoot 'sources.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json
$entries = @($manifest.files) + @($manifest.convertedFiles)
foreach ($entry in $entries)
{
    $destination = [System.IO.Path]::GetFullPath((Join-Path $voiceRoot $entry.file))
    if (-not $destination.StartsWith($voiceRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Card voice path escapes the resource directory: $($entry.file)"
    }

    if ((Get-Item -LiteralPath $destination).Length -ne $entry.bytes -or
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.sha256)
    {
        throw "Card voice checksum mismatch: $($entry.file)"
    }
}

Write-Output "PASS: $($entries.Count) card voice source/output files verified by size and SHA-256."
