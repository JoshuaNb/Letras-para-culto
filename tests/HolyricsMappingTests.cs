using System.Text.Json;
using WorshipStudio.Services;
using Xunit;
using System.Net;
using System.Net.Sockets;
using System.Text;

public sealed class HolyricsMappingTests
{
    [Fact] public async Task UnchangedSourceSlidesDoNotRetriggerFollowerTransitions()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server=Task.Run(async()=>
        {
            for(var i=0;i<3;i++)
            {
                using var client=await listener.AcceptTcpClientAsync(cts.Token);var stream=client.GetStream();
                var header=new List<byte>();var next=new byte[1];
                while(header.Count<8192){if(await stream.ReadAsync(next,cts.Token)==0)throw new IOException();header.Add(next[0]);if(header.Count>=4&&header[^4]==13&&header[^3]==10&&header[^2]==13&&header[^1]==10)break;}
                var text=Encoding.ASCII.GetString(header.ToArray());var length=int.Parse(text.Split("\r\n").First(l=>l.StartsWith("Content-Length:")).Split(':')[1]);
                await stream.ReadExactlyAsync(new byte[length],cts.Token);
                var json="{\"status\":\"ok\",\"data\":{\"name\":\"Paz\",\"slide_number\":"+(i==2?2:1)+",\"slides\":[{\"number\":1,\"text\":\"Luz\"},{\"number\":2,\"text\":\"Paz\"}]}}";
                var bytes=Encoding.UTF8.GetBytes(json);await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"));await stream.WriteAsync(bytes);
            }
        });
        using var service=new HolyricsService();var updates=new List<HolyricsPresentation>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.FollowAsync($"http://127.0.0.1:{port}","test",p=>{updates.Add(p);if(updates.Count==2)cts.Cancel();},cts.Token));
        await server;
        Assert.Equal(2,updates.Count);Assert.Equal(new[]{"Luz","Paz"},updates.Select(p=>p.Text));
    }
    [Fact] public void MapsNumericSlideOrderAndGapsToZeroBasedLocalIndex()
    {
        using var doc=JsonDocument.Parse("""{"name":"Paz","slide_number":8,"total_slides":99,"slides":[{"number":8,"slide_description":"Refrão","text":"Paz"},{"number":3,"slide_description":"Verso","text":"Luz"}]}""");
        var p=HolyricsService.ParsePresentation(doc.RootElement);
        Assert.Equal(1,p.Index);Assert.Equal(2,p.Count);Assert.Equal("Paz",p.Text);
        Assert.Equal(new[]{"Luz","Paz"},p.Slides.Select(s=>s.Text));Assert.Equal("Refrão",p.Slides[p.Index].Label);
        Assert.False(p.Blackout);Assert.False(p.HideLyrics);
    }
    [Fact] public void MissingSlideTypeDoesNotHideValidLyrics()
    {
        using var doc=JsonDocument.Parse("""{"name":"Paz","slide_number":1,"slides":[{"text":"Luz"}]}""");
        var p=HolyricsService.ParsePresentation(doc.RootElement);
        Assert.Single(p.Slides);Assert.Equal(0,p.Index);Assert.Equal(1,p.Count);Assert.False(p.HideLyrics);
    }
    [Fact] public void NullPresentationHidesLyricsAndHasNoSourceSlides()
    {
        using var doc=JsonDocument.Parse("null");var p=HolyricsService.ParsePresentation(doc.RootElement);
        Assert.Empty(p.Slides);Assert.Equal(0,p.Count);Assert.True(p.HideLyrics);Assert.False(p.Blackout);
    }
}
