using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using WorshipStudio.Services;

namespace WorshipStudio;

/// <summary>The opening screen waits for the library, while its update check is bounded and optional.</summary>
public sealed class StartupWindow : Window
{
    private readonly StartupSession _session;
    private readonly Action<StartupLibraryResult, StartupUpdateResult?> _openOperation;
    private readonly TextBlock _libraryStatus;
    private readonly TextBlock _updateStatus;
    private readonly ProgressBar _progress;
    private readonly Button _continue;
    private StartupLibraryResult? _library;
    private StartupUpdateResult? _update;
    private bool _started, _closed, _opening;

    public StartupWindow(StartupSession session, Action<StartupLibraryResult, StartupUpdateResult?> openOperation)
    {
        _session = session;
        _openOperation = openOperation;
        Title = "Worship Studio · Iniciando";
        Width = 600;
        Height = 520;
        MinWidth = 520;
        MinHeight = 400;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.Bind(BackgroundProperty, new DynamicResourceExtension("WsBackground"));
        var scope = new NameScope();
        NameScope.SetNameScope(this, scope);
        _libraryStatus = Label("Carregando seus louvores salvos neste computador…", 13, "WsMuted");
        _libraryStatus.Name = "StartupLibraryStatus";
        _updateStatus = Label("Verificando se há uma nova versão…", 13, "WsMuted");
        _updateStatus.Name = "StartupUpdateStatus";
        _progress = new ProgressBar { Name = "StartupProgress", IsIndeterminate = true, Height = 4, Margin = new Thickness(0, 7, 0, 7) };
        _progress.Bind(ProgressBar.ForegroundProperty, new DynamicResourceExtension("WsAccent"));
        _progress.Bind(ProgressBar.BackgroundProperty, new DynamicResourceExtension("WsBorder"));
        _continue = new Button
        {
            Name = "ContinueStartupButton", Content = "Abrir programa agora", HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, Height = 44,
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(7),
            FontWeight = FontWeight.SemiBold
        };
        _continue.Bind(Button.BackgroundProperty, new DynamicResourceExtension("WsAccent"));
        _continue.Bind(Button.ForegroundProperty, new DynamicResourceExtension("WsAccentText"));
        _continue.Click += (_, _) =>
        {
            _continue.IsEnabled = false;
            _continue.Content = "Concluindo abertura…";
            if (_library == null) _libraryStatus.Text = "Concluindo a leitura da biblioteca local…";
            _session.SkipUpdateCheck();
            TryOpen();
        };
        scope.Register(_libraryStatus.Name, _libraryStatus);
        scope.Register(_updateStatus.Name, _updateStatus);
        scope.Register(_continue.Name, _continue);
        scope.Register(_progress.Name, _progress);
        var content = new StackPanel { Spacing = 13, Margin = new Thickness(30, 25) };
        content.Children.Add(Label("W  /  LETRAS PARA CULTO", 11, "WsAccentLabel", true));
        content.Children.Add(Label("Worship Studio", 28, "WsText", true));
        content.Children.Add(Label("Preparando sua biblioteca e o espaço de operação", 13, "WsMuted"));
        content.Children.Add(_progress);
        content.Children.Add(Stage("BIBLIOTECA LOCAL", _libraryStatus));
        content.Children.Add(Stage("ATUALIZAÇÕES", _updateStatus));
        content.Children.Add(_continue);
        content.Children.Add(Label("Versão " + UpdateService.CurrentVersion + " · Sua operação continua disponível sem internet.", 11, "WsSubtle"));
        Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Opened += (_, _) => Begin();
        Closing += (_, _) => { _closed = true; _session.Cancel(); };
        Closed += (_, _) => _session.Dispose();
    }

    private void Begin()
    {
        if (_started || _closed) return;
        _started = true;
        _ = ObserveLibrary();
        _ = ObserveUpdate();
    }

    private async Task ObserveLibrary()
    {
        try
        {
            var result = await _session.Library;
            if (_closed) return;
            _library = result;
            _libraryStatus.Text = result.Error == null
                ? result.Document.Songs.Count + " louvor(es) carregado(s)."
                : "Arquivo original preservado. A recuperação estará disponível no programa.";
            TryOpen();
        }
        catch (OperationCanceledException) { }
    }

    private async Task ObserveUpdate()
    {
        try
        {
            var result = await _session.Update;
            if (_closed) return;
            _update = result;
            _updateStatus.Text = result.Message;
            TryOpen();
        }
        catch (OperationCanceledException) { }
    }

    private void TryOpen()
    {
        if (_closed || _opening || _library == null || _update == null) return;
        _opening = true;
        _progress.IsIndeterminate = false;
        _progress.Value = 100;
        try
        {
            _openOperation(_library, _update);
            Close(); // App switches its MainWindow before this opening screen closes.
        }
        catch (Exception ex)
        {
            _opening = false;
            _progress.Value = 0;
            _libraryStatus.Text = "Não foi possível abrir a operação: " + ex.Message;
            _continue.Content = "Tentar abrir programa";
            _continue.IsEnabled = true;
            AppLog.Write("Inicialização da operação: " + ex);
        }
    }

    private static TextBlock Label(string text, double size, string colorResource, bool bold = false)
    {
        var label = new TextBlock
        {
            Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal
        };
        label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(colorResource));
        return label;
    }

    private static Border Stage(string title, TextBlock status)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 11), BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 6, Children = { Label(title, 11, "WsText", true), status } }
        };
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("WsSurface"));
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("WsBorder"));
        return border;
    }
}
