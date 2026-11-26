using System.Text;
namespace Vanta.Core;

// Never accumulate an entire diagnostic stream from a long-running tool.
// Drain to EOF even after hitting the cap, so redirected pipes cannot deadlock.
// Oversized machine-readable stdout is a failure, not a truncated success.
internal static class BoundedProcessText
{
    internal const int OutputLimit=8*1024*1024;
    internal const int DiagnosticLimit=128*1024;
    internal const string Truncation="\n[VANTA: process text exceeded capture limit]";
    internal static async Task<(string Text,bool Truncated)> CaptureAsync(TextReader reader,int limit)
    {
        if(limit<1)throw new ArgumentOutOfRangeException(nameof(limit));
        var text=new StringBuilder(Math.Min(limit,4096));
        var buffer=new char[4096];
        bool truncated=false;
        int n;
        while((n=await reader.ReadAsync(buffer.AsMemory()))!=0)
        {
            int take=Math.Min(n,limit-text.Length);
            if(take>0)text.Append(buffer,0,take);
            if(take<n)truncated=true;
        }
        return(text.ToString(),truncated);
    }
    internal static async Task<string> ReadOutputAsync(TextReader reader)
    {
        var r=await CaptureAsync(reader,OutputLimit);
        if(r.Truncated)throw new InvalidDataException("Machine-readable process output exceeded capture limit");
        return r.Text;
    }
    internal static async Task<string> ReadDiagnosticAsync(TextReader reader)
    {
        var r=await CaptureAsync(reader,DiagnosticLimit);
        return r.Truncated?r.Text+Truncation:r.Text;
    }
}
