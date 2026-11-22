# Componentes de terceiros

O código original do Worship Studio está sob MIT. As bibliotecas abaixo não são cobertas por essa licença do aplicativo. Os textos de licença são fornecidos nesta pasta. As versões correspondem ao pacote 1.5.0; o instalador seleciona o conjunto moderno ou legado conforme o Windows.

| Componente | Versão | Licença / origem |
| --- | --- | --- |
| Avalonia, Desktop, Fluent, Inter e backends | 11.3.22 | MIT — https://github.com/AvaloniaUI/Avalonia/tree/11.3.22 |
| Avalonia ANGLE Windows Natives | 2.1.25547.20250602 | BSD e avisos incluídos em ANGLE-LICENSE.txt — https://github.com/AvaloniaUI/angle |
| SkiaSharp | 2.88.9 | MIT e terceiros — SkiaSharp-LICENSE.txt e SkiaSharp-THIRD-PARTY-NOTICES.txt |
| HarfBuzzSharp | 8.3.1.1 | MIT — https://github.com/mono/SkiaSharp |
| Inter | Incluída no pacote Avalonia.Fonts.Inter | SIL OFL 1.1 — Inter-OFL.txt |
| NAudio e seus módulos | 2.2.1 | MIT — NAudio-LICENSE.txt — https://github.com/naudio/NAudio/tree/v2.2.1 |
| LibVLCSharp, LibVLCSharp.Avalonia | 3.10.2 | LGPL 2.1 ou posterior — https://code.videolan.org/videolan/LibVLCSharp |
| VideoLAN.LibVLC.Windows | 3.0.24 | LGPL 2.1 ou posterior — https://www.videolan.org/vlc/libvlc.html |
| FFmpeg | n8.1.3-39-g660b5aa7db-20261122 | LGPL 3.0 ou posterior — FFmpeg-LGPL-3.0.txt |
| FFmpeg da edição legada | 8.1.3-worshipstudio-win7 | LGPL 2.1 ou posterior — FFmpeg-legacy-LGPL-2.1.txt; receita própria em tools/ffmpeg-legacy/build.sh |
| Zlib da edição legada | 1.4 | Licença zlib — FFmpeg-legacy-zlib-LICENSE.txt — https://github.com/madler/zlib/releases/tag/v1.4 |
| Runtime MinGW-w64 da edição legada | Incluído estaticamente pelo w64devkit 2.11.0 | Avisos BSD e outros — FFmpeg-legacy-MinGW-runtime.txt — https://github.com/skeeto/w64devkit/releases/tag/v2.11.0 |
| MicroCom.Runtime | 0.11.0 | MIT — https://github.com/kekekeks/MicroCom |
| .NET runtime moderno | 10.0, patch incluído na publicação self-contained | MIT e avisos Microsoft — Dotnet-10-LICENSE.txt e Dotnet-10-THIRD-PARTY-NOTICES.txt — https://github.com/dotnet/runtime |
| .NET runtime legado | 6.0.36, incluído na publicação self-contained | MIT e avisos Microsoft — Dotnet-6-LICENSE.txt e Dotnet-6-THIRD-PARTY-NOTICES.txt — https://github.com/dotnet/runtime/tree/v6.0.36 |
| Microsoft.Bcl.TimeProvider, alvo net6.0 | 8.0.1 | MIT e avisos Microsoft — Microsoft.Bcl.TimeProvider-LICENSE.txt e Microsoft.Bcl.TimeProvider-THIRD-PARTY-NOTICES.txt — https://www.nuget.org/packages/Microsoft.Bcl.TimeProvider/8.0.1 |

Os pacotes restaurados incluem também backends para outros sistemas que não são usados no Windows. xUnit e Microsoft.NET.Test.Sdk são dependências de desenvolvimento e não são necessários para executar o aplicativo.

## FFmpeg moderno

Build Windows x64 LGPL shared do projeto indicado pela página oficial https://ffmpeg.org/download.html:

- Arquivo original: `ffmpeg-n8.1-latest-win64-lgpl-shared-8.1.zip`.
- Origem: https://github.com/FFmpeg-Builds/FFmpeg-Builds/releases/tag/latest (antigo BtbN/FFmpeg-Builds).
- Download em 22/11/2026. SHA256 conferido com o `digest` da API GitHub da release: `deef27287b542b921a8ee0d5430083e4f70dd6f4c928fcd11be59fc67cf9706f`.
- Binários não modificados: ffmpeg.exe, ffprobe.exe e as DLLs compartilhadas em `tools/ffmpeg`.
- Configuração original completa preservada em `FFmpeg-build-configuration.txt`; GPL, x264 e x265 desativados. O codificador padrão do aplicativo é MPEG-4 Part 2, com AAC quando há trilha de áudio. H.264 AMF é opcional conforme disponibilidade do dispositivo.
- Fonte FFmpeg correspondente: commit `660b5aa7db`, arquivo `third-party-sources/ffmpeg-660b5aa7db.tar.gz` no pacote de código. SHA256: `dc3ae92ca2514a0c26dbc2dc5aa28ed1cc39b3d0645ac9c828035f4cb3e96eb7`.
- Receitas de build e referências das dependências: `third-party-sources/ffmpeg-build-recipes.tar.gz`, commit `7f2a6b1050d302144de8366afa37a75e46f326aa`, https://github.com/FFmpeg-Builds/FFmpeg-Builds . O arquivo de configuração preserva as opções do binário original.

FFmpeg é software livre e pode ser estudado, modificado e substituído segundo a LGPL. O Worship Studio o chama por linha de comando e não incorpora seu código no executável gerenciado.

## FFmpeg legado

O pacote para Windows legado utiliza uma compilação própria reduzida, em `tools/ffmpeg-legacy` no código-fonte. No aplicativo instalado, os executáveis correspondentes ficam em `tools/ffmpeg`. São executados como processos externos e podem ser recompilados e substituídos.

- FFmpeg: mesmas fontes não modificadas do commit `660b5aa7dbce1f95cb04b3c7b8e94d79f893d15d`, no TAR FFmpeg já indicado acima.
- Zlib 1.4: fonte não modificado em `third-party-sources/zlib-1.4.tar.gz`, obtido da Release oficial de `madler/zlib`; SHA256 `02dd159783677a749c04adbdd02b55b8a2bcc7e054f51b76b549199a916ad584`.
- Receita completa: `tools/ffmpeg-legacy/build.sh`, com as instruções de reprodução em seu README. Usa o compilador MSVCRT w64devkit 2.11.0 / GCC 16.2.0, alvo Win32 x64, `_WIN32_WINNT=0x0601`, subsistema PE 6.1 e Win32 threads. Assembly, rede e bibliotecas externas opcionais estão desativados; GPL e componentes não livres não são habilitados.
- A compilação inclui zlib estaticamente para PNG. As partes estáticas do runtime MinGW têm avisos redistribuídos em `FFmpeg-legacy-MinGW-runtime.txt`; a exceção de biblioteca de runtime GCC aplica-se ao código do compilador usado.
- MPEG4 e AAC são os codificadores da exportação legada; AMF não é incluído nesse binário. A compilação reduzida não contém todos os codecs do pacote moderno. A licença é LGPL 2.1 ou posterior, integral em `FFmpeg-legacy-LGPL-2.1.txt`.

As fontes FFmpeg e zlib, a configuração e os avisos acompanham o mesmo pacote de código para permitir reconstrução independente. Os binários novos foram produzidos dessas fontes, e não modificam a distribuição moderna preservada.

## VideoLAN

- LibVLCSharp 3.10.2: commit `3082c09ca955131c705fc4767fde03e9c4a14999`, idêntico ao declarado no metadado `repository` dos pacotes NuGet. Fonte original em `third-party-sources/libvlcsharp-3.10.2.tar.gz`, obtida do [espelho oficial VideoLAN no GitHub](https://github.com/videolan/libvlcsharp/tree/3082c09ca955131c705fc4767fde03e9c4a14999); SHA256 `b9e12cbca8038fc78b468f53da80c0fb24f01963cb3c7205a23abbc7f832b79f`.
- LibVLC 3.0.24: fonte original em `third-party-sources/vlc-3.0.24.tar.xz`; SHA256 `e7cab503d1d7d5849b89d2cf0e1ee60d0ef6d012407791b644b9cfc0cc225fdf`.
- Origem da distribuição: https://download.videolan.org/pub/videolan/vlc/3.0.24/ . Build do pacote NuGet oficial `VideoLAN.LibVLC.Windows`.
- LGPL 2.1 integral em `LGPL-2.1.txt`. O aplicativo usa vinculação dinâmica; os binários da biblioteca não foram modificados e podem ser substituídos por versões compatíveis. As fontes do aplicativo são fornecidas para permitir recompilação.

As fontes de terceiros acompanham a pasta de código-fonte e não são necessárias para a operação offline. Os arquivos foram validados como TAR e seus hashes constam em `third-party-sources/SHA256SUMS.txt`. Preserve estes avisos ao redistribuir o pacote.
