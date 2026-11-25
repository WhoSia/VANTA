using System.Diagnostics;
using System.Globalization;
namespace Vanta.Core;
// Streaming comparison: one stream at a time, bounded memory. Video frame
// presentation timestamps are decoded order; audio packet timestamps are copied.
public static class PacketTimeline
{
    public static async Task ValidateAsync(string ffprobe,string source,string output,int audioTracks,CancellationToken ct=default,bool compareAudioPayloads=false)
    {
        const double tolerance=.002;
        double origin=await CompareAsync(ffprobe,source,output,"v:0",true,tolerance,null,ct,false);
        for(int i=0;i<audioTracks;i++)
            await CompareAsync(ffprobe,source,output,$"a:{i}",false,tolerance,origin,ct,compareAudioPayloads);
    }
    private static Process Open(string exe,string path,string selector,bool video,bool payload)
    {
        var psi=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        var args=new List<string>{"-v","error","-select_streams",selector,video?"-show_frames":"-show_packets","-show_entries",video?"frame=best_effort_timestamp_time":payload?"packet=pts_time,data_hash":"packet=pts_time"};
        if(payload)args.AddRange(["-show_data_hash","sha256"]);
        args.AddRange(["-of",payload?"compact=p=1:nk=0":"compact=p=0:nk=0",path]);
        foreach(var a in args)psi.ArgumentList.Add(a);
        return Process.Start(psi)??throw new InvalidOperationException("Cannot start timeline probe");
    }
    private static async Task<double?> ReadAsync(StreamReader reader,string key,CancellationToken ct)
    {
        while(await reader.ReadLineAsync(ct) is string line)
        {
            if(ParseTimestamp(line,key) is double time)return time; // skip possible frame side data
        }
        return null;
    }
    internal static double? ParseTimestamp(string line,string key)
    {
        var entry=line.Split('|').FirstOrDefault(s=>s.StartsWith(key+"=",StringComparison.Ordinal));
        if(entry is null)return null;
        if(!double.TryParse(entry[(key.Length+1)..],NumberStyles.Float,CultureInfo.InvariantCulture,out var t)||!double.IsFinite(t))
            throw new InvalidDataException("Missing presentation timestamp");
        return t;
    }
    private sealed record AudioPacket(double Pts,string Hash);
    private static async Task<AudioPacket?> ReadPacketAsync(StreamReader reader,CancellationToken ct)
    {
        while(await reader.ReadLineAsync(ct) is string line)
        {
            if(string.IsNullOrWhiteSpace(line))continue;
            // Explicit section prefix distinguishes packet rows from side data.
            if(!line.StartsWith("packet|",StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected packet probe record.");
            string? pts=null,hash=null;
            foreach(var field in line.Split('|'))
            {
                if(field.StartsWith("pts_time=",StringComparison.Ordinal))
                {
                    if(pts is not null)throw new InvalidDataException("Duplicate packet timestamp");
                    pts=field["pts_time=".Length..];
                }
                else if(field.StartsWith("data_hash=",StringComparison.Ordinal))
                {
                    if(hash is not null)throw new InvalidDataException("Duplicate packet hash");
                    hash=field["data_hash=".Length..];
                }
            }
            if(!double.TryParse(pts,NumberStyles.Float,CultureInfo.InvariantCulture,out var time)||!double.IsFinite(time))
                throw new InvalidDataException("Missing packet timestamp");
            if(hash is null||!hash.StartsWith("SHA256:",StringComparison.OrdinalIgnoreCase)||hash.Length!=71||!hash[7..].All(Uri.IsHexDigit))
                throw new InvalidDataException("Missing or malformed packet SHA-256");
            return new(time,hash.ToUpperInvariant());
        }
        return null;
    }
    private static async Task<double> CompareAsync(string exe,string source,string output,string selector,bool video,double tolerance,double? sharedOrigin,CancellationToken ct,bool payload)
    {
        ct.ThrowIfCancellationRequested();
        using var a=Open(exe,source,selector,video,payload);
        Task<string> ae=a.StandardError.ReadToEndAsync();
        Process? b=null; Task<string>? be=null;
        try
        {
            b=Open(exe,output,selector,video,payload);
            be=b.StandardError.ReadToEndAsync();
            double? origin=sharedOrigin; long count=0;
            var key=video?"best_effort_timestamp_time":"pts_time";
            while(true)
            {
                double? x,y;
                if(payload)
                {
                    var ap=await ReadPacketAsync(a.StandardOutput,ct);
                    var bp=await ReadPacketAsync(b.StandardOutput,ct);
                    x=ap?.Pts;y=bp?.Pts;
                    if(ap is not null&&bp is not null&&ap.Hash!=bp.Hash)
                        throw new InvalidDataException($"Audio packet payload changed in {selector} at {count}");
                }
                else
                {
                    x=await ReadAsync(a.StandardOutput,key,ct);
                    y=await ReadAsync(b.StandardOutput,key,ct);
                }
                if(x is null && y is null) break;
                if(x is null || y is null) throw new InvalidDataException($"Timestamp count mismatch: {selector}");
                double delta=y.Value-x.Value;
                origin??=delta;
                if(Math.Abs(delta-origin.Value)>tolerance) throw new InvalidDataException($"Timestamp drift in {selector} at {count}");
                count++;
            }
            await a.WaitForExitAsync(ct); await b.WaitForExitAsync(ct);
            if(a.ExitCode!=0 || b.ExitCode!=0 || count==0 || !string.IsNullOrWhiteSpace(await ae) || !string.IsNullOrWhiteSpace(await be)) throw new InvalidDataException($"Timeline probe failed: {selector}");
            return origin!.Value;
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