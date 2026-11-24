using System.Text.RegularExpressions;
namespace Vanta.Core;
public sealed record Toolchain(string Ffmpeg,string Ffprobe,IReadOnlyList<string> Codecs)
{
    public static async Task<Toolchain> DiscoverAsync(CancellationToken ct=default)
    {
        var candidates=new List<string>();
        var configured=Environment.GetEnvironmentVariable("VANTA_FFMPEG_DIR");
        if(!string.IsNullOrWhiteSpace(configured))
        {
            if(!Path.IsPathFullyQualified(configured))throw new InvalidOperationException("VANTA_FFMPEG_DIR must be absolute.");
            candidates.Add(configured);
        }
        else
        {
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim().Trim('"')).Where(Path.IsPathFullyQualified));
        }
        string suffix=OperatingSystem.IsWindows()?".exe":"";
        foreach(var dir in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if(string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)),Path.TrimEndingDirectorySeparator(Environment.CurrentDirectory),StringComparison.OrdinalIgnoreCase)&&configured is null)continue;
            var ff=Path.Combine(dir,"ffmpeg"+suffix);var fp=Path.Combine(dir,"ffprobe"+suffix);
            if(!File.Exists(ff)||!File.Exists(fp))continue;
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var a=await ProcessRunner.RunAsync(ff,["-version"],deadline.Token);
                var b=await ProcessRunner.RunAsync(fp,["-version"],deadline.Token);
                var enc=await ProcessRunner.RunAsync(ff,["-hide_banner","-encoders"],deadline.Token);
                var av=Regex.Match(a.Output,@"^ffmpeg version (\S+)");var bv=Regex.Match(b.Output,@"^ffprobe version (\S+)");
                if(a.ExitCode!=0||b.ExitCode!=0||enc.ExitCode!=0||!av.Success||!bv.Success||av.Groups[1].Value!=bv.Groups[1].Value)continue;
                var codecs=PlanRules.Codecs.Where(c=>Regex.IsMatch(enc.Output,@"(?m)^\s*V\S*\s+"+Regex.Escape(c)+@"\s")).ToArray();
                if(codecs.Length>0)return new(ff,fp,Array.AsReadOnly(codecs));
            }
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){}
            catch(System.ComponentModel.Win32Exception){}
        }
        throw new InvalidOperationException("Compatible FFmpeg and FFprobe not found together. Install Gyan.FFmpeg with winget, reopen VANTA, or set VANTA_FFMPEG_DIR to the absolute bin directory.");
    }
}