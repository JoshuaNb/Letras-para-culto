# Worship Studio 1.7.0

Aplicativo desktop em C#/Avalonia UI para preparar e projetar letras de louvores. Edição atual .NET 10 e edição legado .NET 6 para Windows 7 SP1, 8, 8.1, 10 e 11 x64. A operação, a biblioteca, a reprodução de mídia e a exportação funcionam sem internet. Não há conta, assinatura, anúncios ou licença paga para operar o aplicativo.

## Instalação e primeira projeção

1. Execute `WorshipStudio-Setup-1.7.0.exe` no Windows x64. O instalador escolhe a edição adequada e inclui .NET, LibVLC e FFmpeg. A instalação é por usuário e preserva a biblioteca. No Windows 7, instale o SP1 e a KB3063858 ou atualização equivalente antes de instalar o programa.
2. Para dois monitores, escolha **Estender estes vídeos** nas configurações de vídeo do Windows. Ligue o projetor antes de iniciar.
3. Abra Worship Studio. Na aba **Cena**, escolha o monitor de saída.
4. Clique **Abrir projeção**. A janela de operação permanece independente da projeção. A janela de saída pode funcionar no mesmo monitor quando não há segunda tela.
5. Selecione um louvor na biblioteca. Um clique prepara a prévia; duplo clique envia ao vivo. Selecione uma estrofe e use **Projetar estrofe**. Esse comando também abre a saída automaticamente quando estiver fechada.
6. Use **Próxima**, **Anterior**, **Ocultar letra** e **Tela preta** durante o culto.

O louvor inicial é um texto original curto para conferir a operação. Importe ou cadastre a biblioteca da igreja.

## Novidades da versão 1.7

O espaço de operação foi reorganizado a partir da referência DaVinci Resolve: superfícies em carvão, biblioteca à esquerda, retorno ao vivo maior, prévia independente e inspetor à direita. A faixa azul mostra as estrofes reais e a faixa âmbar representa o roteiro do culto. A seleção continua preparando a prévia; **Projetar estrofe** envia ao vivo.

Os menus **Arquivo**, **Biblioteca**, **Projeção**, **Ferramentas** e **Ajuda** abrem as operações do programa. Use Arquivo para importar/exportar e trabalhar com backups, Biblioteca para editar louvores, Projeção para operar a saída, Ferramentas para animações/conexões/recebidos e Ajuda para o guia e as atualizações. Os quatro temas continuam disponíveis no cabeçalho.

Ao abrir o aplicativo, uma janela carrega as letras da biblioteca local em segundo plano e inicia a consulta opcional de atualizações em paralelo. A preferência padrão habilita a consulta, respeitando a configuração já salva. Cada nova abertura relê a biblioteca e consulta novamente quando habilitado.

A espera da rede tem limite de seis segundos. **Abrir programa agora** pula/cancela essa espera e abre a operação assim que os dados locais estiverem prontos. Falta de internet não impede abrir. Download e instalação continuam dependendo de uma escolha no aplicativo. Se a biblioteca estiver corrompida, o arquivo original é preservado e a gravação fica bloqueada até a recuperação.

O APK 1.7 acompanha o instalador com a mesma implementação de controle e transferência da 1.6. Não é necessário substituir o APK 1.6 para continuar usando o PC 1.7.

## Recursos da versão 1.6

No celular conectado, escolha **Enviar arquivos** no APK ou no navegador. Selecione até dez arquivos locais, cada um com até 512 MB. Acompanhe o progresso ou cancele; os comandos de projeção continuam disponíveis.

No PC, abra **Conexões → Arquivos recebidos do celular**. A lista permite salvar uma cópia de qualquer arquivo, usar imagens/vídeos como fundo e importar letras TXT/JSON/OpenLyrics. O conteúdo só é aplicado quando você escolher. Arquivos concluídos permanecem no PC; transferências incompletas são descartadas. [Guia da transferência](https://github.com/JoshuaNb/Letras-para-culto/blob/main/docs/TRANSFERIR-ARQUIVOS.md).

## Recursos da versão 1.5

- **Windows antigos:** edição legado incluída para Windows 7 SP1, 8, 8.1 e versões anteriores do Windows 10. Letras, animações, Holyrics, controle remoto e MP4 continuam disponíveis. A edição legado renderiza por software; Windows 10 22H2/11 mantém GPU com fallback.
- **Instalador universal:** seleciona o runtime por versão do Windows e preserva biblioteca e preferências. Não precisa baixar o runtime durante a instalação.
- **Validação:** os dois runtimes são testados no computador de desenvolvimento. A execução em máquinas Windows 7/8/10 permanece pendente; não foi comprovada nesta sessão. [Requisitos e limites](https://github.com/JoshuaNb/Letras-para-culto/blob/main/docs/COMPATIBILIDADE-WINDOWS.md).

## Recursos da versão anterior

- **Android:** em Conexões, inicie o controle remoto. No APK Letras para culto — Controle, informe o endereço do PC e o código de seis dígitos mostrado na tela. O código dura dez minutos; desligar o controle revoga o acesso. Os aparelhos devem estar na mesma rede local.
- **APK incluído:** Baixar APK para Android permite salvar uma cópia do aplicativo para transferir ao celular, sem internet. O APK também está disponível na Release do GitHub.
- **Holyrics:** em Conexões, clique Baixar letras do Holyrics, conecte ao API Server, pesquise o catálogo e marque as músicas desejadas. O download traz letras e estrofes para a biblioteca local; não baixa arquivos MP3.
- **Temas e atualização:** escolha Branco, Escuro, Cinza ou Amarelo pastel no cabeçalho. O botão Atualizações consulta novas versões estáveis do GitHub; a busca automática pode ser desativada em App. A operação permanece offline.

Guia completo e downloads: [Letras para culto no GitHub](https://github.com/JoshuaNb/Letras-para-culto).

## Biblioteca e roteiro

**+ Novo** e **Editar** abrem o editor de letras. Informe título, autoria e letra. Separe slides com uma linha em branco; títulos entre colchetes, como `[Verso 1]` e `[Refrão]`, identificam as partes.

```text
[Verso 1]
Primeira linha
Segunda linha

[Refrão]
Terceira linha
Quarta linha
```

O campo de busca procura título, autoria e letra. **Adicionar ao roteiro** monta a ordem do culto; escolha **Roteiro do culto** para reordenar ou remover entradas. A seleção na biblioteca prepara a prévia e não muda por si só o slide ao vivo.

**Importar** aceita letras TXT UTF-8, músicas JSON e OpenLyrics XML. JSON aceita uma música, lista, `songs` ou resposta Holyrics `data`, com `title`, `artist` e `lyrics` ou `slides`. OpenLyrics respeita `verseOrder`. `.hcb` e `.hcc` são formatos proprietários e não são lidos.

## Cenas, animações e mídia

A aba **Cena** contém os controles reais do renderizador: fundo procedural, imagem/vídeo local, velocidade, escurecimento, cores, fonte, negrito, tamanho, posição e animações das letras. As alterações atualizam a prévia e a projeção em tempo real. **Reiniciar vídeo do fundo** reinicia a mídia nas superfícies de prévia e projeção. **Salvar cena** e **Carregar** permitem reutilizar os parâmetros.

Os efeitos procedurais usam geometria em perspectiva e animação contínua. O editor permite ajustar as entradas das letras por unidade e percorrer sua prévia no tempo. Não possui edição livre de keyframes nem importação de modelos 3D. Imagens e vídeos escolhidos são copiados para a pasta de mídia da biblioteca. A reprodução de fundo usa LibVLC local, em loop e sem som, para preservar o áudio do culto.

O Avalonia usa Skia/ANGLE no Windows com aceleração gráfica e fallback de software. O LibVLC solicita decodificação por hardware quando disponível. O uso efetivo da GPU depende do driver e do formato da mídia. A versão não inclui um benchmark certificado no Ryzen 5 5600G/RX 6600.

## Cores, fontes e negrito

Em **Cena → Tipografia da letra**, escolha uma fonte na lista de fontes instaladas no computador. Cada nome é mostrado na própria fonte; a amostra **Adoração · Aa Bb Cc** permite conferir a seleção. A versão 1.1 corrige o cache que impedia a troca da fonte de aparecer quando a estrofe permanecia a mesma. A escolha também vale para a janela de projeção e para o MP4.

Marque **Negrito** para usar o peso 700; desmarque para usar o peso normal 400. O tamanho da fonte continua expresso em pixels de referência para saída 1080p.

Clique na amostra de **Cor da letra** para abrir o seletor visual. Arraste no quadro de saturação e luminosidade, ajuste o matiz, use os controles Vermelho/Verde/Azul ou escolha uma cor da paleta. Também é possível digitar um código HEX, como `#FFD166`. As mudanças aparecem ao vivo enquanto o seletor está aberto. **Aplicar cor** mantém a escolha; **Cancelar**, Esc ou fechar a janela restaura a cor anterior. Códigos HEX inválidos não são aplicados. **Cor do efeito** e **Cor do fundo** usam o mesmo seletor.

## Animações das letras

Em **Cena → Animações das letras**, clique **Abrir galeria · animações granulares**. A galeria contém **66 opções, incluindo Nenhuma**, com os nomes e as categorias das quatro imagens de referência. Use a busca pelo nome ou o filtro de categoria para encontrar a predefinição. Passe o mouse sobre um cartão para ver sua animação; clique para editar o efeito com o texto da estrofe na prévia maior.

As predefinições incluem Fade Suave, Pluma Caindo, Fluxo de Seda, Cascata, Blur Reveal, os três Typewriter Blur, Focus Pull, Luz Divina, Gold Reveal, Névoa, Ressurreição, RGB Split, Jelly Pop e Spring Bounce. Cada opção carrega um perfil de movimento e parâmetros iniciais próprios. Fonte, negrito, cores e fundo da cena permanecem escolhidos pelo operador.

### Granularidade e sequência

**Granularidade** define o que recebe uma animação independente:

| Opção | Unidade animada |
| --- | --- |
| Por letra | Cada grafema Unicode: acentos combinados e sequências de emoji permanecem unidos |
| Por palavra | Cada palavra da estrofe |
| Por linha | Cada linha do texto na projeção |
| Bloco inteiro | A estrofe completa como uma única unidade |

O desenho completo do texto define a posição final de cada unidade. Revelar uma letra ou palavra não recentraliza o restante da frase. A quantidade de unidades e o tempo de entrada completa aparecem junto aos controles.

| Controle | Efeito |
| --- | --- |
| Duração por unidade | Tempo para cada letra, palavra, linha ou bloco completar o movimento; de 0 a 5 segundos |
| Intervalo entre unidades | Espaço de tempo entre o início de uma unidade e o da seguinte; de 0 a 0,5 segundo |
| Atraso inicial | Espera antes de iniciar a entrada; de 0 a 3 segundos |
| Ordem de aparição | Do início ao fim, do fim ao início, do centro para fora ou aleatória repetível |
| Curva do movimento | Linear, desacelerar, suavizar início e fim, ultrapassar e voltar, elástica ou quique |
| Intensidade do efeito | Ajusta a força do movimento, escala, desfoque, brilho e deslocamentos do perfil; de 0 a 200% |
| Repetir na projeção | Repete o ciclo na saída ao vivo e na exportação MP4 |
| Pausa na repetição | Mantém o texto completo por um período antes de repetir a entrada; de 0 a 8 segundos |

As unidades podem se sobrepor no tempo. Por exemplo, com oito palavras, duração de 0,70 segundo, intervalo de 0,06 segundo e atraso inicial de 0,20 segundo, a entrada completa termina em **1,32 segundo**: `0,20 + 0,70 + 7 × 0,06`. O intervalo é entre os inícios; não é uma espera adicional depois que cada palavra termina. A ordem aleatória é determinística, para que a sequência seja reproduzida da mesma forma na prévia, na projeção e no MP4.

### Prévia e aplicação à cena

A prévia maior usa um fundo neutro para destacar as letras. **Pausar/Reproduzir** controla sua reprodução e **Recomeçar** retorna ao início. Arraste a barra de tempo para inspecionar um instante específico; essa ação pausa a prévia. **Texto para testar** altera somente o texto de teste do editor, sem editar o louvor salvo.

A prévia repete o efeito para facilitar os ajustes. Essa repetição de teste é independente da opção **Repetir na projeção**. Selecionar cartões, alterar parâmetros ou percorrer a barra de tempo não muda a cena projetada. **Aplicar à cena** confirma a predefinição e seus ajustes; **Cancelar** ou fechar o editor descarta as alterações. A opção **Nenhuma** exibe o texto completo sem entrada animada.

Ao aplicar uma predefinição, **Usar animação granular** fica marcado. Desmarque essa opção para voltar aos controles clássicos descritos abaixo. Bibliotecas e cenas anteriores continuam no modo clássico até que o operador aplique ou ative uma animação granular. Os novos ajustes são preservados na biblioteca, nas cenas JSON e no backup `.wstudio`.

As referências recebidas são **imagens estáticas de uma galeria**. Os nomes, as categorias e a organização orientaram as predefinições; os movimentos foram interpretados a partir dessas informações. Não foi recebido um vídeo desses efeitos, portanto esta versão não afirma reproduzir uma animação de referência quadro a quadro.

### Controles clássicos de entrada e movimento

Combine uma **Entrada da letra**, executada quando o texto muda, com uma **Animação contínua**, mantida enquanto a estrofe permanece visível. A animação das letras tem velocidade própria, independente do movimento do fundo, e também aparece no vídeo exportado.

| Entrada da letra | Comportamento |
| --- | --- |
| Dissolver | A letra aparece gradualmente |
| Subir / Descer | Entrada com deslocamento vertical |
| Entrar pela esquerda / direita | Entrada com deslocamento horizontal |
| Aproximar | Entrada por escala |
| Salto com aproximação | Escala com ressalto |
| Girar e aparecer | Rotação curta com aparecimento |
| Revelar da esquerda | Revelação progressiva pela área de recorte |
| Máquina de escrever | Revelação de caracteres com suporte a acentos e Unicode |
| Palavra por palavra | Revelação de palavras |
| Linha por linha | Revelação de linhas |
| Corte direto | Exibição imediata, sem animação de entrada |

São 12 entradas animadas e a opção Corte direto. Ajuste a **Duração da transição** entre 0 e 3 segundos; zero exibe a letra completa imediatamente. Escolher outra entrada já a reproduz na prévia. **Testar entrada na prévia** permite repetir o teste sem mudar a saída ao vivo.

Os oito efeitos contínuos são **Flutuar**, **Onda nas linhas**, **Pulsar**, **Respiração**, **Balançar**, **Saltar**, **Brilho pulsante** e **Vibração suave**. Escolha **Sem animação contínua** para deixar o texto parado. **Velocidade da letra** varia de 0,2× a 3×; **Intensidade da animação** varia de 0 a 100%. A intensidade zero mantém a letra estável.

Fonte, negrito, cores e parâmetros de animação são preservados na biblioteca, nas cenas JSON e no backup `.wstudio`. Cenas antigas recebem negrito ligado e nenhuma animação contínua, preservando a leitura sem movimento adicional.

## Reação ao áudio

Ative **Sincronizar efeitos** e escolha **Som do computador (loopback)** ou **Entrada padrão / microfone**. O medidor mostra o nível capturado; ajuste **Sensibilidade**. O nível RMS altera a intensidade dos efeitos em tempo real. **Pulso manual** permite disparar uma reação sem captura.

É uma reação à amplitude do áudio; esta versão não estima BPM, não detecta compassos e não faz alinhamento automático de letra com fonemas. Para entrada da mesa de som, configure o dispositivo de gravação padrão do Windows antes de ativar a captura.

## Controle pelo celular

Na aba **Conexões**, clique **Iniciar controle remoto**, copie o endereço mostrado e abra no navegador do celular conectado à mesma rede local. Não é necessário instalar aplicativo no celular nem ter internet. A página contém navegação de slides, ocultar letra, tela preta e pulso manual.

O endereço contém uma chave aleatória. Quem tiver o endereço na rede pode operar a projeção. Ao desligar e religar o controle, a chave anterior é revogada. Se o Windows pedir autorização de rede, permita para **redes privadas**. Redes Wi-Fi com isolamento entre clientes impedem o acesso entre celular e computador; conecte ambos a uma rede que permita a comunicação local. O servidor é desligado ao fechar o programa.

## Holyrics

A integração usa somente a API oficial local do Holyrics e não precisa de internet. No Holyrics, habilite o **API Server**, crie um token com permissões de leitura para `GetSongs`, `GetLyrics` e `GetCurrentPresentation`, e informe endereço e token em **Conexões**.

- **Importar biblioteca via API** copia título, autoria e slides para a biblioteca local.
- **Seguir apresentação** consulta a apresentação atual e envia sua letra à janela Worship Studio. Também acompanha as indicações de tela preta e letra oculta retornadas pela API.

O token não é salvo no arquivo de biblioteca. A integração é de leitura; o Worship Studio não envia comandos ao Holyrics. Referência: [API Server oficial do Holyrics](https://github.com/holyrics/API-Server). A validação automatizada usa servidor local de teste compatível com os contratos documentados; valide com a versão do Holyrics usada pela sua igreja antes do culto.

## MP4

**Exportar MP4** gera um vídeo a partir das estrofes e dos parâmetros de cena, incluindo as animações granulares, sua ordem e sua repetição quando ativada. A renderização usa quadros reais do mesmo motor de letras e efeitos; fundos em vídeo são compostos pelo FFmpeg. A exportação é offline. A duração por slide é fixa conforme a configuração da exportação; não é uma gravação da sessão ao vivo. Confira no editor o tempo de entrada completa e escolha um tempo por estrofe que permita mostrar o resultado desejado.

O codificador padrão é MPEG-4 Part 2 em contêiner MP4, com pixel format YUV420P. Uma opção de aceleração AMD AMF pode ser usada quando disponível; o programa testa o codificador antes de escolhê-lo. A trilha de áudio, quando selecionada, é codificada em AAC. Não há dependência de codecs pagos. O cancelamento preserva um arquivo de destino preexistente e remove a saída parcial.

## Dados e cópias de segurança

Os dados ficam em `%LOCALAPPDATA%\WorshipStudio`: `library.json`, revisões `.bak`, pasta `media` e `studio.log`. O pacote e o instalador não precisam de rede para abrir ou usar essa biblioteca.

**Backup** cria um arquivo `.wstudio` com biblioteca, roteiro, cena e a mídia atualmente referenciada pela cena. **Restaurar** substitui a biblioteca depois da validação do arquivo e conserva a revisão anterior. O backup desta versão inclui a mídia da cena ativa, não arquivos avulsos que não estejam referenciados. Faça cópia antes de excluir louvores ou restaurar uma biblioteca. A desinstalação preserva os dados locais.

## Atalhos

| Tecla | Ação |
| --- | --- |
| Seta direita / Espaço | Próximo slide ao vivo |
| Seta esquerda | Slide anterior |
| B | Alternar tela preta |
| L | Ocultar / exibir letra |
| F11 | Alternar tela cheia da projeção |
| Esc na projeção | Sair da tela cheia |

Os atalhos de operação não devem interromper a digitação nos campos de edição.

## Compilar e verificar

Requer SDK .NET 10 x64 para desenvolvimento e Inno Setup 6 para reconstruir o instalador. O executável distribuído é self-contained e dispensa SDK/runtime instalado.

```powershell
.\build.ps1
# Ou com ferramentas em caminhos personalizados:
.\build.ps1 -DotnetPath 'C:\sdk\dotnet.exe' -InnoCompiler 'C:\Inno Setup 6\ISCC.exe'
# Sem reconstruir instalador:
.\build.ps1 -SkipInstaller
```

A primeira restauração de pacotes de desenvolvimento usa NuGet. Isso não é necessário na operação do aplicativo pronto. `build.ps1` executa os testes nos dois runtimes, publica em `app\win-x64` e `app\win-legacy-x64` e compila o instalador universal. Os testes incluem biblioteca, importação, contrato Holyrics, autenticação remota, geração/decodificação de MP4 real e comparação dos pixels de fontes, pesos, cores e animações.

Para executar apenas as verificações visuais de tipografia:

```powershell
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -f net10.0 -- --output test-evidence/typography-1.7.0-net10.0
# Para a edição legado, repita com -f net6.0 e outra pasta de saída.
```

Consulte `VERIFICACAO.md` para resultados observados e limites da validação. A galeria desta atualização usa as quatro capturas estáticas fornecidas; os movimentos são interpretações dos nomes e categorias, sem comparação com um vídeo de referência.

## Licenças

Worship Studio: MIT. As dependências têm licenças próprias; consulte `THIRD-PARTY-NOTICES.txt`, a pasta `licenses` e `third-party-sources`. Fontes do aplicativo e arquivos de construção acompanham a entrega.
