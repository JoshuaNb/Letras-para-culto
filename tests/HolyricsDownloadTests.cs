using System.Net;
using System.Text;
using System.Text.Json;
using WorshipStudio.Services;
using Xunit;

public sealed class HolyricsDownloadTests
{
    private const string BaseUrl="http://127.0.0.1:8091";
    private const string Secret="private token never disclose";

    [Fact] public async Task CatalogUsesOfficialMetadataActionWithoutDownloadingEveryLyric()
    {
        using var handler=new Stub(async(request,_)=>
        {
            Assert.Equal(HttpMethod.Post,request.Method);
            Assert.Equal("/api/GetSongs",request.RequestUri!.AbsolutePath);
            Assert.Contains("token=private%20token",request.RequestUri.Query);
            Assert.Equal("application/json",request.Content!.Headers.ContentType!.MediaType);
            using var body=JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            Assert.Equal("id,title,artist,author",body.RootElement.GetProperty("fields").GetString());
            return Ok("""[{"id":"7","title":"Graça","artist":"Ministério"},{"id":"8","title":"Paz","author":"Autor"}]""");
        });
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var catalog=await service.GetCatalogAsync(BaseUrl,Secret);
        Assert.Equal(1,handler.Calls);Assert.Equal(new[]{"Graça","Paz"},catalog.Select(s=>s.Title));Assert.Equal("Autor",catalog[1].Artist);
    }

    [Fact] public async Task CatalogAcceptsNumericIdsAndDeduplicatesInvalidRecords()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(Ok("""[{"id":7,"title":"Paz"},{"id":"7","title":"Duplicada"},{"title":"Sem ID"},null,{"id":"8","title":"Luz"}]""")));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var catalog=await service.GetCatalogAsync(BaseUrl,Secret);
        Assert.Equal(new[]{"7","8"},catalog.Select(s=>s.Id));
    }

    [Fact] public async Task CachedLyricsAvoidDetailRequestsWhenServerIgnoresMetadataFields()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(Ok("""[{"id":"1","title":"Luz","slides":[{"slide_description":"Refrão","text":"Luz e paz"}]}]""")));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var catalog=await service.GetCatalogAsync(BaseUrl,Secret);
        var songs=await service.DownloadSongsAsync(BaseUrl,Secret,catalog);
        Assert.Equal(1,handler.Calls);Assert.Equal("[Refrão]\nLuz e paz",Assert.Single(songs).Lyrics);
    }

    [Fact] public async Task DownloadsOnlySelectedIdsOnceWithMetadataFallbackAndProgress()
    {
        var ids=new List<string>();
        using var handler=new Stub(async(request,_)=>
        {
            Assert.Equal("/api/GetLyrics",request.RequestUri!.AbsolutePath);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var id=body.RootElement.GetProperty("id").GetString()!;ids.Add(id);
            Assert.Contains("slides",body.RootElement.GetProperty("fields").GetString());
            return Ok(id=="2"?"""{"slides":[{"text":"Segunda"}]}""":"""{"title":"Terceira","slides":[{"text":"Final"}]}""");
        });
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var progress=new List<HolyricsDownloadProgress>();
        var selection=new[]{new HolyricsCatalogSong("2","Título original","Autor"),new HolyricsCatalogSong("3","Três",""),new HolyricsCatalogSong("2","Duplicada","")};
        var songs=await service.DownloadSongsAsync(BaseUrl,Secret,selection,new InlineProgress(progress.Add));
        Assert.Equal(new[]{"2","3"},ids);Assert.Equal(2,songs.Count);Assert.Equal("Título original",songs[0].Title);Assert.Equal("Autor",songs[0].Artist);
        Assert.Equal(new[]{0,1,2},progress.Select(p=>p.Completed));Assert.All(progress,p=>Assert.Equal(2,p.Total));Assert.Equal(1,progress[^1].Fraction);
    }

    [Fact] public async Task EmptySelectionDoesNotIssueRequests()
    {
        using var handler=new Stub((_,_)=>throw new InvalidOperationException("No request expected"));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        Assert.Empty(await service.DownloadSongsAsync(BaseUrl,Secret,Array.Empty<HolyricsCatalogSong>()));Assert.Equal(0,handler.Calls);
    }

    [Fact] public async Task CancellationBeforeDownloadDoesNotFetchDetails()
    {
        using var handler=new Stub((_,_)=>throw new InvalidOperationException("No request expected"));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.DownloadSongsAsync(BaseUrl,Secret,new[]{Item("1")},ct:cts.Token));Assert.Equal(0,handler.Calls);
    }

    [Fact] public async Task CancellationAfterFirstSongRejectsEntireBatchAndStopsFurtherRequests()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(Ok("""{"title":"Paz","slides":[{"text":"Paz"}]}""")));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);using var cts=new CancellationTokenSource();
        var progress=new InlineProgress(p=>{if(p.Completed==1)cts.Cancel();});
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.DownloadSongsAsync(BaseUrl,Secret,new[]{Item("1"),Item("2")},progress,cts.Token));
        Assert.Equal(1,handler.Calls);
    }

    [Fact] public async Task CatalogNetworkRequestObservesCancellation()
    {
        using var handler=new Stub(async(_,token)=>{await Task.Delay(Timeout.Infinite,token);return Ok("[]");});
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);using var cts=new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.GetCatalogAsync(BaseUrl,Secret,cts.Token));
    }

    [Fact] public async Task MissingOrEmptyLyricsRejectsSelectedBatch()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(Ok("null")));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>service.DownloadSongsAsync(BaseUrl,Secret,new[]{Item("1")}));
        Assert.Contains("não está disponível",error.Message);
    }

    [Fact] public async Task InvalidSelectedIdentifierIsRejectedBeforeNetwork()
    {
        using var handler=new Stub((_,_)=>throw new InvalidOperationException("No request expected"));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.DownloadSongsAsync(BaseUrl,Secret,new[]{Item("bad\nidentifier")}));Assert.Equal(0,handler.Calls);
    }

    [Theory][InlineData(401)][InlineData(403)] public async Task AuthorizationFailuresAreActionableAndDoNotExposeToken(int status)
    {
        using var handler=new Stub((_,_)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.GetCatalogAsync(BaseUrl,Secret));
        Assert.Contains("permissões",error.Message);Assert.DoesNotContain(Secret,error.ToString());
    }

    [Fact] public async Task TransportErrorsNeverExposeRequestUriOrToken()
    {
        using var handler=new Stub((request,_)=>throw new HttpRequestException("Sensitive: "+request.RequestUri));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        var error=await Assert.ThrowsAsync<HttpRequestException>(()=>service.GetCatalogAsync(BaseUrl,Secret));
        Assert.DoesNotContain("token=",error.ToString());Assert.DoesNotContain("private",error.ToString());Assert.Null(error.InnerException);
    }

    [Fact] public async Task InvalidJsonAndServerErrorBodiesAreNotReflectedToUser()
    {
        foreach(var payload in new[]{"secret "+Secret,"{\"status\":\"error\",\"error\":\""+Secret+"\"}"})
        {
            using var handler=new Stub((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(payload)}));
            using var client=new HttpClient(handler);using var service=new HolyricsService(client);
            var error=await Record.ExceptionAsync(()=>service.GetCatalogAsync(BaseUrl,Secret));
            Assert.NotNull(error);Assert.DoesNotContain(Secret,error.ToString());Assert.DoesNotContain("private",error.ToString());
        }
    }

    [Fact] public async Task RedirectedResponseIsRejected()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=new HttpRequestMessage(HttpMethod.Post,"http://different-host.example/api/GetSongs"),Content=new StringContent("{\"status\":\"ok\",\"data\":[]}")}));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.GetCatalogAsync(BaseUrl,Secret));
    }

    [Fact] public async Task OversizedResponseIsRejectedBeforeReadingBody()
    {
        using var handler=new Stub((_,_)=>
        {
            var response=Ok("[]");response.Content.Headers.ContentLength=33*1024*1024;return Task.FromResult(response);
        });
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.GetCatalogAsync(BaseUrl,Secret));
    }

    [Fact] public async Task NonArrayCatalogIsRejected()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(Ok("{}")));
        using var client=new HttpClient(handler);using var service=new HolyricsService(client);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.GetCatalogAsync(BaseUrl,Secret));
    }

    [Fact] public void SearchMatchesPortugueseAccentsCaseTitleAndArtist()
    {
        var catalog=new[]{new HolyricsCatalogSong("1","Graça e paz","Adoração"),new HolyricsCatalogSong("2","Luz","Outro")};
        Assert.Equal("1",Assert.Single(HolyricsService.FilterCatalog(catalog," GRACA ")).Id);
        Assert.Equal("1",Assert.Single(HolyricsService.FilterCatalog(catalog,"ADORACAO")).Id);
        Assert.Equal(2,HolyricsService.FilterCatalog(catalog," ").Count);Assert.Empty(HolyricsService.FilterCatalog(catalog,"inexistente"));
    }

    [Fact] public async Task ServiceDoesNotDisposeInjectedHttpClient()
    {
        using var handler=new Stub((_,_)=>Task.FromResult(Ok("[]")));using var client=new HttpClient(handler);
        using(var service=new HolyricsService(client)){Assert.Empty(await service.GetCatalogAsync(BaseUrl,Secret));}
        using var response=await client.GetAsync("http://127.0.0.1/health");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
    }

    private static HolyricsCatalogSong Item(string id)=>new(id,"Música "+id,"Autor");
    private static HttpResponseMessage Ok(string data)=>new(HttpStatusCode.OK){Content=new StringContent("{\"status\":\"ok\",\"data\":"+data+"}",Encoding.UTF8,"application/json")};
    private sealed class InlineProgress(Action<HolyricsDownloadProgress> callback):IProgress<HolyricsDownloadProgress>
    {public void Report(HolyricsDownloadProgress value)=>callback(value);}
    private sealed class Stub(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> responder):HttpMessageHandler
    {
        public int Calls{get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Calls++;return responder(request,cancellationToken);}
    }
}
