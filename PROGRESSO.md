# Versão atual: 1.7.0

Pedido autorizado: futuras atualizações publicadas no GitHub público JoshuaNb/Letras-para-culto; busca automática e instalação pelo aplicativo; reorganização inspirada em estações de edição como DaVinci Resolve; temas branco, escuro, cinza e amarelo pastel.

O aplicativo continua funcionando sem internet. A consulta opcional ao GitHub ocorre em segundo plano; instalar requer ação do operador e encerra a projeção somente após confirmação no aplicativo.

Implementação 1.7.0 concluída: interface inspirada na referência DaVinci Resolve, com menus reais, retorno maior, prévia independente, inspetor e faixas reais de estrofes/roteiro. A janela de abertura carrega as letras locais em background e consulta atualizações em paralelo, com seis segundos de teto e botão para dispensar a rede. Respeita a preferência salva e não instala automaticamente. A biblioteca corrompida permanece protegida contra gravação.

Validação 1.7: 734 verificações automáticas aprovadas, zero falhas finais: 182 de núcleo, 108 visuais, 35 de interface e 26 de transporte Java/HTTP por runtime, mais 32 Java. A operação nativa também abriu com busca automática e GPU no Windows 11; o menu avançou a projeção independente mantendo a prévia. O APK 1.7 é assinado com a mesma chave e mantém Java/DEX da 1.6. Veja `VERIFICACAO.md` e os relatórios no ZIP completo. Celular físico, Wi-Fi e Windows antigos permanecem com limites explícitos.

O QA isolado 1.6→1.7 passou nas edições atual e legado: quatro aberturas com runtime próprio e janela de operação respondendo, preservação de biblioteca/preferências/recebidos inclusive após desinstalar as identidades de QA. A instalação original e seus atalhos ficaram intactos. Evidências e fontes: `test-evidence/installer-1.7`. Publicação segue quatro anexos e três hashes conforme `docs/PUBLICACAO.md`.

Histórico 1.6: envio de arquivos do celular ao PC pelo APK Android e pelo navegador, usando o pareamento local. Inclui progresso, cancelamento, verificação SHA256, gravação atômica e caixa de recebidos no desktop. Imagens/vídeos e letras somente entram na cena ou biblioteca por ação explícita. Limites: dez arquivos por lote, 512 MiB por arquivo, 5 GiB e 5.000 arquivos na caixa de entrada, um envio simultâneo e lista dos cem mais recentes. Os comandos da projeção continuam disponíveis durante o envio.

Validação 1.6: 690 verificações aprovadas, zero falhas finais, nos dois runtimes, incluindo 52 verificações do transporte Java exato do APK por HTTP real e 32 do parser. Um ensaio manual adicional no navegador real confirmou bytes/SHA256 e comandos de projeção. Fontes, relatórios e limites constam em `VERIFICACAO.md` e no ZIP completo. A Activity, o seletor SAF e a instalação/toques em celular físico permanecem pendentes. Os ensaios de rede usam Loopback no Windows 11; não comprovam uma rede Wi-Fi específica.

O QA isolado 1.5→1.6 passou nas edições atual e legado, preservando biblioteca, preferências e arquivos recebidos inclusive após desinstalar as identidades de teste. A instalação original e os atalhos do operador foram preservados. Evidências e fontes: `test-evidence/installer-1.6` no ZIP completo.

Histórico 1.5: instalador universal com .NET 10 e .NET 6.0.36, selecionados por versão do Windows, com edição legado x64 destinada a Windows 7 SP1, 8, 8.1 e versões anteriores do Windows 10. Renderização por software, manifesto/DPI compatíveis, verificação da KB3063858 e FFmpeg recompilado para Win7/MSVCRT. Essa versão registrou 583 verificações. A compatibilidade é preservada na 1.6; execução física em Windows 7/8/10 permanece pendente. Publicação segue quatro anexos e três hashes conforme `docs/PUBLICACAO.md`. Consulte também `docs/COMPATIBILIDADE-WINDOWS.md`.

Histórico 1.4: controle Android nativo assinado, pareamento por seis dígitos e download seletivo das letras da biblioteca do Holyrics instalado no PC. Temas, atualizador, operação e projeção anteriores preservados. Essa versão registrou 320 verificações e QA isolado 1.3→1.4. Atividade/toques em celular físico e integração com instalação real do Holyrics ainda dependem de validação no equipamento do operador. A instalação original do usuário foi preservada durante o desenvolvimento.

Limites de uso do Codex são controlados pela conta/serviço; o aplicativo não altera esses limites. Este registro permite retomar o trabalho sem depender da memória da conversa.
