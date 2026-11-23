# Histórico de mudanças

O projeto é publicado como **Letras para culto**. O aplicativo e os arquivos de PC mantêm o nome **Worship Studio**, preservando a atualização das instalações existentes.

## 1.8.0 — Fundos no compositor e novos cenários

- Reprodução dos vídeos locais substituída por decodificação em CPU no FFmpeg empacotado, em processo isolado. Quadros BGRA e letras são compostos na mesma superfície, sem VideoView/overlay nativo do LibVLC e sem decodificação de vídeo por hardware.
- Aurora e letras permanecem visíveis até o primeiro quadro completo. Prazos para abertura e entrega de quadros evitam espera indefinida; uma falha no modo automático dispara uma tentativa em modo econômico, seguida de diagnóstico e fundo de segurança se a recuperação falhar.
- Vídeos continuam locais, em loop e sem som. Modo automático usa 1280×720 a 30 quadros/s; **Vídeos em modo econômico** usa 854×480 a 24 quadros/s, aplica imediatamente e é o padrão da edição legado. As letras mantêm a resolução da projeção.
- Nova preferência **Renderização gráfica por software**, com reinício confirmado para aplicar, e argumento de recuperação `--software-rendering`. A edição atual conserva GPU para interface/letras quando disponível; a edição legado já usa software.
- Oito fundos procedurais novos: Ondas de graça, Raios de luz, Galáxia em movimento, Bokeh suave, Horizonte 3D, Oceano sereno, Fitas de adoração e Prismas de luz. O catálogo reúne 14 escolhas: 11 procedurais, cor sólida, imagem e vídeo.
- Catálogo normaliza identificadores e nomes antigos em português; modo desconhecido usa Aurora. Imagem ausente ou corrompida exibe Aurora e letras com aviso. Mídia de uma biblioteca copiada é localizada na pasta `media` atual pelo nome do arquivo.
- APK 1.8 assinado com a mesma chave, com fontes Java e DEX idênticos à 1.7. Mantidos abertura assistida, menus, quatro temas, controle remoto, Holyrics e transferência de arquivos.

[Notas 1.8](docs/releases/v1.8.0.md) · [Guia de operação](README.pt-BR.md) · [Resultados e limites](VERIFICACAO.md). A falha no outro computador não foi reproduzida nessa GPU; os testes locais não comprovam correção em qualquer equipamento.

## 1.7.0 — Espaço de operação e abertura assistida

- Reorganizada a interface desktop a partir da referência DaVinci Resolve: superfícies em carvão, painéis com divisórias, retorno ao vivo em destaque e prévia independente para preparar a próxima estrofe.
- Adicionados menus **Arquivo**, **Biblioteca**, **Projeção**, **Ferramentas** e **Ajuda**, ligados às operações de biblioteca, backup/restauração, exportação MP4, navegação, tela preta, animações, conexões, recebidos e atualização.
- Estrofes apresentadas em uma faixa de cartões azuis e roteiro em uma faixa âmbar, vinculados aos louvores reais, à seleção da prévia e ao estado da saída ao vivo.
- Nova janela de abertura carrega as letras da biblioteca local em segundo plano e inicia a consulta opcional de atualizações em paralelo. A preferência padrão habilita a consulta; uma preferência existente é respeitada.
- A espera da consulta de rede é limitada a seis segundos. **Abrir programa agora** pula/cancela essa espera e mantém a leitura local necessária. Falta de internet não impede abrir o programa; download e instalação continuam exigindo ação do operador.
- A biblioteca é relida e uma nova consulta é iniciada a cada abertura real do aplicativo, quando habilitada. Bibliotecas corrompidas permanecem preservadas, com gravação bloqueada até a recuperação, como nas versões anteriores.
- APK 1.7 recompilado e assinado com a mesma chave. Os fontes Java e o DEX são idênticos à 1.6; somente a versão do pacote foi atualizada para acompanhar o instalador.
- Mantidos os quatro temas, os dois runtimes Windows x64, Holyrics, transferência de arquivos e os controles de projeção existentes.

[Notas 1.7](docs/releases/v1.7.0.md) · [Guia de operação](README.pt-BR.md) · [Resultados e limites](VERIFICACAO.md).

## 1.6.0 — Enviar arquivos do celular ao PC

- Transferência local no APK e no navegador, com até dez arquivos por lote, 512 MB por arquivo, progresso e cancelamento, independente dos comandos da projeção.
- Janela **Arquivos recebidos do celular** para salvar cópias, aplicar imagens/vídeos como fundo ou importar letras TXT/JSON/OpenLyrics por ação explícita.
- Recebimento autenticado em streaming com SHA256, nomes Windows validados, proteção contra sobrescrita e publicação atômica; interrupções descartam arquivos incompletos.
- Caixa de entrada limitada a 5 GB e 5.000 arquivos; um recebimento por vez, sem bloquear a navegação da apresentação.
- APK usa o seletor local do Android sem permissão geral de armazenamento. Pausar, desconectar ou cancelar interrompe o lote, sem reenvio automático.
- Consulta dos recebidos e operações de mídia fora da thread da interface; versão do APK exibida a partir do pacote real.
- Mantidas as edições atual/legado, quatro temas, animações, Holyrics e atualização pelo aplicativo.

[Notas 1.6](docs/releases/v1.6.0.md) · [Como transferir](docs/TRANSFERIR-ARQUIVOS.md). O APK e o PC precisam de 1.6 para o novo envio; controles anteriores continuam compatíveis com a projeção.

## 1.5.0 — Compatibilidade com Windows antigos

- Instalador universal x64 para Windows 7 SP1, 8, 8.1, 10 e 11, com seleção automática de .NET 10 ou .NET 6.0.36 e runtimes incluídos para instalação offline.
- Edição legado com o mesmo painel, projeção, animações, Holyrics e controle remoto, usando Skia por software e sem carregar o ANGLE moderno.
- FFmpeg legado recompilado para Windows 7/MSVCRT, mantendo MP4, áudio AAC e composição de vídeo. Edição moderna conserva seu codificador e GPU.
- Manifesto Windows e informações de DPI ampliados; instalador verifica a API da KB3063858 no Windows 7 antes de alterar a instalação.
- Serviços de rede, hashes e exportação compatíveis com .NET 6; atualizador legado usa TLS 1.2. Remoção controlada de binários exclusivos do outro runtime ao atualizar.
- Testes nos dois runtimes, regressões de leitura HTTP fragmentada e ensaios isolados dos dois caminhos do instalador. Windows de 32 bits e Windows 7 sem SP1 não estão incluídos.

**Limite:** os testes de execução desta sessão usam Windows 11. A execução em Windows 7/8/10 ainda precisa ser validada nesses sistemas; a edição legado não restaura suporte oficial de seus fornecedores. [Compatibilidade e requisitos](docs/COMPATIBILIDADE-WINDOWS.md) · [Notas 1.5](docs/releases/v1.5.0.md).

## 1.4.0 — Controle Android e biblioteca do Holyrics

- Adicionado **Letras para culto · Controle**, APK nativo em português brasileiro para Android 8.0 ou superior, sem conta, nuvem ou dependência de serviços Google.
- Adicionado pareamento local com IPv4/porta do PC e código de seis dígitos válido por dez minutos. O servidor limita tentativas por endereço e globalmente; desligar o controle revoga código e token.
- Preservada a conexão por link completo com token e o controle pelo navegador das versões anteriores.
- Adicionados ao APK retorno da letra atual, navegação anterior/próxima, seleção direta de estrofe, ocultar letra, tela preta e pulso de efeito. A consulta pausa em segundo plano; código e token ficam somente em memória.
- Adicionada janela **Baixar letras do Holyrics**, com catálogo da biblioteca instalada, busca por título/artista, seleção individual ou dos itens visíveis, progresso e cancelamento.
- A importação usa a API local autorizada do Holyrics (`GetSongs`/`GetLyrics`) e adiciona apenas o lote concluído à biblioteca. As letras baixadas ficam disponíveis offline; áudio e MP3 não fazem parte dessa importação.
- Mantidos os quatro temas, o espaço de operação, as animações e o atualizador da versão 1.3.
- Incluídos fonte e scripts Android no ZIP completo. A distribuição 1.4 acrescenta o APK aos anexos da Release e seu hash ao manifesto SHA-256; a chave de assinatura permanece privada.
- Incluído o APK no instalador de PC, com **Baixar APK para Android…** para salvar uma cópia offline e transferi-la ao celular.
- Ajustado o código Android para descartar comandos ainda enfileirados quando o aplicativo entra em segundo plano.
- Validação: **320 verificações aprovadas, zero falhas** — 130 de núcleo (93 anteriores, 18 de pareamento e 19 de Holyrics), 108 visuais, 26 da interface principal, sete da janela Holyrics, 32 Java e 17 da integração HTTP real do cliente Java do APK com o servidor e a operação de produção em Avalonia Headless.
- APK compilado, assinado e alinhado. A integração HTTP valida o cliente e a operação do PC; não executa a Activity ou toques Android. O emulador sem hypervisor ficou indisponível e não houve operação em celular físico; testes Holyrics usam respostas controladas, sem instalação real.

**Migração:** o pareamento por código requer PC 1.4.0. Versões até a 1.2 precisam da instalação manual inicial da 1.4; a 1.3 já pode buscá-la pelo atualizador. Instale o APK no Android e conecte os dispositivos à mesma rede local.

[Notas da versão 1.4.0](docs/releases/v1.4.0.md) · [Controle Android](docs/CONTROLE-ANDROID.md).

## 1.3.0 — Novo espaço de operação, temas e atualizações

- Reorganizado o espaço de operação com inspiração em estações de edição como o DaVinci: biblioteca à esquerda, prévia e retorno ao vivo, estrofes e inspetor de cena/conexões.
- Adicionados retorno visual do estado enviado à projeção e divisórias para redimensionar os painéis, mantendo a preparação independente da saída ao vivo.
- Adicionados os temas **Branco**, **Escuro**, **Cinza** e **Amarelo pastel**, com aplicação imediata, diálogos adaptados e preferência persistente separada da biblioteca.
- Adicionado atualizador pelo GitHub: consulta da última Release estável ao abrir e a cada seis horas, opção de desligar a busca automática e verificação manual.
- Adicionados notas da versão no aplicativo, download com progresso/cancelamento e verificação do instalador por tamanho, manifesto SHA-256 e digest do GitHub quando disponível.
- A instalação requer um comando explícito e confirmação do operador; o aplicativo salva a biblioteca, abre o instalador e encerra a operação.
- Preservada a operação offline quando a rede ou o serviço de atualizações estiverem indisponíveis.
- Documentado o protocolo de publicação das próximas versões, com tag, instalador, código-fonte completo e manifesto de hashes na mesma Release pública.
- Adicionado `package-release.ps1`, que executa os testes e gera instalador, ZIP completo e manifesto SHA-256; a versão do projeto C# orienta os nomes dos três arquivos e a versão do instalador.
- Validação automatizada: **225 aprovações, zero falhas** — 93 de núcleo (44 anteriores, 36 do atualizador e 13 de preferências/contraste), 108 visuais e 24 de interface com o aplicativo real em Avalonia Headless/Skia.
- Validado também o download HTTP real do instalador 1.2.0 publicado no GitHub, com tamanho de 151.413.199 bytes e SHA-256 correto, sem executar o arquivo. Esta integração é separada do contador de 225.

**Migração:** as versões até a 1.2 não possuem atualizador. Instale a 1.3 uma vez pela Release; as próximas atualizações podem ser obtidas no aplicativo.

[Notas da versão 1.3.0](docs/releases/v1.3.0.md) · [Publicação no GitHub](docs/PUBLICACAO.md).

## 1.2.0 — Granularidade e galeria de animações

- Adicionada galeria com **66 opções: 65 animadas e Nenhuma**, busca e filtro por categoria.
- Adicionada granularidade por grafema Unicode, palavra, linha visual após a quebra automática e bloco de texto.
- Adicionados duração por unidade, intervalo, atraso inicial, ordem direta/inversa/centro/aleatória repetível, seis curvas, intensidade e repetição com pausa.
- Adicionados prévia com reprodução/pausa, reinício, barra de tempo e edição antes de **Aplicar à cena**; **Cancelar** preserva a cena anterior.
- Implementados desfoque real pelo Skia, máscaras, brilho, separação RGB e transformações independentes por unidade.
- Compartilhado o motor entre prévia, projeção e MP4, com reinício da entrada em cada estrofe.
- Preservados os controles clássicos da versão 1.1 e a leitura de bibliotecas/cenas antigas.
- Validação: **159 verificações aprovadas** — 44 de núcleo, 29 visuais anteriores, 79 visuais granulares e sete de integração MP4 — além de operação das janelas e teste do instalador.

Os movimentos são interpretações das referências estáticas recebidas. [Notas da versão 1.2.0](docs/releases/v1.2.0.md).

## 1.1.0 — Fontes, cores, negrito e mais movimento

- Corrigida a troca de fonte que não aparecia ao manter a mesma estrofe, por reutilização indevida do cache de texto.
- Adicionadas lista de fontes instaladas, amostra visual e alternância de negrito entre pesos normal e 700.
- Substituídos os campos simples de cor por seletor visual para letra, fundo e efeito, com paleta, matiz, saturação/luminosidade, RGB e HEX.
- Adicionados **Aplicar cor**, **Cancelar** e restauração da cor anterior ao cancelar ou fechar o seletor.
- Ampliadas as entradas para **12 animadas e Corte direto**.
- Adicionados oito movimentos contínuos, velocidade independente do fundo e controle de intensidade.
- Preservados fonte, peso, cores e animações na biblioteca, cenas e backup; cenas antigas continuam legíveis.
- Validação: **30 testes de operação e 29 verificações visuais aprovados**, além dos fluxos manuais e teste isolado do instalador.

[Notas da versão 1.1.0](docs/releases/v1.1.0.md).

## 1.0.0 — Primeira versão funcional

- Adicionados biblioteca local, editor de letras, busca, roteiro do culto, importação TXT/JSON/OpenLyrics e backup `.wstudio`.
- Implementados painel de operação e janela de projeção independentes, escolha de monitor, preparação sem substituir o texto ao vivo, navegação, letra oculta e tela preta.
- Adicionados fundos de imagem/vídeo local, Aurora, túnel 3D e partículas 3D, controles de aparência e cenas reutilizáveis.
- Adicionadas entradas Dissolver, Deslizar, Aproximar e Máquina de escrever, além de Corte direto.
- Implementados captura WASAPI, reação por amplitude/RMS, sensibilidade e pulso manual.
- Implementados controle remoto autenticado na rede local e integração de leitura com a API oficial do Holyrics.
- Implementada exportação MP4 offline com mídia, trilha opcional e cancelamento que preserva o arquivo anterior.
- Preparado instalador Windows 11 x64 com .NET, LibVLC e FFmpeg incluídos.
- Validação: **27 testes de operação aprovados**, inspeção das janelas, reprodução local de vídeo e instalação/abertura/desinstalação.

[Notas da versão 1.0.0](docs/releases/v1.0.0.md).
