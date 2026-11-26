namespace Vanta.Core;

// Elapsed time is monotonic job time supplied by the caller. This predicts only
// the current continuous encoding phase, never validation or publication.
public sealed class EncodingEta
{
    private readonly Queue<(double Seconds,double Fraction)> samples=new();
    private double lastElapsed=-1,lastProgress=-1,lastFraction;
    private bool active;
    public void Observe(EncodeProgress progress,TimeSpan elapsed)
    {
        double t=elapsed.TotalSeconds;
        if(t<0||t<lastElapsed)return;
        lastElapsed=t;
        if(progress.Stage!="Encoding"||progress.Fraction is not double f||!double.IsFinite(f)||f<0||f>=1)
        {
            active=false;samples.Clear();lastProgress=-1;lastFraction=0;return;
        }
        if(!active){active=true;samples.Clear();lastProgress=t;lastFraction=f;samples.Enqueue((t,f));return;}
        if(f<=lastFraction)return;
        // Keep at most one sample per second and a bounded recent window.
        lastFraction=f;lastProgress=t;
        if(t-samples.Last().Seconds>=1)
        {
            samples.Enqueue((t,f));
            while(samples.Count>12||samples.Count>2&&t-samples.Peek().Seconds>20)samples.Dequeue();
        }
    }
    public TimeSpan? Remaining(TimeSpan elapsed)
    {
        double t=elapsed.TotalSeconds;
        if(!active||samples.Count<2||t<lastElapsed||t-lastProgress>10||lastFraction>=.999)return null;
        var first=samples.Peek();var last=samples.Last();
        double span=last.Seconds-first.Seconds,delta=last.Fraction-first.Fraction;
        if(span<3||delta<.02)return null;
        double seconds=(1-lastFraction)*span/delta;
        if(!double.IsFinite(seconds)||seconds<0||seconds>TimeSpan.FromDays(30).TotalSeconds)return null;
        return TimeSpan.FromSeconds(Math.Max(1,seconds));
    }
}
