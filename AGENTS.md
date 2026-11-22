# Letras para culto / Worship Studio

- O usuário definiu o repositório público `JoshuaNb/Letras-para-culto` como canal das atualizações do aplicativo. Ao concluir uma atualização solicitada, valide, gere os pacotes e publique código e Release no GitHub. Preserve as versões anteriores e documente as novidades.
- A versão vem de `src/WorshipStudio/WorshipStudio.csproj`. Use `package-release.ps1` para testar e gerar instalador, código-fonte e manifesto. Instruções completas em `docs/PUBLICACAO.md`.
- O atualizador depende da tag estável `vX.Y.Z` e dos anexos exatos `WorshipStudio-Setup-X.Y.Z.exe`, `WorshipStudio-Codigo-Fonte-X.Y.Z.zip` e `SHA256SUMS-vX.Y.Z.txt`. Marque a nova distribuição estável como Latest somente depois de validar os três arquivos.
- A operação deve funcionar offline. Consultas de atualização são opcionais e não podem bloquear a projeção, iniciar instalação silenciosa ou apagar a biblioteca. Preserve o AppId do instalador e as preferências/biblioteca nas atualizações.
- Faça testes de instalação em uma identidade e pasta isoladas. Não substitua a instalação usada pelo operador durante o desenvolvimento.
- Registre trabalho pendente em `PROGRESSO.md` quando necessário. Limites da conta Codex não podem ser redefinidos por código ou automação.
