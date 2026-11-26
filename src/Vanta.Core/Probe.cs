using System.Text.Json;
using System.Globalization;
namespace Vanta.Core;
public sealed record MediaDetails(MediaInfo Info,IReadOnlyList<StreamTiming> Timing,bool Supported,string Reason,VisualMetadata? Visual=null)
{
    internal IReadOnlyList<AudioMetadata> Audio {get;init;}=[];
    internal IReadOnlyList<int> AudioStreamIndices {get;init;}=[];
}
public static class Probe
{
    public static async Task<MediaInfo> ReadAsync(string ffprobe,string path,CancellationToken ct=default)=>(await InspectAsync(ffprobe,path,ct)).Info;
    public static async Task<MediaDetails> InspectAsync(string ffprobe,string path,CancellationToken ct=default)
    {
        var file=new FileInfo(path);
        var r=await ProcessRunner.RunAsync(ffprobe,["-v","error","-show_format","-show_streams","-of","json",file.FullName],ct);
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error)) throw new InvalidOperationException("Media probe failed: "+r.Error);
        using var doc=JsonDocument.Parse(r.Output);
        var streams=doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var videos=streams.Where(s=>Str(s,"codec_type")=="video" && !(s.TryGetProperty("disposition",out var d)&&d.TryGetProperty("attached_pic",out var a)&&a.GetInt32()==1)).ToArray();
        if(videos.Length==0) throw new InvalidDataException("No video stream");
        var video=videos[0];
        var audios=streams.Where(s=>Str(s,"codec_type")=="audio").ToArray();
        var f=Str(video,"avg_frame_rate").Split('/');
        double fps=f.Length==2 && Num(f[1])>0 ? Num(f[0])/Num(f[1]):0;
        var seconds=Num(Str(doc.RootElement.GetProperty("format"),"duration"));
        if(!double.IsFinite(seconds)||seconds<=0) throw new InvalidOperationException("Unknown duration");
        double? Positive(double value)=>double.IsFinite(value)&&value>0?value:null;
        var videoDuration=Positive(Num(Str(video,"duration")));
        var videoRate=Positive(Num(Str(video,"bit_rate")));
        var audioPayload=audios.Select(s=>Positive(Num(Str(s,"duration")))*Positive(Num(Str(s,"bit_rate")))/8).ToArray();
        double? audioBytes=audioPayload.All(x=>x.HasValue)?audioPayload.Sum(x=>x.GetValueOrDefault()):null;
        var info=new MediaInfo(file.Length,seconds,video.GetProperty("width").GetInt32(),video.GetProperty("height").GetInt32(),fps,Str(video,"codec_name"),audios.Length,audios.Sum(s=>(long)Math.Max(0,double.IsFinite(Num(Str(s,"bit_rate")))?Num(Str(s,"bit_rate")):0)),videoDuration,audioBytes,videoRate);
        var timing=new[]{video}.Concat(audios).Select(s=>new StreamTiming(Str(s,"codec_type"),Num(Str(s,"start_time")),Num(Str(s,"duration")))).ToArray();
        var visual=VisualMetadata.Read(video);
        var audioMetadata=audios.Select(AudioMetadata.Read).ToArray();
        string reason="";
        if(streams.Length!=1+audios.Length||videos.Length!=1) reason="Additional video, subtitle or data streams are not supported yet";
        else if(Str(video,"pix_fmt")!="yuv420p" || new[]{"smpte2084","arib-std-b67"}.Contains(Str(video,"color_transfer"))) reason="Only 8-bit SDR yuv420p is supported yet";
        else if(visual.HasTransform) reason="Display rotation or transformation is unsupported; normalization has not been validated";
        else if(info.Width%2!=0||info.Height%2!=0||info.Width<2||info.Height<2) reason="Unsupported dimensions";
        else if(audios.Any(s=>Str(s,"codec_name")!="aac" || !(Num(Str(s,"bit_rate"))>0))) reason="Only AAC audio with known bitrate is supported in copy mode";
        else if(audioMetadata.Any(a=>!a.Supported)) reason="Only AAC mono, stereo, 5.1 or 7.1 with known sample rate and supported channel layout is supported";
        else if(timing.Any(t=>!double.IsFinite(t.Start)||!double.IsFinite(t.Duration)||t.Duration<=0)) reason="Stream timing unavailable; cannot validate";
        return new(info,timing,reason=="",reason,visual){Audio=Array.AsReadOnly(audioMetadata),AudioStreamIndices=Array.AsReadOnly(audios.Select(s=>s.TryGetProperty("index",out var idx)&&idx.ValueKind==JsonValueKind.Number&&idx.TryGetInt32(out int n)?n:-1).ToArray())};
    }
    private static string Str(JsonElement e,string key)=>e.TryGetProperty(key,out var v)?v.ToString():"";
    private static double Num(string s)=>double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v)?v:double.NaN;
}