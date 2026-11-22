# Regressões visuais de tipografia

Execute com o SDK .NET 10 no Windows:

```powershell
dotnet run --project render-tests/WorshipStudio.RenderTests.csproj -c Release -- --output test-evidence/typography
```

O programa usa os arquivos de produção `StageRenderer.cs`, `StudioModels.cs` e `ColorField.cs`, vinculados diretamente pelo projeto. O ambiente Avalonia Headless usa pixels reais do Skia com renderização por software. Não abre janelas reais do Windows, não inicializa o player de mídia e não acessa a biblioteca do usuário.

As verificações comparam os pixels gerados para a mesma estrofe depois de trocar fonte, peso e cor, amostram as entradas e os movimentos das letras em tempos conhecidos e conferem Unicode, limites do quadro, letra oculta e tela preta. Também verificam entradas imediatas quando a duração é zero e a atualização da amostra visual de cor com seus eventos. Os quadros PNG e o resumo JSON ficam na pasta indicada por `--output`; falhas retornam código 1.

A execução requer as fontes Segoe UI, Georgia e Consolas disponíveis no Windows. Esses testes verificam a forma renderizada e a amostragem usada na exportação; não medem FPS ou comportamento de um projetor físico.
