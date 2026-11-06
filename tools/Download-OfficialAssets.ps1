param
(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$rootPrefix = $resolvedRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$manifestPath = Join-Path $resolvedRoot 'src/Sanguosha.Desktop/Assets/Official/sources.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

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

    $directory = Split-Path -Parent $assetPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    Write-Output "Downloading $($entry.id)"
    Invoke-WebRequest -UseBasicParsing -Uri $sourceUri -OutFile $assetPath
}

& (Join-Path $PSScriptRoot 'Verify-OfficialAssets.ps1') -ProjectRoot $resolvedRoot
