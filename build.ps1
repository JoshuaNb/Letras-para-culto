param(
    [string]$DotnetPath = '',
    [string]$InnoCompiler = '',
    [switch]$SkipTests,
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = $PSScriptRoot
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
$taskPublish = Join-Path $taskProjectRoot 'app\win-x64'
& $DotnetPath restore $taskProject
if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar dependências.' }
if (-not $SkipTests) {
    & $DotnetPath test $taskTests -c Release --logger 'trx;LogFileName=verification.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam. O pacote não foi publicado.' }
    & $DotnetPath run --project (Join-Path $taskProjectRoot 'render-tests\WorshipStudio.RenderTests.csproj') -c Release -- --output (Join-Path $taskProjectRoot "test-evidence\typography-$taskVersion")
    if ($LASTEXITCODE -ne 0) { throw 'Os testes visuais de letras falharam. O pacote não foi publicado.' }
    $taskUiTests = Join-Path $taskProjectRoot 'ui-tests\WorshipStudio.UiTests.csproj'
    if (Test-Path -LiteralPath $taskUiTests) {
        & $DotnetPath run --project $taskUiTests -c Release -- --output (Join-Path $taskProjectRoot "test-evidence\ui-headless-$taskVersion")
        if ($LASTEXITCODE -ne 0) { throw 'Os testes de operação da interface falharam. O pacote não foi publicado.' }
    }
}
& $DotnetPath publish $taskProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $taskPublish
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar.' }
Copy-Item -Path (Join-Path $taskProjectRoot 'licenses') -Destination $taskPublish -Recurse -Force
Copy-Item -Path (Join-Path $taskProjectRoot 'LICENSE.txt') -Destination (Join-Path $taskPublish 'WorshipStudio-LICENSE.txt') -Force
Copy-Item -Path (Join-Path $taskProjectRoot 'README.pt-BR.md') -Destination $taskPublish -Force
$taskApk=Join-Path (Split-Path $taskProjectRoot -Parent) "LetrasParaCulto-Controle-$taskVersion.apk"
if(Test-Path -LiteralPath $taskApk){Copy-Item -LiteralPath $taskApk -Destination $taskPublish -Force}
if (Test-Path (Join-Path $taskProjectRoot 'VERIFICACAO.md')) { Copy-Item -Path (Join-Path $taskProjectRoot 'VERIFICACAO.md') -Destination $taskPublish -Force }
if (-not $SkipInstaller) {
    if (-not $InnoCompiler) {
        $taskLocalCompiler = Join-Path $taskProjectRoot '..\..\work\tools\inno\ISCC.exe'
        $taskProgramCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
        if (Test-Path $taskLocalCompiler) { $InnoCompiler = (Resolve-Path $taskLocalCompiler).Path }
        elseif (Test-Path $taskProgramCompiler) { $InnoCompiler = $taskProgramCompiler }
        else { throw 'Instale Inno Setup 6 ou informe -InnoCompiler.' }
    }
    & $InnoCompiler /Q "/DAppVersion=$taskVersion" (Join-Path $taskProjectRoot 'installer\WorshipStudio.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
}
Write-Host "Aplicativo publicado em $taskPublish"
