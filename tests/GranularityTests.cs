using System.Text.Json;
using WorshipStudio.Models;
using WorshipStudio.Services;
using Xunit;

namespace WorshipStudio.Tests;

public sealed class GranularityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorshipStudio-granularity-" + Guid.NewGuid().ToString("N"));
    public GranularityTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public void CatalogContainsEveryReferenceNameAndNoDuplicateIdentityOrBehavior()
    {
        string[] requested =
        [
            "Nenhuma", "Fade Suave", "Pluma Caindo", "Fade Down Elegante", "Fluxo de Seda", "Arco Gracioso", "Cascata", "Blur Reveal",
            "Cinematográfico", "Film Fade", "Typewriter Blur", "Typewriter Blur Rápido", "Typewriter Blur Forte", "Máquina de Escrever", "Emerge da Profundidade", "Focus Pull",
            "Horizonte", "Luz Divina", "Vinda do Céu", "Glória Flash", "Pulso Sagrado", "Slide da Esquerda", "Slide da Direita", "Snap",
            "Mask Up", "Mask Down", "Zoom Power", "Pop Burst", "Rotate In", "Spring", "Wiggle", "Impacto",
            "Subida Dramática", "Gold Reveal", "Luxury Fade", "Real", "Tender Rise", "Heart Pulse", "Soul Rise", "Abraço Gentil",
            "Névoa", "Maré", "Glow Emerge", "Ressurreição", "Glória Eterna", "Espírito Vivo", "Vento de Fé", "Coração Ardente",
            "Florescer Sagrado", "Ascensão", "Templo", "Maná dos Céus", "Shofar", "Luz da Cruz", "Pomba Descendo", "Jelly Pop",
            "Carimbo", "RGB Split", "Kinetic", "IG Pop", "Squeeze", "Tilt", "Punch", "Flicker", "Mirror Flip", "Spring Bounce"
        ];
        var catalog = AnimationPresets.All;
        Assert.Equal(requested.Length, catalog.Count);
        Assert.Equal(requested.OrderBy(x=>x,StringComparer.Ordinal), catalog.Select(p=>p.Name).OrderBy(x=>x,StringComparer.Ordinal));
        Assert.Equal(catalog.Count, catalog.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(catalog, p=> { Assert.False(string.IsNullOrWhiteSpace(p.Category)); Assert.False(string.IsNullOrWhiteSpace(p.CategoryLabel)); });
        // Names and IDs are deliberately excluded: relabeling the same motion is not a new preset.
        var behaviors = catalog.Select(p=>JsonSerializer.Serialize(new { p.Profile, p.Duration, p.Stagger, p.Granularity, p.Easing })).ToArray();
        Assert.Equal(catalog.Count, behaviors.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CatalogDefaultsApplyAnIndependentCopyAndPreserveTypographyAndMedia()
    {
        var original = new StageStyle { FontFamily="Georgia", FontSize=92, Bold=false,TextColor="#FFD166",BackgroundMode="Image",BackgroundPath="media/background.jpg" };
        var preset = AnimationPresets.All.Single(p=>p.Name=="Typewriter Blur Forte");
        var updated = AnimationPresets.ApplyDefaults(original,preset);
        Assert.NotSame(original,updated);
        Assert.False(original.UsePresetAnimation);
        Assert.True(updated.UsePresetAnimation);
        Assert.Equal(preset.Id, updated.AnimationPreset);
        Assert.Equal(preset.Duration,updated.AnimationDuration);
        Assert.Equal(preset.Stagger,updated.AnimationStagger);
        Assert.Equal(original.FontFamily,updated.FontFamily);
        Assert.Equal(original.Bold,updated.Bold);
        Assert.Equal(original.TextColor,updated.TextColor);
        Assert.Equal(original.BackgroundPath,updated.BackgroundPath);
        Assert.Equal("Nenhuma",AnimationPresets.Find("unknown-future-preset").Name);
    }

    [Fact]
    public void DurationIncludesDelayAndStaggerAndUnitsStartIndependently()
    {
        var style = new StageStyle { UsePresetAnimation=true, AnimationDuration=.8,AnimationStagger=.15,AnimationDelay=.25,AnimationEasing="Linear" };
        Assert.Equal(1.5,AnimationTimeline.TotalDuration(style,4),10);
        Assert.Equal(0,AnimationTimeline.Progress(style,.2,0,4));
        Assert.Equal(.5,AnimationTimeline.Progress(style,.65,0,4),10);
        Assert.Equal(0,AnimationTimeline.Progress(style,.65,3,4));
        Assert.Equal(1,AnimationTimeline.Progress(style,1.5,3,4));
        Assert.Equal(1,AnimationTimeline.Progress(style,12,0,4));
    }

    [Fact]
    public void ZeroDurationCutsEachUnitAtItsOwnConfiguredStart()
    {
        var style=new StageStyle{UsePresetAnimation=true,AnimationDuration=0,AnimationDelay=.2,AnimationStagger=.2};
        Assert.Equal(.6,AnimationTimeline.TotalDuration(style,3),10);
        Assert.Equal(0,AnimationTimeline.Progress(style,.19,0,3));
        Assert.Equal(1,AnimationTimeline.Progress(style,.201,0,3));
        Assert.Equal(0,AnimationTimeline.Progress(style,.201,1,3));
        Assert.Equal(1,AnimationTimeline.Progress(style,.401,1,3));
        Assert.Equal(0,AnimationTimeline.Progress(style,.401,2,3));
        Assert.Equal(1,AnimationTimeline.Progress(style,.601,2,3));
    }

    [Fact]
    public void OrderingChangesFirstUnitAndRandomOrderIsAStablePermutation()
    {
        Assert.Equal(0,AnimationTimeline.OrderIndex(0,7,"Forward"));
        Assert.Equal(6,AnimationTimeline.OrderIndex(0,7,"Reverse"));
        var center = Enumerable.Range(0,7).Select(i=>AnimationTimeline.OrderIndex(i,7,"Center")).ToArray();
        Assert.Equal(0,center[3]);
        Assert.True(center[0]>center[2] && center[6]>center[4]);
        var random = Enumerable.Range(0,17).Select(i=>AnimationTimeline.OrderIndex(i,17,"Random")).ToArray();
        Assert.Equal(Enumerable.Range(0,17),random.OrderBy(x=>x));
        Assert.Equal(random,Enumerable.Range(0,17).Select(i=>AnimationTimeline.OrderIndex(i,17,"Random")));
        Assert.NotEqual(Enumerable.Range(0,17),random);
    }

    [Theory]
    [InlineData("Linear")]
    [InlineData("EaseOut")]
    [InlineData("EaseInOut")]
    [InlineData("Back")]
    [InlineData("Elastic")]
    [InlineData("Bounce")]
    public void EasingStartsAndFinishesExactlyAndStaysFinite(string easing)
    {
        Assert.Equal(0,AnimationTimeline.Ease(0,easing),10);
        Assert.Equal(1,AnimationTimeline.Ease(1,easing),10);
        Assert.All(Enumerable.Range(0,101).Select(i=>AnimationTimeline.Ease(i/100d,easing)),p=>Assert.True(double.IsFinite(p)));
        if(easing=="Linear")Assert.Equal(.35,AnimationTimeline.Ease(.35,easing),10);
        if(easing=="Back" || easing=="Elastic")Assert.Contains(Enumerable.Range(1,99).Select(i=>AnimationTimeline.Ease(i/100d,easing)),p=>p>1.001);
    }

    [Fact]
    public void LoopHoldsCompleteDuringPauseThenRestartsTheSameSequence()
    {
        var style = new StageStyle { UsePresetAnimation=true,AnimationDuration=.5,AnimationStagger=.1,AnimationDelay=.2,AnimationLoop=true,AnimationLoopPause=.4 };
        var total = AnimationTimeline.TotalDuration(style,3);
        Assert.Equal(.9,total,10);
        Assert.Equal(1,AnimationTimeline.Progress(style,total+.2,2,3));
        Assert.Equal(AnimationTimeline.Progress(style,.35,0,3),AnimationTimeline.Progress(style,total+.4+.35,0,3),10);
    }

    [Fact]
    public void GranularSettingsSurviveLibrarySceneAndBackupExactly()
    {
        var style = new StageStyle
        {
            UsePresetAnimation=true,AnimationPreset=AnimationPresets.All.Single(p=>p.Name=="RGB Split").Id,
            AnimationGranularity="Character",AnimationDuration=1.3,AnimationStagger=.035,AnimationDelay=.4,
            AnimationOrder="Center",AnimationEasing="Elastic",AnimationStrength=1.6,AnimationLoop=true,AnimationLoopPause=2.4,
            FontFamily="Consolas",Bold=false,TextColor="#FFD166",LyricAnimation="Wave",LyricAnimationIntensity=.2
        };
        var store = new LibraryStore(Path.Combine(_directory,"original"));
        store.Save(new StudioDocument {Style=style});
        Assert.Equal(style,new LibraryStore(store.DirectoryPath).Load().Style);
        Assert.Equal(style,JsonSerializer.Deserialize<StageStyle>(JsonSerializer.Serialize(style,LibraryStore.JsonOptions),LibraryStore.JsonOptions));
        var backup = Path.Combine(_directory,"granularity.wstudio");
        store.Backup(backup,new StudioDocument {Style=style});
        Assert.Equal(style,new LibraryStore(Path.Combine(_directory,"restored")).Restore(backup).Style);
    }

    [Fact]
    public void OlderScenesKeepLegacyAnimationsUntilGranularPresetsAreEnabled()
    {
        var old = JsonSerializer.Deserialize<StageStyle>("""{"FontFamily":"Georgia","Transition":"Words","TransitionSeconds":1.5,"LyricAnimation":"Wave","TextColor":"#FFD166"}""",LibraryStore.JsonOptions)!;
        Assert.False(old.UsePresetAnimation);
        Assert.Equal("Words",old.Transition);
        Assert.Equal("Wave",old.LyricAnimation);
        Assert.Equal("#FFD166",old.TextColor);
        Assert.Equal("Word",old.AnimationGranularity);
        Assert.Equal(.7,old.AnimationDuration);
        Assert.Equal(.06,old.AnimationStagger);
        Assert.Equal(0,old.AnimationDelay);
        Assert.Equal("Forward",old.AnimationOrder);
        Assert.False(old.AnimationLoop);
    }
}
