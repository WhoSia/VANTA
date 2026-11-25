namespace Vanta.Core;

public sealed record BatchTotals(int Completed,double InputBytes,double OutputBytes,TimeSpan JobTime)
{
    public double? Savings=>InputBytes>0?1-OutputBytes/InputBytes:null;
    public static BatchTotals From(IEnumerable<EncodeReport> reports)
    {
        var count=0;double input=0,output=0;var elapsed=TimeSpan.Zero;
        foreach(var report in reports)
        {
            count++;input+=report.InputBytes;output+=report.OutputBytes;elapsed+=report.Total;
        }
        return new(count,input,output,elapsed);
    }
}
public static class BatchRecovery
{
    public static bool CanEdit(string state,bool analyzed)=>analyzed&&state is ("Ready" or "Skipped" or "Failed" or "Canceled");
    public static bool CanRetry(string state,bool analyzed)=>analyzed&&state is ("Failed" or "Canceled");
}
