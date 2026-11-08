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

$heroCount = [int]$manifest.portraits
if ($heroCount -lt 25 -or $manifest.files.Count -ne ($heroCount + 2) -or $portraits.Count -ne $heroCount)
{
    throw 'Expected the declared official hero count and 2 card atlas files.'
}

if (@($portraits.id | Select-Object -Unique).Count -ne $heroCount)
{
    throw 'Portrait identifiers must be unique.'
}

$entries = @($manifest.files)
foreach ($relativeManifest in @('Audio/sources.json', 'Interface/sources.json', 'Skins/sources.json', 'Icon/sources.json', 'KillEffects/sources.json', 'GameStart/sources.json'))
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

# Derived crops preserve official pixels but are not claimed as downloaded original files.
foreach ($relativeManifest in @('KillEffects/sources.json', 'GameStart/sources.json', 'EffectFrames/sources.json'))
{
    $effectManifest = Get-Content (Join-Path $resolvedRoot "src/Sanguosha.Desktop/Assets/Official/$relativeManifest") -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $effectManifest.derived)
    {
        $path = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot $entry.path))
        if (-not $path.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
            (Get-Item -LiteralPath $path).Length -ne $entry.bytes -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256)
        {
            throw "Effect crop invalid: $($entry.path). Restore the supplied file or run tools/Extract-KillEffectTitles.py and tools/Extract-GameStartSprites.py and tools/Build-OfficialEffectFrames.py (Pillow)."
        }
    }
}

$skinEntries = @($entries | Where-Object { $_.category -eq 'skin' })
if ($skinEntries.Count -ne ($heroCount * 2))
{
    throw 'Expected two additional official skin portraits per hero.'
}

foreach ($portrait in $portraits)
{
    $heroSkins = @($skinEntries | Where-Object { $_.hero -eq $portrait.id })
    if ($heroSkins.Count -ne 2 -or @($heroSkins.sha256 | Select-Object -Unique).Count -ne 2 -or
        @($heroSkins | Where-Object { $_.sha256 -eq $portrait.sha256 }).Count -gt 0)
    {
        throw "Expected two different alternative skins for $($portrait.id)."
    }
}

Write-Output "PASS: $($entries.Count) official source files verified by size and SHA-256 ($($heroCount * 3) portraits, cards, audio, interface and archived original icon)."
