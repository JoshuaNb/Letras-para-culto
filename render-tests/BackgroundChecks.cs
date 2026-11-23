using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using WorshipStudio.Controls;
using WorshipStudio.Models;

/// <summary>Real Skia frames from the shipped renderer; no image fixtures or native video decoder.</summary>
public static class BackgroundChecks
{
    private static readonly string[] Added={"GraceWaves","LightRays","Galaxy","Bokeh","WireLandscape","Ocean","Ribbons","Prism"};
    public static void RunAndThrow(string output)
    {
        Directory.CreateDirectory(output);
        var results=new List<Result>();
        var hashes=new Dictionary<string,string>();
        var timings=new Dictionary<string,double>();
        var style=new StageStyle{BackgroundColor="#071322",AccentColor="#5BC9D9",Dim=0,MotionSpeed=1,Transition="Cut",LyricAnimation="None",FontSize=110};
        void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        void Check(string name,Action action)
        {
            try{action();results.Add(new(name,true,null));Console.WriteLine("PASS "+name);}
            catch(Exception ex){results.Add(new(name,false,ex.Message));Console.WriteLine("FAIL "+name+": "+ex.Message);}
        }
        Pixels Frame(StageRenderer renderer,StageStyle scene,double time,string text="",bool hide=false,bool black=false,string? artifact=null,int width=768,int height=432,double audio=0)
        {
            renderer.SetFrame(new StageFrame{Style=scene,Text=text,HideLyrics=hide,Blackout=black,AudioLevel=audio});
            using var capture=renderer.Capture(width,height,time,1);
            using var memory=new MemoryStream();capture.Save(memory);var png=memory.ToArray();
            if(artifact!=null)File.WriteAllBytes(Path.Combine(output,artifact+".png"),png);
            using var decoded=SKBitmap.Decode(png)??throw new InvalidDataException("PNG inválido.");
            return new Pixels(decoded.Pixels);
        }
        Check("Catálogo preserva IDs e reconhece nomes portugueses antigos",()=>
        {
            Require(BackgroundCatalog.Choices.Count==14&&BackgroundCatalog.Choices.Select(c=>c.Id).Distinct().Count()==14,"O catálogo precisa de14IDs distintos.");
            foreach(var choice in BackgroundCatalog.Choices)
            {
                Require(BackgroundCatalog.Normalize(choice.Name)==choice.Id&&BackgroundCatalog.Normalize(choice.Id.ToLowerInvariant())==choice.Id,"ID/nome não reconhecido: "+choice.Name);
                Require(BackgroundCatalog.IsProcedural(choice.Id)==choice.IsProcedural,"Classificação incompatível: "+choice.Id);
            }
            Require(BackgroundCatalog.Normalize("  TÚNEL 3D  ")=="Tunnel"&&BackgroundCatalog.Normalize("Particulas 3D")=="Particles"&&BackgroundCatalog.Normalize("Vídeo local")=="Video","Aliases de cenas antigas falharam.");
            Require(!BackgroundCatalog.TryNormalize("efeito desconhecido",out var fallback)&&fallback=="Aurora","ID desconhecido precisa devolverfallback seguro.");
        });
        foreach(var mode in Added)
        {
            Check("Fundo "+mode+" anima pixels reais de forma determinística",()=>
            {
                using var renderer=new StageRenderer();var scene=style with{BackgroundMode=mode};
                var early=Frame(renderer,scene,.2,artifact:mode+"-inicio");
                var later=Frame(renderer,scene,3.4,artifact:mode+"-movimento");
                var repeat=Frame(renderer,scene,3.4);
                var solid=Frame(renderer,scene with{BackgroundMode="Solid"},3.4);
                Require(early.Hash!=later.Hash,"O fundo não mudou com o tempo.");
                Require(later.Hash==repeat.Hash,"O mesmo tempo mudou os pixels na exportação.");
                Require(later.Difference(solid)>.01,"O fundo ficou praticamente sólido.");
                Require(later.Values.All(c=>c.Alpha==255),"O fundo normal precisa cobrir a saída inteira com pixels opacos.");
                hashes[mode]=later.Hash;
                // Supplemental timing: synchronous CPU capture at full HD; not a live-frame-rate claim.
                using var warm=renderer.Capture(1920,1080,3.4,1);
                var watch=Stopwatch.StartNew();
                for(var i=0;i<3;i++){using var full=renderer.Capture(1920,1080,3.4+i*.033,1);}
                timings[mode]=watch.Elapsed.TotalMilliseconds/3;
            });
            Check("Fundo "+mode+" congela quando velocidade é zero",()=>
            {
                using var renderer=new StageRenderer();var scene=style with{BackgroundMode=mode,MotionSpeed=0};
                Require(Frame(renderer,scene,.1).Hash==Frame(renderer,scene,17).Hash,"Velocidade zero ainda movimentou o fundo.");
            });
        }
        Check("Os oito fundos têm composições distintas e aliases não apagam o fundo",()=>
        {
            Require(hashes.Count==8&&hashes.Values.Distinct().Count()==8,"Nomes diferentes reutilizaram a mesma composição.");
            using var renderer=new StageRenderer();
            foreach(var alias in new[]{("Aurora fluida","Aurora"),("Túnel 3D","Tunnel"),("Particulas 3D","Particles"),("Ondas de graça","GraceWaves"),("inventado-antigo","Aurora")})
            {
                var fromAlias=Frame(renderer,style with{BackgroundMode=alias.Item1},2);
                var canonical=Frame(renderer,style with{BackgroundMode=alias.Item2},2);
                Require(fromAlias.Hash==canonical.Hash,"O alias/fallback não desenhou o fundo esperado: "+alias.Item1);
                Require(fromAlias.Hash!=Frame(renderer,style with{BackgroundMode="Solid"},2).Hash,"O alias virou fundo vazio.");
            }
        });
        Check("Cores, velocidade, escurecimento e áudio atuam em cada fundo novo",()=>
        {
            using var renderer=new StageRenderer();
            foreach(var mode in Added)
            {
                var scene=style with{BackgroundMode=mode};var normal=Frame(renderer,scene,2.1);
                Require(normal.Hash!=Frame(renderer,scene with{AccentColor="#EA7868"},2.1).Hash,"A cor do efeito não mudou "+mode);
                Require(normal.Hash!=Frame(renderer,scene with{BackgroundColor="#32150C"},2.1).Hash,"A cor do fundo não mudou "+mode);
                Require(normal.Hash!=Frame(renderer,scene with{MotionSpeed=2.3},2.1).Hash,"A velocidade não mudou "+mode);
                var dimmed=Frame(renderer,scene with{Dim=.8},2.1);
                Require(dimmed.Light<normal.Light*.35,"Escurecer não reduziu a luz de "+mode);
                Require(normal.Hash!=Frame(renderer,scene,2.1,audio:.9).Hash,"O áudio não alterou "+mode);
            }
        });
        Check("Ocultar letra conserva os fundos e tela preta prevalece em todos",()=>
        {
            using var renderer=new StageRenderer();
            foreach(var mode in Added)
            {
                var scene=style with{BackgroundMode=mode};var background=Frame(renderer,scene,2);
                var hidden=Frame(renderer,scene,2,text:"A graça permanece",hide:true);
                var visible=Frame(renderer,scene,2,text:"A graça permanece");
                var blackout=Frame(renderer,scene,2,text:"A graça permanece",black:true);
                Require(background.Hash==hidden.Hash,"Ocultar letra removeu o fundo "+mode);
                Require(visible.Hash!=hidden.Hash,"A letra visível não foi composta sobre "+mode);
                Require(blackout.Values.All(c=>c.Red==0&&c.Green==0&&c.Blue==0&&c.Alpha==255),"O fundo vazou na tela preta: "+mode);
            }
        });
        Check("Imagem ausente ou sem caminho usa Aurora animada e preserva a letra",()=>
        {
            foreach(var missing in new[]{null,"",Path.Combine(Path.GetTempPath(),"WorshipStudio-missing-"+Guid.NewGuid().ToString("N")+".png")})
            {
                using var renderer=new StageRenderer();var errors=0;renderer.Error+=_=>errors++;
                var scene=style with{BackgroundMode="Imagem local",BackgroundPath=missing};
                var first=Frame(renderer,scene,.2,text:"A graça permanece");
                var later=Frame(renderer,scene,2.8,text:"A graça permanece",artifact:missing==null?"imagem-sem-caminho-fallback":null);
                Require(!string.IsNullOrWhiteSpace(renderer.MediaError)&&errors==1,"A imagem ausente precisa avisar uma vez por seleção.");
                Require(first.Hash!=later.Hash,"O fallback de imagem não animou.");
                Require(later.Hash!=Frame(renderer,scene,2.8,hide:true).Hash,"A letra desapareceu junto com a imagem.");
                Require(later.Hash==Frame(renderer,scene with{BackgroundMode="Aurora"},2.8,text:"A graça permanece").Hash,"O fundo substituto não corresponde à Aurora real.");
                Require(renderer.MediaError==null,"Ao escolher um fundo procedural o erro de imagem deve ser limpo.");
            }
        });
        Check("Imagem corrompida usa o mesmo fallback animado da projeção e exportação",()=>
        {
            var corrupt=Path.Combine(Path.GetTempPath(),"WorshipStudio-corrupt-"+Guid.NewGuid().ToString("N")+".png");
            try
            {
                File.WriteAllText(corrupt,"fixture local sem pixels PNG");
                using var renderer=new StageRenderer();var scene=style with{BackgroundMode="Image",BackgroundPath=corrupt};
                var first=Frame(renderer,scene,.2,text:"Fé e esperança");
                var later=Frame(renderer,scene,2.8,text:"Fé e esperança",artifact:"imagem-corrompida-fallback");
                Require(renderer.MediaError?.Contains("Aurora")==true,"A falha de decodificação precisa informar o fundo substituto.");
                Require(first.Hash!=later.Hash,"A imagem corrompida deixou somente a letra parada.");
                Require(later.Hash==Frame(renderer,scene with{BackgroundMode="Aurora"},2.8,text:"Fé e esperança").Hash,"O fallback da imagem corrompida divergiu da Aurora.");
            }
            finally{File.Delete(corrupt);}
        });
        var summary=new{Passed=results.Count(r=>r.Passed),Failed=results.Count(r=>!r.Passed),Checks=results,FullHdSynchronousCaptureMilliseconds=timings,TimingScope="Three CPU capture samples per effect on this test host; not physical projector, GPU, or live FPS validation."};
        File.WriteAllText(Path.Combine(output,"background-render-tests.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Fundos: {summary.Passed} aprovados; {summary.Failed} falhas. Evidências: {output}");
        if(summary.Failed>0)throw new InvalidOperationException("Um ou mais fundos de produção falharam no teste Skia.");
    }
    private sealed record Result(string Name,bool Passed,string? Error);
    private sealed record Pixels(SKColor[] Values)
    {
        public string Hash=>Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(Values.AsSpan())));
        public double Light=>Values.Average(c=>(c.Red+c.Green+c.Blue)/3d);
        public double Difference(Pixels other)=>Values.Zip(other.Values).Count(pair=>Math.Abs(pair.First.Red-pair.Second.Red)+Math.Abs(pair.First.Green-pair.Second.Green)+Math.Abs(pair.First.Blue-pair.Second.Blue)>12)/(double)Values.Length;
    }
}
