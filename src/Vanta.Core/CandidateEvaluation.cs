namespace Vanta.Core;
public sealed record CandidateProfile(double Savings, double PixelRetention, double? RateDensity, double? MeasuredQuality)
{
    // Only comparable known dimensions participate; no invented quality scores.
    public double[] ComparableVector => [Savings,PixelRetention];
}
public static class CandidateEvaluation
{
    public static CandidateProfile Evaluate(Plan p,MediaInfo m)
    {
        PlanRules.Validate(p,m);
        double ratio=p.Height/(double)m.Height;
        double pixels=ratio*ratio;
        double? density=double.IsFinite(m.Fps)&&m.Fps>0?p.VideoBitsPerSecond/(m.Width*(double)m.Height*pixels*m.Fps):null;
        return new(Math.Clamp(p.Savings(m),0,1),pixels,density,null);
    }
}