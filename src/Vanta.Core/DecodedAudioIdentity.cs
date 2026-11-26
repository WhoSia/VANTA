namespace Vanta.Core;

// Additional exact decoded-sample guard for extended audio copy. Compressed
// payload equality alone does not establish that the same samples are played.
internal static class DecodedAudioIdentity
{
    internal static async Task ValidateAsync(string exe,string source,string output,int tracks,CancellationToken ct)
    {
        if(tracks<=0)throw new ArgumentOutOfRangeException(nameof(tracks));
        async Task<string> Read(string path,int track)
        {
            var r=await ProcessRunner.RunAsync(exe,["-v","error","-xerror","-nostdin","-i",path,"-map",$"0:a:{track}","-c:a","pcm_s32le","-f","hash","-hash","sha256","-"],ct);
            string value=r.Output.Trim();
            if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error)||value.Length!=71||!value.StartsWith("SHA256=",StringComparison.OrdinalIgnoreCase)||!value[7..].All(Uri.IsHexDigit))
                throw new InvalidDataException("Decoded audio hash unavailable");
            return value.ToUpperInvariant();
        }
        for(int i=0;i<tracks;i++)
        {
            ct.ThrowIfCancellationRequested();
            string a=await Read(source,i),b=await Read(output,i);
            if(a!=b)throw new InvalidDataException($"Decoded audio samples changed: track {i+1}");
        }
    }
}
