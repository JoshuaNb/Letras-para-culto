# Verificação — Worship Studio 1.0.0

Data: 22/11/2026, fuso America/Sao_Paulo. Este relatório distingue testes observados de capacidades implementadas que ainda dependem de validação com os equipamentos da igreja.

## Ambiente observado

- Windows x64, build 26300.
- SDK .NET 10.0.404.
- AMD Ryzen 5 5600G, Radeon RX 6600 e Radeon Graphics integrada.
- 31,4 GB de memória reportada pelo sistema.
- Uma tela física detectada no ambiente. A operação em duas janelas é validada neste monitor; a disposição final em dois monitores físicos continua dependente de conectar o segundo dispositivo.

Inventário legível por máquina: `test-evidence/environment.json`. A publicação é self-contained win-x64 e inclui o runtime; o usuário final não precisa do SDK.

## Testes automatizados

Comando executado sobre o projeto entregue:

```powershell
dotnet test tests\WorshipStudio.Tests.csproj -c Release --logger 'trx;LogFileName=verification.trx'
```

**Resultado observado: 27 aprovados, 0 falhas, 0 ignorados.** Registro: `test-evidence/verification.trx`.

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

## Verificação visual e operação

O aplicativo compilado foi aberto e operado na sessão Windows, com inspeção visual das janelas reais.

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
- O editor ajusta fonte, cores, posição, velocidade e transições e permite salvar cenas. Não há linha do tempo de keyframes ou importação de modelos 3D; os efeitos são procedurais com geometria em perspectiva.
- A sincronização usa amplitude/RMS, sem estimativa automática de BPM ou alinhamento de sílabas.
- A exportação gera as estrofes do louvor selecionado com duração fixa por estrofe e áudio local opcional. Não grava a sessão de operação ao vivo.
- GPU: Avalonia/Skia/ANGLE e decodificação LibVLC por hardware quando disponível, com fallback. Não foi medido um perfil de FPS contínuo/latência sob carga de culto com duas telas.
- O controle remoto foi validado por requisições HTTP autenticadas em loopback, incluindo a entrega da interface HTML. A inicialização pelo painel também foi observada. Não houve teste em navegador ou celular físico. A solicitação de firewall do Windows não foi aceita nem alterada pela automação; o processo de teste foi encerrado. O acesso de outros dispositivos depende da permissão de rede concedida pelo operador e do isolamento Wi-Fi.
- O vídeo de referência citado não estava disponível entre os arquivos recebidos. Não foi possível analisá-lo ou validar fidelidade visual.

## Empacotamento

As ferramentas nativas são distribuídas sem modificações, com licenças, configuração e referências de origem. O arquivo original FFmpeg LGPL foi conferido por SHA256 com o digest da release GitHub. Inno Setup foi obtido do site oficial e teve assinatura Authenticode válida, publicada por Pyrsys B.V.

A instalação silenciosa por usuário foi executada em uma pasta de teste. O executável instalado abriu a janela de operação, carregou o CoreCLR do próprio pacote com a descoberta externa do .NET desativada, persistiu a biblioteca e terminou normalmente. Evidências: `test-evidence/setup-install.log` e `test-evidence/installed-app-smoke.json`.

A desinstalação terminou com código 0. A pasta do aplicativo e seu registro de desinstalação foram removidos; a biblioteca de teste foi preservada. Evidências: `test-evidence/setup-uninstall.log` e `test-evidence/uninstall-result.json`. Não é necessária uma instalação prévia do .NET para usar o executável distribuído.

Os quatro arquivos de código-fonte de terceiros foram abertos como arquivos TAR válidos, e seus diretórios correspondem às versões ou commits identificados. O código LibVLCSharp foi obtido do espelho oficial VideoLAN no GitHub, no mesmo commit declarado pelos pacotes NuGet. Hashes dos arquivos entregues: `third-party-sources/SHA256SUMS.txt`.
