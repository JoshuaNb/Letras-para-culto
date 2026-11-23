# Compatibilidade com Windows — versão 1.7

O instalador `WorshipStudio-Setup-1.7.0.exe` contém duas publicações completas x64 e escolhe uma conforme o Windows. Não é necessário instalar o SDK nem baixar o runtime durante a instalação.

| Windows x64 | Edição escolhida | Renderização da interface e das letras |
| --- | --- | --- |
| Windows 11 e Windows 10 22H2, build 19045 ou posterior | Atual, .NET 10 | GPU por ANGLE, com fallback de software |
| Windows 10 anterior à build 19045 | Legado, .NET 6.0.36 | Skia por software |
| Windows 8 e 8.1 | Legado, .NET 6.0.36 | Skia por software |
| Windows 7 SP1 com atualização de carregamento de bibliotecas | Legado, .NET 6.0.36 | Skia por software |

Windows 7 sem Service Pack 1 e Windows de 32 bits não são atendidos por este instalador. A arquitetura x64 é a mesma das versões anteriores do aplicativo.

## Preparar o Windows 7

Instale o SP1 e as atualizações oficiais do Windows. A KB3063858, ou uma atualização equivalente que disponibilize `AddDllDirectory`, é necessária para carregar as bibliotecas nativas. O instalador verifica essa função e explica o componente ausente antes de copiar os arquivos. Não baixa atualizações do Windows nem altera configurações de segurança.

O pacote legado inclui o runtime .NET 6 completo, com UCRT app-local. Não exige o SDK, instalação global do .NET nem instalação separada do Visual C++ Redistributable. A operação, vídeos locais, biblioteca e exportação continuam offline. Para consultar o GitHub pelo atualizador, o sistema também precisa de TLS 1.2 e certificados raiz válidos; falta de internet ou falha nessa consulta não bloqueia a projeção.

## O que mudou no aplicativo

- O mesmo código C#/Avalonia é compilado para .NET 10 e .NET 6. Os formatos da biblioteca, cenas e preferências são compartilhados.
- O manifesto declara Windows 7, 8, 8.1 e 10/11 e inclui informações de DPI compatíveis com as gerações anteriores.
- A edição legado usa Skia por software e dispensa o ANGLE moderno, cujo binário depende de funções de sistemas mais novos. As animações e o editor continuam disponíveis; o desempenho depende da CPU e da resolução.
- O FFmpeg legado é compilado do fonte com alvo Windows 7/MSVCRT e sem as dependências modernas incompatíveis. Mantém exportação MP4 por MPEG-4/AAC e composição dos quadros PNG e do vídeo de fundo. O FFmpeg da edição atual é preservado.
- A leitura de comandos remotos, cálculo de hashes e espera do processo de exportação usam APIs presentes no .NET 6, mantendo cancelamento e verificação dos downloads. O atualizador legado pede TLS 1.2 explicitamente.
- O instalador mantém o AppId e a biblioteca das versões anteriores. Remove apenas nomes de DLLs pertencentes aos payloads para evitar mistura de runtimes ao atualizar.

O espaço de operação, os menus e a janela de abertura da 1.7 estão presentes nas duas edições. O carregamento da biblioteca é local e a consulta de atualização opcional ocorre em paralelo, com espera de rede limitada a seis segundos e botão **Abrir programa agora**. Sem internet, a biblioteca e a projeção continuam disponíveis.

O controle Android e a importação de letras do Holyrics continuam disponíveis nas duas edições. APKs anteriores continuam controlando a projeção. Para enviar arquivos do celular, atualize tanto o PC quanto o APK para 1.6 ou superior. O APK 1.7 acompanha o instalador com o mesmo código de controle e transferência da 1.6.

## Validação e limites

As suítes de núcleo, renderização e operação são executadas nos dois runtimes no computador de desenvolvimento. A instalação de cada payload é ensaiada em uma identidade isolada, incluindo a escolha do legado em um instalador compilado exclusivamente para QA. Esse instalador de QA não é publicado.

**Executar o runtime legado no Windows 11 não equivale a executar no Windows 7, 8 ou 10.** Não há imagem licenciada nem máquina desses sistemas disponível nesta sessão. Os binários foram preparados para esses alvos e suas dependências foram auditadas; a execução física/virtual em cada versão antiga permanece pendente. Veja os resultados concretos e evidências em [VERIFICACAO.md](../VERIFICACAO.md).

.NET 6 e os sistemas antigos encerraram seu suporte regular. Esta edição amplia a compatibilidade do aplicativo; não restaura suporte da Microsoft ou do Avalonia aos sistemas antigos.

## Referências técnicas

- [Microsoft: dependências e fim do suporte do .NET nos Windows antigos](https://learn.microsoft.com/en-us/dotnet/core/install/windows#dependencies).
- [Host oficial .NET 6.0.36: identificação de Windows 7 e Windows 8](https://github.com/dotnet/runtime/blob/v6.0.36/src/native/corehost/hostmisc/pal.windows.cpp#L435).
- [Avalonia 11: plataformas e suporte limitado ao Windows 7](https://v11.docs.avaloniaui.net/docs/overview/supported-platforms/).
- [Reprodução do FFmpeg legado](../tools/ffmpeg-legacy/build.sh).
