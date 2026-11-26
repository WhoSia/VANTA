namespace Vanta.Core;

// Conservative ABR admission estimate, not a quota or a guarantee that a
// filesystem will remain writable. Reservations are process-local only.
// The active file bytes may already reduce free space; counting outstanding
// estimates as well intentionally errs on the cautious side.
internal sealed class ScratchBudget
{
    internal static readonly ScratchBudget Shared=new(LocalVolume);
    private const long Headroom=32L*1024*1024;
    private readonly Func<string,(string Volume,long FreeBytes)?> available;
    private readonly Dictionary<string,long> reserved=new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate=new();
    internal ScratchBudget(Func<string,(string Volume,long FreeBytes)?> available)=>this.available=available;

    internal static long Required(double estimatedBytes,bool segmented)
    {
        double bytes=Math.Ceiling(estimatedBytes*(segmented?3.0:1.25))+Headroom;
        if(!double.IsFinite(estimatedBytes)||estimatedBytes<=0||!double.IsFinite(bytes)||bytes>=long.MaxValue)
            throw new ArgumentException("Invalid scratch-space estimate");
        return checked((long)bytes);
    }
    internal IDisposable? Reserve(string directory,double estimatedBytes,bool segmented)
    {
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException(directory);
        long required=Required(estimatedBytes,segmented);
        lock(gate)
        {
            var volume=available(directory);
            if(volume is null)return null;
            var (key,free)=volume.Value;
            if(free<0)return null;
            long held=reserved.GetValueOrDefault(key);
            if(held>free||required>free-held)
                throw new IOException($"Estimated scratch space is insufficient: need {required/1048576d:F1} MiB, {Math.Max(0,free-held)/1048576d:F1} MiB available after active reservations. Choose a different destination or lower the bitrate budget.");
            reserved[key]=checked(held+required);
            return new Lease(this,key,required);
        }
    }
    private void Release(string key,long size)
    {
        lock(gate)
        {
            long remaining=reserved[key]-size;
            if(remaining==0)reserved.Remove(key);
            else reserved[key]=remaining;
        }
    }
    private sealed class Lease(ScratchBudget owner,string volume,long size):IDisposable
    {
        private int released;
        public void Dispose(){if(Interlocked.Exchange(ref released,1)==0)owner.Release(volume,size);}
    }
    private static (string Volume,long FreeBytes)? LocalVolume(string directory)
    {
        // DriveInfo's root is not a dependable volume identity for network
        // shares, mount points, junctions or symlinks. Limit the known-volume
        // admission gate to ordinary Windows fixed-drive paths.
        if(!OperatingSystem.IsWindows())return null;
        try
        {
            var path=Path.GetFullPath(directory);
            string? root=Path.GetPathRoot(path);
            if(root is null||root.Length!=3||root[1]!=':')return null;
            for(var d=new DirectoryInfo(path);d is not null;d=d.Parent)
                if((d.Attributes&FileAttributes.ReparsePoint)!=0)return null;
            var drive=new DriveInfo(root);
            return drive.IsReady&&drive.DriveType==DriveType.Fixed?(root,drive.AvailableFreeSpace):null;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {return null;}
    }
}
