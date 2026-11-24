using System.Text.Json;
using System.Globalization;
namespace Vanta.Core;
public static class Probe
{
    public static async Task<MediaInfo> ReadAsync(string ffprobe,string path,CancellationToken ct=default)
    {
        var file=new FileInfo(path);
        var r=await ProcessRunner.RunAsync(ffprobe,["-v","error","-show_format","-show_streams","-of","json",file.FullName],ct);
        if(r.ExitCode!=0) throw new InvalidOperationException(r.Error);
        using var doc=JsonDocument.Parse(r.Output);
        var streams=doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var video=streams.First(s=>Str(s,"codec_type")=="video" && !(s.TryGetProperty("disposition",out var d)&&d.TryGetProperty("attached_pic",out var a)&&a.GetInt32()==1));
        var audios=streams.Where(s=>Str(s,"codec_type")=="audio").ToArray();
        var f=Str(video,"avg_frame_rate").Split('/');
        double fps=f.Length==2 && Num(f[1])>0 ? Num(f[0])/Num(f[1]):0;
        var seconds=Num(Str(doc.RootElement.GetProperty("format"),"duration"));
        if(seconds<=0) throw new InvalidOperationException("Unknown duration");
        return new(file.Length,seconds,video.GetProperty("width").GetInt32(),video.GetProperty("height").GetInt32(),fps,Str(video,"codec_name"),audios.Length,audios.Sum(s=>(long)Num(Str(s,"bit_rate"))));
    }
    private static string Str(JsonElement e,string key)=>e.TryGetProperty(key,out var v)?v.ToString():"";
    private static double Num(string s)=>double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v)?v:0;
}