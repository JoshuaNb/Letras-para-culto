# Letras para culto

Aplicativo desktop gratuito e offline para preparar e projetar letras de louvores em igrejas, desenvolvido em **C#/.NET 10 e Avalonia UI** para **Windows 11 x64**.

Este é o nome do projeto no GitHub. Nas três primeiras versões, o aplicativo, o executável e os instaladores conservam o nome **Worship Studio**.

## Versões disponíveis

| Versão | O que oferece | Novidades em relação à anterior |
| --- | --- | --- |
| [1.0.0](docs/releases/v1.0.0.md) | Biblioteca, roteiro, editor de letras, painel e projeção independentes, mídia local, efeitos, áudio, controle remoto, Holyrics e MP4 | Primeira versão funcional; quatro entradas animadas e corte direto |
| [1.1.0](docs/releases/v1.1.0.md) | Todos os recursos da 1.0, com mais controles de aparência e movimento | Correção da troca de fonte, fontes instaladas, negrito, seletor visual de cores, 12 entradas animadas e oito movimentos contínuos |
| [1.2.0](docs/releases/v1.2.0.md) | Todos os recursos anteriores e editor de animações granulares | Galeria com 66 opções, animação por letra/palavra/linha/bloco, tempos, ordem, curvas, intensidade, repetição e prévia com barra de tempo |

Para uma instalação nova, prefira **1.2.0**. Os instaladores e os ZIPs completos de código-fonte ficam anexados às respectivas **Releases**. Consulte o [histórico de mudanças](CHANGELOG.md).

[Baixar a versão 1.2.0](https://github.com/JoshuaNb/Letras-para-culto/releases/tag/v1.2.0) · [Todos os lançamentos](https://github.com/JoshuaNb/Letras-para-culto/releases)

## Recursos de operação

- Interface em português brasileiro; operação sem conta, assinatura ou internet.
- Biblioteca local, busca, roteiro do culto, edição e importação de TXT, JSON e OpenLyrics.
- Prévia e saída ao vivo independentes; seleção do monitor, navegação, ocultar letra e tela preta.
- Fundos com imagens e vídeos locais, Aurora, túnel e partículas com geometria em perspectiva.
- Fonte, cores, posição e animações aplicadas à prévia, projeção e exportação.
- Reação à amplitude do áudio por WASAPI e pulso manual.
- Controle pelo navegador do celular na mesma rede, com chave de acesso.
- Importação e acompanhamento da apresentação pela API local oficial do Holyrics.
- Exportação MP4 offline com áudio opcional; biblioteca, cenas e backup `.wstudio`.

## Instalar e projetar

1. Na Release desejada, baixe e execute `WorshipStudio-Setup-<versão>.exe`. O pacote inclui .NET, LibVLC e FFmpeg; não exige instalação prévia do SDK.
2. Abra Worship Studio. Para dois monitores, configure a extensão de telas no Windows e selecione a saída em **Cena**.
3. Cadastre ou importe um louvor. Um clique prepara a prévia; **Projetar estrofe** envia ao vivo.
4. Na versão 1.2, abra **Cena → Animações das letras → Abrir galeria** para ajustar a granularidade e clique **Aplicar à cena**.

A instalação é por usuário. A atualização preserva a biblioteca; faça backup antes de restaurar ou excluir dados. Os instaladores mantêm o nome original para permitir a atualização das versões já instaladas.

## Compilar e testar

O desenvolvimento requer SDK .NET 10 x64. Reconstruir o instalador requer também Inno Setup 6. A primeira restauração das dependências usa NuGet; a operação do programa pronto continua offline.

Execute na raiz do código-fonte da versão:

```powershell
# Ao usar git clone ou o ZIP automático do GitHub, restaure as ferramentas:
.\Restaurar-Distribuicao.ps1
dotnet test tests/WorshipStudio.Tests.csproj -c Release
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -- --output test-evidence/typography-1.2
.\build.ps1 -SkipInstaller
# Com Inno Setup disponível, para gerar o instalador:
.\build.ps1
```

A suíte `render-tests` existe a partir da versão 1.1. O script publica o executável independente do .NET em `app/win-x64`. O pacote completo de código-fonte inclui dependências nativas, licenças e arquivos de origem de terceiros.

O Git guarda os fontes e as licenças; os executáveis nativos, os arquivos TAR de terceiros e os registros de teste acompanham o ZIP completo anexado à Release. Na versão 1.2, `Restaurar-Distribuicao.ps1` baixa esse ZIP, verifica seu SHA256 e restaura apenas `tools` e `third-party-sources`. Também aceita um ZIP já baixado com `-UsarArquivoLocal -Arquivo <caminho>`. Para compilar as tags 1.0 e 1.1, use o ZIP completo da respectiva Release. A restauração usa internet somente no ambiente de desenvolvimento.

## Validação e limites

| Versão | Resultado registrado |
| --- | --- |
| 1.0.0 | 27 testes de operação aprovados |
| 1.1.0 | 30 testes de operação e 29 verificações visuais aprovados |
| 1.2.0 | 44 testes de núcleo, 108 verificações visuais e sete de integração MP4: 159 aprovações |

Os testes não registraram falhas. Instalação, abertura e desinstalação também foram verificadas. Os relatórios `VERIFICACAO.md` e as evidências nos pacotes de cada versão detalham os resultados.

A saída em duas janelas foi observada em um único monitor físico. Permanecem pendentes os testes com dois monitores físicos, celular físico, instalação real do Holyrics e sinal de áudio não nulo no equipamento da igreja. A aceleração depende do driver; não há benchmark de FPS ou latência em culto. A reação de áudio usa amplitude/RMS, sem detecção de BPM ou sincronização de sílabas. A exportação usa tempo fixo por estrofe e não grava a sessão ao vivo. Os fundos 3D são procedurais; não há importação de modelos nem edição livre de keyframes.

A galeria 1.2 interpreta quatro imagens estáticas de referência; não representa uma reprodução comprovadamente idêntica dos movimentos de um vídeo.

## Licença

O aplicativo usa licença MIT. LibVLC, FFmpeg e demais dependências conservam suas próprias licenças; consulte `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`, `licenses` e `third-party-sources` no código-fonte completo.
