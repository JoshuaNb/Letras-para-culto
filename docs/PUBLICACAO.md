# Publicar atualizações no GitHub

As distribuições oficiais de **Letras para culto / Worship Studio** ficam no repositório público [JoshuaNb/Letras-para-culto](https://github.com/JoshuaNb/Letras-para-culto). As próximas atualizações seguem o mesmo canal. O atualizador existe a partir da versão 1.3.0 e consulta a Release estável marcada como **Latest**.

Este documento descreve o procedimento de manutenção e publicação. Não há workflow automático de release configurado; o build e a publicação são executados e verificados antes de disponibilizar uma versão.

## Contrato do atualizador

Cada lançamento estável deve ter uma tag `vMAJOR.MINOR.PATCH`, como `v1.7.0`, e os três anexos de PC: instalador, ZIP completo e manifesto de hashes. Quando incluir o controle Android, como na versão 1.7.0, acrescente o APK usando o mesmo número da Release. Nesse caso, publique estes **quatro anexos**:

| Arquivo | Conteúdo |
| --- | --- |
| `WorshipStudio-Setup-1.7.0.exe` | Instalador universal Windows x64 (7 SP1, 8, 8.1, 10 e 11) |
| `LetrasParaCulto-Controle-1.7.0.apk` | Controle nativo Android assinado |
| `WorshipStudio-Codigo-Fonte-1.7.0.zip` | Código-fonte de PC e Android, construção, dependências nativas, licenças, fontes de terceiros e evidências |
| `SHA256SUMS-v1.7.0.txt` | SHA-256 do instalador, do APK e do ZIP completo |

O atualizador consulta `https://api.github.com/repos/JoshuaNb/Letras-para-culto/releases/latest`. Rejeita rascunhos, pré-lançamentos, versões iguais/anteriores e arquivos ausentes ou inconsistentes. Os endereços dos anexos devem pertencer à tag e ao repositório oficial. Downloads exigem HTTPS; redirecionamentos ficam restritos aos hosts de arquivos de Release do GitHub.

O manifesto usa uma linha por arquivo, com 64 caracteres hexadecimais, dois espaços e o nome exato do arquivo. O lançamento 1.7.0 contém três linhas de hash: instalador, APK e ZIP. Não inclua o hash do próprio manifesto. O atualizador de PC continua validando seu instalador pelo nome correspondente; a presença do APK não altera esse protocolo. Os dois arquivos automáticos de código-fonte que o GitHub gera para tags não substituem o ZIP completo da distribuição.

## Preparar uma versão

1. Atualize `<Version>` em `src/WorshipStudio/WorshipStudio.csproj`, a fonte de versão do build. `build.ps1` passa esse número ao Inno Setup; `package-source.ps1` e `package-release.ps1` o utilizam nos nomes do ZIP e do manifesto. Mantenha a identidade do instalador e o diretório da biblioteca para permitir atualização sobre a versão anterior.
2. Atualize `README.md`, `CHANGELOG.md`, `docs/releases/v<versão>.md` e `VERIFICACAO.md` com comportamento, novidades, testes executados e limites observados. Uma evidência de versão anterior não comprova a versão nova.
3. Em um checkout limpo, restaure as ferramentas nativas modernas com `Restaurar-Distribuicao.ps1`. A origem 1.2.0 fixada por SHA-256 não contém o FFmpeg legado. Para a 1.5 e posteriores, restaure também `tools/ffmpeg-legacy` e as fontes/licenças a partir do ZIP completo da distribuição ou compile-o pela receita fornecida. O build recusa gerar o instalador universal se esse FFmpeg estiver ausente. Ao trocar uma dependência, publique as fontes/licenças correspondentes e atualize sua origem e hash.
4. Quando incluir Android, use JDK 21, SDK Platform 36 e Build Tools 36.0.0 para executar os testes Java e gerar o APK assinado com `android-remote/build.ps1`. Consulte [Android](../android-remote/README.md). Conserve o keystore usado na versão anterior e mantenha-o, com as senhas, fora do código público. Confira assinatura, alinhamento, nome e versão antes de preparar a distribuição.
5. Use SDK .NET 10 x64, runtime .NET 6.0.36 para executar a suíte legada e Inno Setup 6. Execute `package-release.ps1`: ele chama testes de núcleo, visuais e de interface em `net10.0` e `net6.0`, publica os dois aplicativos self-contained, compila o instalador universal, gera o ZIP completo e calcula o manifesto SHA-256. Os relatórios ficam separados por versão e framework em `test-evidence`. A variável de processo `WORSHIP_FFMPEG` seleciona o codificador correspondente durante cada suíte e é restaurada ao terminar. Quando o APK fizer parte da versão, inclua-o entre os pacotes e hashes. Rode também integrações adicionais aplicáveis às mudanças. Consulte [ui-tests](../ui-tests/README.md) para executar somente a suíte de interface.
6. Para a 1.7, valide os menus operacionais e a independência entre prévia e saída. Confira a abertura com biblioteca salva, consulta automática habilitada/desabilitada, rede indisponível, espera de rede limitada e **Abrir programa agora**. Cada abertura real deve reler a biblioteca e realizar uma nova consulta quando habilitada; biblioteca corrompida deve permanecer preservada e com gravação bloqueada. Execute essas verificações nos dois runtimes e registre os resultados, sem reutilizar uma prova da versão anterior.
7. Verifique instalação, abertura, preservação de dados e atualização a partir da versão estável anterior em identidade, pasta e grupo de atalhos exclusivos de teste. Confirme que o ZIP contém os fontes Android, ferramentas nativas e licenças necessárias e exclui dados pessoais, credenciais, keystores, `.git`, caches, `bin`, `obj` e a pasta de saída `app`.
8. Atualize as evidências finais e gere novamente o pacote se algum arquivo tiver mudado. Todos os anexos devem corresponder ao código da versão publicada. Não modifique instalador, APK ou ZIP depois de calcular os hashes.

Execute na raiz do projeto:

```powershell
.\Restaurar-Distribuicao.ps1
.\package-release.ps1
# Se os executáveis não estiverem no PATH:
# .\package-release.ps1 -DotnetPath 'C:\SDK\dotnet.exe' -InnoCompiler 'C:\Inno Setup 6\ISCC.exe'
```

Os pacotes são gravados na pasta acima da raiz do projeto. A geração é local; o envio ao GitHub continua sendo uma etapa explícita de publicação. Na versão 1.7.0, confirme os quatro nomes da tabela e as três entradas do manifesto. O APK 1.7 conserva os fontes Java e o DEX da 1.6, mas tem versão própria no manifesto e deve ser compilado, assinado e conferido como o pacote que será publicado.

## Instalador universal e compatibilidade

O mesmo `WorshipStudio-Setup-X.Y.Z.exe` conserva o AppId das versões anteriores e seleciona apenas um payload x64 conforme o sistema real. Windows 10 build 19045 ou superior e Windows 11 recebem `app/win-x64` com .NET 10. Windows 7 SP1, 8, 8.1 e builds anteriores do Windows 10 recebem `app/win-legacy-x64` com .NET 6.0.36. Os runtimes acompanham o instalador; não é necessário instalar um SDK no computador de operação.

O build limpa somente suas duas pastas de saída verificadas, substitui `tools/ffmpeg` pelo codificador correto e exclui a biblioteca ANGLE incompatível do payload legado. `installer/runtime-cleanup.iss`, gerado pelo build, lista nomes exatos dos binários que não pertencem ao runtime selecionado. O instalador remove esses resíduos ao trocar de payload e preserva os arquivos comuns pelo mecanismo normal de substituição do Inno Setup. Biblioteca e preferências continuam no diretório de dados do usuário.

No Windows 7 SP1, o instalador verifica a presença da API `AddDllDirectory`, fornecida pela KB3063858 ou atualização equivalente. Se ela faltar, informa o pré-requisito e encerra antes de alterar a instalação. O runtime legado inclui seu UCRT local; o instalador não exige uma instalação global de Visual C++ ou UCRT nem inicia downloads ou atualização do Windows.

Para QA isolado, `AppSource`, `LegacyAppSource`, `RuntimeCleanupFile`, `AppIdentity` e `PackageOutputDir` podem ser sobrescritos na compilação. `ForceLegacyQA` força o payload legado somente no instalador de ensaio; não é um parâmetro de execução do instalador oficial. Teste os dois caminhos em identidades, diretórios e grupos de atalhos exclusivos, e registre separadamente ensaios feitos no Windows atual e em sistemas antigos. Executar .NET 6 no Windows 11 não comprova operação em Windows 7/8.

A edição legada usa uma versão de runtime cujo suporte Microsoft terminou. A compatibilidade técnica pretendida e as limitações da validação devem estar explícitas em `VERIFICACAO.md` e nas notas; não apresentar a edição como suporte oficial da Microsoft aos sistemas antigos.

Na versão 1.3.0 foram aprovadas 225 verificações (93 de núcleo, 108 visuais e 24 de interface Avalonia Headless/Skia), sem falhas. Uma integração adicional de HTTP real baixou o instalador 1.2.0 com os 151.413.199 bytes e SHA-256 esperados, sem execução. A validação do instalador é registrada separadamente em `VERIFICACAO.md` e deve ser repetida para cada distribuição.

## Publicar código e anexos

1. Revise e envie o commit da versão para `main`. Crie uma tag `v<versão>` no commit que corresponde ao instalador e ao ZIP. Nunca mova uma tag que já foi publicada.
2. Abra **Releases → Draft a new release** no repositório e selecione essa tag. Preencha título e notas a partir de `docs/releases/v<versão>.md`, convertendo os links relativos em links públicos quando necessário.
3. Anexe instalador, código-fonte completo e manifesto; nas versões com Android, anexe também o APK. Mantenha a Release como rascunho até todos os uploads terminarem.
4. Confira os nomes, tamanhos e hashes com os arquivos locais. Para uma distribuição estável, desmarque **Pre-release**, marque **Latest** e publique. Isso permite que o atualizador encontre a versão completa.
5. Confirme que a página e todos os anexos podem ser acessados publicamente — quatro na versão 1.7.0. Consulte `/releases/latest` e verifique se a tag anunciada é a correta.
6. Em uma versão anterior que já possua atualizador, execute o fluxo real: descobrir a atualização, baixar, validar e instalar. Confira que os louvores, cenas e preferências foram mantidos. Versões até a 1.2 precisam de uma instalação manual inicial de uma versão com atualizador. Teste o APK com o pareamento e os comandos de rede local e distinga emulador de celular físico no relatório; o APK é atualizado pelo arquivo Android da Release, não pelo instalador de PC.

As notas devem indicar claramente qualquer etapa manual de migração. A consulta em segundo plano não dispara a instalação: o operador escolhe baixar e instalar. A instalação encerra a projeção, portanto deve ser programada para depois da apresentação.

## Correções após publicar

Se encontrar um problema no código ou nos pacotes, prepare uma versão superior com tag própria e novos hashes. Preserve as Releases anteriores para que o histórico e as verificações de integridade continuem reproduzíveis. Pré-lançamentos podem ser usados para testes, mas não serão oferecidos pelo atualizador estável.
