using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using WorshipStudio.Models;
using WorshipStudio.Services;

namespace WorshipStudio;

/// <summary>Selects lyrics from an operator-authorized Holyrics API Server. Does not modify either library.</summary>
public sealed class HolyricsImportWindow : Window
{
    private readonly HolyricsService _service;
    private readonly bool _ownsService;
    private readonly TextBox _url = new() { Name="HolyricsDownloadUrl",Watermark="http://127.0.0.1:8091" };
    private readonly TextBox _token = new() { Name="HolyricsDownloadToken",Watermark="Token com permissão GetSongs e GetLyrics",PasswordChar='●' };
    private readonly TextBox _search = new() { Name="HolyricsCatalogSearch",Watermark="Buscar título ou artista…",IsEnabled=false };
    private readonly Button _connect = new() { Name="HolyricsCatalogConnect",Content="Buscar músicas",Classes={"primary"} };
    private readonly Button _download = new() { Name="HolyricsCatalogDownload",Content="Baixar letras selecionadas",Classes={"primary"},IsEnabled=false };
    private readonly Button _cancel = new() { Name="HolyricsCatalogCancel",Content="Fechar" };
    private readonly Button _selectVisible = new() { Content="Selecionar visíveis",IsEnabled=false };
    private readonly Button _clear = new() { Content="Limpar seleção",IsEnabled=false };
    private readonly TextBlock _count = new() { Name="HolyricsCatalogCount",Text="Busque a biblioteca do Holyrics para escolher as músicas.",FontSize=11,TextWrapping=TextWrapping.Wrap,Classes={"muted"} };
    private readonly TextBlock _status = new() { Name="HolyricsCatalogStatus",Text="Nenhuma letra será adicionada até o download terminar.",FontSize=12,TextWrapping=TextWrapping.Wrap,Classes={"muted"} };
    private readonly ProgressBar _progress = new() { Name="HolyricsCatalogProgress",Minimum=0,Maximum=1,Height=6 };
    private readonly ListBox _list = new() { Name="HolyricsCatalogList",SelectionMode=SelectionMode.Single };
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private IReadOnlyList<HolyricsCatalogSong> _catalog = Array.Empty<HolyricsCatalogSong>();
    private string? _catalogUrl,_catalogToken;
    private CancellationTokenSource? _operation;
    private bool _busy,_closed;

    public string BaseUrl => _url.Text?.Trim() ?? "";
    public string AccessToken => _token.Text ?? "";

    public HolyricsImportWindow(string baseUrl="http://127.0.0.1:8091",string token="",HolyricsService? service=null)
    {
        _ownsService=service==null;_service=service??new HolyricsService();
        Title="Baixar letras do Holyrics";Width=820;Height=770;MinWidth=620;MinHeight=570;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        _url.Text=string.IsNullOrWhiteSpace(baseUrl)?"http://127.0.0.1:8091":baseUrl;_token.Text=token;
        var heading=new StackPanel{Spacing=8,Children={
            new TextBlock{Text="HOLYRICS → BIBLIOTECA LOCAL",Classes={"caption"}},
            new TextBlock{Text="Escolha os louvores para baixar",FontSize=24,FontWeight=FontWeight.SemiBold},
            new TextBlock{Text="Importe as letras da biblioteca do Holyrics aberto neste computador ou na mesma rede. Depois de importar, elas ficam disponíveis offline no Worship Studio.",TextWrapping=TextWrapping.Wrap,Classes={"muted"},FontSize=12}}};
        var connection=new Grid{ColumnDefinitions=new ColumnDefinitions("*,12,*"),RowDefinitions=new RowDefinitions("Auto,Auto,Auto")};
        Add(connection,new TextBlock{Text="Endereço do API Server",FontSize=11},0,0);Add(connection,new TextBlock{Text="Token de leitura",FontSize=11},0,2);
        Add(connection,_url,1,0);Add(connection,_token,1,2);_url.Margin=_token.Margin=new Thickness(0,6,0,8);
        var connectRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,Children={_connect,new TextBlock{Text="Holyrics: Arquivo → Configurações → API Server",VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Classes={"muted"},FontSize=11}}};
        Add(connection,connectRow,2,0);Grid.SetColumnSpan(connectRow,3);
        var searchRow=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto")};searchRow.Children.Add(_search);
        Add(searchRow,_selectVisible,0,1);_selectVisible.Margin=new Thickness(8,0,0,0);Add(searchRow,_clear,0,2);_clear.Margin=new Thickness(8,0,0,0);
        _list.ItemTemplate=new FuncDataTemplate<HolyricsCatalogSong>((song,_)=>
        {
            if(song==null)return new TextBlock();
            var text=new StackPanel{Spacing=3,Children={new TextBlock{Text=song.Title,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap},new TextBlock{Text=string.IsNullOrWhiteSpace(song.Artist)?"Sem artista informado":song.Artist,FontSize=11,Classes={"muted"},TextTrimming=TextTrimming.CharacterEllipsis}}};
            var check=new CheckBox{Content=text,IsChecked=_selected.Contains(song.Id),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(3,7),Tag=song.Id};
            check.IsCheckedChanged+=(_,_)=>{if(check.IsChecked==true)_selected.Add(song.Id);else _selected.Remove(song.Id);UpdateCount();};
            return check;
        });
        var listBorder=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Child=_list};
        listBorder.Bind(Border.BorderBrushProperty,new DynamicResourceExtension("WsBorder"));listBorder.Bind(Border.BackgroundProperty,new DynamicResourceExtension("WsInput"));
        var footer=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto")};
        footer.Children.Add(new TextBlock{Text="Leva letras e estrofes; não baixa áudio ou MP3.",TextWrapping=TextWrapping.Wrap,Classes={"muted"},FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,15,0)});
        Add(footer,_cancel,0,1);Add(footer,_download,0,2);_download.Margin=new Thickness(8,0,0,0);
        var layout=new Grid{RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto,Auto"),Margin=new Thickness(26)};
        Add(layout,heading,0,0);Add(layout,connection,1,0);connection.Margin=new Thickness(0,20,0,16);
        Add(layout,searchRow,2,0);Add(layout,_count,3,0);_count.Margin=new Thickness(0,9,0,9);
        Add(layout,listBorder,4,0);Add(layout,_progress,5,0);_progress.Margin=new Thickness(0,14,0,10);
        Add(layout,_status,6,0);Add(layout,footer,7,0);footer.Margin=new Thickness(0,18,0,0);Content=layout;
        _connect.Click+=async(_,_)=>await LoadCatalog();_download.Click+=async(_,_)=>await Download();
        _search.TextChanged+=(_,_)=>RefreshList();
        _selectVisible.Click+=(_,_)=>{foreach(var song in HolyricsService.FilterCatalog(_catalog,_search.Text))_selected.Add(song.Id);RefreshList();};
        _clear.Click+=(_,_)=>{_selected.Clear();RefreshList();};
        _cancel.Click+=(_,_)=>{if(_busy)_operation?.Cancel();else Close();};
        _url.TextChanged+=(_,_)=>InvalidateCatalog();_token.TextChanged+=(_,_)=>InvalidateCatalog();
        Closing+=(_,_)=>{_closed=true;_operation?.Cancel();};
        Closed+=(_,_)=>{if(_ownsService)_service.Dispose();};
    }

    private static void Add(Grid grid,Control control,int row,int column)
    {Grid.SetRow(control,row);Grid.SetColumn(control,column);grid.Children.Add(control);}

    private void InvalidateCatalog()
    {
        if(_busy||_catalogUrl==null)return;
        _catalog=Array.Empty<HolyricsCatalogSong>();_selected.Clear();_catalogUrl=_catalogToken=null;
        RefreshList();_status.Text="O endereço ou token mudou. Busque novamente antes de baixar.";
    }

    private void RefreshList()
    {
        var visible=HolyricsService.FilterCatalog(_catalog,_search.Text);_list.ItemsSource=visible;UpdateCount(visible.Count);
    }
    private void UpdateCount(int? visible=null)
    {
        var count=visible??HolyricsService.FilterCatalog(_catalog,_search.Text).Count;
        _count.Text=$"{count} de {_catalog.Count} música(s) · {_selected.Count} selecionada(s)";
        _download.IsEnabled=!_busy&&_selected.Count>0;
    }
    private void SetBusy(bool busy,string action="")
    {
        _busy=busy;_url.IsEnabled=_token.IsEnabled=_connect.IsEnabled=!busy;
        _search.IsEnabled=_selectVisible.IsEnabled=_clear.IsEnabled=_list.IsEnabled=!busy&&_catalog.Count>0;
        _cancel.Content=busy?"Cancelar "+action:"Fechar";UpdateCount();
    }

    private async Task LoadCatalog()
    {
        if(_busy)return;
        using var operation=new CancellationTokenSource();_operation=operation;SetBusy(true,"busca");_progress.IsIndeterminate=true;
        try
        {
            _status.Text="Buscando as músicas da biblioteca do Holyrics…";
            var catalog=await _service.GetCatalogAsync(BaseUrl,AccessToken,operation.Token);
            if(_closed)return;
            _catalog=catalog;_catalogUrl=BaseUrl;_catalogToken=AccessToken;_selected.Clear();RefreshList();
            _status.Text=catalog.Count==0?"A biblioteca do Holyrics está vazia ou não há músicas com identificador válido.":"Marque os louvores desejados. A busca filtra a lista sem precisar consultar o Holyrics novamente.";
        }
        catch(OperationCanceledException){if(!_closed)_status.Text="Busca cancelada. Sua biblioteca não foi alterada.";}
        catch(Exception ex){if(!_closed)_status.Text=ex.Message;}
        finally{if(!_closed){_progress.IsIndeterminate=false;_progress.Value=0;SetBusy(false);}_operation=null;}
    }

    private async Task Download()
    {
        if(_busy||_selected.Count==0||_catalogUrl==null||_catalogToken==null)return;
        using var operation=new CancellationTokenSource();_operation=operation;SetBusy(true,"download");_progress.Value=0;
        try
        {
            var batch=_catalog.Where(song=>_selected.Contains(song.Id)).ToArray();
            var progress=new Progress<HolyricsDownloadProgress>(p=>
            {
                if(_closed)return;_progress.Value=p.Fraction;
                _status.Text=$"Baixando {p.Completed}/{p.Total} · {p.Title}";
            });
            var songs=await _service.DownloadSongsAsync(_catalogUrl,_catalogToken,batch,progress,operation.Token);
            if(!_closed)Close(songs);
        }
        catch(OperationCanceledException){if(!_closed)_status.Text="Download cancelado. Nenhuma letra desta seleção foi adicionada.";}
        catch(Exception ex){if(!_closed)_status.Text=ex.Message;}
        finally{if(!_closed)SetBusy(false);_operation=null;}
    }
}
