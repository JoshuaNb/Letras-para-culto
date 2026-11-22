using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using WorshipStudio.Services;

namespace WorshipStudio;

/// <summary>Downloads in the background; installing remains an explicit operator action.</summary>
public sealed class UpdateWindow : Window
{
    private readonly UpdateService _service;
    private readonly UpdateRelease _release;
    private readonly Func<string,Task<bool>> _install;
    private readonly CancellationTokenSource _cancellation=new();
    private readonly Button _download=new(){Content="Baixar atualização",Classes={"primary"}};
    private readonly Button _close=new(){Content="Depois"};
    private readonly ProgressBar _progress=new(){Minimum=0,Maximum=1,Height=6};
    private readonly TextBlock _status=new(){Text="O download será conferido por SHA-256 antes da instalação.",TextWrapping=TextWrapping.Wrap,FontSize=12};
    private readonly TextBlock _size=new(){FontSize=11};
    private string? _installerPath,_downloadHash;
    private bool _busy,_closed;
    public event Action<string,string>? DownloadVerified;

    public UpdateWindow(UpdateService service,UpdateRelease release,Func<string,Task<bool>> install,string? cachedPath=null,string? cachedHash=null)
    {
        _service=service;_release=release;_install=install;
        if(cachedPath!=null&&cachedHash!=null&&File.Exists(cachedPath))
        {
            _installerPath=cachedPath;_downloadHash=cachedHash;_progress.Value=1;
            _download.Content="Instalar e fechar o app";_close.Content="Instalar depois";
            _status.Text="Download já concluído nesta sessão. O hash será conferido novamente antes de instalar.";
        }
        Title="Atualizar Worship Studio";Width=720;Height=700;MinWidth=520;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        _status.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("WsMuted"));
        _size.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("WsMuted"));
        var heading=new StackPanel{Spacing=7,Children={
            new TextBlock{Text="ATUALIZAÇÃO DISPONÍVEL",Classes={"caption"}},
            new TextBlock{Text=release.Title,FontSize=24,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap},
            new TextBlock{Text=$"Versão instalada {UpdateService.CurrentVersion}  →  {release.Version}",Classes={"muted"},FontSize=12}}};
        var notes=new TextBox{Text=release.Notes,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,FontSize=12,Padding=new Thickness(14)};
        var info=new StackPanel{Spacing=9,Children={_status,_progress,_size}};
        var footer=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto")};
        var reminder=new TextBlock{Text="Instale ao terminar o culto. A projeção será encerrada.",FontSize=11,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,16,0),Classes={"muted"}};
        footer.Children.Add(reminder);Grid.SetColumn(_close,1);footer.Children.Add(_close);Grid.SetColumn(_download,2);_download.Margin=new Thickness(8,0,0,0);footer.Children.Add(_download);
        var layout=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto,Auto"),Margin=new Thickness(26)};
        layout.Children.Add(heading);Grid.SetRow(notes,1);notes.Margin=new Thickness(0,20,0,16);layout.Children.Add(notes);
        Grid.SetRow(info,2);layout.Children.Add(info);Grid.SetRow(footer,3);footer.Margin=new Thickness(0,22,0,0);layout.Children.Add(footer);Content=layout;
        _size.Text=$"Instalador para Windows x64 · {release.InstallerSize/1048576d:0.0} MB · GitHub";
        _download.Click+=async(_,_)=>await DownloadOrInstall();
        _close.Click+=(_,_)=>Close();
        Closing+=(_,_)=>{_closed=true;_cancellation.Cancel();};
    }

    private async Task DownloadOrInstall()
    {
        if(_busy)return;
        _busy=true;_download.IsEnabled=false;
        try
        {
            if(_installerPath==null)
            {
                _close.Content="Cancelar download";
                _status.Text="Baixando do GitHub. Você pode cancelar sem alterar a instalação atual.";
                var progress=new Progress<UpdateDownloadProgress>(p=>
                {
                    if(_closed)return;
                    _progress.Value=p.Fraction;
                    _size.Text=$"{p.BytesReceived/1048576d:0.0} / {p.TotalBytes/1048576d:0.0} MB · {p.Fraction:P0}";
                });
                _installerPath=await _service.DownloadAsync(_release,progress,_cancellation.Token);
                _downloadHash=await FileHash(_installerPath,_cancellation.Token);
                if(_release.InstallerSha256!=null&&!_downloadHash.Equals(_release.InstallerSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("O instalador foi alterado após o download. Baixe novamente.");
                DownloadVerified?.Invoke(_installerPath,_downloadHash);
                if(_closed)return;
                _progress.Value=1;_status.Text="Download concluído e SHA-256 conferido. Pronto para instalar.";
                _download.Content="Instalar e fechar o app";_close.Content="Instalar depois";
            }
            else
            {
                _status.Text="Conferindo o instalador…";
                if(!string.Equals(await FileHash(_installerPath,_cancellation.Token),_downloadHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("O arquivo foi alterado desde o download. Baixe novamente.");
                if(await _install(_installerPath))Close();
                else _status.Text="Instalação adiada. Sua apresentação continua disponível.";
            }
        }
        catch(OperationCanceledException) when(_closed){}
        catch(Exception ex)
        {
            if(_closed)return;
            AppLog.Write("Download de atualização: "+ex);
            _status.Text="Não foi possível concluir: "+ex.Message;
            if(_installerPath!=null){_installerPath=null;_downloadHash=null;}
            _download.Content="Tentar novamente";_close.Content="Fechar";
        }
        finally{_busy=false;if(!_closed)_download.IsEnabled=true;}
    }

    private static async Task<string> FileHash(string path,CancellationToken token)
    {
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,128*1024,true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
    }
}
