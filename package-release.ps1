param([string]$DotnetPath='', [string]$InnoCompiler='')
$ErrorActionPreference='Stop'
# One version source drives the assembly, installer, ZIP and update manifest.
$taskReleaseRoot=$PSScriptRoot
$taskProject=Join-Path $taskReleaseRoot 'src/WorshipStudio/WorshipStudio.csproj'
$taskVersion=([xml](Get-Content -LiteralPath $taskProject -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if($taskVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Informe uma versão estável Major.Minor.Patch no csproj.'}
& (Join-Path $taskReleaseRoot 'build.ps1') -DotnetPath $DotnetPath -InnoCompiler $InnoCompiler
& (Join-Path $taskReleaseRoot 'package-source.ps1')
$taskOutput=Split-Path $taskReleaseRoot -Parent
$taskManifest=Join-Path $taskOutput "SHA256SUMS-v$taskVersion.txt"
$taskNames=@("WorshipStudio-Setup-$taskVersion.exe","WorshipStudio-Codigo-Fonte-$taskVersion.zip")
$taskLines=foreach($taskName in $taskNames){
    $taskHash=(Get-FileHash -LiteralPath (Join-Path $taskOutput $taskName) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$taskHash  $taskName"
}
[IO.File]::WriteAllText($taskManifest,($taskLines -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
Write-Output "Publique no GitHub a tag v$taskVersion com estes três anexos:"
$taskNames
Split-Path $taskManifest -Leaf
