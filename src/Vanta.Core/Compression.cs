namespace Vanta.Core;
public sealed record MediaInfo(long Bytes, double Seconds, int Width, int Height, double Fps, string Codec, int AudioTracks, long AudioBitsPerSecond, double? VideoDurationSeconds=null, double? AudioPayloadBytes=null, double? VideoStreamRate=null)
{
    public double VideoDuration => VideoDurationSeconds??Seconds;
    public double CopiedAudioBytes => AudioPayloadBytes??(Seconds*AudioBitsPerSecond/8);
}
public sealed record Plan(string Name, string Codec, int Height, long VideoBitsPerSecond, long AudioBitsPerSecond, string Preset, bool TwoPass, bool CopyAudio = true)
{
    public double EstimateBytes(MediaInfo m) => (m.VideoDuration*VideoBitsPerSecond/8 + (CopyAudio ? m.CopiedAudioBytes : m.Seconds*AudioBitsPerSecond*m.AudioTracks/8))*PlanRules.Overhead;
    public double Savings(MediaInfo m) => 1 - EstimateBytes(m) / m.Bytes;
}
public static class PlanRules
{
    public static IReadOnlyList<string> Codecs { get; } = Array.AsReadOnly(new[]{"libx264","libx265"});
    public static IReadOnlyList<string> Presets { get; } = Array.AsReadOnly(new[]{"veryfast","fast","medium","slow"});
    public const long MinimumVideoRate=64000;
    public const long MaximumVideoRate=100_000_000;
    public const double Overhead=1.005;
    public static void ValidateSource(MediaInfo m)
    {
        if(m.Bytes<=0 || !double.IsFinite(m.Seconds) || m.Seconds<=0 || m.Width<2 || m.Height<2 || m.Width%2!=0 || m.Height%2!=0 || m.AudioTracks<0 || m.AudioBitsPerSecond<0 || (m.AudioTracks>0 && m.AudioBitsPerSecond==0) || (m.AudioTracks==0 && m.AudioBitsPerSecond!=0) || !double.IsFinite(m.VideoDuration) || m.VideoDuration<=0 || !double.IsFinite(m.CopiedAudioBytes) || m.CopiedAudioBytes<0 || (m.AudioTracks==0 && m.CopiedAudioBytes!=0) || (m.VideoStreamRate is double vr && (!double.IsFinite(vr)||vr<=0)))
            throw new ArgumentException("Invalid source metadata");
    }
    public static void Validate(Plan p,MediaInfo m)
    {
        ValidateSource(m);
        if(!Codecs.Contains(p.Codec) || !Presets.Contains(p.Preset) || p.Height<2 || p.Height%2!=0 || p.Height>m.Height || p.VideoBitsPerSecond<MinimumVideoRate || p.VideoBitsPerSecond>MaximumVideoRate || !p.CopyAudio || p.AudioBitsPerSecond!=0 || p.TwoPass)
            throw new ArgumentException("Unsupported compression settings");
    }
    public static long TargetRate(MediaInfo m,double targetBytes)
    {
        ValidateSource(m);
        if(!double.IsFinite(targetBytes)||targetBytes<=0) throw new ArgumentException("Invalid target size");
        double available=(targetBytes/Overhead-m.CopiedAudioBytes)*8/m.VideoDuration;
        if(!double.IsFinite(available)||available<MinimumVideoRate||available>MaximumVideoRate) throw new ArgumentException("Target outside supported bitrate budget (including copied audio)");
        return (long)Math.Floor(available);
    }
}
public static class Planner
{
    // Conservative source-aware heuristic. No measured visual quality claims.
    public static IReadOnlyList<Plan> Propose(MediaInfo m)
    {
        PlanRules.ValidateSource(m);
        var result=new List<Plan>();
        double sourceRate=m.VideoStreamRate??((m.Bytes-m.CopiedAudioBytes)*8/m.VideoDuration);
        if(!double.IsFinite(sourceRate)||sourceRate<=0) return result;
        bool efficient=m.Codec is "hevc" or "av1" or "vp9";
        bool knownFps=double.IsFinite(m.Fps)&&m.Fps>0;
        double density=knownFps?sourceRate/(m.Width*(double)m.Height*m.Fps):0;
        // Avoid aggressive recommendations for already compressed sources.
        double[] fractions=efficient||density<.035?[.95,.80,.65]:[.80,.55,.30];
        string[] names=["Quality","Balanced","Compact"];
        for(int i=0;i<3;i++)
        {
            int height=i==2&&density>=.035?Math.Min(m.Height,720):m.Height;
            height-=height%2;
            double rate=sourceRate*fractions[i];
            if(!double.IsFinite(rate)||rate<PlanRules.MinimumVideoRate)continue;
            var p=new Plan(names[i],"libx265",height,(long)Math.Min(rate,PlanRules.MaximumVideoRate),0,"medium",false,true);
            if(p.Savings(m)>0 && !result.Any(q=>q.Height==p.Height&&q.VideoBitsPerSecond==p.VideoBitsPerSecond&&q.Codec==p.Codec))result.Add(p);
        }
        return result;
    }
}
public static class Decisions
{
    public static bool Dominates(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        Validate(a); Validate(b);
        if(a.Count != b.Count) throw new ArgumentException("Dimension mismatch");
        return a.Zip(b).All(x=>x.First>=x.Second) && a.Zip(b).Any(x=>x.First>x.Second);
    }
    public static double GeometricUtility(IReadOnlyList<double> v, IReadOnlyList<double> w)
    {
        Validate(v);
        if(v.Count!=w.Count || w.Any(x=>!double.IsFinite(x)||x<0) || Math.Abs(w.Sum()-1)>1e-9) throw new ArgumentException("Invalid weights");
        double sum=0;
        for(int i=0;i<v.Count;i++) { if(w[i]==0) continue; if(v[i]==0) return 0; sum+=w[i]*Math.Log(v[i]); }
        return Math.Exp(sum);
    }
    private static void Validate(IReadOnlyList<double> v) { if(v.Count==0 || v.Any(x=>!double.IsFinite(x)||x<0||x>1)) throw new ArgumentException("Invalid score"); }
}