using System.Diagnostics;
using System.Globalization;
namespace Vanta.Core;

// Independent per-track FIFO joins. Inter-stream mux order may change,
// but within-track order, packet payload and common-video-clock offsets may not.
internal sealed class AudioPacketJoin
{
    internal readonly record struct Packet(int Stream,double Pts,string Hash);
    private readonly Dictionary<int,int> sourceMap,outputMap;
    private readonly Queue<Packet>[] sourcePending,outputPending;
    private readonly long[] matched;
    private readonly int limit;
    private readonly double origin;
    private int pending;
    internal int PeakPending {get;private set;}
    internal AudioPacketJoin(IReadOnlyList<int> source,IReadOnlyList<int> output,double origin,int limit=8192)
    {
        if(source.Count!=output.Count||source.Count is <1 or >32||!double.IsFinite(origin)||limit<1||limit>65536)
            throw new InvalidDataException("Invalid interleaved audio bounds");
        static Dictionary<int,int> Map(IReadOnlyList<int> indices)
        {
            var map=new Dictionary<int,int>();
            for(int i=0;i<indices.Count;i++)
                if(indices[i]<0||!map.TryAdd(indices[i],i))throw new InvalidDataException("Missing or duplicate audio stream index");
            return map;
        }
        sourceMap=Map(source);outputMap=Map(output);this.origin=origin;this.limit=limit;
        sourcePending=source.Select(_=>new Queue<Packet>()).ToArray();
        outputPending=output.Select(_=>new Queue<Packet>()).ToArray();
        matched=new long[source.Count];
    }
    internal static Packet Parse(string line)
    {
        if(!line.StartsWith("packet|",StringComparison.Ordinal))throw new InvalidDataException("Unexpected audio packet row");
        string? index=null,pts=null,hash=null;
        foreach(string field in line.Split('|'))
        {
            if(field.StartsWith("stream_index=",StringComparison.Ordinal)){if(index is not null)throw new InvalidDataException("Duplicate stream index");index=field[13..];}
            else if(field.StartsWith("pts_time=",StringComparison.Ordinal)){if(pts is not null)throw new InvalidDataException("Duplicate packet PTS");pts=field[9..];}
            else if(field.StartsWith("data_hash=",StringComparison.Ordinal)){if(hash is not null)throw new InvalidDataException("Duplicate payload hash");hash=field[10..];}
        }
        if(!int.TryParse(index,NumberStyles.None,CultureInfo.InvariantCulture,out int stream)||stream<0||
           !double.TryParse(pts,NumberStyles.Float,CultureInfo.InvariantCulture,out double time)||!double.IsFinite(time)||
           hash is null||hash.Length!=71||!hash.StartsWith("SHA256:",StringComparison.OrdinalIgnoreCase)||!hash[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid audio packet record");
        return new(stream,time,hash.ToUpperInvariant());
    }
    internal void Push(bool source,Packet packet)
    {
        var map=source?sourceMap:outputMap;
        if(!map.TryGetValue(packet.Stream,out int track))throw new InvalidDataException("Unexpected audio stream");
        var own=source?sourcePending[track]:outputPending[track];
        var peer=source?outputPending[track]:sourcePending[track];
        if(peer.TryDequeue(out var other))
        {
            pending--;
            var a=source?packet:other;var b=source?other:packet;
            if(a.Hash!=b.Hash||!double.IsFinite(a.Pts)||!double.IsFinite(b.Pts)||Math.Abs((b.Pts-a.Pts)-origin)>.002)
                throw new InvalidDataException($"Audio payload or timestamp changed: track {track}, packet {matched[track]}");
            matched[track]++;
        }
        else
        {
            if(pending>=limit)throw new InvalidDataException("Interleaved packet alignment exceeded research buffer");
            own.Enqueue(packet);pending++;PeakPending=Math.Max(PeakPending,pending);
        }
    }
    internal void Complete()
    {
        if(pending!=0||matched.Any(n=>n==0))throw new InvalidDataException("Missing, empty or additional audio packets");
    }
}
internal static class InterleavedAudio
{
    private static Process Open(string exe,string path)
    {
        var psi=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var x in new[]{"-v","error","-select_streams","a","-show_packets","-show_entries","packet=stream_index,pts_time,data_hash","-show_data_hash","sha256","-of","compact=p=1:nk=0",path})psi.ArgumentList.Add(x);
        return Process.Start(psi)??throw new InvalidOperationException("Cannot start interleaved audio probe");
    }
    private static async Task<AudioPacketJoin.Packet?> Read(StreamReader reader,CancellationToken ct)
    {
        while(await reader.ReadLineAsync(ct) is string row)
            if(!string.IsNullOrWhiteSpace(row))return AudioPacketJoin.Parse(row);
        return null;
    }
    internal static async Task<int> ValidateAsync(string exe,string source,string output,IReadOnlyList<int> sourceIndices,IReadOnlyList<int> outputIndices,double origin,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var join=new AudioPacketJoin(sourceIndices,outputIndices,origin);
        using var a=Open(exe,source);var ae=a.StandardError.ReadToEndAsync();
        await using var sourceResource=ProcessResourceTrace.Track(a,"source-interleaved-audio");
        Process? b=null;Task<string>? be=null;
        ProcessResourceTrace.Observation? outputResource=null;
        try
        {
            b=Open(exe,output);be=b.StandardError.ReadToEndAsync();
            outputResource=ProcessResourceTrace.Track(b,"output-interleaved-audio");
            bool aDone=false,bDone=false;
            while(!aDone||!bDone)
            {
                if(!aDone){var p=await Read(a.StandardOutput,ct);if(p is null)aDone=true;else join.Push(true,p.Value);}
                if(!bDone){var p=await Read(b.StandardOutput,ct);if(p is null)bDone=true;else join.Push(false,p.Value);}
            }
            await a.WaitForExitAsync(ct);await b.WaitForExitAsync(ct);
            if(a.ExitCode!=0||b.ExitCode!=0||!string.IsNullOrWhiteSpace(await ae)||!string.IsNullOrWhiteSpace(await be))
                throw new InvalidDataException("Interleaved audio probe failed");
            join.Complete();
            return join.PeakPending;
        }
        finally
        {
            try{await Stop(a,ae);}
            finally{if(b is not null){try{await Stop(b,be!);}finally{try{if(outputResource is not null)await outputResource.DisposeAsync();}finally{b.Dispose();}}}}
        }
    }
    private static async Task Stop(Process p,Task<string> err)
    {
        try{if(!p.HasExited)p.Kill(entireProcessTree:true);}catch(InvalidOperationException){}
        await p.WaitForExitAsync();await err;
    }
}
