namespace Vanta.Core;
public sealed record StreamTiming(string Kind, double Start, double Duration);
public static class Timeline
{
    public static void Validate(IReadOnlyList<StreamTiming> source,IReadOnlyList<StreamTiming> output,double fps)
    {
        if(source.Count==0 || source.Count!=output.Count || source[0].Kind!="video" || output[0].Kind!="video") throw new InvalidDataException("Stream layout changed");
        // Metadata endpoint check only; not proof of perceptual sync.
        var tolerance=Math.Max(.05, double.IsFinite(fps)&&fps>0 ? 1/fps : .05);
        for(int i=0;i<source.Count;i++)
        {
            var a=source[i]; var b=output[i];
            if(a.Kind!=b.Kind || !Valid(a) || !Valid(b)) throw new InvalidDataException("Missing or incompatible stream timing");
            var delta=(b.Start-output[0].Start)-(a.Start-source[0].Start);
            if(Math.Abs(delta)>tolerance || Math.Abs(delta+b.Duration-a.Duration)>tolerance) throw new InvalidDataException($"Relative stream timing changed: {a.Kind} {i}");
        }
    }
    private static bool Valid(StreamTiming t)=>double.IsFinite(t.Start)&&double.IsFinite(t.Duration)&&t.Duration>0;
}