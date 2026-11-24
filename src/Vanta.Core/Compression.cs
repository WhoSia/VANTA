namespace Vanta.Core;
public sealed record MediaInfo(long Bytes, double Seconds, int Width, int Height, double Fps, string Codec, int AudioTracks, long AudioBitsPerSecond);
public sealed record Plan(string Name, string Codec, int Height, long VideoBitsPerSecond, long AudioBitsPerSecond, string Preset, bool TwoPass, bool CopyAudio = true)
{
    public double EstimateBytes(MediaInfo m) => m.Seconds * (VideoBitsPerSecond + (CopyAudio ? m.AudioBitsPerSecond : AudioBitsPerSecond * (double)m.AudioTracks)) / 8 * 1.005;
    public double Savings(MediaInfo m) => 1 - EstimateBytes(m) / m.Bytes;
}
public static class Planner
{
    public static IReadOnlyList<Plan> Propose(MediaInfo m)
    {
        if (m.Bytes <= 0 || !double.IsFinite(m.Seconds) || m.Seconds <= 0 || m.Height <= 0 || m.Width <= 0 || m.AudioBitsPerSecond < 0 || (m.AudioTracks > 0 && m.AudioBitsPerSecond == 0)) throw new ArgumentException("Invalid source metadata");
        var total = m.Bytes * 8.0 / m.Seconds;
        var result = new List<Plan>();
        foreach (var (name, fraction, height, audio) in new[] { ("Quality", .80, m.Height, 192000L), ("Balanced", .55, m.Height, 128000L), ("Compact", .30, Math.Min(720,m.Height), 96000L) })
        {
            var available = total * fraction / 1.005 - m.AudioBitsPerSecond;
            if (height<2 || available < 64000 || available > long.MaxValue) continue;
            var p = new Plan(name,"libx265",height-height%2,(long)available,0,"medium",false,true);
            if (p.Savings(m)>0) result.Add(p);
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