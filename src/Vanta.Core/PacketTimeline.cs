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
    internal static async Task ValidateSingleDecodeAsync(string ffmpeg,string ffprobe,string source,string output,int audioTracks,CancellationToken ct,SourceClockSnapshot? snapshot=null,bool compareAudioPayloads=false)
    {
        double origin=await VideoOriginAsync(ffmpeg,ffprobe,source,output,ct,snapshot);
        for(int i=0;i<audioTracks;i++)
            await CompareAsync(ffprobe,source,output,$"a:{i}",false,.002,origin,ct,compareAudioPayloads);
    }
    internal static async Task<int> ValidateSingleDecodeInterleavedAsync(string ffmpeg,string ffprobe,string source,string output,IReadOnlyList<int> sourceAudio,IReadOnlyList<int> outputAudio,CancellationToken ct,SourceClockSnapshot? snapshot=null)
    {
        double origin=await VideoOriginAsync(ffmpeg,ffprobe,source,output,ct,snapshot);
        return await InterleavedAudio.ValidateAsync(ffprobe,source,output,sourceAudio,outputAudio,origin,ct);
    }
    // Capture the same independent decoded-frame oracle used by the baseline.
    internal static async Task<SourceClockSnapshot> CaptureSourceClockAsync(string ffprobe,string source,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        source=Path.GetFullPath(source);ffprobe=Path.GetFullPath(ffprobe);
        using var lease=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(lease.Length>SourceClockSnapshot.MaxSourceBytes)throw new NotSupportedException("Source clock research byte limit");
        var stamp=SourceStamp.Capture(source);var toolStamp=SourceStamp.Capture(ffprobe);
        byte[] before=await SourceClockSnapshot.HashAsync(lease,ct);
        var frames=new List<double>();
        using var p=Open(ffprobe,source,"v:0",true,false);
        await using var resource=ProcessResourceTrace.Track(p,"source-clock-capture");
        var stderr=BoundedProcessText.ReadDiagnosticAsync(p.StandardError);
        try
        {
            while(await ReadAsync(p.StandardOutput,"best_effort_timestamp_time",ct) is double time)
            {
                if(frames.Count>=SourceClockSnapshot.MaxFrames)throw new NotSupportedException("Source clock research frame limit");
                frames.Add(time);
            }
            await p.WaitForExitAsync(ct);
            if(p.ExitCode!=0||frames.Count==0||!string.IsNullOrWhiteSpace(await stderr))throw new InvalidDataException("Independent source clock unavailable");
        }
        finally{await StopAsync(p,stderr);}
        stamp.RequireUnchanged(source);toolStamp.RequireUnchanged(ffprobe);
        byte[] after=await SourceClockSnapshot.HashAsync(lease,ct);
        if(!before.AsSpan().SequenceEqual(after))throw new IOException("Source changed during clock capture");
        return new SourceClockSnapshot(source,ffprobe,stamp,toolStamp,before,frames.ToArray());
    }
    private static async Task<double> VideoOriginAsync(string ffmpeg,string ffprobe,string source,string output,CancellationToken ct,SourceClockSnapshot? snapshot)
    {
        if(snapshot is null)return await CompareAsync(ffprobe,source,output,"v:0",true,.002,null,ct,false,ffmpeg);
        ct.ThrowIfCancellationRequested();
        using var lease=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
        await snapshot.RequireUnchangedAsync(ffprobe,lease,ct);
        using var p=OpenStrictDecoder(ffmpeg,output);
        await using var resource=ProcessResourceTrace.Track(p,"output-strict-decode");
        var stderr=BoundedProcessText.ReadDiagnosticAsync(p.StandardError);
        double? origin=null;int count=0;
        try
        {
            while(await ReadStrictAsync(p.StandardOutput,count,ct) is double time)
            {
                if(count>=snapshot.Count)throw new InvalidDataException("Additional decoded output frame");
                double delta=time-snapshot.At(count);origin??=delta;
                if(!double.IsFinite(delta)||Math.Abs(delta-origin.Value)>.002)throw new InvalidDataException($"Snapshot timestamp drift at {count}");
                count++;
            }
            await p.WaitForExitAsync(ct);
            if(p.ExitCode!=0||count!=snapshot.Count||count==0||!string.IsNullOrWhiteSpace(await stderr))
                throw new InvalidDataException("Snapshot output decode or frame count failed");
        }
        finally{await StopAsync(p,stderr);}
        await snapshot.RequireUnchangedAsync(ffprobe,lease,ct);
        return origin!.Value;
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
           !long.TryParse(fields[1],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long pts)||(pts is long.MinValue or long.MaxValue))
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
        await using var sourceResource=ProcessResourceTrace.Track(a,video?"source-video-clock":"source-audio-packets");
        Task<string> ae=BoundedProcessText.ReadDiagnosticAsync(a.StandardError);
        Process? b=null; Task<string>? be=null;
        ProcessResourceTrace.Observation? outputResource=null;
        try
        {
            b=strictDecoder is null?Open(exe,output,selector,video,payload):OpenStrictDecoder(strictDecoder,output);
            outputResource=ProcessResourceTrace.Track(b,strictDecoder is not null?"output-strict-decode":video?"output-video-clock":"output-audio-packets");
            be=BoundedProcessText.ReadDiagnosticAsync(b.StandardError);
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
                if(!double.IsFinite(delta)||!double.IsFinite(origin.Value)||Math.Abs(delta-origin.Value)>tolerance) throw new InvalidDataException($"Timestamp drift in {selector} at {count}");
                count++;
            }
            await a.WaitForExitAsync(ct); await b.WaitForExitAsync(ct);
            if(a.ExitCode!=0 || b.ExitCode!=0 || count==0 || !string.IsNullOrWhiteSpace(await ae) || !string.IsNullOrWhiteSpace(await be)) throw new InvalidDataException($"Timeline probe failed: {selector}");
            return origin!.Value;
        }
        finally
        {
            try{await StopAsync(a,ae);}
            finally{if(b is not null){try{await StopAsync(b,be!);}finally{try{if(outputResource is not null)await outputResource.DisposeAsync();}finally{b.Dispose();}}}}
        }
    }
    private static async Task StopAsync(Process p,Task<string> err)
    {
        try{if(!p.HasExited)p.Kill(entireProcessTree:true);}catch(InvalidOperationException){}
        await p.WaitForExitAsync(); await err;
    }
}