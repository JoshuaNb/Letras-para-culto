param(
    [string]$DotnetPath = '',
    [string]$InnoCompiler = '',
    [switch]$SkipTests,
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not $DotnetPath) {
    $taskDotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    $taskLocalSdk = Join-Path $taskProjectRoot '..\..\work\tools\dotnet\dotnet.exe'
    if (Test-Path $taskLocalSdk) { $DotnetPath = (Resolve-Path $taskLocalSdk).Path }
    elseif ($taskDotnetCommand) { $DotnetPath = $taskDotnetCommand.Source }
    else { throw 'Instale o SDK .NET 10 ou informe -DotnetPath.' }
}
$taskProject = Join-Path $taskProjectRoot 'src\WorshipStudio\WorshipStudio.csproj'
$taskVersion = ([xml](Get-Content -LiteralPath $taskProject -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$taskTests = Join-Path $taskProjectRoot 'tests\WorshipStudio.Tests.csproj'
$taskAppRoot = Join-Path $taskProjectRoot 'app'
$taskTargets = @(
    @{ Framework = 'net10.0'; Folder = 'win-x64'; Legacy = $false },
    @{ Framework = 'net6.0'; Folder = 'win-legacy-x64'; Legacy = $true }
)
$taskLegacyFfmpeg = Join-Path $taskProjectRoot 'tools\ffmpeg-legacy'
if (-not (Test-Path -LiteralPath (Join-Path $taskLegacyFfmpeg 'ffmpeg.exe'))) { throw 'FFmpeg compatível ausente em tools/ffmpeg-legacy. Restaure ou compile a distribuição legada antes de publicar.' }
& $DotnetPath restore $taskProject
if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar dependências.' }
$taskPreviousFfmpeg = $env:WORSHIP_FFMPEG
try {
foreach ($taskTarget in $taskTargets) {
    $taskFramework = $taskTarget.Framework
    $env:WORSHIP_FFMPEG = if ($taskTarget.Legacy) { Join-Path $taskLegacyFfmpeg 'ffmpeg.exe' } else { Join-Path $taskProjectRoot 'tools\ffmpeg\ffmpeg.exe' }
    if (-not $SkipTests) {
        & $DotnetPath test $taskTests -c Release -f $taskFramework --results-directory (Join-Path $taskProjectRoot "test-evidence\core-$taskVersion-$taskFramework") --logger 'trx;LogFileName=verification.trx'
        if ($LASTEXITCODE -ne 0) { throw "Os testes $taskFramework falharam. O pacote não foi publicado." }
        & $DotnetPath run --project (Join-Path $taskProjectRoot 'render-tests\WorshipStudio.RenderTests.csproj') -c Release -f $taskFramework -- --output (Join-Path $taskProjectRoot "test-evidence\typography-$taskVersion-$taskFramework")
        if ($LASTEXITCODE -ne 0) { throw "Os testes visuais $taskFramework falharam. O pacote não foi publicado." }
        $taskUiTests = Join-Path $taskProjectRoot 'ui-tests\WorshipStudio.UiTests.csproj'
        if (Test-Path -LiteralPath $taskUiTests) {
            & $DotnetPath run --project $taskUiTests -c Release -f $taskFramework -- --output (Join-Path $taskProjectRoot "test-evidence\ui-headless-$taskVersion-$taskFramework")
            if ($LASTEXITCODE -ne 0) { throw "Os testes de interface $taskFramework falharam. O pacote não foi publicado." }
        }
    }
    $taskPublish = [IO.Path]::GetFullPath((Join-Path $taskAppRoot $taskTarget.Folder))
    # Only these two generated directories may be replaced. Never clean an installation or user data.
    if ($taskPublish -notin @((Join-Path $taskAppRoot 'win-x64'), (Join-Path $taskAppRoot 'win-legacy-x64'))) { throw 'Diretório de publicação inesperado.' }
    if (Test-Path -LiteralPath $taskPublish) {
        if ((Get-Item -LiteralPath $taskPublish -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'A publicação não pode ser um link ou junção.' }
        if ((Resolve-Path -LiteralPath $taskPublish).Path -ne $taskPublish) { throw 'Caminho de publicação divergente.' }
        Remove-Item -LiteralPath $taskPublish -Recurse -Force
    }
    & $DotnetPath publish $taskProject -c Release -f $taskFramework -r win-x64 --self-contained true -p:PublishSingleFile=false -o $taskPublish
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $taskFramework." }
    # The csproj copies native tools; retain only the matching FFmpeg in each payload.
    foreach ($taskToolName in @('ffmpeg', 'ffmpeg-legacy')) {
        $taskToolDestination = [IO.Path]::GetFullPath((Join-Path $taskPublish "tools\$taskToolName"))
        if (-not $taskToolDestination.StartsWith($taskPublish + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Ferramenta fora da publicação.' }
        if (Test-Path -LiteralPath $taskToolDestination) {
            if ((Get-Item -LiteralPath $taskToolDestination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'A ferramenta não pode ser um link ou junção.' }
            Remove-Item -LiteralPath $taskToolDestination -Recurse -Force
        }
    }
    $taskChosenFfmpeg = if ($taskTarget.Legacy) { $taskLegacyFfmpeg } else { Join-Path $taskProjectRoot 'tools\ffmpeg' }
    $taskFfmpegDestination=Join-Path $taskPublish 'tools\ffmpeg'
    New-Item -ItemType Directory -Path $taskFfmpegDestination -Force | Out-Null
    foreach($taskNativeTool in Get-ChildItem -LiteralPath $taskChosenFfmpeg -File | Where-Object { $_.Extension -in @('.exe','.dll') }) {
        Copy-Item -LiteralPath $taskNativeTool.FullName -Destination $taskFfmpegDestination -Force
    }
    if ($taskTarget.Legacy) {
        # The ANGLE binary imports newer Windows APIs. Legacy uses Skia software rendering.
        foreach ($taskAngle in @('av_libglesv2.dll', 'av_libegl.dll')) {
            $taskAnglePath = Join-Path $taskPublish $taskAngle
            if (Test-Path -LiteralPath $taskAnglePath) { Remove-Item -LiteralPath $taskAnglePath -Force }
        }
    }
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'licenses') -Destination $taskPublish -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'LICENSE.txt') -Destination (Join-Path $taskPublish 'WorshipStudio-LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'README.pt-BR.md') -Destination $taskPublish -Force
    $taskApk = Join-Path (Split-Path $taskProjectRoot -Parent) "LetrasParaCulto-Controle-$taskVersion.apk"
    if (Test-Path -LiteralPath $taskApk) { Copy-Item -LiteralPath $taskApk -Destination $taskPublish -Force }
    if (Test-Path -LiteralPath (Join-Path $taskProjectRoot 'VERIFICACAO.md')) { Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'VERIFICACAO.md') -Destination $taskPublish -Force }
}
}
finally { $env:WORSHIP_FFMPEG = $taskPreviousFfmpeg }
# Only remove owned binaries absent from the selected runtime. Inno replaces common files normally.
# Exact relative names avoid wildcard/data cleanup and keep rollback for files that are overwritten.
$taskCleanup = @('; Generated by build.ps1: obsolete binaries only. Never delete common files or user data.')
$taskBinaryNames = @{}
foreach ($taskTarget in $taskTargets) {
    $taskPayloadRoot = Join-Path $taskAppRoot $taskTarget.Folder
    $taskBinaryNames[$taskTarget.Folder] = @(Get-ChildItem -LiteralPath $taskPayloadRoot -File -Recurse | Where-Object { $_.Extension -in @('.dll','.exe') } | ForEach-Object { [IO.Path]::GetRelativePath($taskPayloadRoot, $_.FullName) })
}
foreach ($taskTarget in $taskTargets) {
    $taskOpposite = if ($taskTarget.Legacy) { 'win-x64' } else { 'win-legacy-x64' }
    $taskCheck = if ($taskTarget.Legacy) { 'UseModernRuntime' } else { 'UseLegacyRuntime' }
    foreach ($taskName in ($taskBinaryNames[$taskTarget.Folder] | Where-Object { $_ -notin $taskBinaryNames[$taskOpposite] } | Sort-Object -Unique)) {
        if ($taskName -notmatch '^[A-Za-z0-9_.\\-]+\.(dll|exe)$' -or $taskName.Contains('..') -or [IO.Path]::IsPathRooted($taskName)) { throw 'Nome de binário inesperado na publicação.' }
        $taskCleanup += 'Type: files; Name: "{app}\' + $taskName + '"; Check: ' + $taskCheck
    }
}
[IO.File]::WriteAllText((Join-Path $taskProjectRoot 'installer\runtime-cleanup.iss'), ($taskCleanup -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
if (-not $SkipInstaller) {
    if (-not $InnoCompiler) {
        $taskLocalCompiler = Join-Path $taskProjectRoot '..\..\work\tools\inno\ISCC.exe'
        $taskProgramCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
        if (Test-Path $taskLocalCompiler) { $InnoCompiler = (Resolve-Path $taskLocalCompiler).Path }
        elseif (Test-Path $taskProgramCompiler) { $InnoCompiler = $taskProgramCompiler }
        else { throw 'Instale Inno Setup 6 ou informe -InnoCompiler.' }
    }
    & $InnoCompiler /Q "/DAppVersion=$taskVersion" (Join-Path $taskProjectRoot 'installer\WorshipStudio.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador universal.' }
}
Write-Host "Aplicativos publicados em $taskAppRoot (net10.0 e net6.0)."
