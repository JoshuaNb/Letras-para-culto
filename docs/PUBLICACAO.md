# Publicar atualizações no GitHub

As distribuições oficiais de **Letras para culto / Worship Studio** ficam no repositório público [JoshuaNb/Letras-para-culto](https://github.com/JoshuaNb/Letras-para-culto). As próximas atualizações seguem o mesmo canal. O atualizador existe a partir da versão 1.3.0 e consulta a Release estável marcada como **Latest**.

Este documento descreve o procedimento de manutenção e publicação. Não há workflow automático de release configurado; o build e a publicação são executados e verificados antes de disponibilizar uma versão.

## Contrato do atualizador

Cada lançamento estável deve ter uma tag `vMAJOR.MINOR.PATCH`, como `v1.3.0`, e anexar estes três arquivos, usando exatamente a mesma versão:

| Arquivo | Conteúdo |
| --- | --- |
| `WorshipStudio-Setup-1.3.0.exe` | Instalador Windows 11 x64 completo |
| `WorshipStudio-Codigo-Fonte-1.3.0.zip` | Código-fonte, construção, dependências nativas, licenças, fontes de terceiros e evidências |
| `SHA256SUMS-v1.3.0.txt` | SHA-256 do instalador e do ZIP completo |

O atualizador consulta `https://api.github.com/repos/JoshuaNb/Letras-para-culto/releases/latest`. Rejeita rascunhos, pré-lançamentos, versões iguais/anteriores e arquivos ausentes ou inconsistentes. Os endereços dos anexos devem pertencer à tag e ao repositório oficial. Downloads exigem HTTPS; redirecionamentos ficam restritos aos hosts de arquivos de Release do GitHub.

O manifesto usa uma linha por arquivo, com 64 caracteres hexadecimais, dois espaços e o nome exato do arquivo. Não inclua o hash do próprio manifesto. Os dois arquivos automáticos de código-fonte que o GitHub gera para tags não substituem o ZIP completo da distribuição.

## Preparar uma versão

1. Atualize `<Version>` em `src/WorshipStudio/WorshipStudio.csproj`, a fonte de versão do build. `build.ps1` passa esse número ao Inno Setup; `package-source.ps1` e `package-release.ps1` o utilizam nos nomes do ZIP e do manifesto. Mantenha a identidade do instalador e o diretório da biblioteca para permitir atualização sobre a versão anterior.
2. Atualize `README.md`, `CHANGELOG.md`, `docs/releases/v<versão>.md` e `VERIFICACAO.md` com comportamento, novidades, testes executados e limites observados. Uma evidência de versão anterior não comprova a versão nova.
3. Em um checkout limpo, restaure as ferramentas nativas com `Restaurar-Distribuicao.ps1`. O script usa a distribuição 1.2.0 fixada por SHA-256; enquanto essas dependências permanecerem as mesmas, ele continua válido para as versões seguintes. Se forem alteradas, publique as fontes/licenças correspondentes e atualize a origem e o hash fixado.
4. Use SDK .NET 10 x64 e Inno Setup 6. Execute `package-release.ps1`: ele chama o build com os testes de núcleo, visuais e de interface, publica o aplicativo, compila o instalador, gera o ZIP completo e calcula o manifesto SHA-256. Rode também integrações adicionais aplicáveis às mudanças. Consulte [ui-tests](../ui-tests/README.md) para executar somente a suíte de interface.
5. Verifique instalação, abertura, preservação de dados e atualização a partir da versão estável anterior em uma instalação isolada. Confirme que o ZIP contém as ferramentas nativas e as licenças necessárias e exclui dados pessoais, credenciais, `.git`, caches, `bin`, `obj` e a pasta de saída `app`.
6. Atualize as evidências finais e gere novamente o pacote se algum arquivo tiver mudado. Os três anexos devem corresponder ao mesmo código-fonte. Não modifique o instalador ou o ZIP depois de calcular os hashes.

Execute na raiz do projeto:

```powershell
.\Restaurar-Distribuicao.ps1
.\package-release.ps1
# Se os executáveis não estiverem no PATH:
# .\package-release.ps1 -DotnetPath 'C:\SDK\dotnet.exe' -InnoCompiler 'C:\Inno Setup 6\ISCC.exe'
```

O script grava os três arquivos na pasta acima da raiz do projeto e imprime seus nomes. A geração é local; o envio ao GitHub continua sendo uma etapa explícita de publicação.

Na versão 1.3.0 foram aprovadas 225 verificações (93 de núcleo, 108 visuais e 24 de interface Avalonia Headless/Skia), sem falhas. Uma integração adicional de HTTP real baixou o instalador 1.2.0 com os 151.413.199 bytes e SHA-256 esperados, sem execução. A validação do instalador é registrada separadamente em `VERIFICACAO.md` e deve ser repetida para cada distribuição.

## Publicar código e anexos

1. Revise e envie o commit da versão para `main`. Crie uma tag `v<versão>` no commit que corresponde ao instalador e ao ZIP. Nunca mova uma tag que já foi publicada.
2. Abra **Releases → Draft a new release** no repositório e selecione essa tag. Preencha título e notas a partir de `docs/releases/v<versão>.md`, convertendo os links relativos em links públicos quando necessário.
3. Anexe instalador, código-fonte completo e manifesto. Mantenha a Release como rascunho até os três uploads terminarem.
4. Confira os nomes, tamanhos e hashes com os arquivos locais. Para uma distribuição estável, desmarque **Pre-release**, marque **Latest** e publique. Isso permite que o atualizador encontre a versão completa.
5. Confirme que a página e os três anexos podem ser acessados publicamente. Consulte `/releases/latest` e verifique se a tag anunciada é a correta.
6. Em uma versão anterior que já possua atualizador, execute o fluxo real: descobrir a atualização, baixar, validar e instalar. Confira que os louvores, cenas e preferências foram mantidos. Na primeira migração da 1.2 para a 1.3, a instalação inicial ainda é manual.

As notas devem indicar claramente qualquer etapa manual de migração. A consulta em segundo plano não dispara a instalação: o operador escolhe baixar e instalar. A instalação encerra a projeção, portanto deve ser programada para depois da apresentação.

## Correções após publicar

Se encontrar um problema no código ou nos pacotes, prepare uma versão superior com tag própria e novos hashes. Preserve as Releases anteriores para que o histórico e as verificações de integridade continuem reproduzíveis. Pré-lançamentos podem ser usados para testes, mas não serão oferecidos pelo atualizador estável.
