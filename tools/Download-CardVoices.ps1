param
(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$FFmpegPath = 'ffmpeg'
)

$ErrorActionPreference = 'Stop'
$voiceRoot = Join-Path $ProjectRoot 'src/Sanguosha.Desktop/Assets/CardVoices'
$manifestPath = Join-Path $voiceRoot 'sources.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $manifest.files)
{
    $destination = Join-Path $voiceRoot $entry.file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    if (-not (Test-Path -LiteralPath $destination))
    {
        Invoke-WebRequest -Uri $entry.url -OutFile $destination
    }

    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $entry.sha256)
    {
        throw "Card voice SHA-256 mismatch: $($entry.file)"
    }
}

foreach ($entry in $manifest.convertedFiles)
{
    $destination = Join-Path $voiceRoot $entry.file
    if (-not (Test-Path -LiteralPath $destination))
    {
        $source = Join-Path $voiceRoot $entry.sourceFile
        & $FFmpegPath -hide_banner -loglevel error -y -i $source -codec:a libmp3lame -q:a 3 $destination
        if ($LASTEXITCODE -ne 0)
        {
            throw "Card voice conversion failed: $($entry.file)"
        }
    }
}

& (Join-Path $PSScriptRoot 'Verify-CardVoices.ps1') -ProjectRoot $ProjectRoot
Write-Output "PASS: $($manifest.files.Count) card voice files available."
