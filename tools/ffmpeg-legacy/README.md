# FFmpeg para Windows legado x64

Esta pasta contém uma compilação própria do FFmpeg para a edição de compatibilidade do Worship Studio. O FFmpeg moderno em `tools/ffmpeg` permanece independente.

- Fonte FFmpeg: commit `660b5aa7dbce1f95cb04b3c7b8e94d79f893d15d`, o mesmo fonte não modificado do pacote moderno, em `third-party-sources/ffmpeg-660b5aa7db.tar.gz`.
- Zlib 1.4: fonte não modificado em `third-party-sources/zlib-1.4.tar.gz`, obtido da [Release oficial](https://github.com/madler/zlib/releases/tag/v1.4). SHA256 `02dd159783677a749c04adbdd02b55b8a2bcc7e054f51b76b549199a916ad584`.
- Compilador portátil: [w64devkit 2.11.0 x64](https://github.com/skeeto/w64devkit/releases/tag/v2.11.0), GCC 16.2.0, MSVCRT. O projeto do compilador declara Windows 7 como alvo padrão. Arquivo `w64devkit-x64-2.11.0.7z.exe`, SHA256 `54c2bb9962e8a30c8270e34c5efc3d5fee579f53087fa7d435b95fbba2af7b2b`.
- Licença FFmpeg: LGPL 2.1 ou posterior. GPL, componentes não livres e bibliotecas externas opcionais estão desativados. Zlib usa sua própria licença; o runtime MinGW é estático e tem avisos próprios. Os três textos estão em `licenses/FFmpeg-legacy-*`.

O aplicativo executa `ffmpeg.exe` como processo externo; o programa pode ser recompilado e substituído nesta pasta. Código-fonte correspondente e receita acompanham o ZIP de código do Worship Studio.

## Recompilar

Extraia os dois TARs de código e o compilador em pastas próprias, adicione apenas durante o processo a pasta `w64devkit/bin` ao PATH e execute, usando o `sh` do kit:

```sh
sh tools/ffmpeg-legacy/build.sh /caminho/FFmpeg-660b5aa7dbce1f95cb04b3c7b8e94d79f893d15d /caminho/zlib-1.4 /caminho/pasta-build /caminho/destino
```

Use caminhos absolutos com barras `/`; a receita aceita argumentos entre aspas. O build configura `_WIN32_WINNT=0x0601`, subsistema PE 6.1, Win32 threads, MSVCRT e bibliotecas estáticas, sem UCRT adicional para o processo FFmpeg. Não altera as fontes FFmpeg ou zlib. É uma compilação x64; não atende instalações Windows de 32 bits.

## Recursos

Inclui as partes usadas pela exportação: entrada PNG por pipe, PNG/imagens, fundos MP4/MOV/Matroska, áudio WAV/MP3/AAC/FLAC/Ogg, decodificadores H.264/HEVC/MPEG4, composição/escala/corte de fundo, preenchimento de áudio e saída MP4 com vídeo MPEG4 e áudio AAC. Sem rede. O codificador AMF está ausente; a seleção automática do Worship Studio usa MPEG4 por CPU quando o teste AMF falha. A renderização de quadros ainda usa o backend de desenho escolhido pelo aplicativo.

Outros formatos e codecs do pacote FFmpeg moderno não são incluídos nesta compilação reduzida. O uso de software e compilação sem assembly específico reduz o desempenho em relação ao pacote moderno. A compatibilidade estática de importações PE e os ensaios de exportação no Windows disponível não substituem um teste real em Windows 7/8.
