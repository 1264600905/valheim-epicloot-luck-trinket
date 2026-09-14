# Lucky Trinket - Thunderstore 发布打包脚本
#
# 用法：powershell -ExecutionPolicy Bypass -File .\package.ps1
#
# 产物：dist\LuckyTrinket-<版本>.zip
#   结构（参考 ValheimModding-YamlDotNet）：
#     manifest.json
#     README.md
#     CHANGELOG.md
#     LICENSE.txt
#     icon.png
#     BepInEx/plugins/LuckyTrinket/LuckyTrinket.dll

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectDir = $PSScriptRoot
$projectFile = Join-Path $projectDir "LuckyTrinket.csproj"
$distDir = Join-Path $projectDir "dist"
$stagingDir = Join-Path $distDir "staging"

Write-Host "==> 构建 ($Configuration)..."
dotnet build $projectFile -c $Configuration -v q
if ($LASTEXITCODE -ne 0) {
    throw "构建失败"
}

$manifest = Get-Content (Join-Path $projectDir "manifest.json") -Raw | ConvertFrom-Json
$version = $manifest.version_number
$zipPath = Join-Path $distDir ("LuckyTrinket-{0}.zip" -f $version)

Write-Host "==> 组装发布包 (v$version)..."
if (Test-Path -LiteralPath $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

# 包根文件
Copy-Item (Join-Path $projectDir "manifest.json") $stagingDir
Copy-Item (Join-Path $projectDir "CHANGELOG.md") $stagingDir
Copy-Item (Join-Path $projectDir "LICENSE.txt") $stagingDir
Copy-Item (Join-Path $projectDir "icon.png") $stagingDir
Copy-Item (Join-Path $projectDir "README.md") $stagingDir

# 插件文件
$pluginTarget = Join-Path $stagingDir "BepInEx\plugins\LuckyTrinket"
New-Item -ItemType Directory -Path $pluginTarget -Force | Out-Null
Copy-Item (Join-Path $projectDir "obj\$Configuration\LuckyTrinket.dll") $pluginTarget

Write-Host "==> 压缩..."
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# 注意：PowerShell 5.1 的 Compress-Archive 会写入反斜杠路径（Thunderstore 校验会失败），
# 这里手动构建 zip，显式使用正斜杠条目名。
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stagingDir -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($stagingDir.Length + 1).Replace('\', '/')
        $entry = $zip.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
        $entryStream = $entry.Open()
        try {
            $fileStream = [System.IO.File]::OpenRead($_.FullName)
            try { $fileStream.CopyTo($entryStream) } finally { $fileStream.Dispose() }
        } finally { $entryStream.Dispose() }
    }
} finally {
    $zip.Dispose()
}

Remove-Item -LiteralPath $stagingDir -Recurse -Force

Write-Host "==> 完成: $zipPath"
Write-Host ""
Write-Host "包内容:"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$zip.Entries | ForEach-Object { Write-Host ("  " + $_.FullName) }
$zip.Dispose()
