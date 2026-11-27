namespace Vanta.Core;

// Scenario range based on independently supplied duration references.
// It is not a confidence interval or a CPU/contention model.
public sealed record DurationRange(TimeSpan Low,TimeSpan High);
public static class QueueTiming
{
    private static readonly TimeSpan Limit=TimeSpan.FromDays(30);
    private static bool Valid(DurationRange r)=>r.Low>=TimeSpan.Zero&&r.High>TimeSpan.Zero&&r.Low<=r.High&&r.High<=Limit;
    public static DurationRange? Remaining(DurationRange? total,TimeSpan elapsed)
    {
        if(total is null||!Valid(total)||elapsed<TimeSpan.Zero||elapsed>=total.High)return null;
        return new(total.Low>elapsed?total.Low-elapsed:TimeSpan.Zero,total.High-elapsed);
    }
    public static DurationRange? Estimate(int workers,IReadOnlyList<DurationRange?> active,IReadOnlyList<DurationRange?> queued)
    {
        if(workers is <1 or >2||active.Count>workers||active.Count+queued.Count is <1 or >100)return null;
        if(active.Concat(queued).Any(r=>r is null||!Valid(r)))return null;
        var all=active.Concat(queued).Select(r=>r!).ToArray();
        double lowWork=all.Sum(r=>r.Low.TotalSeconds),highWork=all.Sum(r=>r.High.TotalSeconds);
        double low=Math.Max(all.Max(r=>r.Low.TotalSeconds),lowWork/workers);
        double activeHigh=active.Count==0?0:active.Max(r=>r!.High.TotalSeconds);
        double queuedHigh=queued.Count==0?0:queued.Max(r=>r!.High.TotalSeconds);
        // Work-conserving independent-job scheduling: final active job, or
        // a queued job with all workers busy until its admission.
        double high=queued.Count==0?activeHigh:Math.Max(activeHigh,highWork/workers+(1-1d/workers)*queuedHigh);
        if(!double.IsFinite(low)||!double.IsFinite(high)||high<=0||high<low||high>Limit.TotalSeconds)return null;
        return new(TimeSpan.FromSeconds(Math.Floor(low)),TimeSpan.FromSeconds(Math.Ceiling(high)));
    }
}
