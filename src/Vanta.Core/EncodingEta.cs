namespace Vanta.Core;

// Elapsed time is monotonic job time supplied by the caller. This predicts only
// the current continuous encoding phase, never validation or publication.
public sealed class EncodingEta
{
    private readonly Queue<(double Seconds,double Fraction)> samples=new();
    private double lastElapsed=-1,lastProgress=-1,lastFraction;
    private bool active;
    private int advances;
    private readonly double minimumDelta;
    private readonly double? mediaDuration;
    public EncodingEta(double? mediaSeconds=null)
    {
        if(mediaSeconds is double duration&&(!double.IsFinite(duration)||duration<=0))throw new ArgumentOutOfRangeException(nameof(mediaSeconds));
        // Two percent is unattainable in a bounded recent window for long slow jobs.
        // Require at least half a second of reported media time, capped at the
        // original 2% threshold for short inputs. This is not a confidence bound.
        minimumDelta=mediaSeconds is double length?Math.Min(.02,.5/length):.02;
        mediaDuration=mediaSeconds;
    }
    public void Observe(EncodeProgress progress,TimeSpan elapsed)
    {
        double t=elapsed.TotalSeconds;
        if(t<0||t<lastElapsed)return;
        lastElapsed=t;
        if(progress.Stage!="Encoding"||progress.Fraction is not double f||!double.IsFinite(f)||f<0||f>=1)
        {
            active=false;samples.Clear();lastProgress=-1;lastFraction=0;advances=0;return;
        }
        if(!active){active=true;samples.Clear();lastProgress=t;lastFraction=f;advances=0;samples.Enqueue((t,f));return;}
        if(f<=lastFraction)return;
        if(t-lastProgress>10)
        {
            samples.Clear();lastProgress=t;lastFraction=f;advances=0;samples.Enqueue((t,f));return;
        }
        advances=Math.Min(2,advances+1);
        // Keep at most one sample per second and a bounded recent window.
        lastFraction=f;lastProgress=t;
        if(t-samples.Last().Seconds>=1)
        {
            samples.Enqueue((t,f));
            while(samples.Count>12||samples.Count>2&&t-samples.Peek().Seconds>20)samples.Dequeue();
        }
    }
    private (double Span,double Delta)? RecentWindow(TimeSpan elapsed)
    {
        double t=elapsed.TotalSeconds;
        if(!active||advances<2||samples.Count<2||t<lastElapsed||t-lastProgress>10||lastFraction>=.999)return null;
        var first=samples.Peek();var last=samples.Last();
        double span=last.Seconds-first.Seconds,delta=last.Fraction-first.Fraction;
        if(span<3||delta<minimumDelta)return null;
        return (span,delta);
    }
    // Media seconds processed per wall second over the same recent observation window.
    // Not CPU utilization, and not the rate of validation or publication.
    public double? RealtimeRate(TimeSpan elapsed)
    {
        var window=RecentWindow(elapsed);
        if(mediaDuration is not double duration||window is null)return null;
        double rate=duration*window.Value.Delta/window.Value.Span;
        return double.IsFinite(rate)&&rate>0?rate:null;
    }
    public TimeSpan? Remaining(TimeSpan elapsed)
    {
        var window=RecentWindow(elapsed);
        if(window is null)return null;
        double seconds=(1-lastFraction)*window.Value.Span/window.Value.Delta;
        if(!double.IsFinite(seconds)||seconds<0||seconds>TimeSpan.FromDays(30).TotalSeconds)return null;
        return TimeSpan.FromSeconds(Math.Max(1,seconds));
    }
}
