using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using SkiaSharp;
using WorshipStudio.Controls;
using WorshipStudio.Models;

/// <summary>Real production-renderer regressions. This class assumes Avalonia/Skia is already initialized.</summary>
public static class GranularityChecks
{
    private const string Sample = "Fé em cada coração\nPaz para toda a terra";
    private const int Width=800, Height=450;

    public static void RunAndThrow(string output)
    {
        output=Path.GetFullPath(output); Directory.CreateDirectory(output);
        var results=new List<Result>();
        var style=new StageStyle
        {
            UsePresetAnimation=true,AnimationPreset="fade-suave",AnimationGranularity="Word",
            AnimationDuration=.6,AnimationStagger=.12,AnimationDelay=0,AnimationOrder="Forward",
            AnimationEasing="EaseOut",AnimationStrength=1,AnimationLoop=false,
            FontFamily="Segoe UI",FontSize=110,Bold=true,TextColor="#FFFFFF",BackgroundMode="Solid",
            BackgroundColor="#000000",Dim=0,MotionSpeed=0,LyricAnimation="None",Transition="Cut"
        };
        void Check(string name,Action action)
        {
            try {action(); results.Add(new(name,true,null));Console.WriteLine("PASS "+name);}
            catch(Exception ex){results.Add(new(name,false,ex.Message));Console.WriteLine("FAIL "+name+": "+ex.Message);}
        }
        void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        Snapshot Render(StageRenderer renderer,StageStyle scene,double elapsed,string text=Sample,string? artifact=null,double? absolute=null)
        {
            renderer.SetFrame(new StageFrame{Text=text,Style=scene});
            using var bitmap=renderer.Capture(Width,Height,absolute??elapsed,1,cueElapsed:elapsed);
            using var memory=new MemoryStream();bitmap.Save(memory);var png=memory.ToArray();
            using var pixels=SKBitmap.Decode(png)??throw new InvalidDataException("Quadro PNG inválido.");
            if(artifact!=null)File.WriteAllBytes(Path.Combine(output,artifact+".png"),png);
            return new Snapshot(pixels.Pixels,pixels.Width,pixels.Height);
        }

        Check("Granularidade separa bloco, linhas, palavras e letras na mesma estrofe",()=>
        {
            const string text="Fé e paz\nReina aqui";
            var whole=StageRenderer.GetAnimationUnits(text,style with{AnimationGranularity="Text"},Width,Height);
            var lines=StageRenderer.GetAnimationUnits(text,style with{AnimationGranularity="Line"},Width,Height);
            var words=StageRenderer.GetAnimationUnits(text,style with{AnimationGranularity="Word"},Width,Height);
            var letters=StageRenderer.GetAnimationUnits(text,style with{AnimationGranularity="Character"},Width,Height);
            Require(whole.Count==1 && lines.Count==2 && words.Count==5,"O texto deve produzir 1 bloco, 2 linhas e 5 palavras.");
            Require(letters.Count>words.Count,"Letras precisam ser unidades independentes, sem contar espaços.");
            Require(words.Select(u=>u.Text).SequenceEqual(new[]{"Fé","e","paz","Reina","aqui"}),"As palavras foram perdidas, fundidas ou reordenadas.");
            foreach(var units in new[]{whole,lines,words,letters})
                Require(units.All(u=>u.Bounds.Count>0 && u.Text==text.Substring(u.TextStart,u.TextLength)),"Uma unidade perdeu seus limites ou intervalo no texto original.");
        });
        Check("Caracteres combinados, família emoji e bandeira não são quebrados",()=>
        {
            const string text="Á a\u0301 👨‍👩‍👧‍👦 🇧🇷";
            var units=StageRenderer.GetAnimationUnits(text,style with{AnimationGranularity="Character"},Width,Height);
            Require(units.Select(u=>u.Text).SequenceEqual(new[]{"Á","a\u0301","👨‍👩‍👧‍👦","🇧🇷"}),"Uma letra acentuada ou um emoji composto foi dividido em unidades inválidas.");
            Require(units.Count==4,"Os espaços não devem acrescentar atrasos à animação.");
        });
        Check("Linhas visuais incluem a quebra automática da letra",()=>
        {
            var text=string.Join(' ',Enumerable.Repeat("A esperança permanece em nosso coração",7));
            var units=StageRenderer.GetAnimationUnits(text,style with{AnimationGranularity="Line",FontSize=140},640,360);
            Require(units.Count>=3,"A linha longa não foi dividida pelas linhas reais do renderizador.");
            Require(units.All(u=>u.Bounds.All(b=>b.Left>=0 && b.Top>=0 && b.Right<=641 && b.Bottom<=361)),"A medida das unidades saiu do quadro.");
        });
        Check("Duração total acompanha a quantidade real de unidades",()=>
        {
            var scene=style with{AnimationDelay=.2,AnimationDuration=.7,AnimationStagger=.1};
            foreach(var granularity in new[]{"Text","Line","Word","Character"})
            {
                scene=scene with{AnimationGranularity=granularity};
                var count=StageRenderer.GetAnimationUnits(Sample,scene,Width,Height).Count;
                var expected=.2+.7+.1*(count-1);
                Require(Math.Abs(StageRenderer.GetAnimationDuration(Sample,scene,Width,Height)-expected)<1e-8,"A duração não inclui o atraso de todas as unidades em "+granularity+".");
            }
            Require(StageRenderer.GetAnimationDuration(" \n ",scene,Width,Height)==0,"Uma letra vazia não pode reservar duração de animação.");
        });
        Check("Granularidade muda os pixels e o atraso começa cada unidade separadamente",()=>
        {
            using var renderer=new StageRenderer();
            var frames=new List<string>();
            foreach(var granularity in new[]{"Text","Line","Word","Character"})
            {
                var frame=Render(renderer,style with{AnimationGranularity=granularity,AnimationDuration=.7,AnimationStagger=.25},.4,artifact:"granularity-"+granularity);
                Require(frame.Ink>80,"A granularidade "+granularity+" não mostrou nenhuma unidade.");
                frames.Add(frame.Hash);
            }
            Require(frames.Distinct().Count()==4,"Granularidades diferentes reutilizaram a mesma animação do bloco inteiro.");
            var zero=Render(renderer,style with{AnimationStagger=0},.4);
            var staggered=Render(renderer,style with{AnimationStagger=.25},.4);
            Require(zero.Hash!=staggered.Hash,"Alterar o intervalo entre unidades não alterou os quadros.");
            Require(Render(renderer,style with{AnimationDelay=.9},.4).Ink==0,"A letra apareceu antes do atraso inicial configurado.");
        });
        Check("Ordem esquerda, direita e centro muda as unidades que entram primeiro",()=>
        {
            using var renderer=new StageRenderer();
            const string text="FÉ PAZ LUZ AMOR GRAÇA VIDA LOUVOR";
            var hashes=new List<string>();
            foreach(var order in new[]{"Forward","Reverse","Center","Random"})
            {
                var scene=style with{AnimationOrder=order,AnimationStagger=.3,AnimationDuration=.7};
                var frame=Render(renderer,scene,.35,text,artifact:"order-"+order);
                Require(frame.Ink>30,"A ordem "+order+" não iniciou nenhuma unidade.");
                Require(frame.Hash==Render(renderer,scene,.35,text).Hash,"A ordem aleatória precisa ser reproduzível na exportação.");
                hashes.Add(frame.Hash);
            }
            Require(hashes.Take(3).Distinct().Count()==3,"As ordens direta, reversa e do centro não se diferenciaram.");
            Require(hashes[0]!=hashes[3],"A opção aleatória repetiu a ordem direta nesta estrofe.");
        });

        var fingerprints=new Dictionary<string,string>();
        foreach(var preset in AnimationPresets.All)
        {
            var selected=preset;
            Check("Predefinição "+selected.Name+" renderiza sua sequência e termina legível",()=>
            {
                using var renderer=new StageRenderer();
                var scene=AnimationPresets.ApplyDefaults(style,selected);
                var total=StageRenderer.GetAnimationDuration(Sample,scene,Width,Height);
                var final=Render(renderer,scene,total+.025);
                var plain=Render(renderer,scene with{AnimationPreset="none"},0);
                Require(final.Ink>900,"A sequência terminou sem a letra completa.");
                Require(final.Hash==plain.Hash,"A sequência não retornou à posição/forma/cor final do texto: "+selected.Id+".");
                if(selected.Id=="none")
                {
                    Require(Render(renderer,scene,0).Hash==final.Hash,"Nenhuma deve mostrar a letra sem animar.");
                    return;
                }
                var initial=Render(renderer,scene,0);
                Require(initial.Ink<final.Ink*.01,"A entrada começou com unidades já visíveis.");
                var sample1=Render(renderer,scene,total*.23);
                var sample2=Render(renderer,scene,total*.47,artifact:"preset-"+selected.Id);
                var sample3=Render(renderer,scene,total*.73);
                Require(sample1.Hash!=initial.Hash || sample2.Hash!=initial.Hash || sample3.Hash!=initial.Hash,"A entrada não evoluiu antes do último quadro.");
                Require(new[]{initial.Hash,sample1.Hash,sample2.Hash,sample3.Hash,final.Hash}.Distinct().Count()>=3,"A predefinição apenas saltou de vazio para completo.");
                Require(sample2.Hash==Render(renderer,scene,total*.47).Hash,"A mesma predefinição e tempo não produziram os mesmos pixels.");
                fingerprints[selected.Id]=sample1.Hash+sample2.Hash+sample3.Hash;
            });
        }
        Check("As predefinições não são rótulos diferentes para os mesmos quadros",()=>
        {
            var duplicates=fingerprints.GroupBy(p=>p.Value).Where(g=>g.Count()>1).Select(g=>string.Join(",",g.Select(p=>p.Key))).ToArray();
            Require(fingerprints.Count==65,"Nem todas as 65 predefinições animadas foram amostradas com sucesso.");
            Require(duplicates.Length==0,"Predefinições visualmente idênticas nos três tempos: "+string.Join("; ",duplicates));
        });
        Check("Blur Reveal desfoca os contornos de verdade e depois recupera a nitidez",()=>
        {
            using var renderer=new StageRenderer();
            var scene=AnimationPresets.ApplyDefaults(style,AnimationPresets.Find("blur-reveal")) with{AnimationGranularity="Text",AnimationDuration=1,AnimationStagger=0};
            var blurred=Render(renderer,scene,.35,artifact:"blur-intermediate");
            var crisp=Render(renderer,scene,1.05,artifact:"blur-complete");
            Require(blurred.Ink>200,"O quadro desfocado está vazio.");
            Require(blurred.NormalizedEdgeEnergy<crisp.NormalizedEdgeEnergy*.9,"O desfoque não suavizou as altas frequências das bordas do texto.");
        });
        Check("RGB Split desenha canais deslocados e volta à cor escolhida",()=>
        {
            using var renderer=new StageRenderer();
            var scene=AnimationPresets.ApplyDefaults(style,AnimationPresets.Find("rgb-split")) with{AnimationGranularity="Text",AnimationDuration=1,AnimationStagger=0};
            var split=Render(renderer,scene,.3,artifact:"rgb-split-intermediate");
            Require(split.Pixels.Count(c=>c.Red>40 && c.Red>c.Green*1.4)>40,"O canal vermelho separado não foi desenhado.");
            Require(split.Pixels.Count(c=>c.Green>40 && c.Green>c.Red*1.4)>40,"Os canais ciano separados não foram desenhados.");
            var final=Render(renderer,scene,1.1);
            var plain=Render(renderer,scene with{AnimationPreset="none"},0);
            Require(final.Hash==plain.Hash,"A animação reteve separação cromática além da antialiasagem normal da fonte ao terminar.");
        });
        Check("Loop mantém a letra no intervalo e repete a sequência determinística",()=>
        {
            using var renderer=new StageRenderer();
            var scene=style with{AnimationLoop=true,AnimationLoopPause=.6,AnimationDuration=.7,AnimationStagger=.1,AnimationDelay=.2};
            var total=StageRenderer.GetAnimationDuration(Sample,scene,Width,Height);
            var hold=Render(renderer,scene,total+.25);
            var complete=Render(renderer,scene with{AnimationLoop=false},total+.25);
            Require(hold.Hash==complete.Hash,"O intervalo do loop não manteve a letra completamente visível.");
            Require(Render(renderer,scene,.45).Hash==Render(renderer,scene,total+.6+.45).Hash,"A segunda volta do loop não repetiu os mesmos quadros.");
        });
        Check("MP4 usa tempo local da estrofe sem herdar o tempo da estrofe anterior",()=>
        {
            using var renderer=new StageRenderer();
            var scene=AnimationPresets.ApplyDefaults(style,AnimationPresets.Find("typewriter-blur-forte"));
            var first=Render(renderer,scene,.27,absolute:.27);
            var laterCue=Render(renderer,scene,.27,absolute:72.27);
            Require(first.Hash==laterCue.Hash,"A estrofe posterior pulou sua entrada por usar o tempo absoluto do MP4.");
        });
        Check("Fonte, negrito, cor e Unicode continuam funcionando no caminho granular",()=>
        {
            using var renderer=new StageRenderer();
            const string text="Água e bênçãos 👨‍👩‍👧‍👦\nAmor e paz";
            var plain=style with{AnimationPreset="none",AnimationGranularity="Character"};
            var segoe=Render(renderer,plain,1,text);
            var georgia=Render(renderer,plain with{FontFamily="Georgia"},1,text);
            var normal=Render(renderer,plain with{Bold=false},1,text);
            var colored=Render(renderer,plain with{TextColor="#FFD166"},1,text);
            Require(new[]{segoe.Hash,georgia.Hash,normal.Hash,colored.Hash}.Distinct().Count()==4,"A rasterização granular ignorou uma alteração de tipografia/cor.");
            Require(colored.Pixels.Count(c=>c.Red>180 && c.Green>100 && c.Blue<150)>100,"A cor escolhida não chegou à animação granular.");
        });
        Check("Tela preta, ocultar letra e transparência do vídeo prevalecem sobre predefinições",()=>
        {
            using var renderer=new StageRenderer();
            var scene=AnimationPresets.ApplyDefaults(style,AnimationPresets.Find("luz-divina"));
            foreach(var blackout in new[]{false,true})
            {
                renderer.SetFrame(new StageFrame{Text=Sample,Style=scene,Blackout=blackout,HideLyrics=!blackout});
                using var bitmap=renderer.Capture(Width,Height,.4,1,.4);using var bytes=new MemoryStream();bitmap.Save(bytes);using var image=SKBitmap.Decode(bytes.ToArray());
                Require(image.Pixels.All(p=>p.Red==0 && p.Green==0 && p.Blue==0 && p.Alpha==255),"O brilho de uma predefinição vazou na saída oculta/preta.");
            }
            renderer.VideoOverlay=true;
            renderer.SetFrame(new StageFrame{Text=Sample,Style=scene with{Dim=0,BackgroundMode="Video"}});
            using var overlay=renderer.Capture(Width,Height,.4,1,.4);using var memory=new MemoryStream();overlay.Save(memory);using var pixels=SKBitmap.Decode(memory.ToArray());
            Require(pixels.Pixels.Count(p=>p.Alpha==0)>Width*Height*.7,"O efeito preenchou a transparência que pertence ao vídeo de fundo.");
            Require(pixels.Pixels.Count(p=>p.Alpha>0)>200,"A letra animada desapareceu da camada transparente de vídeo.");
        });

        var summary=new{Passed=results.Count(r=>r.Passed),Failed=results.Count(r=>!r.Passed),CatalogChoices=AnimationPresets.All.Count,Checks=results};
        File.WriteAllText(Path.Combine(output,"granularity-render-tests.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Granularidade: {summary.Passed} aprovados; {summary.Failed} falhas. Evidências: {output}");
        if(summary.Failed>0)throw new InvalidOperationException($"Falharam {summary.Failed} verificações visuais de granularidade. Consulte granularity-render-tests.json.");
    }

    private sealed record Result(string Name,bool Passed,string? Error);
    private sealed record Snapshot(SKColor[] Pixels,int Width,int Height)
    {
        public string Hash=>Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(Pixels.AsSpan())));
        public int Ink=>Pixels.Count(p=>p.Red>25 || p.Green>25 || p.Blue>25);
        public double NormalizedEdgeEnergy
        {
            get
            {
                double edges=0,mass=0;
                for(var y=1;y<Height;y++)for(var x=1;x<Width;x++)
                {
                    var p=Pixels[y*Width+x];var left=Pixels[y*Width+x-1];var top=Pixels[(y-1)*Width+x];
                    double value=Math.Max(p.Red,Math.Max(p.Green,p.Blue));
                    mass+=value;
                    edges+=Math.Abs(value-Math.Max(left.Red,Math.Max(left.Green,left.Blue)))+Math.Abs(value-Math.Max(top.Red,Math.Max(top.Green,top.Blue)));
                }
                return edges/Math.Max(1,mass);
            }
        }
    }
}
