# Interface real em Avalonia Headless

Execute na raiz do projeto com o SDK .NET 10:

```powershell
dotnet run --project ui-tests/WorshipStudio.UiTests.csproj -c Release -- --output test-evidence/ui-headless-1.3
```

O runner referencia o projeto de produção, carrega `App.axaml` e instancia `MainWindow` com Avalonia Headless e Skia. O teste usa uma biblioteca temporária isolada e desliga as consultas automáticas antes de abrir a janela. Não utiliza janelas do Windows, serviços de rede local, Holyrics ou instaladores do sistema.

As 24 verificações cobrem composição e layout, as duas superfícies de prévia/retorno, os quatro temas e sua persistência, busca da biblioteca, abas e espaços de trabalho, opção de atualização automática, projeção independente, preparação da próxima estrofe sem alterar a saída, navegação, tela preta, ocultar letra, galeria e cancelamento. A janela real de atualização é exercitada com o serviço de download de produção e um transporte HTTP em memória: verifica integridade, download, reutilização na sessão, operação disponível enquanto a janela está aberta e recusa de arquivo alterado. O callback de instalação apenas adia a operação; os bytes de teste são inertes e nunca executados.

`results.json` registra os resultados e hashes das quatro capturas PNG da interface. Estes testes verificam composição, eventos, bindings, pixels e estados de aplicação em memória. A validação de monitores físicos, GPU, janelas nativas, áudio, vídeo nativo e dispositivo móvel exige os testes correspondentes no equipamento real.
