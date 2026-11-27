namespace Vanta.Core;

// Same-setting historical reference only. No validation percentage, no promise
// of completion; never keep counting down after the observed duration is exceeded.
public static class CompletionReference
{
    private static readonly TimeSpan Limit=TimeSpan.FromDays(30);
    public static TimeSpan? Tail(EncodeReport report)
    {
        if(report.Validation<=TimeSpan.Zero||report.Publication<TimeSpan.Zero||
            report.Validation>Limit||report.Publication>Limit)return null;
        var tail=report.Validation+report.Publication;
        return tail<=Limit?tail:null;
    }
    public static TimeSpan? TotalRemaining(TimeSpan? encoding,TimeSpan? tail)
    {
        if(encoding is not TimeSpan a||tail is not TimeSpan b||a<=TimeSpan.Zero||b<=TimeSpan.Zero||a>Limit||b>Limit)return null;
        var sum=a+b;
        return sum<=Limit?sum:null;
    }
    public static TimeSpan? ValidationRemaining(TimeSpan? tail,TimeSpan elapsed)
    {
        if(tail is not TimeSpan t||t<=TimeSpan.Zero||t>Limit||elapsed<TimeSpan.Zero||elapsed>=t)return null;
        return t-elapsed;
    }
}
