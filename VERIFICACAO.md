# Verificação — Worship Studio 1.1.0

Data: 22/11/2026, fuso America/Sao_Paulo. Este relatório distingue testes observados de capacidades implementadas que ainda dependem de validação com os equipamentos da igreja.

## Ambiente observado

- Windows x64, build 26300.
- SDK .NET 10.0.404.
- AMD Ryzen 5 5600G, Radeon RX 6600 e Radeon Graphics integrada.
- 31,4 GB de memória reportada pelo sistema.
- Uma tela física detectada no ambiente. A operação em duas janelas é validada neste monitor; a disposição final em dois monitores físicos continua dependente de conectar o segundo dispositivo.

Inventário legível por máquina: `test-evidence/environment.json`. A publicação é self-contained win-x64 e inclui o runtime; o usuário final não precisa do SDK.

## Tipografia e animações — atualização 1.1

A versão 1.1 foi compilada em Release sem erros nem avisos. A correção de fonte usa uma chave de cache que distingue explicitamente família, peso, estilo e largura da fonte, em vez da representação genérica do objeto `Typeface`. A regressão visual troca a fonte na mesma estrofe e no mesmo renderizador, de forma que reutilizar indevidamente o texto da fonte anterior seja detectado.

**Resultado atual: 30 testes de operação aprovados, 0 falhas, 0 ignorados, e 29 verificações visuais aprovadas, 0 falhas.** O registro dos testes é `test-evidence/verification-1.1.trx`; o resumo visual e os quadros PNG estão em `test-evidence/typography-1.1/`.

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

Na versão 1.0, o resultado foi **27 aprovados, 0 falhas, 0 ignorados**, com registro em `test-evidence/verification.trx`. A suíte atual passou novamente com **30 aprovados**, incluindo as três novas regressões de persistência de tipografia; o registro atual é `test-evidence/verification-1.1.trx`.

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
- O editor ajusta fonte, negrito, cores, posição, 12 entradas animadas e oito efeitos contínuos, além das opções sem animação. Os parâmetros podem ser salvos em cenas. Não há linha do tempo de keyframes ou importação de modelos 3D; os efeitos de fundo são procedurais com geometria em perspectiva.
- A sincronização usa amplitude/RMS, sem estimativa automática de BPM ou alinhamento de sílabas.
- A exportação gera as estrofes do louvor selecionado com duração fixa por estrofe e áudio local opcional. Não grava a sessão de operação ao vivo.
- GPU: Avalonia/Skia/ANGLE e decodificação LibVLC por hardware quando disponível, com fallback. Não foi medido um perfil de FPS contínuo/latência sob carga de culto com duas telas.
- O controle remoto foi validado por requisições HTTP autenticadas em loopback, incluindo a entrega da interface HTML. A inicialização pelo painel também foi observada. Não houve teste em navegador ou celular físico. A solicitação de firewall do Windows não foi aceita nem alterada pela automação; o processo de teste foi encerrado. O acesso de outros dispositivos depende da permissão de rede concedida pelo operador e do isolamento Wi-Fi.
- O vídeo de referência citado não estava disponível entre os arquivos recebidos. Não foi possível analisá-lo ou validar fidelidade visual.

## Empacotamento

As ferramentas nativas são distribuídas sem modificações, com licenças, configuração e referências de origem. O arquivo original FFmpeg LGPL foi conferido por SHA256 com o digest da release GitHub. Inno Setup foi obtido do site oficial e teve assinatura Authenticode válida, publicada por Pyrsys B.V.

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
