using System.Globalization;
using System.Text;

namespace WorshipStudio.Models;

public sealed record BackgroundChoice(string Id,string Name,bool IsProcedural)
{
    public override string ToString()=>Name;
}

/// <summary>Stable serialized IDs shared by controls, import, output and offline export.</summary>
public static class BackgroundCatalog
{
    public static IReadOnlyList<BackgroundChoice> Choices { get; }=Array.AsReadOnly(new[]
    {
        new BackgroundChoice("Aurora","Aurora fluida",true),
        new BackgroundChoice("Tunnel","Túnel 3D",true),
        new BackgroundChoice("Particles","Partículas 3D",true),
        new BackgroundChoice("GraceWaves","Ondas de graça",true),
        new BackgroundChoice("LightRays","Raios de luz",true),
        new BackgroundChoice("Galaxy","Galáxia em movimento",true),
        new BackgroundChoice("Bokeh","Bokeh suave",true),
        new BackgroundChoice("WireLandscape","Horizonte 3D",true),
        new BackgroundChoice("Ocean","Oceano sereno",true),
        new BackgroundChoice("Ribbons","Fitas de adoração",true),
        new BackgroundChoice("Prism","Prismas de luz",true),
        new BackgroundChoice("Solid","Cor sólida",false),
        new BackgroundChoice("Image","Imagem local",false),
        new BackgroundChoice("Video","Vídeo local",false)
    });

    private static readonly IReadOnlyDictionary<string,string> Aliases=CreateAliases();
    public static string Normalize(string? value)=>TryNormalize(value,out var id)?id:"Aurora";
    public static bool TryNormalize(string? value,out string id)
    {
        if(value!=null&&Aliases.TryGetValue(Key(value),out var found)){id=found;return true;}
        id="Aurora";return false;
    }
    public static bool IsProcedural(string? value)
    {
        var id=Normalize(value);
        return Choices.Any(choice=>choice.Id==id&&choice.IsProcedural);
    }
    private static IReadOnlyDictionary<string,string> CreateAliases()
    {
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var choice in Choices){aliases[Key(choice.Id)]=choice.Id;aliases[Key(choice.Name)]=choice.Id;}
        foreach(var entry in new[]{("Túnel","Tunnel"),("Partículas","Particles"),("Sólido","Solid"),("Solid color","Solid"),
            ("Imagem","Image"),("Vídeo","Video"),("Galáxia","Galaxy"),("Ondas","GraceWaves"),("Horizonte","WireLandscape"),
            ("Oceano","Ocean"),("Fitas","Ribbons"),("Prismas","Prism")})aliases[Key(entry.Item1)]=entry.Item2;
        return aliases;
    }
    private static string Key(string value)
    {
        var key=new StringBuilder();
        foreach(var c in value.Trim().Normalize(NormalizationForm.FormD))
            if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(c))key.Append(char.ToUpperInvariant(c));
        return key.ToString();
    }
}
