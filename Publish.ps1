param
(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\app')
)

$ErrorActionPreference = 'Stop'

dotnet publish (Join-Path $PSScriptRoot 'src\Sanguosha.Desktop\Sanguosha.Desktop.csproj') `
    --configuration Release `
    --output $OutputDirectory `
    --disable-build-servers `
    --maxcpucount:1 `
    --nologo

if ($LASTEXITCODE -ne 0)
{
    throw '发布失败，请查看编译输出。'
}

foreach ($document in @('README.md', 'docs/ARTWORK.md', 'docs/AUDIO.md', 'docs/VALIDATION.md'))
{
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination $OutputDirectory
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Sanguosha.Desktop/Assets/Official/sources.json') `
    -Destination (Join-Path $OutputDirectory 'official-sources.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Sanguosha.Desktop/Assets/Official/Audio/sources.json') `
    -Destination (Join-Path $OutputDirectory 'official-audio-sources.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Sanguosha.Desktop/Assets/Official/Interface/sources.json') `
    -Destination (Join-Path $OutputDirectory 'official-interface-sources.json')

Write-Host "已发布到 $OutputDirectory"
Write-Host '运行 山河弈.exe；目标电脑需安装 .NET 10 Windows Desktop Runtime。'
