using System.Diagnostics;
using System.Globalization;
namespace Vanta.Core;
// Streaming comparison: one stream at a time, bounded memory. Video frame
// presentation timestamps are decoded order; audio packet timestamps are copied.
public static class PacketTimeline
{
    public static async Task ValidateAsync(string ffprobe,string source,string output,int audioTracks,CancellationToken ct=default)
    {
        await CompareAsync(ffprobe,source,output,"v:0",true,.002,ct);
        for(int i=0;i<audioTracks;i++)
            await CompareAsync(ffprobe,source,output,$"a:{i}",false,.002,ct);
    }
    private static Process Open(string exe,string path,string selector,bool video)
    {
        var psi=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var a in new[]{"-v","error","-select_streams",selector,video?"-show_frames":"-show_packets","-show_entries",video?"frame=best_effort_timestamp_time":"packet=pts_time","-of","compact=p=0:nk=0",path}) psi.ArgumentList.Add(a);
        return Process.Start(psi)??throw new InvalidOperationException("Cannot start timeline probe");
    }
    private static async Task<double?> ReadAsync(StreamReader reader,string key,CancellationToken ct)
    {
        while(await reader.ReadLineAsync(ct) is string line)
        {
            var entry=line.Split('|').FirstOrDefault(s=>s.StartsWith(key+"=",StringComparison.Ordinal));
            if(entry is null) continue; // possible frame side data line
            if(!double.TryParse(entry[(key.Length+1)..],NumberStyles.Float,CultureInfo.InvariantCulture,out var t)||!double.IsFinite(t)) throw new InvalidDataException("Missing presentation timestamp");
            return t;
        }
        return null;
    }
    private static async Task CompareAsync(string exe,string source,string output,string selector,bool video,double tolerance,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var a=Open(exe,source,selector,video);
        Task<string> ae=a.StandardError.ReadToEndAsync();
        Process? b=null; Task<string>? be=null;
        try
        {
            b=Open(exe,output,selector,video);
            be=b.StandardError.ReadToEndAsync();
            double? origin=null; long count=0;
            var key=video?"best_effort_timestamp_time":"pts_time";
            while(true)
            {
                var x=await ReadAsync(a.StandardOutput,key,ct);
                var y=await ReadAsync(b.StandardOutput,key,ct);
                if(x is null && y is null) break;
                if(x is null || y is null) throw new InvalidDataException($"Timestamp count mismatch: {selector}");
                double delta=y.Value-x.Value;
                origin??=delta;
                if(Math.Abs(delta-origin.Value)>tolerance) throw new InvalidDataException($"Timestamp drift in {selector} at {count}");
                count++;
            }
            await a.WaitForExitAsync(ct); await b.WaitForExitAsync(ct);
            if(a.ExitCode!=0 || b.ExitCode!=0 || count==0) throw new InvalidDataException($"Timeline probe failed: {selector}");
        }
        finally
        {
            await StopAsync(a,ae);
            if(b is not null){try{await StopAsync(b,be!);}finally{b.Dispose();}}
        }
    }
    private static async Task StopAsync(Process p,Task<string> err)
    {
        try{if(!p.HasExited)p.Kill(entireProcessTree:true);}catch(InvalidOperationException){}
        await p.WaitForExitAsync(); await err;
    }
}