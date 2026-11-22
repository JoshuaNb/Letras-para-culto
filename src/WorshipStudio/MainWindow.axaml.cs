using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WorshipStudio.Controls;
using WorshipStudio.Models;
using WorshipStudio.Services;

namespace WorshipStudio;

public partial class MainWindow : Window
{
    private readonly LibraryStore _store = new();
    private StudioDocument _document = new();
    private readonly StageSurface _preview = new();
    private readonly AudioReactiveService _audio = new();
    private readonly RemoteServer _remote = new();
    private readonly HolyricsService _holyrics = new();
    private readonly DispatcherTimer _tick;
    private readonly DispatcherTimer _saveTimer;
    private ProjectionWindow? _projection;
    private Song? _selected;
    private List<Slide> _slides = [];
    private int _previewIndex;
    private Guid? _liveSongId;
    private string _liveTitle = "Nenhum louvor ao vivo";
    private List<Slide> _liveSlides = [];
    private int _liveIndex;
    private string _liveText = "", _previousText = "";
    private long _revision;
    private bool _blackout, _hideLyrics, _loading = true, _closing, _restoring;
    private object _remoteState = new { title = "Worship Studio", text = "", index=0, count=0, slides=Array.Empty<object>() };
    private CancellationTokenSource? _followCts;
    private readonly string[] _backgrounds = ["Aurora", "Tunnel", "Particles", "Solid", "Image", "Video"];
    private readonly string[] _transitions = ["Fade", "Slide", "SlideDown", "SlideLeft", "SlideRight", "Zoom", "Pop", "Rotate", "Wipe", "Typewriter", "Words", "Lines", "Cut"];
    private readonly string[] _letterAnimations = ["None", "Float", "Wave", "Pulse", "Breathe", "Sway", "Bounce", "Glow", "Shake"];
    private List<string> _availableFonts = [];
    private string? _startupError;
    private string _gpuStatus = "Renderização em verificação";

    public MainWindow()
    {
        InitializeComponent();
        try { _document = _store.Load(); }
        catch (Exception ex)
        {
            _startupError = "Não foi possível carregar a biblioteca: " + ex.Message + "\nO arquivo original foi preservado. Escolha um backup para restaurar ou corrija library.json na pasta de dados. A gravação automática foi suspensa.";
            _document = LibraryStore.CreateInitial();
            AppLog.Write(ex.ToString());
        }
        C<ContentControl>("PreviewHost").Content = _preview;
        _preview.Error += BackgroundError;
        _remote.Error += BackgroundError;
        _audio.Error += BackgroundError;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _saveTimer.Tick += (_,_) => { _saveTimer.Stop(); Persist(); };
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _tick.Tick += (_,_) => RenderFrames();
        ConfigureControls();
        BindActions();
        _loading = false;
        RefreshLibrary(_document.Songs.FirstOrDefault()?.Id);
        UpdateOutputUi();
        RenderFrames();
        _tick.Start();
        KeyDown += OnKeyDown;
        Opened += async (_,_) =>
        {
            RefreshMonitors();
            try
            {
                var compositor=ElementComposition.GetElementVisual(_preview)?.Compositor;
                var gpu=compositor==null?null:await compositor.TryGetCompositionGpuInterop();
                _gpuStatus=gpu==null?"Renderização por software":"Aceleração de GPU ativa";
                AppLog.Write(_gpuStatus);
            }
            catch(Exception ex){_gpuStatus="Backend gráfico automático";AppLog.Write(ex.Message);}
            if (_startupError != null) await Message("Biblioteca preservada", _startupError);
            else Status("Pronto · "+_gpuStatus+" · biblioteca local salva automaticamente");
        };
        Screens.Changed += (_,_) => Dispatcher.UIThread.Post(RefreshMonitors);
        Closing += OnClosing;
    }

    private T C<T>(string name) where T : Control => this.FindControl<T>(name) ?? throw new InvalidOperationException(name);
    private void Click(string name, Action action) => C<Button>(name).Click += async (_,_) => await Run(()=>{ action(); return Task.CompletedTask; });
    private void ClickAsync(string name, Func<Task> action) => C<Button>(name).Click += async (_,_) => await Run(action);
    private async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { Status("Operação cancelada"); }
        catch (Exception ex) { AppLog.Write(ex.ToString()); Status(ex.Message); await Message("Não foi possível concluir", ex.Message); }
    }

    private void ConfigureControls()
    {
        C<ComboBox>("BackgroundCombo").ItemsSource = new[] { "Aurora fluida", "Túnel 3D", "Partículas 3D", "Cor sólida", "Imagem local", "Vídeo local" };
        C<ComboBox>("TransitionCombo").ItemsSource = new[] { "Dissolver", "Subir", "Descer", "Entrar pela esquerda", "Entrar pela direita", "Aproximar", "Salto com aproximação", "Girar e aparecer", "Revelar da esquerda", "Máquina de escrever", "Palavra por palavra", "Linha por linha", "Corte direto" };
        C<ComboBox>("LetterAnimationCombo").ItemsSource = new[] { "Sem animação contínua", "Flutuar", "Onda nas linhas", "Pulsar", "Respiração", "Balançar", "Saltar", "Brilho pulsante", "Vibração suave" };
        _availableFonts=FontManager.Current.SystemFonts.Select(f=>f.Name).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToList();
        if(_availableFonts.Count==0)_availableFonts.Add("Segoe UI");
        C<ComboBox>("FontCombo").ItemsSource = _availableFonts;
        LoadStyleControls();
        C<NumericUpDown>("RemotePortInput").Value = _document.RemotePort;
        foreach (var name in new[] { "BackgroundCombo", "FontCombo", "LetterAnimationCombo" }) C<ComboBox>(name).SelectionChanged += (_,_)=>ChangeStyle();
        C<ComboBox>("TransitionCombo").SelectionChanged+=(_,_)=>{if(!_loading){ChangeStyle();_preview.ReplayTransition();}};
        C<CheckBox>("BoldCheck").IsCheckedChanged+=(_,_)=>ChangeStyle();
        foreach (var name in new[] { "DimSlider", "SpeedSlider", "DurationSlider", "TextYSlider", "LetterSpeedSlider", "LetterIntensitySlider" }) C<Slider>(name).PropertyChanged += (_,e)=>{if(e.Property==Slider.ValueProperty)ChangeStyle();};
        C<NumericUpDown>("FontSizeInput").ValueChanged += (_,_)=>ChangeStyle();
        foreach (var name in new[]{"AccentField","BackgroundColorField","TextColorField"}) C<ColorField>(name).ColorChanged+=()=>ChangeStyle();
        C<Slider>("GainSlider").PropertyChanged += (_,e)=> {if(e.Property==Slider.ValueProperty)_audio.Gain=C<Slider>("GainSlider").Value;};
        C<ToggleSwitch>("AudioToggle").IsCheckedChanged += async (_,_)=> await Run(()=>{ConfigureAudio(); return Task.CompletedTask;});
        C<ComboBox>("AudioSourceCombo").SelectionChanged += async (_,_)=>{if(C<ToggleSwitch>("AudioToggle").IsChecked==true)await Run(()=>{ConfigureAudio();return Task.CompletedTask;});};
        C<ComboBox>("MonitorCombo").SelectionChanged += (_,_)=>
        {
            if(_loading)return;
            _document.MonitorIndex=C<ComboBox>("MonitorCombo").SelectedIndex;
            if(_projection!=null)MoveProjection();
            ScheduleSave();
        };
    }

    private void BindActions()
    {
        C<TextBox>("SearchBox").TextChanged+=(_,_)=>{if(!_loading)RefreshLibrary(_selected?.Id);};
        C<ComboBox>("LibraryMode").SelectionChanged+=(_,_)=>{if(!_loading)RefreshLibrary(_selected?.Id);};
        C<ListBox>("LibraryList").SelectionChanged+=(_,_)=>SelectSong(C<ListBox>("LibraryList").SelectedItem as Song);
        C<ListBox>("LibraryList").DoubleTapped+=(_,_)=>{if(_selected!=null)SendPreview();};
        ClickAsync("NewButton",()=>EditSong(null));
        ClickAsync("EditButton",async()=>{if(_selected!=null)await EditSong(_selected);});
        ClickAsync("DeleteButton",DeleteSong);
        ClickAsync("ImportButton",ImportSongs);
        Click("AddSetButton",()=>
        {
            if(_selected==null)return;
            if(!_document.Setlist.Contains(_selected.Id))_document.Setlist.Add(_selected.Id);
            ScheduleSave();RefreshLibrary(_selected.Id);Status("Louvor adicionado ao roteiro do culto");
        });
        Click("SetUpButton",()=>MoveSetlist(-1));
        Click("SetDownButton",()=>MoveSetlist(1));
        Click("SetRemoveButton",()=>{if(_selected==null)return;_document.Setlist.Remove(_selected.Id);ScheduleSave();RefreshLibrary();});
        Click("SendButton",SendPreview);
        Click("OpenProjectionButton",()=>{if(_projection==null)EnsureProjection();else _projection.Close();});
        Click("PrevButton",()=>Navigate(-1));Click("NextButton",()=>Navigate(1));
        Click("BlackButton",ToggleBlackout);Click("HideButton",ToggleLyrics);
        Click("BeatButton",()=>_audio.Tap());
        Click("ReplayButton",()=>_preview.ReplayTransition());
        ClickAsync("MediaButton",PickMedia);
        Click("RestartVideoButton",()=>{_preview.RestartVideo();_projection?.Surface.RestartVideo();Status("Vídeo do fundo reiniciado");});
        ClickAsync("BackupButton",SaveBackup);ClickAsync("RestoreButton",RestoreBackup);
        ClickAsync("SaveStyleButton",SaveStyle);ClickAsync("LoadStyleButton",LoadStyle);
        ClickAsync("ExportButton",async()=>
        {
            if(_slides.Count==0)throw new InvalidOperationException("Selecione um louvor com estrofes para exportar.");
            await new ExportDialog(_slides.ToArray(),_document.Style with{},_document.SecondsPerSlide).ShowDialog(this);
        });
        Click("RemoteButton",ToggleRemote);
        ClickAsync("CopyRemoteButton",async()=>{var url=_remote.Urls.FirstOrDefault(u=>!u.Contains("127.0.0.1"))??_remote.Urls.FirstOrDefault();if(_remote.IsRunning&&url!=null&&Clipboard!=null){await Clipboard.SetTextAsync(url);Status("Link do controle remoto copiado");}});
        ClickAsync("HolyricsImportButton",ImportHolyrics);
        C<ToggleSwitch>("HolyricsFollowToggle").IsCheckedChanged+=async(_,_)=>
        {
            if(C<ToggleSwitch>("HolyricsFollowToggle").IsChecked==true)await FollowHolyrics();else StopFollowing();
        };
        ClickAsync("HelpButton",()=>Message("Como operar o Worship Studio", "1. Importe letras TXT UTF-8, JSON ou OpenLyrics XML, ou crie um louvor. Separe estrofes com uma linha em branco; use [Refrão] para identificar partes.\n\n2. Um clique prepara a prévia. Projetar estrofe ou duplo clique envia ao vivo. As setas navegam na apresentação atual. Ao final, Próxima segue o roteiro do culto.\n\n3. Escolha o monitor e abra a projeção. No Windows use Win+P → Estender para usar duas telas. F11 alterna tela cheia; Esc retorna à janela.\n\n4. B alterna tela preta; L oculta apenas a letra. Imagens e vídeos são copiados para a biblioteca local. O vídeo de fundo é silencioso e se repete.\n\n5. Ajuste os efeitos e transições no painel Cena. A reação ao áudio usa a intensidade RMS do som do computador ou da entrada padrão. O pulso manual funciona sem captura.\n\n6. Ative o controle remoto em Conexões e abra o link no celular na mesma rede. Compartilhe o link apenas com operadores.\n\n7. Exporte a música selecionada em MP4 com tempo por estrofe e áudio opcional. Backup salva a biblioteca, roteiro, cena e mídia atual em um arquivo portátil.\n\nDados: " + _store.DirectoryPath));
    }

    private void LoadStyleControls()
    {
        var old=_loading;_loading=true;
        var s=_document.Style;
        C<ComboBox>("BackgroundCombo").SelectedIndex=Math.Max(0,Array.IndexOf(_backgrounds,s.BackgroundMode));
        C<ComboBox>("TransitionCombo").SelectedIndex=Math.Max(0,Array.IndexOf(_transitions,s.Transition));
        var font=_availableFonts.FirstOrDefault(n=>n.Equals(s.FontFamily,StringComparison.OrdinalIgnoreCase))??_availableFonts.FirstOrDefault(n=>n.Equals("Segoe UI",StringComparison.OrdinalIgnoreCase))??_availableFonts[0];
        C<ComboBox>("FontCombo").SelectedItem=font;
        if(s.FontFamily!=font){_document.Style=s=s with{FontFamily=font};}
        C<NumericUpDown>("FontSizeInput").Value=(decimal)s.FontSize;
        C<CheckBox>("BoldCheck").IsChecked=s.Bold;
        C<ColorField>("AccentField").Value=s.AccentColor;C<ColorField>("BackgroundColorField").Value=s.BackgroundColor;C<ColorField>("TextColorField").Value=s.TextColor;
        C<ComboBox>("LetterAnimationCombo").SelectedIndex=Math.Max(0,Array.IndexOf(_letterAnimations,s.LyricAnimation));
        C<Slider>("LetterSpeedSlider").Value=s.LyricAnimationSpeed;C<Slider>("LetterIntensitySlider").Value=s.LyricAnimationIntensity;
        C<Slider>("DimSlider").Value=s.Dim;C<Slider>("SpeedSlider").Value=s.MotionSpeed;C<Slider>("DurationSlider").Value=s.TransitionSeconds;C<Slider>("TextYSlider").Value=s.TextY;
        C<TextBlock>("DimValue").Text=$"{s.Dim:P0}";C<TextBlock>("DurationValue").Text=$"{s.TransitionSeconds:0.0}s";
        C<TextBlock>("MediaLabel").Text=string.IsNullOrEmpty(s.BackgroundPath)?"Efeito procedural · sem arquivo externo":Path.GetFileName(s.BackgroundPath);
        C<Button>("RestartVideoButton").IsEnabled=s.BackgroundMode=="Video";
        UpdateTypographySample();
        _loading=old;
    }

    private void ChangeStyle()
    {
        if(_loading)return;
        var current=_document.Style;
        _document.Style=current with
        {
            BackgroundMode=_backgrounds[Math.Clamp(C<ComboBox>("BackgroundCombo").SelectedIndex,0,_backgrounds.Length-1)],
            Transition=_transitions[Math.Clamp(C<ComboBox>("TransitionCombo").SelectedIndex,0,_transitions.Length-1)],
            FontFamily=C<ComboBox>("FontCombo").SelectedItem as string??"Segoe UI",
            FontSize=(double)(C<NumericUpDown>("FontSizeInput").Value??64),
            Bold=C<CheckBox>("BoldCheck").IsChecked==true,
            AccentColor=C<ColorField>("AccentField").Value,BackgroundColor=C<ColorField>("BackgroundColorField").Value,TextColor=C<ColorField>("TextColorField").Value,
            LyricAnimation=_letterAnimations[Math.Clamp(C<ComboBox>("LetterAnimationCombo").SelectedIndex,0,_letterAnimations.Length-1)],
            LyricAnimationSpeed=C<Slider>("LetterSpeedSlider").Value,LyricAnimationIntensity=C<Slider>("LetterIntensitySlider").Value,
            Dim=C<Slider>("DimSlider").Value,MotionSpeed=C<Slider>("SpeedSlider").Value,TransitionSeconds=C<Slider>("DurationSlider").Value,TextY=C<Slider>("TextYSlider").Value
        };
        C<TextBlock>("DimValue").Text=$"{_document.Style.Dim:P0}";C<TextBlock>("DurationValue").Text=$"{_document.Style.TransitionSeconds:0.0}s";
        C<Button>("RestartVideoButton").IsEnabled=_document.Style.BackgroundMode=="Video";
        UpdateTypographySample();
        RenderFrames();ScheduleSave();
    }

    private void UpdateTypographySample()
    {
        var style=_document.Style;
        var sample=C<TextBlock>("FontSample");
        sample.FontFamily=new FontFamily(style.FontFamily);
        sample.FontWeight=style.Bold?FontWeight.Bold:FontWeight.Normal;
        sample.Foreground=new SolidColorBrush(Color.TryParse(style.TextColor,out var color)?color:Colors.White);
        C<TextBlock>("LetterSpeedValue").Text=$"{style.LyricAnimationSpeed:0.0}×";
        C<TextBlock>("LetterIntensityValue").Text=$"{style.LyricAnimationIntensity:P0}";
        var animated=style.LyricAnimation!="None";
        C<Slider>("LetterSpeedSlider").IsEnabled=animated;C<Slider>("LetterIntensitySlider").IsEnabled=animated;
    }

    private void RefreshLibrary(Guid? select=null)
    {
        var query=C<TextBox>("SearchBox").Text?.Trim()??"";
        var setlist=C<ComboBox>("LibraryMode").SelectedIndex==1;
        IEnumerable<Song> source=setlist?_document.Setlist.Select(id=>_document.Songs.FirstOrDefault(s=>s.Id==id)).OfType<Song>():_document.Songs.OrderBy(s=>s.Title,StringComparer.CurrentCultureIgnoreCase);
        var filtered=source.Where(s=>query.Length==0||s.Title.Contains(query,StringComparison.CurrentCultureIgnoreCase)||s.Artist.Contains(query,StringComparison.CurrentCultureIgnoreCase)||s.Lyrics.Contains(query,StringComparison.CurrentCultureIgnoreCase)).ToList();
        C<ListBox>("LibraryList").ItemsSource=filtered;
        C<ListBox>("LibraryList").SelectedItem=filtered.FirstOrDefault(s=>s.Id==select)??filtered.FirstOrDefault();
        C<StackPanel>("SetlistTools").IsVisible=setlist;
        C<TextBlock>("LibraryCount").Text=$"{filtered.Count} louvor(es) · {_document.Setlist.Count} no roteiro";
        if(filtered.Count==0)SelectSong(null);
    }

    private void SelectSong(Song? song)
    {
        _selected=song;_slides=LyricParser.Parse(song?.Lyrics);_previewIndex=0;
        C<TextBlock>("SelectedTitle").Text=song?.Title??"Selecione um louvor";C<TextBlock>("SelectedArtist").Text=song?.Artist??"Sua biblioteca fica salva neste computador";
        BuildSlideCards();RenderFrames();
    }
    private void BuildSlideCards()
    {
        var panel=C<WrapPanel>("SlidesPanel");panel.Children.Clear();
        for(var i=0;i<_slides.Count;i++)
        {
            var index=i;var slide=_slides[i];
            var isLive=_liveSongId==_selected?.Id&&i==_liveIndex;
            var stack=new StackPanel {Spacing=8};
            stack.Children.Add(new TextBlock{Text=$"{i+1:00}  {slide.Label.ToUpperInvariant()}".Replace("ESTROFE","ESTROFE"),FontSize=10,FontWeight=FontWeight.SemiBold,Foreground=new SolidColorBrush(isLive?Color.Parse("#FF9BAA"):Color.Parse("#6CCDBB"))});
            stack.Children.Add(new TextBlock{Text=slide.Text,FontSize=12,TextWrapping=TextWrapping.Wrap,MaxLines=4,TextTrimming=TextTrimming.CharacterEllipsis,Height=65});
            if(isLive)stack.Children.Add(new TextBlock{Text="● AO VIVO",FontSize=9,Foreground=Brushes.Pink});
            var button=new Button{Content=stack,Width=215,Height=135,Margin=new Thickness(0,0,9,9),Padding=new Thickness(12),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Background=new SolidColorBrush(Color.Parse(i==_previewIndex?"#213831":"#18202D")),BorderBrush=new SolidColorBrush(Color.Parse(i==_previewIndex?"#48DCC4":isLive?"#7B3C4A":"#2C3647")),BorderThickness=new Thickness(1)};
            button.Click+=(_,_)=>{_previewIndex=index;BuildSlideCards();RenderFrames();};
            button.DoubleTapped+=(_,_)=>{_previewIndex=index;SendPreview();};
            panel.Children.Add(button);
        }
        C<TextBlock>("PreviewPosition").Text=_slides.Count==0?"Nenhuma estrofe · edite a letra para começar":$"{_previewIndex+1:00} / {_slides.Count:00} · {_slides[Math.Clamp(_previewIndex,0,_slides.Count-1)].Label}";
        C<Button>("SendButton").IsEnabled=_slides.Count>0;
    }
    private void SendPreview()
    {
        if(_selected==null||_slides.Count==0)return;
        StopFollowing();EnsureProjection();ShowLive(_selected,_previewIndex);
    }
    private void ShowLive(Song song,int index)
    {
        var slides=LyricParser.Parse(song.Lyrics);if(slides.Count==0)return;
        _liveSlides=slides;_liveSongId=song.Id;_liveTitle=song.Title;_liveIndex=Math.Clamp(index,0,slides.Count-1);
        SetLiveText(slides[_liveIndex].Text);_hideLyrics=false;
        UpdateOutputUi();BuildSlideCards();RenderFrames();Status($"Ao vivo: {_liveTitle} · estrofe {_liveIndex+1}/{_liveSlides.Count}");
    }
    private void SetLiveText(string text){_previousText=_liveText;_liveText=text;_revision++;}
    private void Navigate(int direction)
    {
        StopFollowing();
        if(_liveSlides.Count==0){if(_liveText.Length==0)SendPreview();else Status("A fonte atual não disponibilizou estrofes para navegação. Projete uma letra local para assumir a operação.");return;}
        var next=_liveIndex+direction;
        if(next>=0&&next<_liveSlides.Count){_liveIndex=next;SetLiveText(_liveSlides[next].Text);}
        else
        {
            var position=_liveSongId.HasValue?_document.Setlist.IndexOf(_liveSongId.Value):-1;
            var nextSong=position+direction;
            if(position>=0&&nextSong>=0&&nextSong<_document.Setlist.Count)
            {
                var song=_document.Songs.First(s=>s.Id==_document.Setlist[nextSong]);
                var count=LyricParser.Parse(song.Lyrics).Count;
                if(count>0){ShowLive(song,direction>0?0:count-1);return;}
            }
            Status(direction>0?"Fim da apresentação e do roteiro":"Início da apresentação");return;
        }
        _hideLyrics=false;UpdateOutputUi();BuildSlideCards();RenderFrames();Status($"Ao vivo: {_liveTitle} · estrofe {_liveIndex+1}/{_liveSlides.Count}");
    }
    private void ToggleBlackout(){StopFollowing();_blackout=!_blackout;UpdateOutputUi();RenderFrames();}
    private void ToggleLyrics(){StopFollowing();_hideLyrics=!_hideLyrics;UpdateOutputUi();RenderFrames();}
    private void RenderFrames()
    {
        if(_closing)return;
        var style=_document.Style;
        var text=_slides.Count==0?"":_slides[Math.Clamp(_previewIndex,0,_slides.Count-1)].Text;
        _preview.SetFrame(new StageFrame{Text=text,Style=style,AudioLevel=_audio.Level,Revision=_previewIndex});
        _projection?.SetFrame(new StageFrame{Text=_liveText,PreviousText=_previousText,Style=style,AudioLevel=_audio.Level,Blackout=_blackout,HideLyrics=_hideLyrics,Revision=_revision});
        C<ProgressBar>("AudioMeter").Value=_audio.Level;
        _remoteState=new {title=_liveTitle,text=_liveText,index=_liveIndex,count=_liveSlides.Count,blackout=_blackout,hideLyrics=_hideLyrics,slides=_liveSlides.Select(s=>new{label=s.Label,text=s.Text}).ToArray()};
    }
    private void UpdateOutputUi()
    {
        C<TextBlock>("LiveLabel").Text=_projection==null?"SAÍDA FECHADA":_blackout?"TELA PRETA":_hideLyrics?"AO VIVO · LETRA OCULTA":"AO VIVO";
        C<TextBlock>("LiveSummary").Text=_liveSlides.Count>0?$"{_liveTitle} · {_liveIndex+1}/{_liveSlides.Count} · {_liveText.Replace('\n',' ')}":_liveTitle;
        C<Button>("OpenProjectionButton").Content=_projection==null?"Abrir projeção":"Fechar projeção";
        C<Button>("BlackButton").Content=_blackout?"Restaurar tela":"Tela preta";
        C<Button>("HideButton").Content=_hideLyrics?"Mostrar letra":"Ocultar letra";
    }

    private void EnsureProjection()
    {
        if(_projection!=null)return;
        _projection=new ProjectionWindow();
        _projection.NextRequested+=()=>Navigate(1);_projection.PreviousRequested+=()=>Navigate(-1);
        _projection.BlackoutRequested+=ToggleBlackout;_projection.LyricsVisibilityRequested+=ToggleLyrics;
        _projection.Surface.Error+=BackgroundError;
        _projection.Closed+=(_,_)=>{_projection=null;if(!_closing)UpdateOutputUi();};
        MoveProjection();RenderFrames();UpdateOutputUi();
    }
    private void RefreshMonitors()
    {
        if(_closing)return;
        var old=_loading;_loading=true;
        C<ComboBox>("MonitorCombo").ItemsSource=Screens.All.Select((s,i)=>$"{i+1}. {(s.IsPrimary?"Principal":"Externo")} · {s.Bounds.Width} × {s.Bounds.Height}").ToArray();
        C<ComboBox>("MonitorCombo").SelectedIndex=Math.Clamp(_document.MonitorIndex,0,Math.Max(0,Screens.All.Count-1));
        _loading=old;
        if(_projection!=null)MoveProjection();
    }
    private void MoveProjection()
    {
        if(_projection==null||Screens.All.Count==0)return;
        var index=Math.Clamp(C<ComboBox>("MonitorCombo").SelectedIndex,0,Screens.All.Count-1);
        // A single-monitor operator can test output without covering every control.
        _projection.ShowOnScreen(Screens.All[index],Screens.All.Count>1);
        if(Screens.All.Count==1){_projection.Width=960;_projection.Height=540;_projection.Position=new PixelPoint(Screens.All[0].WorkingArea.X+80,Screens.All[0].WorkingArea.Y+80);}
    }
    private void ConfigureAudio()
    {
        if(_loading)return;
        if(C<ToggleSwitch>("AudioToggle").IsChecked==true)
        {
            try{_audio.Gain=C<Slider>("GainSlider").Value;_audio.Start(C<ComboBox>("AudioSourceCombo").SelectedIndex==1);Status("Reação ao áudio ativa · intensidade RMS em tempo real");}
            catch{C<ToggleSwitch>("AudioToggle").IsChecked=false;throw;}
        }
        else{_audio.Stop();Status("Captura de áudio desligada");}
    }
    private void MoveSetlist(int direction)
    {
        if(_selected==null)return;var index=_document.Setlist.IndexOf(_selected.Id);var target=index+direction;
        if(index<0||target<0||target>=_document.Setlist.Count)return;
        (_document.Setlist[index],_document.Setlist[target])=(_document.Setlist[target],_document.Setlist[index]);ScheduleSave();RefreshLibrary(_selected.Id);
    }

    private async Task EditSong(Song? original)
    {
        var editor=new SongEditor(original);
        var result=await editor.ShowDialog<Song?>(this);
        if(result==null)return;
        if(original==null)_document.Songs.Add(result);else{var i=_document.Songs.FindIndex(s=>s.Id==original.Id);if(i>=0)_document.Songs[i]=result;}
        C<ComboBox>("LibraryMode").SelectedIndex=0;C<TextBox>("SearchBox").Text="";
        ScheduleSave();RefreshLibrary(result.Id);Status("Louvor salvo · a saída ao vivo é atualizada ao projetar novamente");
    }
    private async Task DeleteSong()
    {
        if(_selected==null)return;var selected=_selected;
        if(!await Confirm("Excluir louvor",$"Excluir “{selected.Title}” da biblioteca e do roteiro?"))return;
        _document.Songs.RemoveAll(s=>s.Id==selected.Id);_document.Setlist.Remove(selected.Id);ScheduleSave();RefreshLibrary();Status("Louvor excluído");
    }
    private async Task ImportSongs()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Importar louvores",AllowMultiple=true,FileTypeFilter=[new FilePickerFileType("Letras TXT, JSON ou OpenLyrics XML"){Patterns=["*.txt","*.json","*.xml"]}]});
        if(files.Count==0)return;var count=0;var errors=new List<string>();
        foreach(var file in files)
        {
            try{count+=AddImported(_holyrics.ImportFile(Local(file)) );}
            catch(Exception ex){errors.Add(file.Name+": "+ex.Message);}
        }
        ScheduleSave();RefreshLibrary();Status($"{count} louvor(es) importado(s)");
        if(errors.Count>0)await Message("Arquivos não importados",string.Join("\n",errors));
    }
    private int AddImported(IEnumerable<Song> songs)
    {
        var count=0;
        foreach(var song in songs)
        {
            if(_document.Songs.Any(s=>s.Title.Equals(song.Title,StringComparison.OrdinalIgnoreCase)&&s.Lyrics==song.Lyrics))continue;
            song.Id=Guid.NewGuid();_document.Songs.Add(song);count++;
        }
        return count;
    }
    private async Task PickMedia()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Selecionar fundo local",FileTypeFilter=[new FilePickerFileType("Imagens e vídeos"){Patterns=["*.png","*.jpg","*.jpeg","*.webp","*.bmp","*.mp4","*.mov","*.mkv","*.webm","*.avi"]}]});
        if(files.Count==0)return;
        var path=await Task.Run(()=>_store.ImportAsset(Local(files[0])));
        var isImage=new[]{".png",".jpg",".jpeg",".webp",".bmp"}.Contains(Path.GetExtension(path).ToLowerInvariant());
        _document.Style=_document.Style with{BackgroundMode=isImage?"Image":"Video",BackgroundPath=path};LoadStyleControls();RenderFrames();
        if(!isImage){_preview.RestartVideo();_projection?.Surface.RestartVideo();}
        ScheduleSave();Status("Mídia copiada para a biblioteca local");
    }
    private async Task SaveBackup()
    {
        var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="Salvar backup completo",SuggestedFileName="WorshipStudio-"+DateTime.Now.ToString("yyyy-MM-dd")+".wstudio",DefaultExtension="wstudio",FileTypeChoices=[new FilePickerFileType("Backup Worship Studio"){Patterns=["*.wstudio"]}]});
        if(file==null)return;
        var snapshot=JsonSerializer.Deserialize<StudioDocument>(JsonSerializer.Serialize(_document,LibraryStore.JsonOptions),LibraryStore.JsonOptions)!;
        await Task.Run(()=>_store.Backup(Local(file),snapshot));Status("Backup completo salvo");
    }
    private async Task RestoreBackup()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Restaurar backup",FileTypeFilter=[new FilePickerFileType("Backup Worship Studio"){Patterns=["*.wstudio"]}]});
        if(files.Count==0)return;
        if(!await Confirm("Restaurar biblioteca","Este backup substituirá a biblioteca, o roteiro e a cena atuais. Uma cópia do arquivo atual será preservada. Continuar?"))return;
        _saveTimer.Stop();_restoring=true;((Control)Content!).IsEnabled=false;
        try
        {
            _document=await Task.Run(()=>_store.Restore(Local(files[0])));_startupError=null;LoadStyleControls();RefreshLibrary();RenderFrames();Status("Backup restaurado");
        }
        finally{_restoring=false;((Control)Content!).IsEnabled=true;}
    }
    private async Task SaveStyle()
    {
        var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="Salvar parâmetros da cena",SuggestedFileName="Minha cena.json",DefaultExtension="json",FileTypeChoices=[new FilePickerFileType("Cena JSON"){Patterns=["*.json"]}]});
        if(file==null)return;await File.WriteAllTextAsync(Local(file),JsonSerializer.Serialize(_document.Style,LibraryStore.JsonOptions));Status("Cena salva · referências de mídia usam a biblioteca deste computador");
    }
    private async Task LoadStyle()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Carregar parâmetros de cena",FileTypeFilter=[new FilePickerFileType("Cena JSON"){Patterns=["*.json"]}]});
        if(files.Count==0)return;
        var style=JsonSerializer.Deserialize<StageStyle>(await File.ReadAllTextAsync(Local(files[0])),LibraryStore.JsonOptions)??throw new InvalidDataException("Cena inválida.");
        var temp=new StudioDocument{Style=style};LibraryStore.Validate(temp);
        if(style.BackgroundMode is "Image" or "Video" && !File.Exists(style.BackgroundPath))throw new FileNotFoundException("A mídia desta cena não foi encontrada. Use o backup completo para transferir cenas com mídia.");
        _document.Style=style;LoadStyleControls();RenderFrames();ScheduleSave();Status("Cena carregada");
    }
    private static string Local(IStorageItem item)=>item.TryGetLocalPath()??throw new InvalidOperationException("Escolha um arquivo local.");

    private void ToggleRemote()
    {
        if(_remote.IsRunning){_remote.Stop();C<Button>("RemoteButton").Content="Iniciar controle remoto";C<TextBox>("RemoteAddress").Text="Controle remoto desligado";C<NumericUpDown>("RemotePortInput").IsEnabled=true;Status("Controle remoto encerrado · chave revogada");return;}
        _document.RemotePort=(int)(C<NumericUpDown>("RemotePortInput").Value??8765);
        _remote.Start(_document.RemotePort,()=>Volatile.Read(ref _remoteState),(command,index)=>Dispatcher.UIThread.Post(()=>
        {
            if(_closing)return;
            try
            {
                switch(command)
                {
                    case "next":Navigate(1);break;
                    case "previous":Navigate(-1);break;
                    case "blackout":ToggleBlackout();break;
                    case "clear":ToggleLyrics();break;
                    case "tap":_audio.Tap();break;
                    case "slide":if(index>=0&&index<_liveSlides.Count){StopFollowing();_liveIndex=index.Value;SetLiveText(_liveSlides[_liveIndex].Text);_hideLyrics=false;UpdateOutputUi();BuildSlideCards();RenderFrames();}break;
                }
            }
            catch(Exception ex){BackgroundError(ex.Message);}
        }));
        C<Button>("RemoteButton").Content="Desligar controle remoto";C<TextBox>("RemoteAddress").Text=string.Join("\n\n",_remote.Urls);C<NumericUpDown>("RemotePortInput").IsEnabled=false;ScheduleSave();Status("Controle remoto ativo na rede local");
    }
    private async Task ImportHolyrics()
    {
        var button=C<Button>("HolyricsImportButton");button.IsEnabled=false;
        try{var songs=await _holyrics.ImportLibraryAsync(C<TextBox>("HolyricsUrl").Text??"",C<TextBox>("HolyricsToken").Text??"");var count=AddImported(songs);ScheduleSave();RefreshLibrary();C<TextBlock>("HolyricsStatus").Text=$"{count} louvor(es) importado(s)";Status($"Holyrics: {count} louvor(es) importado(s)");}
        finally{button.IsEnabled=true;}
    }
    private async Task FollowHolyrics()
    {
        if(_followCts!=null)return;
        var cancellation=new CancellationTokenSource();_followCts=cancellation;
        var url=C<TextBox>("HolyricsUrl").Text??"";var token=C<TextBox>("HolyricsToken").Text??"";
        C<TextBlock>("HolyricsStatus").Text="Conectando…";
        try
        {
            EnsureProjection();
            await _holyrics.FollowAsync(url,token,p=>Dispatcher.UIThread.Post(()=>
            {
                if(cancellation.IsCancellationRequested||_closing)return;
                _liveSongId=null;_liveTitle=p.Title;_liveIndex=p.Index;_liveSlides=p.Slides.ToList();
                SetLiveText(p.Text);_blackout=p.Blackout;_hideLyrics=p.HideLyrics;UpdateOutputUi();RenderFrames();
                C<TextBlock>("HolyricsStatus").Text=$"Seguindo · {p.Title} · {p.Index+1}/{p.Count}";
            }),cancellation.Token);
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!_closing){C<TextBlock>("HolyricsStatus").Text="Falha: "+ex.Message;BackgroundError("Holyrics: "+ex.Message);}}
        finally
        {
            if(ReferenceEquals(_followCts,cancellation)){_followCts=null;if(!_closing)C<ToggleSwitch>("HolyricsFollowToggle").IsChecked=false;}
            cancellation.Dispose();
        }
    }
    private void StopFollowing()
    {
        _followCts?.Cancel();_followCts=null;
        if(!_closing){C<ToggleSwitch>("HolyricsFollowToggle").IsChecked=false;C<TextBlock>("HolyricsStatus").Text="Controle manual";}
    }
    private void ScheduleSave(){if(_loading||_restoring||_startupError!=null)return;_saveTimer.Stop();_saveTimer.Start();}
    private void Persist()
    {
        if(_startupError!=null||_restoring)return;
        try{_store.Save(_document);}
        catch(Exception ex){BackgroundError("Falha ao salvar a biblioteca: "+ex.Message);}
    }
    private void BackgroundError(string text){AppLog.Write(text);Dispatcher.UIThread.Post(()=>{if(!_closing)Status(text);});}
    private void Status(string text)=>C<TextBlock>("StatusText").Text=text;
    private void OnKeyDown(object? sender,KeyEventArgs e)
    {
        if(e.KeyModifiers!=KeyModifiers.None)return;
        if(e.Source is Control source && (source is TextBox or NumericUpDown or ComboBox or Slider || source.GetVisualAncestors().Any(a=>a is TextBox or NumericUpDown or ComboBox or Slider)))return;
        switch(e.Key)
        {
            case Key.Right:case Key.PageDown:case Key.Space:Navigate(1);break;
            case Key.Left:case Key.PageUp:Navigate(-1);break;
            case Key.B:ToggleBlackout();break;case Key.L:ToggleLyrics();break;
            case Key.F11:EnsureProjection();if(_projection!=null)_projection.WindowState=_projection.WindowState==WindowState.FullScreen?WindowState.Normal:WindowState.FullScreen;break;
            default:return;
        }
        e.Handled=true;
    }
    private void OnClosing(object? sender,WindowClosingEventArgs e)
    {
        if(_restoring){e.Cancel=true;Status("Aguarde a restauração terminar antes de fechar.");return;}
        _closing=true;_tick.Stop();_saveTimer.Stop();Persist();StopFollowing();_remote.Dispose();_audio.Dispose();_holyrics.Dispose();_preview.Dispose();_projection?.Close();
    }

    private async Task Message(string title,string text)
    {
        var dialog=DialogWindow(title,text);var button=new Button{Content="Entendi",Classes={"primary"},HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        ((StackPanel)dialog.Content!).Children.Add(button);button.Click+=(_,_)=>dialog.Close();await dialog.ShowDialog(this);
    }
    private async Task<bool> Confirm(string title,string text)
    {
        var dialog=DialogWindow(title,text);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8,Margin=new Thickness(0,20,0,0)};
        var cancel=new Button{Content="Cancelar"};var yes=new Button{Content="Continuar",Classes={"primary"}};buttons.Children.Add(cancel);buttons.Children.Add(yes);((StackPanel)dialog.Content!).Children.Add(buttons);
        cancel.Click+=(_,_)=>dialog.Close(false);yes.Click+=(_,_)=>dialog.Close(true);return await dialog.ShowDialog<bool>(this);
    }
    private static Window DialogWindow(string title,string text)=>new(){Title=title,Width=580,SizeToContent=SizeToContent.Height,MaxHeight=760,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new StackPanel{Margin=new Thickness(25),Children={new TextBlock{Text=title,FontSize=21,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,0,0,15)},new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=new SolidColorBrush(Color.Parse("#B9C5D6"))}}}};
}

internal sealed class SongEditor : Window
{
    public SongEditor(Song? song)
    {
        Title=song==null?"Novo louvor":"Editar louvor";Width=740;Height=760;MinWidth=520;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var title=new TextBox{Text=song?.Title,Watermark="Título do louvor"};var artist=new TextBox{Text=song?.Artist,Watermark="Autor / ministério / observação"};
        var lyrics=new TextBox{Text=song?.Lyrics,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,Watermark="[Estrofe 1]\nDigite a letra aqui\n\n[Refrão]\nSepare cada estrofe com uma linha em branco",FontSize=16,MinHeight=160};
        var count=new TextBlock{Foreground=Brushes.Gray};
        void Update()=>count.Text=$"{LyricParser.Parse(lyrics.Text).Count} estrofe(s) · use [Refrão] para nomear uma parte";
        lyrics.TextChanged+=(_,_)=>Update();Update();
        var save=new Button{Content="Salvar louvor",Classes={"primary"}};var cancel=new Button{Content="Cancelar"};
        var layout=new Grid{RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto"),Margin=new Thickness(24)};
        Add(new TextBlock{Text=song==null?"Criar louvor":"Editar letra",FontSize=25,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,0,0,18)},0);
        Add(title,1);Add(artist,2);Add(new TextBlock{Text="Uma linha em branco cria uma nova estrofe. A letra só entra ao vivo quando você a projetar.",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,12)},3);
        Add(lyrics,4);Add(count,5);
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8,Margin=new Thickness(0,15,0,0)};footer.Children.Add(cancel);footer.Children.Add(save);Add(footer,6);Content=layout;
        void Add(Control control,int row){Grid.SetRow(control,row);if(row is 1 or 2)control.Margin=new Thickness(0,0,0,8);layout.Children.Add(control);}
        cancel.Click+=(_,_)=>Close();save.Click+=(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(title.Text)){title.Focus();return;}
            if(LyricParser.Parse(lyrics.Text).Count==0){lyrics.Focus();count.Text="Digite pelo menos uma estrofe antes de salvar.";return;}
            Close(new Song{Id=song?.Id??Guid.NewGuid(),Title=title.Text.Trim(),Artist=artist.Text?.Trim()??"",Lyrics=lyrics.Text!.Trim()});
        };
    }
}
