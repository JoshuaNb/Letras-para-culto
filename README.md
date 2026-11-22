# Letras para culto

Aplicativo desktop gratuito e offline para preparar e projetar letras de louvores em igrejas, desenvolvido em **C#/.NET 10 e Avalonia UI** para **Windows 11 x64**.

Este é o nome do projeto no GitHub. O aplicativo, o executável e os instaladores conservam o nome **Worship Studio**, mantendo a atualização das instalações existentes.

## Versões disponíveis

| Versão | O que oferece | Novidades em relação à anterior |
| --- | --- | --- |
| [1.0.0](docs/releases/v1.0.0.md) | Biblioteca, roteiro, editor de letras, painel e projeção independentes, mídia local, efeitos, áudio, controle remoto, Holyrics e MP4 | Primeira versão funcional; quatro entradas animadas e corte direto |
| [1.1.0](docs/releases/v1.1.0.md) | Todos os recursos da 1.0, com mais controles de aparência e movimento | Correção da troca de fonte, fontes instaladas, negrito, seletor visual de cores, 12 entradas animadas e oito movimentos contínuos |
| [1.2.0](docs/releases/v1.2.0.md) | Todos os recursos anteriores e editor de animações granulares | Galeria com 66 opções, animação por letra/palavra/linha/bloco, tempos, ordem, curvas, intensidade, repetição e prévia com barra de tempo |
| [1.3.0](docs/releases/v1.3.0.md) | Todos os recursos anteriores, novo espaço de operação e atualização pelo aplicativo | Interface inspirada em estações de edição, retorno ao vivo, painéis redimensionáveis, quatro temas persistentes e atualizador integrado ao GitHub |
| [1.4.0](docs/releases/v1.4.0.md) | Todos os recursos anteriores, aplicativo de controle Android e seleção de letras do Holyrics | APK nativo, pareamento por código na rede local e download das letras escolhidas da biblioteca instalada do Holyrics |

Para uma instalação nova, prefira **1.4.0**. O instalador de PC, o APK Android, o ZIP completo de código-fonte e os hashes ficam na **Release 1.4.0**. Todas as próximas distribuições serão publicadas neste repositório. Consulte o [histórico de mudanças](CHANGELOG.md).

[Instalador Windows 1.4.0](https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.4.0/WorshipStudio-Setup-1.4.0.exe) · [APK Android 1.4.0](https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.4.0/LetrasParaCulto-Controle-1.4.0.apk) · [Todos os lançamentos](https://github.com/JoshuaNb/Letras-para-culto/releases)

## Recursos de operação

- Interface em português brasileiro; operação sem conta, assinatura ou internet.
- Espaço de trabalho com biblioteca, prévia da próxima estrofe, retorno da saída ao vivo, estrofes e inspetor; divisórias permitem ajustar os painéis.
- Temas **Branco**, **Escuro**, **Cinza** e **Amarelo pastel**, aplicados imediatamente e lembrados ao reabrir.
- Biblioteca local, busca, roteiro do culto, edição e importação de TXT, JSON e OpenLyrics.
- Prévia e saída ao vivo independentes; seleção do monitor, navegação, ocultar letra e tela preta.
- Fundos com imagens e vídeos locais, Aurora, túnel e partículas com geometria em perspectiva.
- Fonte, cores, posição e animações aplicadas à prévia, projeção e exportação.
- Reação à amplitude do áudio por WASAPI e pulso manual.
- Controle pelo APK Android ou navegador do celular na mesma rede, com autenticação local.
- Catálogo, busca e seleção de letras da biblioteca instalada do Holyrics; download com progresso e cancelamento para guardar as músicas no Worship Studio.
- Importação e acompanhamento da apresentação pela API local oficial do Holyrics.
- Exportação MP4 offline com áudio opcional; biblioteca, cenas e backup `.wstudio`.

## Instalar e projetar

1. Na Release desejada, baixe e execute `WorshipStudio-Setup-<versão>.exe`. O pacote inclui .NET, LibVLC e FFmpeg; não exige instalação prévia do SDK.
2. Abra Worship Studio. Para dois monitores, configure a extensão de telas no Windows e selecione a saída no inspetor **Cena**.
3. Cadastre ou importe um louvor. Um clique prepara a prévia; **Projetar estrofe** envia ao vivo.
4. A partir da versão 1.2, abra **Cena → Animações das letras → Abrir galeria** para ajustar a granularidade e clique **Aplicar à cena**.
5. Na versão 1.3, escolha o tema no seletor do cabeçalho. Os temas alteram a interface de operação; as cores da letra e do fundo continuam sendo definidas na cena.

A instalação é por usuário. A atualização preserva a biblioteca; faça backup antes de restaurar ou excluir dados. Os instaladores mantêm o nome original para permitir a atualização das versões já instaladas.

## Controlar pelo celular Android

O [APK Letras para culto · Controle](https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.4.0/LetrasParaCulto-Controle-1.4.0.apk) é nativo, gratuito e requer **Android 8.0 ou superior**. Funciona na rede local sem conta, nuvem ou internet durante o culto.

O instalador de PC também inclui o APK. Em **Conexões → Baixar APK para Android…**, salve uma cópia para transferir ao celular; essa cópia pode ser obtida offline. Se o arquivo não estiver incluído na instalação, o botão abre o download oficial da versão no GitHub.

1. Instale a versão 1.4.0 no PC e o APK no Android. Conecte ambos à mesma rede local.
2. No PC, abra **Conexões** e inicie o controle remoto. Informe no aplicativo Android o IPv4/porta exibidos e o código de seis dígitos.
3. Toque em **Conectar**. Use Próxima, Anterior, Tela preta, Ocultar letra, Pulso de efeito ou escolha uma estrofe da apresentação atual.

O código vale por dez minutos. Desligar e iniciar o controle remoto gera novas credenciais e revoga o acesso anterior. Também é possível colar no APK o link completo com token usado pelo controle web das versões anteriores; o pareamento por código exige PC 1.4.0. A projeção continua no computador se o celular desconectar. O APK lembra somente o endereço do PC, mantendo código e token apenas na memória da sessão.

[Guia de conexão e solução de problemas](docs/CONTROLE-ANDROID.md) · [Código e construção do APK](android-remote/README.md).

## Baixar letras da biblioteca do Holyrics

Na versão 1.4, o PC oferece uma janela para escolher as músicas da biblioteca do **Holyrics instalado no computador ou acessível na rede local**. O download importa letras e estrofes, que passam a funcionar offline no Worship Studio.

1. No Holyrics, ative o **API Server** e crie um token com acesso de leitura a músicas/letras. Mantenha o Holyrics aberto.
2. No Worship Studio, abra a seção **Holyrics** em **Conexões** e o download de letras. Use o endereço do API Server — por exemplo `http://127.0.0.1:8091` no mesmo PC — e o token configurado.
3. Clique em **Buscar músicas**, filtre por título ou artista e marque os louvores desejados. **Selecionar visíveis** marca os resultados da busca; **Limpar seleção** permite recomeçar.
4. Clique em **Baixar letras selecionadas**. A janela mostra o progresso e permite cancelar. Somente o lote concluído é adicionado à biblioteca do Worship Studio.

Esse recurso usa a biblioteca autorizada pelo operador e os endpoints `GetSongs`/`GetLyrics`. Não baixa áudio ou MP3 nem consulta um catálogo da internet. Depois de importar, o Holyrics pode ser fechado; a projeção das letras baixadas continua offline. A opção de acompanhar a apresentação do Holyrics permanece disponível separadamente.

## Atualizar pelo aplicativo

A versão **1.3.0 foi a primeira com atualizador**. Quem está na 1.0, 1.1 ou 1.2 pode instalar a **1.4.0 uma vez** pela Release acima; quem já está na 1.3 pode usar **Atualizações** no aplicativo para chegar à 1.4. A atualização do APK Android é feita pelo arquivo da nova Release.

- Com a busca automática ativada, o aplicativo consulta a última Release estável ao abrir e a cada seis horas enquanto permanece aberto. A consulta ocorre em segundo plano e aceita apenas versão superior à instalada.
- Abra **Atualizações** no cabeçalho para consultar a versão disponível, ler as novidades e baixar o instalador. O download mostra progresso e pode ser cancelado.
- O instalador só é disponibilizado para execução após a conferência do tamanho e do SHA-256 com o manifesto da Release e, quando presente, o digest informado pelo GitHub.
- **Instalar atualização** é uma ação explícita. Após a confirmação, o programa salva a biblioteca, abre o instalador e encerra a operação; programe a instalação para depois da projeção.
- Na aba **App** do inspetor, desative **Buscar atualizações ao abrir** para desligar as consultas automáticas. A verificação manual continua disponível.

Somente a consulta e o download de atualizações precisam de internet. Falta de conexão, indisponibilidade do GitHub ou limite de consultas não impedem a operação offline. Pré-lançamentos não são instalados pelo atualizador. As preferências do tema e da busca automática ficam separadas da biblioteca em `%LocalAppData%/WorshipStudio/preferences.json`.

Para publicar versões compatíveis com o atualizador, consulte [Publicação no GitHub](docs/PUBLICACAO.md).

## Compilar e testar

O desenvolvimento requer SDK .NET 10 x64. Reconstruir o instalador requer também Inno Setup 6. A primeira restauração das dependências usa NuGet; a operação do programa pronto continua offline.

Execute na raiz do código-fonte da versão:

```powershell
# Ao usar git clone ou o ZIP automático do GitHub, restaure as ferramentas:
.\Restaurar-Distribuicao.ps1
dotnet test tests/WorshipStudio.Tests.csproj -c Release
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -- --output test-evidence/typography-1.4.0
dotnet run --project ui-tests/WorshipStudio.UiTests.csproj -c Release -- --output test-evidence/ui-headless-1.4.0
.\build.ps1 -SkipInstaller
# Com Inno Setup disponível, para testar e gerar instalador, ZIP e hashes:
.\package-release.ps1
```

A suíte `render-tests` existe a partir da versão 1.1. A suíte [ui-tests](ui-tests/README.md), adicionada na 1.3, carrega o aplicativo de produção com Avalonia Headless e Skia para conferir layout, eventos, temas e operação. O script publica o executável independente do .NET em `app/win-x64`. `package-release.ps1` usa a versão do projeto C# para gerar instalador, ZIP completo e manifesto de hashes correspondentes na pasta acima do projeto. O pacote completo de código-fonte inclui dependências nativas, licenças e arquivos de origem de terceiros.

O APK tem código Java em `android-remote`, incluído no mesmo ZIP completo. Sua construção usa JDK 21, Android SDK Platform 36 e Build Tools 36.0.0, com `javac`, `aapt2`, `d8`, `zipalign` e `apksigner`; não requer Gradle ou serviços Google. Instruções e testes estão no [README Android](android-remote/README.md). O keystore e as senhas de assinatura ficam fora do repositório e da distribuição pública. O manifesto `SHA256SUMS-v1.4.0.txt` cobre instalador, ZIP de fontes e APK.

O Git guarda os fontes e as licenças; os executáveis nativos, os arquivos TAR de terceiros e os registros de teste acompanham o ZIP completo anexado à Release. `Restaurar-Distribuicao.ps1` restaura `tools` e `third-party-sources` a partir da distribuição 1.2.0, fixada por SHA-256, que contém as mesmas ferramentas nativas utilizadas nesta versão. Também aceita um ZIP 1.2.0 já baixado com `-UsarArquivoLocal -Arquivo <caminho>`. Para compilar as tags 1.0 e 1.1, use o ZIP completo da respectiva Release. A restauração usa internet somente no ambiente de desenvolvimento.

## Validação e limites

| Versão | Resultado registrado |
| --- | --- |
| 1.0.0 | 27 testes de operação aprovados |
| 1.1.0 | 30 testes de operação e 29 verificações visuais aprovados |
| 1.2.0 | 44 testes de núcleo, 108 verificações visuais e sete de integração MP4: 159 aprovações |
| 1.3.0 | 93 testes de núcleo, 108 verificações visuais e 24 de interface Avalonia Headless/Skia: 225 aprovações, zero falhas |
| 1.4.0 | 130 de núcleo, 108 visuais, 26 de interface principal, sete da janela Holyrics, 32 Java e 17 da integração HTTP real: 320 aprovações, zero falhas |

Os 93 testes de núcleo da 1.3 incluem os 44 anteriores, 36 do atualizador e 13 de preferências/contraste. As 24 verificações de interface incluem operação disponível enquanto a janela de atualização permanece aberta, navegação, reutilização do download na sessão, adiamento da instalação e recusa de arquivo adulterado. Uma integração HTTP real também baixou o instalador 1.2.0 do GitHub, conferiu seus 151.413.199 bytes e SHA-256, sem executá-lo; essa integração não está incluída no total de 225.

Na versão 1.4 foram aprovadas **320 verificações, zero falhas**: 130 de núcleo (93 anteriores, 18 de pareamento e 19 do download Holyrics), 108 visuais, 26 da interface principal e sete da janela Holyrics com Avalonia Headless/Skia, 32 do parser de conexão Android em Java e 17 de integração HTTP real. Esta última usa o mesmo cliente Java incluído no APK contra `RemoteServer`, `MainWindow`, `ProjectionWindow` e `StageSurface` de produção no backend headless, cobrindo comandos, estado, pulso, autenticação e revogação. O APK foi compilado, assinado e alinhado.

A integração HTTP não executa a Activity Android nem simula toques. A tentativa de executar um emulador sem hypervisor ficou indisponível, sem alterar a configuração do sistema. A Activity e a operação em celular físico não foram observadas. Os testes Holyrics usam respostas controladas da API; não equivalem a uma instalação real do programa. `VERIFICACAO.md` e as evidências do ZIP completo registram separadamente os resultados do instalador e dessas integrações.

Instalação, abertura e desinstalação das versões 1.0 a 1.2 também foram verificadas. Os relatórios `VERIFICACAO.md` e as evidências nos pacotes de cada versão detalham os resultados, incluindo a validação do instalador separadamente das suítes automatizadas.

A saída em duas janelas foi observada em um único monitor físico. Permanecem pendentes os testes com dois monitores físicos, celular físico, instalação real do Holyrics e sinal de áudio não nulo no equipamento da igreja. A aceleração depende do driver; não há benchmark de FPS ou latência em culto. A reação de áudio usa amplitude/RMS, sem detecção de BPM ou sincronização de sílabas. A exportação usa tempo fixo por estrofe e não grava a sessão ao vivo. Os fundos 3D são procedurais; não há importação de modelos nem edição livre de keyframes.

A galeria 1.2 interpreta quatro imagens estáticas de referência; não representa uma reprodução comprovadamente idêntica dos movimentos de um vídeo.

## Licença

O aplicativo usa licença MIT. LibVLC, FFmpeg e demais dependências conservam suas próprias licenças; consulte `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `licenses` e `third-party-sources` no código-fonte completo.
