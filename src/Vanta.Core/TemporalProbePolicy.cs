namespace Vanta.Core;

// Research-only, bounded low-resolution activity ranking. Each value is
// a heuristic, not a quality or complexity guarantee. Ties use time order.
internal static class TemporalProbePolicy
{
    internal const int Width=64,Height=36,Rate=4,MaxSeconds=86400;
    internal const int FrameBytes=Width*Height;
    internal sealed record Activity(int Second,double Spatial,double Temporal)
    {
        internal double Score=>Spatial+Temporal;
    }
    internal static async Task<Activity[]> ReadAsync(Stream raw,int seconds,CancellationToken ct)
    {
        if(seconds<3||seconds>MaxSeconds)throw new ArgumentOutOfRangeException(nameof(seconds));
        var result=new Activity[seconds];
        byte[] current=new byte[FrameBytes],previous=new byte[FrameBytes];
        bool hasPrevious=false;
        for(int sec=0;sec<seconds;sec++)
        {
            long spatial=0,temporal=0;
            for(int f=0;f<Rate;f++)
            {
                ct.ThrowIfCancellationRequested();
                await raw.ReadExactlyAsync(current.AsMemory(),ct);
                for(int y=0;y<Height;y++)
                for(int x=0;x<Width;x++)
                {
                    int i=y*Width+x;
                    if(x>0)spatial+=Math.Abs(current[i]-current[i-1]);
                    if(y>0)spatial+=Math.Abs(current[i]-current[i-Width]);
                    if(hasPrevious)temporal+=Math.Abs(current[i]-previous[i]);
                }
                (current,previous)=(previous,current);hasPrevious=true;
            }
            result[sec]=new(sec,spatial/(Rate*FrameBytes*2d*255),temporal/(Rate*FrameBytes*255d));
        }
        if(await raw.ReadAsync(current.AsMemory(0,1),ct)!=0)throw new InvalidDataException("Unexpected additional activity samples");
        return result;
    }
    internal static int[] Select(IReadOnlyList<Activity> windows)
    {
        if(windows.Count<3||windows.Count>MaxSeconds)throw new ArgumentException("Invalid activity window count");
        for(int i=0;i<windows.Count;i++)
        {
            var w=windows[i];
            if(w.Second!=i||!double.IsFinite(w.Spatial)||!double.IsFinite(w.Temporal)||
                w.Spatial is <0 or >1||w.Temporal is <0 or >1)throw new InvalidDataException("Invalid activity window");
        }
        var ordered=windows.OrderBy(w=>w.Score).ThenBy(w=>w.Second).ToArray();
        return new[]{ordered[0].Second,ordered[ordered.Length/2].Second,ordered[^1].Second}.Order().ToArray();
    }
}
