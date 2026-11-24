using System.Globalization;
namespace Vanta.Core;
public static class Encoder
{
    public static async Task EncodeAsync(string ffmpeg,string ffprobe,string input,string output,Plan plan,CancellationToken ct=default)
    {
        input=Path.GetFullPath(input); output=Path.GetFullPath(output);
        if(string.Equals(input,output,StringComparison.OrdinalIgnoreCase)||File.Exists(output)) throw new IOException("Output must be a new file");
        if(!string.Equals(Path.GetExtension(output),".mp4",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only MP4 is supported in this prototype");
        if(plan.Codec!="libx265" || plan.Height<2 || plan.Height%2!=0 || plan.VideoBitsPerSecond<64000 || plan.AudioBitsPerSecond<16000 || plan.Preset!="medium") throw new ArgumentException("Unsupported plan");
        var source=await Probe.ReadAsync(ffprobe,input,ct);
        if(plan.Height>source.Height) throw new ArgumentException("Upscaling is not supported");
        var dir=Path.GetDirectoryName(output)!;
        var temp=Path.Combine(dir,$".vanta-{Guid.NewGuid():N}.mp4");
        var stats=Path.Combine(Path.GetTempPath(),$"vanta-{Guid.NewGuid():N}");
        var common=new List<string>{"-hide_banner","-nostdin","-n","-i",input,"-map","0:V:0","-c:v",plan.Codec,"-preset",plan.Preset,"-b:v",plan.VideoBitsPerSecond.ToString(CultureInfo.InvariantCulture),"-fps_mode","passthrough"};
        if(plan.Height!=source.Height) common.AddRange(["-vf",$"scale=-2:{plan.Height}"]);
        try
        {
            // Single continuous encode for each pass. No segment concatenation.
            // x265 stats paths use encoder-specific escaping; keep two-pass disabled until tested.
            if(plan.TwoPass) throw new NotSupportedException("Two-pass encoding is pending Windows path and timing validation");
            var args=new List<string>(common);
            args.AddRange(["-map","0:a?","-c:a","aac","-b:a",plan.AudioBitsPerSecond.ToString(CultureInfo.InvariantCulture),"-movflags","+faststart",temp]);
            await Checked(ffmpeg,args,ct);
            var actual=await Probe.ReadAsync(ffprobe,temp,ct);
            if(actual.AudioTracks!=source.AudioTracks || Math.Abs(actual.Seconds-source.Seconds)>0.25) throw new InvalidDataException("Output stream count or duration mismatch");
            await Checked(ffmpeg,["-v","error","-xerror","-nostdin","-i",temp,"-map","0:V:0","-map","0:a?","-f","null","-"],ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temp,output,false);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    private static async Task Checked(string exe,IEnumerable<string> args,CancellationToken ct)
    {
        var r=await ProcessRunner.RunAsync(exe,args,ct);
        if(r.ExitCode!=0) throw new InvalidOperationException(r.Error);
    }
}