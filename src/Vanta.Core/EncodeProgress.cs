using System.Globalization;
namespace Vanta.Core;
public enum VerificationPhase { Metadata, Timeline, AudioIdentity, FullDecode, Combined, DecodedAudio, Publishing }
public sealed record EncodeProgress(string Stage,double? Fraction)
{
    // Informational only: no phase implies verification success or a percentage.
    public VerificationPhase? Verification {get;init;}
    public int? DisplayPercent=>Fraction is double f&&double.IsFinite(f)
        ? Stage=="Complete"?100:(int)Math.Floor(Math.Clamp(f,0,.99)*100)
        :null;
    public string? DisplayPercentText
    {
        get
        {
            if(DisplayPercent is not int percent)return null;
            if(percent!=0||Fraction is not double f||f<=0)return percent.ToString(CultureInfo.InvariantCulture)+"%";
            double hundredths=Math.Floor(f*10000);
            return hundredths<1?"<0.01%":(hundredths/100).ToString("0.00",CultureInfo.InvariantCulture)+"%";
        }
    }
}
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