using System.Security.Cryptography;
namespace Vanta.Core;

// In-memory, bounded, research-only independent decoded source observation.
// The source bytes are hashed at capture and on each use. No cross-session
// persistence, no reuse for another path, no cached output or audio checks.
internal sealed class SourceClockSnapshot
{
    internal const long MaxSourceBytes=512L*1024*1024;
    internal const int MaxFrames=500_000;
    private readonly string source,probe;
    private readonly SourceStamp sourceStamp,probeStamp;
    private readonly byte[] digest;
    private readonly double[] frames;
    internal SourceClockSnapshot(string source,string probe,SourceStamp sourceStamp,SourceStamp probeStamp,byte[] digest,double[] frames)
    {
        if(digest.Length!=32||frames.Length is <1 or >MaxFrames||frames.Any(t=>!double.IsFinite(t)))
            throw new InvalidDataException("Invalid source clock snapshot");
        this.source=source;this.probe=probe;this.sourceStamp=sourceStamp;this.probeStamp=probeStamp;
        this.digest=(byte[])digest.Clone();this.frames=(double[])frames.Clone();
    }
    internal int Count=>frames.Length;
    internal double At(int i)=>frames[i];
    internal void RequireMetadata(string ffprobe,FileStream lease)
    {
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(!string.Equals(source,Path.GetFullPath(lease.Name),comparison)||!string.Equals(probe,Path.GetFullPath(ffprobe),comparison))
            throw new IOException("Source clock identity or tool path changed");
        if(lease.Length>MaxSourceBytes)throw new NotSupportedException("Source clock research byte limit");
        sourceStamp.RequireUnchanged(source);probeStamp.RequireUnchanged(probe);
    }
    internal async Task RequireUnchangedAsync(string ffprobe,FileStream lease,CancellationToken ct)
    {
        RequireMetadata(ffprobe,lease);
        byte[] observed=await HashAsync(lease,ct);
        if(!CryptographicOperations.FixedTimeEquals(digest,observed))throw new IOException("Source clock byte identity changed");
        RequireMetadata(ffprobe,lease);
    }
    internal static async Task<byte[]> HashAsync(FileStream lease,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(lease.Length>MaxSourceBytes)throw new NotSupportedException("Source clock research byte limit");
        lease.Position=0;
        try{return await SHA256.HashDataAsync(lease,ct);}
        finally{lease.Position=0;}
    }
}
