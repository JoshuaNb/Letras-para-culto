# Componentes de terceiros

O código original do Worship Studio está sob MIT. As bibliotecas abaixo não são cobertas por essa licença do aplicativo. Os textos de licença são fornecidos nesta pasta. As versões correspondem ao pacote 1.0.0.

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
| MicroCom.Runtime | 0.11.0 | MIT — https://github.com/kekekeks/MicroCom |
| .NET runtime | Versão incluída na publicação self-contained | MIT e avisos Microsoft incluídos na publicação — https://github.com/dotnet/runtime |

Os pacotes restaurados incluem também backends para outros sistemas que não são usados no Windows. xUnit e Microsoft.NET.Test.Sdk são dependências de desenvolvimento e não são necessários para executar o aplicativo.

## FFmpeg

Build Windows x64 LGPL shared do projeto indicado pela página oficial https://ffmpeg.org/download.html:

- Arquivo original: `ffmpeg-n8.1-latest-win64-lgpl-shared-8.1.zip`.
- Origem: https://github.com/FFmpeg-Builds/FFmpeg-Builds/releases/tag/latest (antigo BtbN/FFmpeg-Builds).
- Download em 22/11/2026. SHA256 conferido com o `digest` da API GitHub da release: `deef27287b542b921a8ee0d5430083e4f70dd6f4c928fcd11be59fc67cf9706f`.
- Binários não modificados: ffmpeg.exe, ffprobe.exe e as DLLs compartilhadas em `tools/ffmpeg`.
- Configuração original completa preservada em `FFmpeg-build-configuration.txt`; GPL, x264 e x265 desativados. O codificador padrão do aplicativo é MPEG-4 Part 2, com AAC quando há trilha de áudio. H.264 AMF é opcional conforme disponibilidade do dispositivo.
- Fonte FFmpeg correspondente: commit `660b5aa7db`, arquivo `third-party-sources/ffmpeg-660b5aa7db.tar.gz` no pacote de código. SHA256: `dc3ae92ca2514a0c26dbc2dc5aa28ed1cc39b3d0645ac9c828035f4cb3e96eb7`.
- Receitas de build e referências das dependências: `third-party-sources/ffmpeg-build-recipes.tar.gz`, commit `7f2a6b1050d302144de8366afa37a75e46f326aa`, https://github.com/FFmpeg-Builds/FFmpeg-Builds . O arquivo de configuração preserva as opções do binário original.

FFmpeg é software livre e pode ser estudado, modificado e substituído segundo a LGPL. O Worship Studio o chama por linha de comando e não incorpora seu código no executável gerenciado.

## VideoLAN

- LibVLCSharp 3.10.2: commit `3082c09ca955131c705fc4767fde03e9c4a14999`, idêntico ao declarado no metadado `repository` dos pacotes NuGet. Fonte original em `third-party-sources/libvlcsharp-3.10.2.tar.gz`, obtida do [espelho oficial VideoLAN no GitHub](https://github.com/videolan/libvlcsharp/tree/3082c09ca955131c705fc4767fde03e9c4a14999); SHA256 `b9e12cbca8038fc78b468f53da80c0fb24f01963cb3c7205a23abbc7f832b79f`.
- LibVLC 3.0.24: fonte original em `third-party-sources/vlc-3.0.24.tar.xz`; SHA256 `e7cab503d1d7d5849b89d2cf0e1ee60d0ef6d012407791b644b9cfc0cc225fdf`.
- Origem da distribuição: https://download.videolan.org/pub/videolan/vlc/3.0.24/ . Build do pacote NuGet oficial `VideoLAN.LibVLC.Windows`.
- LGPL 2.1 integral em `LGPL-2.1.txt`. O aplicativo usa vinculação dinâmica; os binários da biblioteca não foram modificados e podem ser substituídos por versões compatíveis. As fontes do aplicativo são fornecidas para permitir recompilação.

As fontes de terceiros acompanham a pasta de código-fonte e não são necessárias para a operação offline. Os arquivos foram validados como TAR e seus hashes constam em `third-party-sources/SHA256SUMS.txt`. Preserve estes avisos ao redistribuir o pacote.
