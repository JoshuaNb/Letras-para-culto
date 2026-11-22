# Versão atual: 1.5.0

Pedido autorizado: futuras atualizações publicadas no GitHub público JoshuaNb/Letras-para-culto; busca automática e instalação pelo aplicativo; reorganização inspirada em estações de edição como DaVinci Resolve; temas branco, escuro, cinza e amarelo pastel.

O aplicativo continua funcionando sem internet. A consulta opcional ao GitHub ocorre em segundo plano; instalar requer ação do operador e encerra a projeção somente após confirmação no aplicativo.

Implementação 1.5.0 concluída: o instalador universal contém .NET 10 e .NET 6.0.36, selecionados por versão do Windows, com edição legado x64 destinada a Windows 7 SP1, 8, 8.1 e versões anteriores do Windows 10. Renderização por software, manifesto/DPI compatíveis, verificação da KB3063858 e FFmpeg recompilado para Win7/MSVCRT. Validação: 583 verificações aprovadas, zero falhas finais, incluindo núcleo/renderização/interface nos dois runtimes, Java e MP4/pixels do FFmpeg legado. A execução física em Windows 7/8/10 permanece pendente; todos os ensaios locais usam Windows 11. Publicação segue quatro anexos e três hashes conforme `docs/PUBLICACAO.md`. Consulte `VERIFICACAO.md` e `docs/COMPATIBILIDADE-WINDOWS.md` para evidências e limites.

Histórico 1.4: controle Android nativo assinado, pareamento por seis dígitos e download seletivo das letras da biblioteca do Holyrics instalado no PC. Temas, atualizador, operação e projeção anteriores preservados. Essa versão registrou 320 verificações e QA isolado 1.3→1.4. Atividade/toques em celular físico e integração com instalação real do Holyrics ainda dependem de validação no equipamento do operador. A instalação original do usuário foi preservada durante o desenvolvimento.

Limites de uso do Codex são controlados pela conta/serviço; o aplicativo não altera esses limites. Este registro permite retomar o trabalho sem depender da memória da conversa.
