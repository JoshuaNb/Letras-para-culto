# Verificação — Worship Studio 1.8.0

## Resultado 1.8 — fundos e reprodução compatível

Data: 23/11/2026, America/Sao_Paulo. **842 verificações automáticas aprovadas, zero falhas finais**, mais **18 cenários nativos suplementares**. Os ensaios executaram no Windows 11 disponível; o equipamento em que o usuário relatou a falha não foi acessado.

| Suíte | .NET 10 | .NET 6.0.36 | Evidência no ZIP completo |
| --- | ---: | ---: | --- |
| Núcleo e serviços | 211 | 211 | `test-evidence/core-1.8.0-<framework>/verification.trx` |
| Tipografia, letras e fundos animados | 130 | 130 | `test-evidence/typography-1.8.0-<framework>` |
| Interface real Avalonia Headless/Skia | 38 | 38 | `test-evidence/ui-headless-1.8.0-<framework>/results.json` |
| Cliente Java/HTTP real | 26 | 26 | `test-evidence/file-transfer-1.8/<framework>/android-upload-results.json` |

Completam o total **32 verificações Java** do APK. O cálculo é `2 × (211 + 130 + 38 + 26) + 32 = 842`. Os testes seletivos de decodificação e os relatórios adicionais dos hosts não são somados novamente.

### Caminhos corrigidos

O usuário esclareceu que a falha ocorre nos efeitos internos atrás das letras, em Windows 10. Não houve acesso a esse computador. Para esse caminho, a atualização normaliza os nomes dos efeitos nas cenas, recupera modos desconhecidos com Aurora animada e permite selecionar renderização por software para contornar incompatibilidades de driver. A velocidade zero continua sendo uma pausa intencional, agora indicada no painel. As alterações de reprodução de vídeo abaixo são uma melhoria adicional; não demonstram a causa da falha dos efeitos internos.

O código anterior escondia o renderizador antes de receber o primeiro quadro do vídeo, usava decodificação por hardware do LibVLC e dependia de uma janela nativa de vídeo com outra janela transparente para as letras. Esses caminhos poderiam falhar conforme o driver ou a composição da saída; não se confirmou qual deles ocorreu na outra máquina. A superfície agora recebe quadros BGRA completos de um FFmpeg local em processo isolado, com decodificação por CPU. Vídeo, efeitos e letras são compostos no mesmo Grid/Avalonia, sem HWND de vídeo nem overlay transparente separado. O primeiro quadro substitui o fundo de segurança somente quando está pronto.

Foram verificadas leitura fragmentada/truncada, limite de tempo mesmo com stream que ignora cancelamento, bytes BGRA de vídeo azul, repetição além de duas durações, troca de arquivo, cancelamento/encerramento do processo, arquivo inválido e recuperação. Há no máximo uma tentativa econômica após falha em Auto; o econômico não cria uma sequência infinita de tentativas. O slot do quadro antigo é esvaziado ao falhar/recuperar, para não reapresentar um quadro obsoleto. Filas e buffers são limitados. Esses **14 testes novos por runtime** já fazem parte dos 211 de núcleo, com o FFmpeg correspondente selecionado por `WORSHIP_FFMPEG` no build.

Os outros **15 casos novos** de núcleo cobrem normalização dos nomes antigos/desconhecidos, letras preservadas, mídia gerenciada de uma biblioteca copiada resolvida na pasta atual, referência ausente preservada para recuperação, rejeição de caminho relativo fora da biblioteca e preferências de compatibilidade que permanecem locais sem entrar na cena/backup. A escolha explícita de software elimina ANGLE na inicialização, e a edição legado mantém esse caminho.

### Projeção nativa com vídeo

Um probe abre `ProjectionWindow` real e uma janela independente de prévia. Em cada execução, seis cenários conferem vídeo azul com letras brancas nos mesmos pixels, ocultar somente as letras, tela preta integral, restaurar/trocar qualidade/reiniciar, arquivo ausente com Aurora/letras e quadros animados com hashes/pixels diferentes. As três execuções finais passaram: .NET 10 por software, .NET 6 com FFmpeg legado por software e .NET 10 em ANGLE com interoperabilidade GPU/D3D11 efetiva. Não se tratou de fallback de software nessa terceira execução.

São **18 cenários nativos suplementares**, separados das 842 verificações. Não houve decodificador órfão ao final. Fonte do probe, projeto, relatórios, PNGs e hashes dos fontes finais estão em `test-evidence/video-1.8`, especialmente `verification-final.json` e `native-{modern,legacy,gpu-modern}`. As provas usaram um monitor físico no Windows 11 e não equivalem a testar o telão ou a GPU do outro computador.

### Oito novos fundos e operação

Ondas de graça, Raios de luz, Galáxia em movimento, Bokeh suave, Horizonte 3D, Oceano sereno, Fitas de adoração e Prismas de luz usam composições distintas, sem arquivos externos. Há 11 fundos procedurais entre 14 escolhas. Os **22 novos checks visuais por runtime**, incluídos nos 130, conferem pixels que mudam com o tempo, amostragem determinística, pausa em velocidade zero, distinção entre composições, aliases, cores/áudio, escala e prevalência de tela preta. Imagens ausentes, vazias ou corrompidas produzem Aurora animada com as letras e aviso de mídia, sem repetir o aviso a cada quadro. Capturas e relatório estão em `backgrounds-1.8` dentro da suíte visual.

Os três novos checks de interface conferem os oito fundos chegando à prévia/retorno/projeção, letras preservadas, qualidade econômica herdada pelas superfícies, preferências persistidas e pedido de reinício gráfico sem fechar o operador automaticamente. A regressão HTTP usa fundo Solid; seus 21 fingerprints abrangem os fontes atuais, mas não representam cobertura de vídeo por essa prova.

### Limites e distribuição

Os dois upgrades isolados **1.7 → 1.8** passaram: edição atual com CLR próprio 10 → 10 e legado com CLR 10 → 6.0.36. As quatro aberturas alcançaram a janela principal correta e respondendo; hashes dos payloads conferidos, sem binários inesperados. Biblioteca, preferências, recebido e recibo mantiveram seus hashes após atualizar e após desinstalar as identidades de QA. Ambas as desinstalações retornaram zero. A instalação e os atalhos do operador permaneceram inalterados. A compressão foi desativada somente no ISS de QA; o instalador público mantém a compressão normal. Evidências e fontes: `test-evidence/installer-1.8`. Esses ensaios complementam as 842 verificações.

A prévia/projeção de vídeo usa 1280×720/30 em Auto ou 854×480/24 no econômico; letras e fundos procedurais continuam na resolução da superfície. A edição legado usa o econômico. A exportação MP4 continua compondo diretamente o vídeo original com os quadros das letras na resolução escolhida, sem herdar esse limite da reprodução ao vivo. A preferência gráfica por software exige reinício confirmado; vídeos e renderização da interface são escolhas distintas.

O APK 1.8 mantém Java e DEX da 1.7 e o mesmo certificado. LibVLC continua incluído no pacote com suas licenças, embora não seja usado na superfície de reprodução atual. As provas não validam outra GPU física, rede Wi-Fi/telão da igreja, Android físico ou execução em Windows 7/8/10. Permanecem os requisitos x64, Windows 7 SP1 com KB3063858/equivalente e os limites históricos de Holyrics, áudio e duas telas físicas. Os resultados seguintes são históricos da 1.7 e anteriores.

## Resultado 1.7 — interface, menus e abertura

**734 verificações automáticas aprovadas, zero falhas finais.** As duas edições foram exercitadas no Windows 11 disponível. Os ensaios nativos e do instalador complementam esse número.

| Suíte | .NET 10 | .NET 6.0.36 | Evidência no ZIP completo |
| --- | ---: | ---: | --- |
| Núcleo e serviços | 182 | 182 | `test-evidence/core-1.7.0-<framework>/verification.trx` |
| Tipografia e animações | 108 | 108 | `test-evidence/typography-1.7.0-<framework>` |
| Interface real em Avalonia Headless/Skia | 35 | 35 | `test-evidence/ui-headless-1.7.0-<framework>/results.json` |
| Transporte Java/HTTP real | 26 | 26 | `test-evidence/file-transfer-1.7/<framework>/android-upload-results.json` |

Completam o total **32 verificações Java** do APK, registradas em `android-remote/verification/build-1.7.0.json`. Os relatórios dos hosts complementam a integração sem serem somados novamente.

### O que foi exercitado

Os 15 novos casos de núcleo abrangem 11 cenários de inicialização e quatro de contraste das faixas nos temas. A biblioteca é lida em um worker independentemente da consulta de atualização; foram exercitados opt-out sem requisição, erro de rede, nova consulta em cada sessão, biblioteca corrompida e falha na criação da pasta sem sobrescrever dados, cancelamento e timeout mesmo quando o cliente ignora o token. Uma resposta tardia não prevalece sobre o cancelamento. O teto de produção é de seis segundos; os testes usam prazos menores para exercitar os mesmos caminhos.

As sete novas verificações de interface abrem os menus por entrada de ponteiro nos controles reais, usam o roteiro para preparar e reordenar louvores sem alterar a saída ao vivo, operam tela preta/letra pelo menu, carregam uma biblioteca real pela janela de abertura e clicam **Abrir programa agora** durante uma consulta injetada pendente. O painel recebe as letras e o resultado da abertura sem disparar uma segunda consulta. Fechar a abertura cancela e não abre uma janela tardiamente. O tamanho mínimo 1180 × 760 preserva monitores, menus, botões e cartões. Capturas dos quatro temas, da abertura e da operação acompanham os relatórios.

A regressão de rede usa os arquivos Java exatos do APK e os serviços C# de produção com `MainWindow`/`ProjectionWindow` reais em Headless/Skia. Foram observados 26 casos em cada host 1.7.0.0, em Debug, e cinco arquivos concluídos conferidos byte a byte, com SHA256/eventos, durante comandos de projeção. Essa prova usa o construtor direto de `MainWindow`; a abertura é exercitada separadamente pelas suítes acima. Os 13 fingerprints dos fontes correspondem aos arquivos executados. Instruções em `test-evidence/file-transfer-1.7`.

### Ensaio nativo no Windows disponível

Um aplicativo self-contained atual foi iniciado com `--data` apontando para uma pasta sintética isolada e busca automática habilitada. A operação abriu com dois louvores, mostrou **Você está usando a versão mais recente** após a consulta oficial e registrou **Aceleração de GPU ativa**. **Projetar estrofe** abriu a janela independente; **Projeção → Próxima estrofe** avançou a saída e o retorno para 2/3, mantendo a prévia em 1/3. A operação foi encerrada normalmente e os dados sintéticos conferidos. Esse ensaio manual é separado das 734 verificações. Registro e fixtures: `test-evidence/native-1.7`.

### Atualização do instalador

Dois upgrades isolados **1.6 → 1.7** passaram no Windows 11: atual com CLR próprio 10 → 10 e legado com CLR 10 → 6.0.36, este forçado apenas no ISS de QA. As quatro aberturas alcançaram a `MainWindow` correta/respondendo, passando pela nova inicialização. DLLs, FFmpeg e APK corresponderam aos payloads e a limpeza de runtimes não deixou binários inesperados. Biblioteca, preferências, arquivo recebido e recibo preservaram seus hashes na atualização e após desinstalar as identidades de teste; ambas as desinstalações retornaram zero. Instalação e atalhos originais permaneceram inalterados. A compressão foi desativada somente nos instaladores de QA; o pacote público mantém a compressão normal. Fontes e relatórios: `test-evidence/installer-1.7`. Esses ensaios não entram novamente nas 734 verificações.

### APK e limites

O APK 1.7 mantém os quatro fontes Java e `classes.dex` idênticos à 1.6, com novo versionName/versionCode, compilação Java, alinhamento e assinaturas v2/v3 conferidos. A mesma chave de assinatura foi preservada fora do Git e do ZIP.

Os ensaios locais não confirmam Activity/SAF/toques em celular físico, uma rede Wi-Fi específica nem execução em Windows 7/8/10. O ensaio nativo usou um monitor físico; duas telas físicas, captura de áudio e Holyrics no equipamento da igreja continuam com os limites documentados. A operação offline não exige concluir uma consulta de atualização. Os resultados seguintes são históricos da 1.6 e anteriores.

## Resultado 1.6 — arquivos do celular ao PC

**690 verificações aprovadas, zero falhas finais.** Os testes de núcleo, renderização e interface usam os runtimes .NET 10 e .NET 6.0.36 no Windows 11 disponível. O teste de rede utiliza somente Loopback, sem acessar dados do operador nem solicitar permissões de firewall.

| Suíte | .NET 10 | .NET 6 | Evidência no ZIP completo |
| --- | ---: | ---: | --- |
| Núcleo e serviços | 167 | 167 | `test-evidence/core-1.6.0-<framework>/verification.trx` |
| Tipografia e animações | 108 | 108 | `test-evidence/typography-1.6.0-<framework>` |
| Interface real em Avalonia Headless/Skia | 28 | 28 | `test-evidence/ui-headless-1.6.0-<framework>/results.json` |
| Cliente Java do APK por HTTP real | 26 | 26 | `test-evidence/file-transfer-1.6/<framework>/android-upload-results.json` |

Completam o total 32 verificações do parser Java, registradas em `android-remote/verification/build-1.6.0.json`. Os relatórios adicionais do host complementam a integração e não são somados à contagem.

### Recebimento e operação

Os 29 novos casos de núcleo exercitam autenticação antes da leitura, tamanho conhecido e HTTP chunked, Unicode e nomes Windows, SHA256, publicação atômica, arquivos vazios, nomes repetidos, limites, quota, concorrência, parada do servidor e limpeza após cancelamento/truncamento. Os comandos de projeção continuam disponíveis durante o upload. O servidor despacha cada cliente fora do loop de aceitação e limita o recebimento a um arquivo simultâneo.

A integração compila os arquivos exatos `RemoteUploadClient.java`, `RemoteClient.java` e `RemoteConnection.java` incluídos no APK e envia bytes por `HttpURLConnection` ao servidor C# de produção. Os hosts instanciam `MainWindow` e `ProjectionWindow` reais em Headless/Skia. Foram conferidos cinco arquivos por host, bytes, hashes, eventos e comandos durante um envio lento de 2 MiB. Cancelamento, truncamento, falta de autorização, excesso de tamanho, concorrência e revogação também foram observados. Código e instruções acompanham `test-evidence/file-transfer-1.6`.

A janela de recebidos foi exercitada com os controles reais: abre sem ativar a rede ou alterar a apresentação, lista os arquivos persistidos e habilita ações pelo tipo. Um TXT só foi adicionado à biblioteca após clicar **Importar letras**; uma imagem só alterou a cena após clicar **Usar como fundo**, sendo copiada à mídia local. Os originais recebidos e o documento salvo foram conferidos. A consulta da lista acontece em segundo plano. O primeiro ensaio esperava o temporizador de gravação do backend Headless; a sonda foi ajustada para observar a ação na interface e conferir a persistência após fechar a operação, sem alterar a gravação de produção.

### Navegador e APK

Em uma janela real do DuckDuckGo no Windows, o formulário de produção selecionou e enviou uma fixture de 3.072 bytes. A interface confirmou um recebimento e progresso completo; o servidor conferiu os bytes e SHA256 `4681054725f3613d6500d138adf9f172a518d86937b2690338aa462fd029368b`. **Próxima** e **Tela preta** continuaram funcionando depois do envio. Esse ensaio manual é suplementar às 690 verificações. Relatório, fonte do host e encerramento estão em `test-evidence/file-transfer-1.6/browser`, sem a credencial temporária. O JavaScript também passou na validação de sintaxe.

O APK foi compilado e assinado com o mesmo certificado das versões anteriores, conferindo esquemas v2/v3, alinhamento e manifesto 1.6.0/10600. A revisão estática cobre executor de upload independente, geração de sessão, cancelamento e seleção SAF diferida para `onResume`. **Activity, seletor SAF, toques e instalação em celular físico não foram executados nesta sessão.**

### Atualização e instalador

Dois ensaios isolados atualizaram a versão 1.5 para 1.6 no Windows 11: edição atual com CLR 10 e edição legado forçada somente no instalador de QA, com CLR 6.0.36. Os hashes de DLLs, FFmpeg e APK corresponderam aos payloads, a janela respondeu e a limpeza de runtimes não deixou binários inesperados. Biblioteca, preferências, arquivo recebido e recibo permaneceram idênticos na atualização e após desinstalar as identidades de teste. A instalação original e seus atalhos foram preservados. O QA usa compressão desativada para agilizar os ensaios; o instalador público mantém a compressão normal. Evidências e sondas reproduzíveis: `test-evidence/installer-1.6`. Esses dois ensaios complementam a contagem de 690 verificações.

### Limites mantidos

As provas não confirmam uma rede Wi-Fi específica, celulares físicos ou execução em Windows 7/8/10. Permanecem as exigências x64 e Windows 7 SP1 com KB3063858/equivalente, além dos limites de equipamento da igreja e Holyrics real já documentados. A transferência tem limites reais de 512 MiB por arquivo, 5 GiB de armazenamento e 5.000 arquivos; não executa os arquivos recebidos e não inclui retomada automática. Os resultados seguintes são históricos da 1.5 e anteriores.

## Resultado 1.5 — runtimes atual e legado

Data: 22/11/2026, America/Sao_Paulo. **583 verificações aprovadas, zero falhas finais:** 544 nas suítes C#/Avalonia dos dois runtimes, 32 Java e sete do FFmpeg legado. Os testes executaram no Windows 11 disponível; não constituem teste de execução em Windows 7, 8 ou 10.

| Suíte | .NET 10 | .NET 6.0.36 | Evidência no ZIP completo |
| --- | ---: | ---: | --- |
| Núcleo e serviços | 138 | 138 | `test-evidence/core-1.5.0-<net10.0 ou net6.0>/verification.trx` |
| Tipografia e animações clássicas | 29 | 29 | `test-evidence/typography-1.5.0-<framework>/typography-render-tests.json` |
| Animações granulares | 79 | 79 | `test-evidence/typography-1.5.0-<framework>/granularity-1.2/granularity-render-tests.json` |
| Operação real do PC em Avalonia Headless/Skia | 26 | 26 | `test-evidence/ui-headless-1.5.0-<framework>/results.json` |

Os 138 casos incluem cinco verificações da política de renderização e três novas regressões de corpos HTTP fragmentados, chunked e truncados, autenticação e operação única. O código de produção é o mesmo nas duas compilações. O build selecionou explicitamente o FFmpeg correspondente por `WORSHIP_FFMPEG`: os testes MP4 da edição legado exercitaram o executável legado final, incluindo composição real de fundo e conferência de pixels.

O parser Android passou em 32 verificações Java. O APK 1.5 mantém o protocolo e código de operação da 1.4; a mudança é a versão do pacote, recompilado e assinado com o mesmo certificado definitivo, com esquemas v2/v3 e alinhamento conferidos. A instalação em celular físico continua pendente.

### Dependências nativas e MP4 legado

O FFmpeg legado foi compilado do mesmo fonte 8.1.3 com w64devkit/MSVCRT e alvo Windows 7, sem rede e sem bibliotecas externas opcionais. A receita final usa `--disable-asm`: uma compilação intermediária apresentou cores incorretas na conversão RGB/YUV e foi descartada. O binário final passou na leitura independente de pixels vermelhos sobre fundo azul, PNG por pipe, oito frames MPEG4, áudio AAC preenchido e composição de fundo em loop.

As sete verificações estão em `test-evidence/windows-legacy-native/final/ffmpeg-legacy-results.json`. A auditoria normal e de delay-imports dos dois executáveis constatou x64, subsistema PE 6.1 e ausência de imports conhecidos posteriores ao Windows 7. O código do verificador e a receita estão em `tools/ffmpeg-legacy`. A auditoria CRT do runtime 6 e dos plugins LibVLC está em `test-evidence/windows-legacy-native/crt-audit.json`.

O payload publicado inclui .NET 6.0.36 completo, `ucrtbase.dll` e API sets locais. Skia/HarfBuzz e LibVLC não exigem instalação global do Visual C++ para essa composição de binários. O ANGLE moderno é excluído da edição legado; `WindowsCompatibility` seleciona somente Skia por software nessa edição.

### Instalador universal

Os três ensaios finais passaram com identidade, pasta, dados e grupo de atalhos isolados: **1.4 → 1.5 atual**, **1.4 → 1.5 legado** e **1.5 legado → 1.5 atual**. Cada etapa abriu o aplicativo, carregou o `coreclr.dll` da própria instalação com o runtime esperado e respondeu. Os hashes dos binários, FFmpeg e APK corresponderam aos payloads; não restaram binários exclusivos da outra edição. Biblioteca e preferências foram preservadas, as desinstalações retornaram zero e registro/atalhos da instalação original do operador permaneceram iguais.

Relatório final: `test-evidence/installer-1.5/results.json`, `RunId=final`, `Passed=true`. O primeiro ensaio da sonda esperava pontos no texto de versão do runtime, que veio com vírgulas; a verificação foi corrigida para usar os campos numéricos `FileMajorPart`/`FileMinorPart`. Nenhum binário do aplicativo foi alterado por essa correção. O resultado inicial está preservado como `results-initial-probe.json`; os três cenários foram repetidos em identidades novas. O código e as instruções de reprodução acompanham a pasta de evidências. O instalador forçado para legado é exclusivo de QA e não é publicado.

### Limites desta validação

Não há máquinas ou imagens Windows 7/8/10 disponíveis nesta sessão. Compilação, inspeção de dependências, testes no runtime antigo e instalação forçada do payload legado em Windows 11 não substituem um ensaio nesses sistemas. Windows 7 requer SP1 e a API `AddDllDirectory` da KB3063858 ou equivalente; x86/32 bits não está incluído. A edição legado usa um runtime cujo suporte regular terminou. Os [requisitos e limites](docs/COMPATIBILIDADE-WINDOWS.md) distinguem compatibilidade pretendida de suporte oficial dos fornecedores.

Continuam pendentes celular físico, biblioteca real do Holyrics, dois monitores físicos e sinal de áudio não nulo na igreja. As evidências seguintes são históricas da 1.4 e anteriores.

## Resultado da versão 1.4 — Android e Holyrics

Data: 22/11/2026, America/Sao_Paulo. **320 verificações aprovadas, zero falhas finais ou testes ignorados.**

| Suíte | Aprovações | Evidência no código-fonte completo |
| --- | ---: | --- |
| Núcleo e serviços | 130 | `test-evidence/core-1.4/verification.trx` |
| Tipografia e animações clássicas | 29 | `test-evidence/typography-1.4.0/typography-render-tests.json` |
| Animações granulares | 79 | `test-evidence/typography-1.4.0/granularity-1.2/granularity-render-tests.json` |
| Operação real do PC em Avalonia Headless/Skia | 26 | `test-evidence/ui-headless-1.4.0/results.json` |
| Janela de download Holyrics em Headless/Skia | 7 | `test-evidence/holyrics-1.4/window-results.json` |
| Validação da conexão Android em Java | 32 | `android-remote/verification/build-1.4.0.json` |
| Cliente Android e operação PC por HTTP real | 17 | `test-evidence/android-1.4/android-client-results.json` |

O núcleo inclui 18 casos novos de pareamento e 19 de seleção/download do Holyrics. Os testes TCP usam Loopback; não abrem o processo de testes à rede da igreja. Na produção, o servidor escuta a rede local somente depois de o operador iniciar o controle.

### Prova entre os componentes de produção

`RemoteConnection.java` e `RemoteClient.java` são os arquivos exatos incluídos no APK. Executaram na JVM com a implementação oficial org.json para Java, conectados por HTTP real ao `RemoteServer` de produção em Loopback. O host usou `MainWindow`, `ProjectionWindow`, `StageSurface` e serviço de áudio reais com backend de janelas Avalonia Headless. Não se substituíram respostas HTTP por mocks nessa prova.

Foram observados pareamento real, leitura da apresentação, próxima/anterior, tela preta, ocultar/restaurar letra, seleção de estrofe e pulso no frame. Credencial incorreta, comandos inválidos e índice negativo foram recusados; reiniciar revogou o token, novo pareamento preservou a apresentação e desligar encerrou o acesso. O relatório complementar `test-evidence/android-1.4/desktop-host-results.json` confirma a chegada dos comandos aos componentes de produção e o pulso no serviço de áudio.

### APK e Holyrics

O APK nativo foi compilado com JDK 21, plataforma Android 36 e build-tools 36.0.0, compatível com API mínima 26. A assinatura definitiva RSA 3072 foi conferida pelos esquemas APK v2/v3; alinhamento e manifesto foram verificados com ferramentas Android oficiais. O pacote inclui a licença MIT. Keystore e senhas ficam fora do código-fonte, dos pacotes e do GitHub. Hash e tamanho finais estão no manifesto da Release.

O APK lembra somente o endereço do PC; código e token ficam na sessão. Uma revisão estática corrigiu o envio de tarefas enfileiradas depois de pausar a Activity. Isso não é apresentado como teste de ciclo de vida em Android real.

O Holyrics foi verificado com respostas controladas do contrato oficial GetSongs/GetLyrics e sua janela real: catálogo, busca sem diferença de acento, seleção conservada ao filtrar, quatro temas, download somente das selecionadas, progresso, cancelamento e lote completo sem gravação parcial. A operação do botão da janela principal também foi testada, incluindo abertura e cancelamento sem alterar a biblioteca.

### Instalador da versão 1.4

A instalação 1.3.0, atualização para 1.4.0 e desinstalação foram aprovadas com identidade, diretório e grupo de atalhos isolados. Os hashes do executável gerenciado, runtime, LibVLC, FFmpeg e APK instalado corresponderam ao payload. Biblioteca e preferências de teste foram preservadas; o runtime veio da própria instalação, e o programa abriu e respondeu. Registro e atalhos da instalação existente do operador permaneceram iguais. Relatório: `test-evidence/installer-1.4/results.json`.

### Limites da versão 1.4

O APK foi compilado e assinado, e seus clientes de rede foram exercitados por HTTP real. **A Activity, os toques e a instalação em celular físico ainda não foram observados.** O emulador local não ficou disponível sem aceleração de virtualização; não se alterou BIOS, hipervisor ou configuração de segurança do equipamento para tentar habilitá-lo. As capturas de PC são desenhos reais da interface em Skia com backend Headless.

A importação não foi exercitada contra uma biblioteca física específica do Holyrics do usuário: é necessário ativar o API Server e fornecer seu token de leitura. Permanecem pendentes dois monitores físicos e áudio não nulo na igreja. Não há benchmark de FPS/latência, download de áudio MP3, BPM, sincronização de sílabas ou importação de modelos 3D.

## Histórico — versão 1.3.0

Os registros seguintes descrevem a distribuição anterior; suas evidências históricas estão no ZIP da Release 1.3.0. A validação atual está detalhada acima.

Data: 22/11/2026, America/Sao_Paulo. Compilação Release .NET 10, Avalonia 11.3.22, Windows x64, runtime incluído na distribuição. O equipamento de desenvolvimento tem Ryzen 5 5600G, RX 6600 e um monitor físico.

## Resultado automatizado da versão 1.3

**225 verificações aprovadas, zero falhas ou testes ignorados.**

| Suíte | Aprovações | Evidência no ZIP completo |
| --- | ---: | --- |
| Núcleo e serviços | 93 | `test-evidence/update-1.3/verification.trx` |
| Tipografia e animações clássicas | 29 | `test-evidence/typography-1.3.0/typography-render-tests.json` |
| Animações granulares | 79 | `test-evidence/typography-1.3.0/granularity-1.2/granularity-render-tests.json` |
| Interface real Avalonia Headless + Skia | 24 | `test-evidence/ui-headless-1.3.0/results.json` |

O núcleo inclui 36 casos novos do atualizador e 13 de preferências/contraste. A interface usa `App.axaml`, `MainWindow.axaml`, controles, eventos e renderizadores reais. Somente o backend de janelas é substituído por Avalonia Headless; o desenho é feito por Skia.

### Verificações da atualização

- Inicialização real do XAML, biblioteca e preferências isoladas dos dados do operador.
- Troca imediata dos quatro temas e persistência; contraste mínimo 4,5:1 para os textos de interface avaliados, incluindo destaques no amarelo pastel.
- Fonte, negrito, cor da letra e cena preservados ao trocar o tema da interface.
- Prévia e retorno com superfícies independentes, proporção 16:9, três abas do inspetor, busca e biblioteca.
- Envio à projeção independente, preparar outra estrofe sem enviar, próxima/anterior, tela preta e letra oculta somente na saída ao vivo.
- Galeria com 66 cartões, abertura e cancelamento; os 65 efeitos animados continuam gerando sequências distintas e terminam no texto completo.
- Atualizador rejeita versões antigas/iguais, rascunhos, pré-lançamentos, nomes/hosts/arquivos inválidos, corrupção e download incompleto. Respeita cancelamento, timeout e limites de tamanho, removendo arquivos parciais.
- Janela de atualização modeless: o operador pode continuar navegando estrofes. Progresso, SHA-256, cache da mesma sessão, adiamento e recusa de arquivo adulterado antes da instalação foram exercitados com transporte HTTP injetado e bytes inertes. Nenhum arquivo de teste é executado.

Houve também **uma integração HTTP real**: o serviço detectou a versão 1.2.0 ao simular a 1.1.0, baixou o instalador oficial de 151.413.199 bytes e conferiu manifesto e digest do GitHub. SHA-256: `baa830fe16393958c86135f5b5cc4434136ee18f461e4839c77c77ac7e13e7f8`. O instalador baixado por essa prova não foi executado. Evidência: `test-evidence/update-1.3/live-result.json`. Esta prova é adicional às 225 verificações.

### Reproduzir a versão 1.3

```powershell
dotnet test tests/WorshipStudio.Tests.csproj -c Release
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -- --output test-evidence/typography-1.3.0
dotnet run --project ui-tests/WorshipStudio.UiTests.csproj -c Release -- --output test-evidence/ui-headless-1.3.0
.\package-release.ps1
```

O ZIP completo inclui ferramentas nativas e fontes/licenças de terceiros. Após `git clone`, execute `Restaurar-Distribuicao.ps1` antes de empacotar.

### Instalador e atualização da versão anterior

A instalação 1.2.0, atualização sobre ela para 1.3.0 e desinstalação terminaram com código zero em identidade, diretório e grupo de atalhos exclusivos de QA. Biblioteca e preferências de teste foram preservadas. Os hashes instalados de WorshipStudio.dll, runtime, LibVLC e FFmpeg corresponderam ao payload. O executável abriu e respondeu com coreclr.dll carregado da própria instalação e DOTNET_ROOT externo desativado. Registro e atalhos da instalação do operador permaneceram iguais no ensaio final. Evidência: `test-evidence/installer-1.3/results.json`.

### Limites da versão 1.3

As capturas dos quatro temas são renderizações da interface real no backend headless. A abertura nativa do executável 1.3.0 foi observada pela árvore de acessibilidade: janela de operação, biblioteca inicial, GPU ativa e consulta da versão instalada. Isso não equivale a benchmark de FPS/latência nem a inspeção de dois monitores físicos.

A confirmação nativa de instalação pelo botão do aplicativo requer interação do operador. Descoberta e download foram exercitados na rede real; acionamento e adiamento foram testados com callback controlado. Não se publicou uma nova versão oficial falsa para simular atualização superior à 1.3.0.

Permanecem pendentes os testes na igreja com dois monitores físicos, celular físico, Holyrics real e áudio não nulo. Não há detecção de BPM, sincronização de sílabas, importação de modelos 3D ou edição livre de keyframes. As animações interpretam imagens estáticas recebidas, sem comprovação de reprodução idêntica a um vídeo.

## Histórico — verificação da versão 1.2.0

Os registros abaixo descrevem versões anteriores. Suas evidências acompanham os respectivos ZIPs nas [Releases](https://github.com/JoshuaNb/Letras-para-culto/releases), e não substituem a validação 1.3 acima.

Data: 22/11/2026, fuso America/Sao_Paulo. Este relatório distingue testes observados de capacidades implementadas que ainda dependem de validação com os equipamentos da igreja.

## Ambiente observado

- Windows x64, build 26300.
- SDK .NET 10.0.404.
- AMD Ryzen 5 5600G, Radeon RX 6600 e Radeon Graphics integrada.
- 31,4 GB de memória reportada pelo sistema.
- Uma tela física detectada no ambiente. A operação em duas janelas é validada neste monitor; a disposição final em dois monitores físicos continua dependente de conectar o segundo dispositivo.

Inventário legível por máquina: `test-evidence/environment.json`. A publicação é self-contained win-x64 e inclui o runtime; o usuário final não precisa do SDK.

## Granularidade e galeria — atualização 1.2

A compilação Release da versão 1.2.0 terminou sem erros nem avisos. Foram executados **44 testes de núcleo, 29 verificações visuais anteriores e 79 verificações visuais granulares: todos aprovados, sem falhas ou testes ignorados**. Os registros são `test-evidence/core-1.2/core-1.2.trx`, `test-evidence/render-complete-1.2/typography-render-tests.json` e `test-evidence/render-complete-1.2/granularity-1.2/granularity-render-tests.json`; os quadros PNG acompanham os resumos.

```powershell
dotnet test tests/WorshipStudio.Tests.csproj -c Release
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -- --output test-evidence/typography-1.2
```

| Recurso da atualização | Verificação observada |
| --- | --- |
| Galeria das referências | 66 opções com identificadores únicos: 65 animadas e Nenhuma; os 65 efeitos geram sequências distintas de pixels |
| Granularidade | Texto inteiro, linhas visuais após quebra automática, palavras e grafemas Unicode animados separadamente |
| Unicode | Acentos combinados, emoji de família e bandeira preservados como unidades completas |
| Tempo e ordem | Duração por unidade, intervalo, atraso inicial, início/fim/centro/aleatória repetível e repetição com pausa validados |
| Curvas | Seis curvas, extremos e ultrapassagem de Back conferidos; transformações finitas e estado final completo |
| Desfoque real | Quadros intermediários apresentam bordas suavizadas pelo filtro Skia; o fim recupera exatamente o texto nítido de Nenhuma |
| Máscaras e RGB Split | Recortes, separação dos canais e transformação por unidade alteram os quadros intermediários e terminam no texto completo |
| Aparência e operação | Fonte, negrito e cor respeitados; letra oculta, tela preta e sobreposição transparente para vídeo preservadas |
| MP4 | O tempo local da estrofe reinicia a entrada granular independentemente do tempo global do fundo |
| Persistência | Parâmetros sobrevivem à biblioteca, cena JSON e backup; dados fora dos limites são normalizados e cenas antigas mantêm o modo clássico |

Os testes desenham com os arquivos reais de produção em Avalonia Headless e Skia. O motor é compartilhado pela prévia, projeção e captura para MP4. A textura do texto completo é armazenada em cache durante a animação, e o estado final usa um único desenho do texto. Isso não equivale a uma medição de FPS ou de latência em dois projetores físicos.

Na interface Windows da versão 1.2 foram observados: abertura da galeria com 66 cartões; busca por Typewriter Blur Forte; seleção com parâmetros padrão por letra; pausa da prévia mostrando letras completas e letras ainda desfocadas; mudança para Por linha com duas unidades; Aplicar à cena atualizando o painel e a biblioteca; reabertura com os parâmetros salvos; escolha de Nenhuma seguida de Cancelar preservando Typewriter Blur Forte; envio à projeção independente, avanço de estrofe reiniciando a entrada e tela preta cobrindo a animação. O aplicativo reportou aceleração de GPU ativa e foi fechado pelos controles da janela. Registro: `test-evidence/ui-verification-1.2.json`.

As quatro referências recebidas são imagens estáticas de uma galeria. Os nomes e categorias foram implementados; os movimentos foram interpretados a partir dessas referências. As imagens não permitem comprovar reprodução idêntica dos movimentos de um vídeo.

Foi executada também uma integração MP4 com o serviço de exportação e o renderizador de produção: **7 verificações adicionais aprovadas**. FFprobe confirmou 640×360, 12 quadros/s, três segundos e 36 quadros. FFmpeg decodificou todos os quadros; as duas estrofes reiniciaram sem letras, evoluíram durante a entrada e terminaram completas. A comparação dos quadros finais com as imagens de referência sem animação confirmou sobreposição de 95,9% e 96,6% e erro médio RGB de aproximadamente 0,14/255 após compressão. Vídeo, quadros, código do verificador e métricas: `test-evidence/mp4-granularity-1.2/`. **Total automatizado da versão 1.2: 159 verificações aprovadas, zero falhas.**

## Tipografia e animações — histórico da atualização 1.1

A versão 1.1 foi compilada em Release sem erros nem avisos. A correção de fonte usa uma chave de cache que distingue explicitamente família, peso, estilo e largura da fonte, em vez da representação genérica do objeto `Typeface`. A regressão visual troca a fonte na mesma estrofe e no mesmo renderizador, de forma que reutilizar indevidamente o texto da fonte anterior seja detectado.

**Resultado da versão 1.1: 30 testes de operação aprovados, 0 falhas, 0 ignorados, e 29 verificações visuais aprovadas, 0 falhas.** O registro dos testes é `test-evidence/verification-1.1.trx`; o resumo visual e os quadros PNG estão em `test-evidence/typography-1.1/`.

```powershell
dotnet test tests/WorshipStudio.Tests.csproj -c Release --logger 'trx;LogFileName=verification-1.1.trx'
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -- --output test-evidence/typography-1.1
```

| Regressão da atualização | Evidência observada |
| --- | --- |
| Troca de fonte | A mesma estrofe produz pixels distintos para Segoe UI, Georgia e Consolas sem reiniciar o renderizador |
| Negrito | Alternar peso normal 400 e negrito 700 muda os pixels e a área ocupada pelos traços |
| Cor da letra | Vermelho e azul alteram imediatamente os pixels do texto |
| Amostra visual de cor | Alterar `ColorField.Value` atualiza a amostra renderizada e emite um evento por mudança efetiva |
| Entradas | As 12 entradas animadas evoluem em tempos determinados e terminam com a letra completa |
| Duração zero | A letra completa aparece no primeiro quadro ao vivo quando a duração é zero |
| Movimento contínuo | Os oito efeitos variam com o tempo mesmo com fundo parado; Sem animação permanece estável |
| Velocidade e intensidade | Alterar esses parâmetros modifica o movimento escolhido; intensidade zero estabiliza o texto |
| Unicode e segurança de leitura | Acentos e estrofe extensa permanecem dentro do quadro; letra oculta e tela preta prevalecem sobre os efeitos |
| Persistência | Fonte, negrito, cor e animações sobrevivem à biblioteca, cena JSON e backup/restauração |
| Compatibilidade | Cenas antigas mantêm fonte/cor e recebem negrito ligado, animação contínua None, velocidade 1 e intensidade 0,5 |

As verificações visuais usam os arquivos de produção do renderizador em Avalonia Headless com pixels reais do Skia. Elas amostram o mesmo caminho de desenho usado na projeção e na captura de quadros para MP4; não medem FPS nem substituem um projetor físico. `build.ps1` executa essa suíte antes de publicar.

Na interface real da atualização, foram observados os seguintes fluxos:

| Operação manual na versão 1.1 | Resultado |
| --- | --- |
| Fonte Segoe UI → Palatino Linotype | A letra passou a apresentar a aparência serifada da fonte escolhida |
| Alternar Negrito | A espessura do texto mudou entre os pesos normal e negrito |
| Cor pela paleta | Escolher amarelo atualizou a prévia enquanto o seletor permanecia aberto |
| Cancelar cor com Esc | A cor branca anterior foi restaurada |
| Cor HEX e Aplicar | Digitar `#FFD166` e clicar Aplicar cor fechou o seletor e manteve a letra dourada na prévia |
| Botões do seletor | Aplicar cor e Cancelar permaneceram visíveis no rodapé |
| Animação contínua | Selecionar Onda nas linhas habilitou seus controles e animou a letra na prévia |

O empacotamento 1.1 foi validado com identidade de instalação isolada, conforme descrito no fim deste relatório.

## Testes de operação — base preservada

Comando executado sobre o projeto entregue:

```powershell
dotnet test tests\WorshipStudio.Tests.csproj -c Release --logger 'trx;LogFileName=verification.trx'
```

Na versão 1.0, o resultado foi **27 aprovados, 0 falhas, 0 ignorados**, com registro em `test-evidence/verification.trx`. Na versão 1.1, a suíte passou com **30 aprovados**, incluindo três regressões de persistência de tipografia; o registro é `test-evidence/verification-1.1.trx`. A versão 1.2 passou os 44 testes descritos no início deste relatório.

| Área | Verificação efetiva | Resultado |
| --- | --- | --- |
| Letras | Português/Unicode, CRLF, separação de estrofes, rótulos e linhas vazias sem slides fantasma | Aprovado |
| Persistência | Títulos/letras/roteiro/cena preservados em gravação e leitura | Aprovado |
| Integridade | Gravação inválida não substitui a biblioteca anterior; revisão `.bak` preservada; JSON corrompido é reportado sem sobrescrita | Aprovado |
| Backup | Mídia copiada/restaurada após remover o original; caminhos ZIP com `..` rejeitados | Aprovado |
| Mídia ausente | Backup de fundo ativo ausente é rejeitado sem destruir destino anterior; referência inativa ausente é limpa | Aprovado |
| Biblioteca legada | Autoria `null` é normalizada, e referências ausentes do roteiro são removidas | Aprovado |
| Controle remoto | HTTP real em loopback; token exigido; comandos válidos despachados; comandos/índices inválidos rejeitados; reinício revoga token | Aprovado |
| OpenLyrics | Ordem de versos e quebras de linha preservadas; entidades XML externas rejeitadas | Aprovado |
| JSON Holyrics | Ordem de slides importada e repetição preservada | Aprovado |
| API Holyrics | Servidor TCP local responde ao POST oficial `GetSongs` e fallback `GetLyrics`; token codificado e parâmetros verificados | Aprovado |
| Seguidor Holyrics | Mapeamento de seleção com numeração descontínua, tela preta, letra oculta, ausência de apresentação e atualização sem duplicar eventos a cada consulta | Aprovado |
| MP4 real | FFmpeg codifica os quadros; ffprobe confirma MPEG-4, 320×180 e 8 quadros esperados | Aprovado |
| Composição de vídeo | Fundo azul em MP4 é combinado com PNG transparente/vermelho; quadro final decodificado confirma ambos por comparação de pixels | Aprovado |
| Cancelamento MP4 | Arquivo preexistente preservado; saída parcial removida | Aprovado |

Os testes da API Holyrics usam um servidor local controlado. Uma instalação real do Holyrics e suas permissões não foram utilizadas; a interoperabilidade com essa instalação deve ser conferida antes do uso em culto.

## Verificação visual e operação — base 1.0

O aplicativo 1.0 foi aberto e operado na sessão Windows, com inspeção visual das janelas reais. Os ajustes de tipografia e as verificações da atualização 1.1 estão descritos na seção anterior.

| Fluxo observado | Resultado |
| --- | --- |
| Painel principal | Biblioteca, prévia, cartões de estrofes e controles de cena renderizados e legíveis |
| Janela de projeção | Janela própria, independente do painel de operação |
| Preparação sem interferir no culto | Selecionar a segunda estrofe mantém a primeira no ar até o comando de envio |
| Navegação ao vivo | Comando Próxima muda a projeção para a segunda estrofe |
| Tela preta | A saída de projeção ficou preta mantendo o painel disponível |
| Vídeo local real | Vídeo de barras de cor animado reproduzido com letras sobrepostas na prévia e na janela de projeção |
| Composição nativa | Permaneceram apenas as janelas de operação e projeção, sem janela externa do VLC |
| Tela preta com vídeo | Vídeo ocultado na projeção enquanto a prévia continuou reproduzindo |
| Retorno após tela preta | Vídeo e letra retomados na janela de projeção |
| Compositor | A sessão reportou aceleração gráfica ativa |
| Início do controle remoto | Botão do painel iniciou o servidor e gerou o endereço de acesso; o Windows exibiu sua solicitação de permissão de firewall |

Essa verificação não mede uma taxa mínima de quadros ou latência em dois dispositivos físicos. As imagens de inspeção foram vistas na ferramenta de controle de tela; não há captura salva acompanhando esta seção.

## Áudio

Foi possível inicializar e parar a captura WASAPI loopback e verificar o pulso manual com sua atenuação. Foi tentado também capturar uma senoide local gerada a 440 Hz, com saída no próprio endpoint padrão, sem mudar configurações de áudio do usuário.

O endpoint reportou formato IEEE float de 32 bits, 44.100 Hz e oito canais. A captura NAudio direta recebeu 31 pacotes com todos os samples em zero, e o serviço do aplicativo também registrou zero. Isso não valida a resposta a um sinal físico nesse endpoint. A mesma observação ocorreu com reprodução estéreo e com o formato extensível exato do dispositivo.

Evidência: `test-evidence/audio-smoke.json`. O código de captura, cálculo RMS e reação está implementado, mas a verificação de nível não nulo com a entrada/saída real da igreja permanece pendente. Não foi alterado o volume global nem o roteamento de som do computador.

## Escopo e limites da versão

- O painel prepara letras sem substituir automaticamente o slide ao vivo. Projeção e painel têm janelas independentes; a saída pode ser posicionada na tela escolhida.
- O editor ajusta fonte, negrito, cores e posição. A galeria oferece 66 opções, granularidade, tempos, ordem, curva, intensidade, repetição e prévia com barra de tempo. O modo clássico mantém 12 entradas animadas e oito efeitos contínuos. Os parâmetros podem ser salvos em cenas. Não há edição de keyframes arbitrários ou importação de modelos 3D; os efeitos de fundo são procedurais com geometria em perspectiva.
- A sincronização usa amplitude/RMS, sem estimativa automática de BPM ou alinhamento de sílabas.
- A exportação gera as estrofes do louvor selecionado com duração fixa por estrofe e áudio local opcional. Não grava a sessão de operação ao vivo.
- GPU: Avalonia/Skia/ANGLE e decodificação LibVLC por hardware quando disponível, com fallback. Não foi medido um perfil de FPS contínuo/latência sob carga de culto com duas telas.
- O controle remoto foi validado por requisições HTTP autenticadas em loopback, incluindo a entrega da interface HTML. A inicialização pelo painel também foi observada. Não houve teste em navegador ou celular físico. A solicitação de firewall do Windows não foi aceita nem alterada pela automação; o processo de teste foi encerrado. O acesso de outros dispositivos depende da permissão de rede concedida pelo operador e do isolamento Wi-Fi.
- O vídeo citado no pedido inicial não foi recebido. Na atualização 1.2 foram recebidas quatro imagens estáticas da galeria; elas orientam os nomes e categorias, sem permitir comparar movimentos quadro a quadro.

## Empacotamento

As ferramentas nativas são distribuídas sem modificações, com licenças, configuração e referências de origem. O arquivo original FFmpeg LGPL foi conferido por SHA256 com o digest da release GitHub. Inno Setup foi obtido do site oficial e teve assinatura Authenticode válida, publicada por Pyrsys B.V.

### Validação da versão 1.2

O pacote de teste 1.2.0 foi compilado com o mesmo script e os mesmos binários publicados da distribuição, usando uma identidade de instalação e pasta isoladas para preservar a instalação existente. O pacote de distribuição mantém a identidade padrão para atualização.

| Verificação do pacote 1.2 | Resultado |
| --- | --- |
| Instalação e desinstalação silenciosas por usuário | Código 0 em ambas |
| Integridade instalada | Hashes de WorshipStudio.dll, CoreCLR, LibVLC e FFmpeg iguais aos da publicação |
| Runtime independente | Aplicativo iniciou com DOTNET_ROOT inválido e descoberta externa desativada; CoreCLR carregado da própria instalação |
| Inicialização | Janela Worship Studio — Operação e processo respondendo |
| Limpeza do teste | Aplicativo e registro QA removidos; processo de teste encerrado |
| Instalação existente | Registro da versão 1.1.0 em D:\Worship Studio permaneceu inalterado |

Evidências: `test-evidence/installer-verification-1.2.json`, `test-evidence/setup-install-1.2.log` e `test-evidence/setup-uninstall-1.2.log`. O processo instalado usado nesse teste foi encerrado por gerenciamento do processo. A verificação manual do editor usou outro diretório e terminou pelos controles da janela. A versão instalada do usuário não foi atualizada automaticamente; o novo instalador é entregue para atualização.

### Validação da versão 1.1

O instalador de teste foi compilado com o mesmo script, versão 1.1.0 e arquivos publicados da distribuição. Somente a identidade de instalação e a pasta de saída do pacote foram substituídas para isolar o teste e preservar uma instalação existente do usuário. O instalador de distribuição mantém a identidade padrão para atualizar a versão anterior.

| Verificação do pacote 1.1 | Resultado |
| --- | --- |
| Instalação silenciosa por usuário | Código de saída 0 |
| Arquivo instalado | Hash da assembly instalado igual ao da publicação final |
| Runtime independente | CoreCLR carregado da própria pasta de instalação, com descoberta externa do .NET desativada |
| Inicialização | Processo em execução e janela Worship Studio — Operação identificada |
| Dependências locais | LibVLC e FFmpeg presentes na instalação |
| Desinstalação isolada | Código de saída 0; aplicativo e registro de teste removidos |
| Preservação da instalação existente | Instalação do usuário permaneceu inalterada |

Evidências: `test-evidence/installer-verification-1.1.json`, `test-evidence/setup-install-1.1.log` e `test-evidence/setup-uninstall-1.1.log`. O teste de inicialização desta versão foi encerrado por gerenciamento do processo.

### Histórico da versão 1.0

A instalação silenciosa por usuário da versão 1.0 foi executada em uma pasta de teste. O executável instalado abriu a janela de operação, carregou o CoreCLR do próprio pacote com a descoberta externa do .NET desativada, persistiu a biblioteca e terminou normalmente. Evidências: `test-evidence/setup-install.log` e `test-evidence/installed-app-smoke.json`.

A desinstalação terminou com código 0. A pasta do aplicativo e seu registro de desinstalação foram removidos; a biblioteca de teste foi preservada. Evidências: `test-evidence/setup-uninstall.log` e `test-evidence/uninstall-result.json`. Não é necessária uma instalação prévia do .NET para usar o executável distribuído.

Os quatro arquivos de código-fonte de terceiros foram abertos como arquivos TAR válidos, e seus diretórios correspondem às versões ou commits identificados. O código LibVLCSharp foi obtido do espelho oficial VideoLAN no GitHub, no mesmo commit declarado pelos pacotes NuGet. Hashes dos arquivos entregues: `third-party-sources/SHA256SUMS.txt`.
