param
(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\app')
)

$ErrorActionPreference = 'Stop'
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

dotnet publish (Join-Path $PSScriptRoot 'src\Sanguosha.Desktop\Sanguosha.Desktop.csproj') `
    --configuration Release `
    --output $OutputDirectory `
    --disable-build-servers `
    --maxcpucount:1 `
    --nologo

if ($LASTEXITCODE -ne 0)
{
    throw '发布失败，请查看编译输出；输出目录可能仍有上次的旧程序，请勿将其当成本次结果运行。'
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
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Sanguosha.Desktop/Assets/Official/Skins/sources.json') `
    -Destination (Join-Path $OutputDirectory 'official-skin-sources.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Sanguosha.Desktop/Assets/Official/Icon/sources.json') `
    -Destination (Join-Path $OutputDirectory 'official-icon-sources.json')

$executable = Join-Path $OutputDirectory '山河弈.exe'
if (-not (Test-Path -LiteralPath $executable))
{
    throw "未找到本次发布程序：$executable"
}
$assembly = Join-Path $OutputDirectory '山河弈.dll'
$hash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
Write-Host "已成功发布到 $executable"
Write-Host "山河弈.dll SHA256: $hash"
Write-Host '源码根目录的 Run.cmd 默认编译运行源码；Run.cmd --published 才启动此发布目录。'
Write-Host '运行 山河弈.exe；目标电脑需安装 .NET 10 Windows Desktop Runtime。'
