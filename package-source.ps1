param([string]$Destination = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not $Destination) { $Destination = Join-Path (Split-Path $taskRoot -Parent) 'WorshipStudio-Codigo-Fonte-1.1.0.zip' }
$taskTarget = [IO.Path]::GetFullPath($Destination)
if ($taskTarget.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Salve o arquivo ZIP fora da pasta de origem para evitar inclusão recursiva.'
}
$taskPending = $taskTarget + '.partial'
if (Test-Path $taskPending) { Remove-Item -LiteralPath $taskPending -Force }
try {
    $taskArchive = [IO.Compression.ZipFile]::Open($taskPending,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($taskFile in Get-ChildItem -LiteralPath $taskRoot -File -Recurse) {
            $taskRelative = [IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)
            if ($taskRelative -match '^(app)[\\/]' -or $taskRelative -match '(^|[\\/])(bin|obj|TestResults|\.avalonia-build-tasks)[\\/]') { continue }
            $taskEntry = 'WorshipStudio/' + $taskRelative.Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive,$taskFile.FullName,$taskEntry,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $taskArchive.Dispose() }
    Move-Item -LiteralPath $taskPending -Destination $taskTarget -Force
}
finally { if (Test-Path $taskPending) { Remove-Item -LiteralPath $taskPending -Force } }
Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256
