param
(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$rootPrefix = $resolvedRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$manifestPath = Join-Path $resolvedRoot 'src/Sanguosha.Desktop/Assets/Official/sources.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$portraits = @($manifest.files | Where-Object { $_.category -eq 'portrait' })

if ($manifest.files.Count -ne 27 -or $portraits.Count -ne 25)
{
    throw 'Expected 25 official portraits and 2 official card atlas files.'
}

if (@($portraits.id | Select-Object -Unique).Count -ne 25)
{
    throw 'Portrait identifiers must be unique.'
}

$entries = @($manifest.files)
foreach ($relativeManifest in @('Audio/sources.json', 'Interface/sources.json'))
{
    $extraManifestPath = Join-Path $resolvedRoot "src/Sanguosha.Desktop/Assets/Official/$relativeManifest"
    $extraManifest = Get-Content -LiteralPath $extraManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $entries += @($extraManifest.files)
}

foreach ($entry in $entries)
{
    $sourceUri = [System.Uri]$entry.url
    if ($sourceUri.Scheme -ne 'https' -or $sourceUri.Host -ne 'web.sanguosha.com')
    {
        throw "Unexpected official asset source: $($entry.url)"
    }

    $assetPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $entry.path))
    if (-not $assetPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Asset path escapes the project directory: $($entry.path)"
    }

    $asset = Get-Item -LiteralPath $assetPath
    if ($asset.Length -ne $entry.bytes)
    {
        throw "Asset size mismatch: $($entry.path)"
    }

    $actualHash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash
    if ($actualHash -ne $entry.sha256)
    {
        throw "Asset SHA-256 mismatch: $($entry.path)"
    }
}

Write-Output "PASS: $($entries.Count) official source files verified by size and SHA-256 (portraits, cards, audio and interface)."
