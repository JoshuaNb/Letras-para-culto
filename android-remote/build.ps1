param(
    [Parameter(Mandatory=$true)][string]$AndroidSdkRoot,
    [Parameter(Mandatory=$true)][string]$JdkRoot,
    [Parameter(Mandatory=$true)][string]$KeyStorePath,
    [string]$KeyAlias='worshipstudio-remote',
    [string]$StorePasswordEnvironment='WORSHIP_ANDROID_STORE_PASSWORD',
    [string]$KeyPasswordEnvironment='WORSHIP_ANDROID_KEY_PASSWORD',
    [string]$BuildToolsVersion='36.0.0',
    [string]$Platform='android-36',
    [string]$OutputPath=''
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$taskSdk=[IO.Path]::GetFullPath($AndroidSdkRoot)
$taskJdk=[IO.Path]::GetFullPath($JdkRoot)
$taskKeyStore=[IO.Path]::GetFullPath($KeyStorePath)
if($taskKeyStore.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Mantenha o keystore de assinatura fora do código-fonte.'}
if(-not [Environment]::GetEnvironmentVariable($StorePasswordEnvironment) -or -not [Environment]::GetEnvironmentVariable($KeyPasswordEnvironment)){throw 'Defina as variáveis de ambiente das senhas antes de compilar; não coloque as senhas nos argumentos.'}
$taskTools=Join-Path $taskSdk "build-tools/$BuildToolsVersion"
$taskAndroidJar=Join-Path $taskSdk "platforms/$Platform/android.jar"
$taskJavac=Join-Path $taskJdk 'bin/javac.exe'
$taskAapt=Join-Path $taskTools 'aapt2.exe'
$taskD8=Join-Path $taskTools 'd8.bat'
$taskZipAlign=Join-Path $taskTools 'zipalign.exe'
$taskApkSigner=Join-Path $taskTools 'apksigner.bat'
foreach($taskRequired in @($taskAndroidJar,$taskJavac,$taskAapt,$taskD8,$taskZipAlign,$taskApkSigner,$taskKeyStore)){if(-not(Test-Path -LiteralPath $taskRequired)){throw "Arquivo necessário ausente: $taskRequired"}}
$taskBin=Join-Path $taskRoot 'bin'
$taskClasses=Join-Path $taskBin 'classes'
$taskDex=Join-Path $taskBin 'dex'
if(Test-Path -LiteralPath $taskBin){
    if((Get-Item -LiteralPath $taskBin -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'O diretório de build não pode ser um link ou junção.'}
    $taskResolved=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $taskBin).Path)
    if($taskResolved -ne (Join-Path $taskRoot 'bin')){throw 'Diretório de build inesperado.'}
    Remove-Item -LiteralPath $taskResolved -Recurse -Force
}
New-Item -ItemType Directory -Path $taskClasses,$taskDex -Force | Out-Null
if(-not $OutputPath){
    $taskManifest=[xml](Get-Content -LiteralPath (Join-Path $taskRoot 'AndroidManifest.xml') -Raw)
    $taskVersion=$taskManifest.manifest.GetAttribute('versionName','http://schemas.android.com/apk/res/android')
    $OutputPath=Join-Path (Split-Path (Split-Path $taskRoot -Parent) -Parent) "LetrasParaCulto-Controle-$taskVersion.apk"
}
$taskOutput=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $taskOutput -Parent) -Force | Out-Null
$taskPreviousJava=$env:JAVA_HOME
$taskPreviousPath=$env:PATH
try{
    $env:JAVA_HOME=$taskJdk
    $env:PATH=(Join-Path $taskJdk 'bin')+[IO.Path]::PathSeparator+$env:PATH
    $taskSources=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.java' -Recurse | ForEach-Object FullName)
    & $taskJavac -encoding UTF-8 --release 8 -Xlint:-options -classpath $taskAndroidJar -d $taskClasses @taskSources
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar Java Android.'}
    & $taskAapt compile --dir (Join-Path $taskRoot 'res') -o (Join-Path $taskBin 'resources.zip')
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar recursos.'}
    & $taskAapt link -o (Join-Path $taskBin 'unsigned.apk') --manifest (Join-Path $taskRoot 'AndroidManifest.xml') -I $taskAndroidJar -A (Join-Path $taskRoot 'assets') --auto-add-overlay (Join-Path $taskBin 'resources.zip')
    if($LASTEXITCODE -ne 0){throw 'Falha ao montar manifesto e recursos.'}
    $taskClassFiles=@(Get-ChildItem -LiteralPath $taskClasses -Filter '*.class' -Recurse | ForEach-Object FullName)
    & $taskD8 --release --lib $taskAndroidJar --min-api 26 --output $taskDex @taskClassFiles
    if($LASTEXITCODE -ne 0){throw 'Falha ao converter classes para DEX.'}
    $taskZip=[IO.Compression.ZipFile]::Open((Join-Path $taskBin 'unsigned.apk'),[IO.Compression.ZipArchiveMode]::Update)
    try{foreach($taskDexFile in Get-ChildItem -LiteralPath $taskDex -Filter '*.dex'){
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip,$taskDexFile.FullName,$taskDexFile.Name,[IO.Compression.CompressionLevel]::NoCompression) | Out-Null
    }}finally{$taskZip.Dispose()}
    & $taskZipAlign -P 16 -f 4 (Join-Path $taskBin 'unsigned.apk') (Join-Path $taskBin 'aligned.apk')
    if($LASTEXITCODE -ne 0){throw 'Falha no alinhamento APK.'}
    & $taskApkSigner sign --ks $taskKeyStore --ks-key-alias $KeyAlias --ks-pass "env:$StorePasswordEnvironment" --key-pass "env:$KeyPasswordEnvironment" --out $taskOutput (Join-Path $taskBin 'aligned.apk')
    if($LASTEXITCODE -ne 0){throw 'Falha ao assinar o APK.'}
    & $taskApkSigner verify --verbose --print-certs $taskOutput
    if($LASTEXITCODE -ne 0){throw 'Assinatura do APK inválida.'}
    & $taskZipAlign -c -P 16 4 $taskOutput
    if($LASTEXITCODE -ne 0){throw 'APK desalinhado.'}
    Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256
}finally{
    $env:JAVA_HOME=$taskPreviousJava
    $env:PATH=$taskPreviousPath
}
