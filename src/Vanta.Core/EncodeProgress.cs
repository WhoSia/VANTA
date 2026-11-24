using System.Globalization;
namespace Vanta.Core;
public sealed record EncodeProgress(string Stage,double? Fraction);
public sealed class ProgressParser(double duration,IProgress<EncodeProgress>? observer)
{
    private double last;
    public void Read(string line)
    {
        if(!line.StartsWith("out_time_us=",StringComparison.Ordinal)||!long.TryParse(line["out_time_us=".Length..],NumberStyles.Integer,CultureInfo.InvariantCulture,out long us)||!double.IsFinite(duration)||duration<=0)return;
        var value=Math.Clamp(us/1_000_000d/duration,0,.999);
        if(value<=last)return;
        last=value;
        observer?.Report(new("Encoding",last));
    }
}