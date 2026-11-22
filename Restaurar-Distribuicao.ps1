param([switch]$UsarArquivoLocal,[string]$Arquivo='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$taskExpected='2c14e31309e94627b2fee0099e94eadc5e03bfb79a56090aebd78e88e81039a6'
$taskCache=Join-Path $taskRoot '.distribution-cache'
if($UsarArquivoLocal -and -not $Arquivo){throw 'Informe -Arquivo com o ZIP completo do código-fonte 1.2.0.'}
if(-not $Arquivo){
    New-Item -ItemType Directory -Path $taskCache -Force | Out-Null
    $Arquivo=Join-Path $taskCache 'WorshipStudio-Codigo-Fonte-1.2.0.zip'
    if(-not (Test-Path -LiteralPath $Arquivo)){
        Invoke-WebRequest -Uri 'https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.2.0/WorshipStudio-Codigo-Fonte-1.2.0.zip' -OutFile $Arquivo
    }
}
if((Get-FileHash -LiteralPath $Arquivo -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpected){throw 'O SHA256 do arquivo não corresponde à distribuição 1.2.0. Nenhum arquivo foi restaurado.'}
$taskZip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Arquivo))
$taskRestored=0
try{
    foreach($taskEntry in $taskZip.Entries){
        if(-not $taskEntry.Name -or $taskEntry.FullName -notmatch '^WorshipStudio/(tools/|third-party-sources/)'){continue}
        $taskRelative=$taskEntry.FullName.Substring('WorshipStudio/'.Length)
        $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
        if(-not $taskDestination.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Caminho ZIP fora do projeto.'}
        New-Item -ItemType Directory -Path (Split-Path $taskDestination -Parent) -Force | Out-Null
        $taskInput=$taskEntry.Open();$taskOutput=[IO.File]::Create($taskDestination)
        try{$taskInput.CopyTo($taskOutput)}finally{$taskOutput.Dispose();$taskInput.Dispose()}
        $taskRestored++
    }
}finally{$taskZip.Dispose()}
Write-Output "Distribuição verificada: $taskRestored arquivos de ferramentas e fontes de terceiros restaurados."
