namespace Vanta.Core;

// A session-local observational reference, not a calibrated prediction interval.
// Callers keep single and batch sessions separate, and scope batch observations by concurrency.
// No timing data is persisted or transferred between machines.
public sealed record TimingObservation(string SourceKey,MediaInfo Media,Plan Plan,int? Threads,string ToolKey,TimeSpan Total,SourceStamp? Stamp=null);
public sealed record TimingRange(TimeSpan Low,TimeSpan High,int Sources);
public sealed class TimingHistory
{
    private readonly List<TimingObservation> observations=new();
    public int Count=>observations.Count;
    private static double SourceRate(MediaInfo m)=>m.VideoStreamRate??((m.Bytes-m.CopiedAudioBytes)*8/m.VideoDuration);
    private static bool Valid(TimingObservation o)
    {
        try{PlanRules.Validate(o.Plan,o.Media);}catch(ArgumentException){return false;}
        return !string.IsNullOrWhiteSpace(o.SourceKey)&&!string.IsNullOrWhiteSpace(o.ToolKey)&&
            double.IsFinite(o.Media.Fps)&&o.Media.Fps>0&&
            double.IsFinite(SourceRate(o.Media))&&SourceRate(o.Media)>0&&o.Total>TimeSpan.Zero&&o.Total<=TimeSpan.FromDays(30);
    }
    public void Record(TimingObservation o)
    {
        if(!Valid(o)||(!SimilarEligible(o)&&o.Stamp is null)||(o.Stamp is SourceStamp stamp&&stamp.Bytes!=o.Media.Bytes))return;
        observations.RemoveAll(x=>string.Equals(x.SourceKey,o.SourceKey,StringComparison.OrdinalIgnoreCase)&&
            (x.Plan with{Name=""})==(o.Plan with{Name=""})&&x.Threads==o.Threads&&x.ToolKey==o.ToolKey);
        if(observations.Count>=32)observations.RemoveAt(0);
        observations.Add(o);
    }
    private static bool SimilarEligible(TimingObservation o)=>o.Media.Seconds>=10&&o.Media.VideoDuration>=10;
    public TimeSpan? Previous(string sourceKey,SourceStamp stamp,MediaInfo media,Plan plan,int? threads,string toolKey)
    {
        var query=new TimingObservation(sourceKey,media,plan,threads,toolKey,TimeSpan.FromSeconds(1),stamp);
        if(!Valid(query)||stamp.Bytes!=media.Bytes)return null;
        return observations.LastOrDefault(o=>string.Equals(o.SourceKey,sourceKey,StringComparison.OrdinalIgnoreCase)&&o.Stamp==stamp&&o.Media==media&&
            (o.Plan with{Name=""})==(plan with{Name=""})&&o.Threads==threads&&o.ToolKey==toolKey)?.Total;
    }
    private static bool Near(double x,double y,double lo,double hi)=>double.IsFinite(x)&&double.IsFinite(y)&&x>0&&y>0&&x/y>=lo&&x/y<=hi;
    public TimingRange? Estimate(string sourceKey,MediaInfo media,Plan plan,int? threads,string toolKey)
    {
        var query=new TimingObservation(sourceKey,media,plan,threads,toolKey,TimeSpan.FromSeconds(1));
        if(!Valid(query)||!SimilarEligible(query))return null;
        var samples=observations.Where(o=>SimilarEligible(o)&&!string.Equals(o.SourceKey,sourceKey,StringComparison.OrdinalIgnoreCase)&&
            o.ToolKey==toolKey&&o.Threads==threads&&o.Media.Codec==media.Codec&&o.Media.Width==media.Width&&o.Media.Height==media.Height&&
            o.Media.AudioTracks==media.AudioTracks&&o.Plan.Codec==plan.Codec&&o.Plan.Preset==plan.Preset&&o.Plan.Height==plan.Height&&
            Near(o.Media.Fps,media.Fps,.98,1.02)&&Near(o.Media.Seconds,media.Seconds,.5,2)&&Near(o.Media.VideoDuration,media.VideoDuration,.5,2)&&
            Near(SourceRate(o.Media),SourceRate(media),.8,1.25)&&Near(o.Plan.VideoBitsPerSecond,plan.VideoBitsPerSecond,.8,1.25)&&
            (media.AudioTracks==0||Near(o.Media.CopiedAudioBytes/o.Media.Seconds,media.CopiedAudioBytes/media.Seconds,.8,1.25)))
            .GroupBy(o=>o.SourceKey,StringComparer.OrdinalIgnoreCase).Select(g=>g.Last()).ToArray();
        if(samples.Length<3)return null;
        var scaled=samples.Select(o=>o.Total.TotalSeconds*media.Seconds/o.Media.Seconds).ToArray();
        double low=scaled.Min(),high=scaled.Max();
        if(!double.IsFinite(low)||!double.IsFinite(high)||low<=0||high>TimeSpan.FromDays(30).TotalSeconds||high/low>3)return null;
        return new(TimeSpan.FromSeconds(Math.Max(1,Math.Floor(low))),TimeSpan.FromSeconds(Math.Max(1,Math.Ceiling(high))),samples.Length);
    }
}
