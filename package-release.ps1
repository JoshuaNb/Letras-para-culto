param([string]$DotnetPath='', [string]$InnoCompiler='', [string]$AndroidSdkRoot='', [string]$JdkRoot='', [string]$AndroidKeyStore='', [string]$AndroidKeyAlias='worshipstudio-remote')
$ErrorActionPreference='Stop'
# One version source drives the assembly, installer, ZIP and update manifest.
$taskReleaseRoot=$PSScriptRoot
$taskProject=Join-Path $taskReleaseRoot 'src/WorshipStudio/WorshipStudio.csproj'
$taskVersion=([xml](Get-Content -LiteralPath $taskProject -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if($taskVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Informe uma versão estável Major.Minor.Patch no csproj.'}
$taskOutput=Split-Path $taskReleaseRoot -Parent
$taskApkName="LetrasParaCulto-Controle-$taskVersion.apk"
$taskApk=Join-Path $taskOutput $taskApkName
$taskAndroid=Join-Path $taskReleaseRoot 'android-remote/build.ps1'
if(Test-Path -LiteralPath $taskAndroid){
    $taskAndroidManifest=[xml](Get-Content -LiteralPath (Join-Path $taskReleaseRoot 'android-remote/AndroidManifest.xml') -Raw)
    if($taskAndroidManifest.manifest.GetAttribute('versionName','http://schemas.android.com/apk/res/android') -ne $taskVersion){throw 'Atualize versionName/versionCode do AndroidManifest.xml para a versão da distribuição.'}
    if($AndroidKeyStore){
        if(-not $AndroidSdkRoot -or -not $JdkRoot){throw 'Informe AndroidSdkRoot e JdkRoot para construir o APK assinado.'}
        & $taskAndroid -AndroidSdkRoot $AndroidSdkRoot -JdkRoot $JdkRoot -KeyStorePath $AndroidKeyStore -KeyAlias $AndroidKeyAlias -OutputPath $taskApk
    }
    if(-not(Test-Path -LiteralPath $taskApk)){throw 'Compile primeiro o APK assinado em outputs ou informe AndroidSdkRoot, JdkRoot e AndroidKeyStore.'}
    if($JdkRoot){
        & (Join-Path $taskReleaseRoot 'android-remote/test.ps1') -JdkRoot $JdkRoot
        if($LASTEXITCODE -ne 0){throw 'Testes Java do controle Android falharam.'}
    }
}
& (Join-Path $taskReleaseRoot 'build.ps1') -DotnetPath $DotnetPath -InnoCompiler $InnoCompiler
# Both runtime payloads belong to the same updater-compatible installer.
foreach($taskPayload in @('app/win-x64','app/win-legacy-x64')){
    foreach($taskPayloadFile in @('WorshipStudio.exe','WorshipStudio.runtimeconfig.json','coreclr.dll','tools/ffmpeg/ffmpeg.exe')){
        if(-not(Test-Path -LiteralPath (Join-Path $taskReleaseRoot "$taskPayload/$taskPayloadFile"))){throw "Payload incompleto: $taskPayload/$taskPayloadFile"}
    }
}
& (Join-Path $taskReleaseRoot 'package-source.ps1')
$taskManifest=Join-Path $taskOutput "SHA256SUMS-v$taskVersion.txt"
$taskNames=@("WorshipStudio-Setup-$taskVersion.exe","WorshipStudio-Codigo-Fonte-$taskVersion.zip")
if(Test-Path -LiteralPath $taskAndroid){$taskNames+= $taskApkName}
$taskLines=foreach($taskName in $taskNames){
    $taskHash=(Get-FileHash -LiteralPath (Join-Path $taskOutput $taskName) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$taskHash  $taskName"
}
[IO.File]::WriteAllText($taskManifest,($taskLines -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
Write-Output "Publique no GitHub a tag v$taskVersion com os pacotes e o manifesto abaixo:"
$taskNames
Split-Path $taskManifest -Leaf
