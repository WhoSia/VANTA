namespace Vanta.Core;

// Budget suggestions, not measured quality levels. Retain manual entry.
public static class QuickSettings
{
    public static IReadOnlyList<int> Heights(MediaInfo m)
    {
        PlanRules.ValidateSource(m);
        return new[]{m.Height,2160,1440,1080,720,480,360,240,144}.Where(h=>h<=m.Height).Distinct().OrderByDescending(h=>h).ToArray();
    }
    public static IReadOnlyList<long> Rates(MediaInfo m)
    {
        PlanRules.ValidateSource(m);
        double source=m.VideoStreamRate??((m.Bytes-m.CopiedAudioBytes)*8/m.VideoDuration);
        long ceiling=(long)Math.Clamp(source,PlanRules.MinimumVideoRate,PlanRules.MaximumVideoRate);
        return new long[]{ceiling,64_000,128_000,250_000,500_000,1_000_000,1_500_000,2_000_000,3_000_000,5_000_000,8_000_000,12_000_000,20_000_000,40_000_000,80_000_000}.Where(r=>r<=ceiling).Distinct().OrderByDescending(r=>r).ToArray();
    }
    public static Plan? MoreCompression(Plan plan,MediaInfo m)
    {
        PlanRules.Validate(plan,m);
        if(plan.VideoBitsPerSecond==PlanRules.MinimumVideoRate)return null;
        return plan with {Name="Custom",VideoBitsPerSecond=Math.Max(PlanRules.MinimumVideoRate,plan.VideoBitsPerSecond*3/4)};
    }
}
