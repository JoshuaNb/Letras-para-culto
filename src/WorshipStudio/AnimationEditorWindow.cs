using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using WorshipStudio.Controls;
using WorshipStudio.Models;

namespace WorshipStudio;

/// <summary>Edits a staged animation with the same renderer used by the live output.</summary>
public sealed class AnimationEditorWindow : Window
{
    private StageStyle _style;
    private readonly StageRenderer _preview = new();
    private readonly WrapPanel _cards = new() { Orientation=Orientation.Horizontal };
    private readonly List<(AnimationPreset Preset,Button Button,StageRenderer Renderer)> _items=[];
    private readonly TextBox _search=new(){Watermark="Buscar animação…"};
    private readonly ComboBox _category=new(){HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly ComboBox _granularity=new(){ItemsSource=new[]{"Por letra", "Por palavra", "Por linha", "Bloco inteiro"},HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly ComboBox _order=new(){ItemsSource=new[]{"Do início ao fim", "Do fim ao início", "Do centro para fora", "Aleatória (repetível)"},HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly ComboBox _easing=new(){ItemsSource=new[]{"Linear", "Desacelerar", "Suavizar início e fim", "Ultrapassar e voltar", "Elástica", "Quique"},HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly NumericUpDown _duration=Number(0,5,.05m);
    private readonly NumericUpDown _stagger=Number(0,.5m,.01m);
    private readonly NumericUpDown _delay=Number(0,3,.05m);
    private readonly NumericUpDown _pause=Number(0,8,.1m);
    private readonly Slider _strength=new(){Minimum=0,Maximum=2,Value=1};
    private readonly CheckBox _loop=new(){Content="Repetir na projeção"};
    private readonly TextBox _text=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=72,MaxHeight=108};
    private readonly TextBlock _title=new(){FontSize=21,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock _summary=new(){FontSize=11,Foreground=Brush.Parse("#94A3BB"),TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock _strengthValue=new(){Foreground=Brush.Parse("#A895FF"),FontSize=11};
    private readonly TextBlock _count=new(){FontSize=10,Foreground=Brush.Parse("#8B9BB1")};
    private readonly Slider _timeline=new(){Minimum=0,Maximum=1,Value=0};
    private readonly TextBlock _timeLabel=new(){FontSize=10,Foreground=Brush.Parse("#8B9BB1")};
    private readonly Button _play=new(){Content="Pausar"};
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock=Stopwatch.StartNew();
    private double _offset;
    private double _entryDuration;
    private bool _loading=true,_playing=true,_movingTimeline;
    private readonly string[] _units=["Character","Word","Line","Text"];
    private readonly string[] _orders=["Forward","Reverse","Center","Random"];
    private readonly string[] _easings=["Linear","EaseOut","EaseInOut","Back","Elastic","Bounce"];
    private readonly Dictionary<string,string> _categories=new();

    public AnimationEditorWindow(StageStyle style,string text)
    {
        _style=style with{};
        if(!_style.UsePresetAnimation)_style=AnimationPresets.ApplyDefaults(_style,AnimationPresets.Find(_style.AnimationPreset));
        Title="Worship Studio • Animações das letras";
        Width=1180;Height=900;MinWidth=1020;MinHeight=740;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        _text.Text=string.IsNullOrWhiteSpace(text)?"Tua presença\nEnche este lugar":text;
        var shell=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto")};
        var heading=new StackPanel{Margin=new Thickness(24,20,24,16),Spacing=5};
        heading.Children.Add(new TextBlock{Text="Animações das letras",FontSize=27,FontWeight=FontWeight.SemiBold});
        heading.Children.Add(new TextBlock{Text="Escolha um estilo e controle como cada letra, palavra ou linha entra em cena.",Foreground=Brush.Parse("#91A2B8"),FontSize=12});
        shell.Children.Add(heading);
        var columns=new Grid{ColumnDefinitions=new ColumnDefinitions("1.65*,1*"),Margin=new Thickness(20,0,20,0)};
        Grid.SetRow(columns,1);shell.Children.Add(columns);
        var gallery=new Grid{RowDefinitions=new RowDefinitions("Auto,Auto,*"),Margin=new Thickness(0,0,18,0)};
        var filters=new Grid{ColumnDefinitions=new ColumnDefinitions("*,12,*")};
        filters.Children.Add(_search);Grid.SetColumn(_category,2);filters.Children.Add(_category);gallery.Children.Add(filters);
        _categories["Todas as categorias"]="";
        foreach(var preset in AnimationPresets.All)
            if(!_categories.Values.Contains(preset.Category))_categories[preset.CategoryLabel]=preset.Category;
        _category.ItemsSource=_categories.Keys.ToArray();_category.SelectedIndex=0;
        _count.Margin=new Thickness(2,10,0,10);Grid.SetRow(_count,1);gallery.Children.Add(_count);
        var scroll=new ScrollViewer{Content=_cards,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        Grid.SetRow(scroll,2);gallery.Children.Add(scroll);columns.Children.Add(gallery);
        var editor=new StackPanel{Spacing=11,Margin=new Thickness(18,0,3,16)};
        editor.Children.Add(_title);
        editor.Children.Add(new TextBlock{Text="PRÉVIA DAS LETRAS · FUNDO NEUTRO",Classes={"caption"}});
        var previewHost=new AspectRatioPanel();previewHost.Children.Add(_preview);
        editor.Children.Add(new Border{Height=205,Background=Brushes.Black,CornerRadius=new CornerRadius(8),ClipToBounds=true,Child=previewHost});
        var controls=new Grid{ColumnDefinitions=new ColumnDefinitions("Auto,8,Auto,*,Auto")};
        var replay=new Button{Content="Recomeçar"};controls.Children.Add(_play);Grid.SetColumn(replay,2);controls.Children.Add(replay);
        Grid.SetColumn(_timeLabel,4);_timeLabel.VerticalAlignment=VerticalAlignment.Center;controls.Children.Add(_timeLabel);editor.Children.Add(controls);
        editor.Children.Add(_timeline);
        editor.Children.Add(Field("TEXTO PARA TESTAR",_text));
        editor.Children.Add(Field("GRANULARIDADE",_granularity));
        editor.Children.Add(TwoFields("Duração por unidade (s)",_duration,"Intervalo entre unidades (s)",_stagger));
        editor.Children.Add(_summary);
        editor.Children.Add(TwoFields("Atraso inicial (s)",_delay,"Pausa na repetição (s)",_pause));
        editor.Children.Add(Field("ORDEM DE APARIÇÃO",_order));
        editor.Children.Add(Field("CURVA DO MOVIMENTO",_easing));
        var strengthHeader=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};strengthHeader.Children.Add(new TextBlock{Text="Intensidade do efeito"});Grid.SetColumn(_strengthValue,1);strengthHeader.Children.Add(_strengthValue);
        editor.Children.Add(strengthHeader);editor.Children.Add(_strength);editor.Children.Add(_loop);
        editor.Children.Add(new TextBlock{Text="A prévia repete o efeito para facilitar o ajuste. O botão Repetir na projeção controla a saída ao vivo e o MP4. As mudanças só entram na cena ao aplicar.",FontSize=10,Foreground=Brush.Parse("#8394AD"),TextWrapping=TextWrapping.Wrap});
        var rightScroll=new ScrollViewer{Content=editor,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};
        Grid.SetColumn(rightScroll,1);columns.Children.Add(rightScroll);
        var footer=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,10,Auto"),Margin=new Thickness(24,14,24,18)};
        footer.Children.Add(new TextBlock{Text="Passe o mouse nos cartões para ver o movimento.",FontSize=11,Foreground=Brush.Parse("#8696AE"),VerticalAlignment=VerticalAlignment.Center});
        var cancel=new Button{Content="Cancelar"};var apply=new Button{Content="Aplicar à cena",Classes={"primary"}};
        Grid.SetColumn(cancel,1);Grid.SetColumn(apply,3);footer.Children.Add(cancel);footer.Children.Add(apply);Grid.SetRow(footer,2);shell.Children.Add(footer);Content=shell;
        _preview.AnimationEnabled=false;
        BuildCards();LoadParameters();
        _search.TextChanged+=(_,_)=>FilterCards();_category.SelectionChanged+=(_,_)=>FilterCards();
        _granularity.SelectionChanged+=(_,_)=>ParametersChanged();_order.SelectionChanged+=(_,_)=>ParametersChanged();_easing.SelectionChanged+=(_,_)=>ParametersChanged();
        foreach(var number in new[]{_duration,_stagger,_delay,_pause})number.ValueChanged+=(_,_)=>ParametersChanged();
        _loop.IsCheckedChanged+=(_,_)=>ParametersChanged();
        _strength.PropertyChanged+=(_,e)=>{if(e.Property==Slider.ValueProperty)ParametersChanged();};
        _text.TextChanged+=(_,_)=>RestartPreview();
        _play.Click+=(_,_)=>{if(_playing){_offset=Elapsed;_clock.Stop();_playing=false;}else{_clock.Restart();_playing=true;}_play.Content=_playing?"Pausar":"Reproduzir";};
        replay.Click+=(_,_)=>RestartPreview();
        _timeline.PropertyChanged+=(_,e)=>
        {
            if(e.Property!=Slider.ValueProperty||_movingTimeline)return;
            _offset=_timeline.Value;_playing=false;_clock.Reset();_play.Content="Reproduzir";DrawPreview(_offset);
        };
        cancel.Click+=(_,_)=>Close();apply.Click+=(_,_)=>{ReadParameters();Close(_style with{});};
        _timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};
        _timer.Tick+=(_,_)=>
        {
            if(!_playing)return;
            var duration=CycleLength;
            var time=Elapsed%duration;
            DrawPreview(time);
            _movingTimeline=true;_timeline.Maximum=duration;_timeline.Value=time;_movingTimeline=false;
        };
        Opened+=(_,_)=>_timer.Start();
        Closed+=(_,_)=>{_timer.Stop();_preview.Dispose();foreach(var item in _items)item.Renderer.Dispose();};
        _loading=false;RestartPreview();
    }

    private double Elapsed=>_offset+(_playing?_clock.Elapsed.TotalSeconds:0);
    private double Duration=>_entryDuration;
    private double CycleLength=>Math.Max(.3,Duration+Math.Max(.6,_style.AnimationLoopPause));
    private static NumericUpDown Number(decimal minimum,decimal maximum,decimal step)=>new(){Minimum=minimum,Maximum=maximum,Increment=step,FormatString="0.00",HorizontalAlignment=HorizontalAlignment.Stretch};
    private static Control Field(string label,Control input)=>new StackPanel{Spacing=6,Children={new TextBlock{Text=label,Classes={"caption"}},input}};
    private static Control TwoFields(string a,Control first,string b,Control second)
    {
        var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("*,12,*")};
        var left=new StackPanel{Spacing=5,Children={new TextBlock{Text=a,FontSize=10,TextWrapping=TextWrapping.Wrap},first}};
        var right=new StackPanel{Spacing=5,Children={new TextBlock{Text=b,FontSize=10,TextWrapping=TextWrapping.Wrap},second}};
        Grid.SetColumn(right,2);grid.Children.Add(left);grid.Children.Add(right);return grid;
    }

    private void BuildCards()
    {
        foreach(var preset in AnimationPresets.All)
        {
            var style=AnimationPresets.ApplyDefaults(_style,preset) with{BackgroundMode="Solid",BackgroundPath=null,BackgroundColor="#000000",TextColor="#FFFFFF",FontFamily="Segoe UI",FontSize=140,Bold=true,Dim=0,AnimationLoop=false,LyricAnimation="None"};
            var renderer=new StageRenderer{Height=86,AnimationEnabled=false};
            renderer.SetFrame(new StageFrame{Text="Aa",Style=style});
            renderer.SetRenderTime(0,1,Math.Max(1,StageRenderer.GetAnimationDuration("Aa",style)));
            var stack=new StackPanel{Spacing=7};
            stack.Children.Add(new Border{Background=Brushes.Black,CornerRadius=new CornerRadius(6),ClipToBounds=true,Child=renderer});
            stack.Children.Add(new TextBlock{Text=preset.Name,FontSize=12,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap,MinHeight=30});
            stack.Children.Add(new TextBlock{Text=preset.CategoryLabel.ToUpperInvariant(),FontSize=9,LetterSpacing=1,Foreground=Brush.Parse("#8190AC")});
            var button=new Button{Content=stack,Width=191,Height=163,Padding=new Thickness(11),Margin=new Thickness(0,0,9,9),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Background=Brush.Parse("#181923"),BorderBrush=Brush.Parse("#30303D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10)};
            Avalonia.Automation.AutomationProperties.SetName(button,"Animação: "+preset.Name);
            button.Click+=(_,_)=>SelectPreset(preset);
            button.PointerEntered+=(_,_)=>
            {
                renderer.SetFrame(new StageFrame{Text="Aa",Style=style with{AnimationLoop=true,AnimationLoopPause=.7}});
                renderer.SetRenderTime(null);renderer.ReplayTransition();renderer.AnimationEnabled=true;
            };
            button.PointerExited+=(_,_)=>
            {
                renderer.AnimationEnabled=false;renderer.SetFrame(new StageFrame{Text="Aa",Style=style});renderer.SetRenderTime(0,1,Math.Max(1,StageRenderer.GetAnimationDuration("Aa",style)));
            };
            _items.Add((preset,button,renderer));_cards.Children.Add(button);
        }
        Highlight();FilterCards();
    }

    private void FilterCards()
    {
        var query=_search.Text?.Trim()??"";
        var category=_category.SelectedItem is string name&&_categories.TryGetValue(name,out var key)?key:"";
        var count=0;
        foreach(var item in _items)
        {
            item.Button.IsVisible=(category.Length==0||item.Preset.Category==category)&&(query.Length==0||item.Preset.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase));
            if(item.Button.IsVisible)count++;else item.Renderer.AnimationEnabled=false;
        }
        _count.Text=$"{count} estilos · prévia real ao passar o mouse";
    }

    private void SelectPreset(AnimationPreset preset)
    {
        _style=AnimationPresets.ApplyDefaults(_style,preset);LoadParameters();Highlight();RestartPreview();
    }
    private void Highlight()
    {
        foreach(var item in _items)
        {
            var selected=item.Preset.Id==_style.AnimationPreset;
            item.Button.BorderBrush=Brush.Parse(selected?"#9A6BFF":"#30303D");item.Button.Background=Brush.Parse(selected?"#251D38":"#181923");
        }
        _title.Text=AnimationPresets.Find(_style.AnimationPreset).Name;
    }
    private void LoadParameters()
    {
        var previous=_loading;_loading=true;
        _granularity.SelectedIndex=Math.Max(0,Array.IndexOf(_units,_style.AnimationGranularity));
        _order.SelectedIndex=Math.Max(0,Array.IndexOf(_orders,_style.AnimationOrder));
        _easing.SelectedIndex=Math.Max(0,Array.IndexOf(_easings,_style.AnimationEasing));
        _duration.Value=(decimal)_style.AnimationDuration;_stagger.Value=(decimal)_style.AnimationStagger;_delay.Value=(decimal)_style.AnimationDelay;_pause.Value=(decimal)_style.AnimationLoopPause;
        _strength.Value=_style.AnimationStrength;_loop.IsChecked=_style.AnimationLoop;_loading=previous;
    }
    private void ReadParameters()
    {
        _style=_style with
        {
            UsePresetAnimation=true,
            AnimationGranularity=_units[Math.Clamp(_granularity.SelectedIndex,0,_units.Length-1)],
            AnimationOrder=_orders[Math.Clamp(_order.SelectedIndex,0,_orders.Length-1)],
            AnimationEasing=_easings[Math.Clamp(_easing.SelectedIndex,0,_easings.Length-1)],
            AnimationDuration=(double)(_duration.Value??.7m),AnimationStagger=(double)(_stagger.Value??.06m),AnimationDelay=(double)(_delay.Value??0),AnimationLoopPause=(double)(_pause.Value??1),
            AnimationStrength=_strength.Value,AnimationLoop=_loop.IsChecked==true
        };
    }
    private void ParametersChanged(){if(_loading)return;ReadParameters();RestartPreview();}
    private void RestartPreview()
    {
        if(_loading)return;
        _offset=0;_clock.Restart();_playing=true;_play.Content="Pausar";
        var units=StageRenderer.GetAnimationUnits(_text.Text??"",_style).Count;
        _entryDuration=StageRenderer.GetAnimationDuration(_text.Text??"",_style);
        _summary.Text=$"{units} unidade(s) · entrada completa em {Duration:0.00} s\nIntervalo aplicado separadamente a cada unidade.";
        _strengthValue.Text=$"{_style.AnimationStrength:P0}";
        _movingTimeline=true;_timeline.Maximum=CycleLength;_timeline.Value=0;_movingTimeline=false;
        DrawPreview(0);
    }
    private void DrawPreview(double cueElapsed)
    {
        var style=_style with{BackgroundMode="Solid",BackgroundPath=null,BackgroundColor="#070910",Dim=0,AnimationLoop=false,LyricAnimation="None"};
        _preview.SetFrame(new StageFrame{Text=_text.Text??"",Style=style});
        _preview.SetRenderTime(cueElapsed,1,cueElapsed);_preview.InvalidateVisual();
        _timeLabel.Text=$"{cueElapsed:0.00}s / {Duration:0.00}s";
    }
}
