param([Parameter(Mandatory=$true)][string]$JdkRoot)
$ErrorActionPreference='Stop'
$taskJava=Join-Path $JdkRoot 'bin/java.exe'
$taskJavac=Join-Path $JdkRoot 'bin/javac.exe'
$taskClasses=Join-Path $PSScriptRoot 'bin/tests'
New-Item -ItemType Directory -Path $taskClasses -Force | Out-Null
& $taskJavac -encoding UTF-8 -d $taskClasses (Join-Path $PSScriptRoot 'src/br/com/letrasparaculto/controle/RemoteConnection.java') (Join-Path $PSScriptRoot 'tests/RemoteConnectionTests.java')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar testes de conexão.'}
& $taskJava -cp $taskClasses RemoteConnectionTests
if($LASTEXITCODE -ne 0){throw 'Testes de conexão falharam.'}
