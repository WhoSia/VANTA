namespace Vanta.Core;

// Lightweight stale-plan guard, not a cryptographic identity proof.
// Windows read sharing prevents ordinary concurrent writes/deletion while encoding.
public sealed record SourceStamp(long Bytes,DateTime LastWriteUtc,DateTime CreationUtc)
{
    public static SourceStamp Capture(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists)throw new FileNotFoundException("Source is no longer available.",path);
        return new(file.Length,file.LastWriteTimeUtc,file.CreationTimeUtc);
    }
    public void RequireUnchanged(string path)
    {
        if(this!=Capture(path))
            throw new IOException("Source changed since analysis. Remove/re-add or reopen the video to review a new plan.");
    }
}
