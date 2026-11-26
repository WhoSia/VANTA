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
    // Research: output video timestamps and full audio/video decode in one strict FFmpeg pass.
    // Source timestamps and audio packet comparisons remain independent ffprobe reads.
    internal static async Task ValidateSingleDecodeAsync(string ffmpeg,string ffprobe,string source,string output,int audioTracks,CancellationToken ct)
    {
        double origin=await CompareAsync(ffprobe,source,output,"v:0",true,.002,null,ct,false,ffmpeg);
        for(int i=0;i<audioTracks;i++)
            await CompareAsync(ffprobe,source,output,$"a:{i}",false,.002,origin,ct,false);
    }
    private static Process OpenStrictDecoder(string exe,string path)
    {
        var psi=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var arg in new[]{"-v","error","-xerror","-nostdin","-copyts","-i",path,"-map","0:V:0","-map","0:a?","-fps_mode","passthrough","-stats_enc_pre:v","pipe:1","-stats_enc_pre_fmt:v","{n} {ptsi} {tbi}","-f","null","-"})
            psi.ArgumentList.Add(arg);
        return Process.Start(psi)??throw new InvalidOperationException("Cannot start strict timestamp decoder");
    }
    internal static double ParseStrictFrame(string line,long expectedOrdinal)
    {
        var fields=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(fields.Length!=3||!long.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out long n)||n!=expectedOrdinal||
           !long.TryParse(fields[1],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long pts)||pts==long.MinValue)
            throw new InvalidDataException("Invalid decoded frame ordinal or timestamp");
        var tb=fields[2].Split('/');
        if(tb.Length!=2||!int.TryParse(tb[0],NumberStyles.None,CultureInfo.InvariantCulture,out int numerator)||numerator<=0||
           !int.TryParse(tb[1],NumberStyles.None,CultureInfo.InvariantCulture,out int denominator)||denominator<=0)
            throw new InvalidDataException("Invalid decoded frame time base");
        double seconds=pts*(double)numerator/denominator;
        if(!double.IsFinite(seconds))throw new InvalidDataException("Nonfinite decoded frame timestamp");
        // Match ffprobe's six-decimal timestamp convention before tolerance comparison.
        return Math.Round(seconds,6,MidpointRounding.AwayFromZero);
    }
    private static async Task<double?> ReadStrictAsync(StreamReader reader,long ordinal,CancellationToken ct)
    {
        var line=await reader.ReadLineAsync(ct);
        return line is null?null:ParseStrictFrame(line,ordinal);
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
        var rest=line.AsSpan();
        while(true)
        {
            int separator=rest.IndexOf('|');
            var field=separator<0?rest:rest[..separator];
            if(field.Length>key.Length&&field[key.Length]=='='&&field[..key.Length].SequenceEqual(key.AsSpan()))
            {
                if(!double.TryParse(field[(key.Length+1)..],NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value))
                    throw new InvalidDataException("Missing presentation timestamp");
                return value;
            }
            if(separator<0)return null;
            rest=rest[(separator+1)..];
        }
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
    private static async Task<double> CompareAsync(string exe,string source,string output,string selector,bool video,double tolerance,double? sharedOrigin,CancellationToken ct,bool payload,string? strictDecoder=null)
    {
        ct.ThrowIfCancellationRequested();
        using var a=Open(exe,source,selector,video,payload);
        Task<string> ae=a.StandardError.ReadToEndAsync();
        Process? b=null; Task<string>? be=null;
        try
        {
            b=strictDecoder is null?Open(exe,output,selector,video,payload):OpenStrictDecoder(strictDecoder,output);
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
                    y=strictDecoder is null?await ReadAsync(b.StandardOutput,key,ct):await ReadStrictAsync(b.StandardOutput,count,ct);
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
            try{await StopAsync(a,ae);}
            finally{if(b is not null){try{await StopAsync(b,be!);}finally{b.Dispose();}}}
        }
    }
    private static async Task StopAsync(Process p,Task<string> err)
    {
        try{if(!p.HasExited)p.Kill(entireProcessTree:true);}catch(InvalidOperationException){}
        await p.WaitForExitAsync(); await err;
    }
}