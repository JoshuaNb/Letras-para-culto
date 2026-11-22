using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WorshipStudio.Services;

namespace WorshipStudio;

/// <summary>Local inbox. Incoming files are never opened or applied automatically.</summary>
public sealed class ReceivedFilesWindow : Window
{
    private readonly ReceivedFileStore _store;
    private readonly Func<ReceivedFile,Task> _background, _lyrics;
    private readonly ListBox _files=new(){Name="ReceivedFilesList",SelectionMode=SelectionMode.Single};
    private readonly TextBlock _status=new(){Name="ReceivedFilesStatus",TextWrapping=TextWrapping.Wrap,Classes={"muted"},FontSize=12};
    private readonly Button _useBackground=new(){Name="ReceivedUseBackground",Content="Usar como fundo",IsEnabled=false,Classes={"primary"}};
    private readonly Button _importLyrics=new(){Name="ReceivedImportLyrics",Content="Importar letras",IsEnabled=false};
    private readonly Button _saveCopy=new(){Name="ReceivedSaveCopy",Content="Salvar cópia…",IsEnabled=false};
    private readonly Button _refresh=new(){Name="ReceivedRefresh",Content="Atualizar lista"};
    private readonly CancellationTokenSource _lifetime=new();
    private bool _busy,_closed,_refreshing,_refreshPending;
    private static readonly string[] Images=[".png",".jpg",".jpeg",".webp",".bmp"];
    private static readonly string[] Videos=[".mp4",".mov",".mkv",".webm",".avi"];

    public ReceivedFilesWindow(ReceivedFileStore store,Func<ReceivedFile,Task> useBackground,Func<ReceivedFile,Task> importLyrics)
    {
        _store=store;_background=useBackground;_lyrics=importLyrics;
        Title="Arquivos recebidos do celular";Width=830;Height=650;MinWidth=650;MinHeight=490;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var header=new StackPanel{Spacing=8,Children={
            new TextBlock{Text="CELULAR → COMPUTADOR",Classes={"caption"}},
            new TextBlock{Text="Arquivos recebidos",FontSize=25,FontWeight=FontWeight.SemiBold},
            new TextBlock{Text="No celular conectado, toque em Enviar arquivos. Os arquivos ficam guardados neste PC; escolha abaixo o que deseja usar.",TextWrapping=TextWrapping.Wrap,Classes={"muted"},FontSize=12}}};
        _files.ItemTemplate=new FuncDataTemplate<ReceivedFile>((file,_)=>file==null?new TextBlock():new StackPanel{Spacing=4,Margin=new Thickness(8),Children={
            new TextBlock{Text=file.DisplayName,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap},
            new TextBlock{Text=FormatBytes(file.Size)+" · "+file.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),FontSize=11,Classes={"muted"}}}});
        var border=new Border{Child=_files,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6)};
        border.Bind(Border.BorderBrushProperty,new DynamicResourceExtension("WsBorder"));border.Bind(Border.BackgroundProperty,new DynamicResourceExtension("WsInput"));
        var actions=new WrapPanel{Orientation=Orientation.Horizontal,Children={_useBackground,_importLyrics,_saveCopy}};
        foreach(var control in actions.Children)control.Margin=new Thickness(0,0,8,8);
        var folder=new Button{Name="ReceivedOpenFolder",Content="Abrir pasta"};
        var close=new Button{Name="ReceivedClose",Content="Fechar"};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,HorizontalAlignment=HorizontalAlignment.Right,Children={_refresh,folder,close}};
        var layout=new Grid{Margin=new Thickness(22),RowDefinitions=new RowDefinitions("Auto,*,Auto,Auto,Auto")};
        layout.Children.Add(header);Add(layout,border,1);Add(layout,actions,2);Add(layout,_status,3);Add(layout,footer,4);Content=layout;
        _files.SelectionChanged+=(_,_)=>UpdateActions();
        _refresh.Click+=(_,_)=>RefreshFiles();
        close.Click+=(_,_)=>Close();
        _useBackground.Click+=async(_,_)=>await Perform(_background,"Fundo atualizado. O arquivo recebido continua guardado.");
        _importLyrics.Click+=async(_,_)=>await Perform(_lyrics,"Importação concluída. Os louvores aparecem na biblioteca.");
        _saveCopy.Click+=async(_,_)=>await Perform(SaveCopy,"Cópia salva no local escolhido.");
        folder.Click+=(_,_)=>
        {
            try
            {
                var directory=_files.SelectedItem is ReceivedFile selected?Path.GetDirectoryName(_store.GetFilePath(selected.Id))!:_store.DirectoryPath;
                Directory.CreateDirectory(directory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory){UseShellExecute=true});
            }
            catch(Exception ex){_status.Text="Não foi possível abrir a pasta: "+ex.Message;}
        };
        Closed+=(_,_)=>{_closed=true;_lifetime.Cancel();};
        RefreshFiles();
    }

    public static bool IsBackground(string name)=>Images.Contains(Path.GetExtension(name).ToLowerInvariant())||Videos.Contains(Path.GetExtension(name).ToLowerInvariant());
    public static bool IsImage(string name)=>Images.Contains(Path.GetExtension(name).ToLowerInvariant());
    public static bool IsLyrics(string name)=>new[]{".txt",".json",".xml"}.Contains(Path.GetExtension(name).ToLowerInvariant());
    public static string FormatBytes(long bytes)=>bytes>=1024L*1024*1024?$"{bytes/(1024d*1024*1024):0.0} GB":bytes>=1024*1024?$"{bytes/(1024d*1024):0.0} MB":bytes>=1024?$"{bytes/1024d:0.0} KB":$"{bytes} bytes";

    public void RefreshFiles()
    {
        if(_closed)return;
        if(_busy||_refreshing){_refreshPending=true;return;}
        _=RefreshFilesAsync();
    }

    private async Task RefreshFilesAsync()
    {
        _refreshing=true;UpdateActions();_status.Text="Buscando arquivos recebidos…";
        try
        {
            var files=await Task.Run(()=>_store.List(100),_lifetime.Token);
            if(_closed)return;
            if(_busy){_refreshPending=true;return;}
            var id=(_files.SelectedItem as ReceivedFile)?.Id;
            _files.ItemsSource=files;
            _files.SelectedItem=files.FirstOrDefault(f=>f.Id==id);
            _status.Text=files.Count==0?"Nenhum arquivo recebido. Inicie o controle remoto no PC e envie pelo celular.":$"{files.Count} arquivo(s) mais recente(s) · até 512 MB por arquivo · pasta com limite de 5 GB.";
            UpdateActions();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!_closed)_status.Text="Não foi possível ler os arquivos recebidos: "+ex.Message;}
        finally
        {
            _refreshing=false;
            if(!_closed){UpdateActions();if(_refreshPending&&!_busy){_refreshPending=false;RefreshFiles();}}
        }
    }

    private async Task Perform(Func<ReceivedFile,Task> action,string success)
    {
        if(_busy||_files.SelectedItem is not ReceivedFile file)return;
        _busy=true;UpdateActions();_status.Text="Aguarde…";
        try{await action(file);if(!_closed)_status.Text=success;}
        catch(OperationCanceledException){if(!_closed)_status.Text="Operação cancelada.";}
        catch(Exception ex){if(!_closed)_status.Text="Não foi possível concluir: "+ex.Message;}
        finally{_busy=false;if(!_closed){UpdateActions();if(_refreshPending){_refreshPending=false;RefreshFiles();}}}
    }

    private async Task SaveCopy(ReceivedFile file)
    {
        var selected=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="Salvar cópia do arquivo recebido",SuggestedFileName=file.DisplayName});
        if(selected==null)throw new OperationCanceledException();
        var destination=selected.TryGetLocalPath()??throw new IOException("Escolha um destino local.");
        var source=_store.GetFilePath(file.Id);
        if(Path.GetFullPath(source).Equals(Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))throw new IOException("Escolha outra pasta para a cópia.");
        var pending=destination+"."+Guid.NewGuid().ToString("N")+".partial";
        try
        {
            await using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,true))
            await using(var output=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,true))
                await input.CopyToAsync(output,_lifetime.Token);
            _lifetime.Token.ThrowIfCancellationRequested();File.Move(pending,destination,true);
        }
        finally{try{if(File.Exists(pending))File.Delete(pending);}catch(IOException){}}
    }

    private void UpdateActions()
    {
        var file=_files.SelectedItem as ReceivedFile;
        _useBackground.IsEnabled=!_busy&&file!=null&&IsBackground(file.DisplayName);
        _importLyrics.IsEnabled=!_busy&&file!=null&&IsLyrics(file.DisplayName);
        _saveCopy.IsEnabled=!_busy&&file!=null;_refresh.IsEnabled=!_busy&&!_refreshing;_files.IsEnabled=!_busy;
    }
    private static void Add(Grid grid,Control control,int row){Grid.SetRow(control,row);control.Margin=new Thickness(0,12,0,0);grid.Children.Add(control);}
}
