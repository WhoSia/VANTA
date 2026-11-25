using System.Text.Json;
using System.Globalization;
namespace Vanta.Core;

// Preserve declared display geometry and known SDR color tags.
// Rotation/display matrices are rejected until a separate normalization contract exists.
public sealed record VisualMetadata(double? DisplayRatio,string Range,string Space,string Transfer,string Primaries,bool HasTransform)
{
    private static string Tag(JsonElement v,string key)
    {
        var value=v.TryGetProperty(key,out var x)?x.ToString():"";
        return value is "unknown" or "unspecified" or "N/A"?"":value;
    }
    public static VisualMetadata Read(JsonElement video)
    {
        var ratio=Tag(video,"display_aspect_ratio").Split(':');
        double? dar=ratio.Length==2&&double.TryParse(ratio[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var a)&&double.TryParse(ratio[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var b)&&a>0&&b>0&&double.IsFinite(a/b)?a/b:null;
        bool transform=video.TryGetProperty("side_data_list",out var side)&&side.EnumerateArray().Any(s=>Tag(s,"side_data_type")=="Display Matrix"||s.TryGetProperty("rotation",out _));
        transform|=video.TryGetProperty("tags",out var tags)&&tags.TryGetProperty("rotate",out var rotation)&&rotation.ToString()!="0";
        return new(dar,Tag(video,"color_range"),Tag(video,"color_space"),Tag(video,"color_transfer"),Tag(video,"color_primaries"),transform);
    }
    public void ValidateOutput(VisualMetadata actual)
    {
        if(HasTransform||actual.HasTransform)throw new InvalidDataException("Display transform is unsupported.");
        if(DisplayRatio is double expected&&(actual.DisplayRatio is not double observed||Math.Abs(observed/expected-1)>.001))
            throw new InvalidDataException("Display aspect ratio changed.");
        foreach(var (key,source,dest) in new[]{("range",Range,actual.Range),("space",Space,actual.Space),("transfer",Transfer,actual.Transfer),("primaries",Primaries,actual.Primaries)})
            if(source.Length>0&&source!=dest)throw new InvalidDataException("Declared color "+key+" changed.");
    }
}
