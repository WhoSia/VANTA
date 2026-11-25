using System.Globalization;
using System.Diagnostics;
namespace Vanta.Core;
public static class Encoder
{
    public static async Task<EncodeReport> EncodeAsync(string ffmpeg,string ffprobe,string input,string output,Plan plan,CancellationToken ct=default,IProgress<EncodeProgress>? progress=null,bool experimentalSplit=false,int? encoderThreads=null,SourceStamp? expectedSource=null)
    {
        var clock=Stopwatch.StartNew();
        var mark=TimeSpan.Zero;
        TimeSpan Lap()
        {
            var now=clock.Elapsed;var elapsed=now-mark;mark=now;return elapsed;
        }
        ct.ThrowIfCancellationRequested();
        ThreadBudget.ValidateEncoder(encoderThreads,experimentalSplit);
        input=Path.GetFullPath(input); output=Path.GetFullPath(output);
        if(string.Equals(input,output,StringComparison.OrdinalIgnoreCase)||File.Exists(output)) throw new IOException("Output must be a new file");
        if(!string.Equals(Path.GetExtension(output),".mp4",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only MP4 is supported in this prototype");
        // Hold a read-only handle across probing, encoding and validation.
        // FileShare.Read denies ordinary write/delete opens on Windows.
        using var sourceLease=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read);
        var stamp=SourceStamp.Capture(input);
        expectedSource?.RequireUnchanged(input);
        var details=await Probe.InspectAsync(ffprobe,input,ct);
        stamp.RequireUnchanged(input);
        if(!details.Supported) throw new NotSupportedException(details.Reason);
        var source=details.Info;
        PlanRules.Validate(plan,source);
        var dir=Path.GetDirectoryName(output)!;
        var temp=Path.Combine(dir,$".vanta-{Guid.NewGuid():N}.mp4");
        var common=new List<string>{"-hide_banner","-nostdin","-n","-xerror","-i",input,"-map","0:V:0","-c:v",plan.Codec,"-preset",plan.Preset,"-b:v",plan.VideoBitsPerSecond.ToString(CultureInfo.InvariantCulture),"-fps_mode","passthrough"};
        if(encoderThreads is int threads)common.AddRange(["-threads",threads.ToString(CultureInfo.InvariantCulture)]);
        if(plan.Height!=source.Height) common.AddRange(["-vf",$"scale=-2:{plan.Height}"]);
        var preparation=Lap();
        try
        {
            // Continuous encoding is the default. The explicit experimental path shares every final validation gate.
            // x265 stats paths use encoder-specific escaping; keep two-pass disabled until tested.
            if(plan.TwoPass) throw new NotSupportedException("Two-pass encoding is pending Windows path and timing validation");
            if(experimentalSplit)
            {
                progress?.Report(new("Experimental split",null));
                await ExperimentalSplit.EncodeVideoAndMuxAsync(ffmpeg,ffprobe,input,temp,details,plan,ct,encoderThreads);
            }
            else
            {
            var args=new List<string>(common);
            args.AddRange(["-map","0:a?","-c:a","copy","-tag:v",plan.Codec=="libx265"?"hvc1":"avc1","-movflags","+faststart",temp]);
            args.InsertRange(0,["-progress","pipe:1","-nostats","-loglevel","error"]);
            progress?.Report(new("Encoding",0));
            var parser=new ProgressParser(source.Seconds,progress);
            var encoded=await ProcessRunner.RunAsync(ffmpeg,args,ct,parser.Read);
            if(encoded.ExitCode!=0)throw new InvalidOperationException(encoded.Error);
            }
            var encoding=Lap();
            progress?.Report(new("Validating",null));
            var actualDetails=await Probe.InspectAsync(ffprobe,temp,ct);
            if(!actualDetails.Supported) throw new InvalidDataException(actualDetails.Reason);
            var actual=actualDetails.Info;
            if(actual.Height!=plan.Height || actual.Codec!=(plan.Codec=="libx265"?"hevc":"h264")) throw new InvalidDataException("Output codec or height mismatch");
            if(details.Visual is not null&&actualDetails.Visual is not null)details.Visual.ValidateOutput(actualDetails.Visual);
            Timeline.Validate(details.Timing,actualDetails.Timing,source.Fps);
            if(actual.AudioTracks!=source.AudioTracks || Math.Abs(actual.Seconds-source.Seconds)>0.25) throw new InvalidDataException("Output stream count or duration mismatch");
            var outputProbe=Lap();
            await PacketTimeline.ValidateAsync(ffprobe,input,temp,source.AudioTracks,ct);
            var timeline=Lap();
            var audioHash=TimeSpan.Zero;
            if(source.AudioTracks>0)
            {
                await ValidateAudioAsync(ffmpeg,input,temp,ct);
                audioHash=Lap();
            }
            await Checked(ffmpeg,["-v","error","-xerror","-nostdin","-i",temp,"-map","0:V:0","-map","0:a?","-f","null","-"],ct);
            var decode=Lap();
            var bytes=new FileInfo(temp).Length;
            ct.ThrowIfCancellationRequested();
            stamp.RequireUnchanged(input);
            File.Move(temp,output,false);
            var publication=Lap();
            var report=new EncodeReport(preparation,encoding,outputProbe,timeline,audioHash,decode,publication,mark,bytes,source.Bytes);
            // Publication is the commit point. A completion observer cannot turn
            // an already-published verified output into a failed encode.
            try { progress?.Report(new("Complete",1)); }
            catch(Exception) { }
            return report;
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    private static async Task ValidateAudioAsync(string exe,string source,string output,CancellationToken ct)
    {
        async Task<string> Hash(string path)
        {
            var r=await ProcessRunner.RunAsync(exe,["-v","error","-nostdin","-i",path,"-map","0:a","-c:a","copy","-f","streamhash","-hash","sha256","-"],ct);
            if(r.ExitCode!=0 || string.IsNullOrWhiteSpace(r.Output)) throw new InvalidDataException("Audio payload hash unavailable");
            return r.Output.Trim();
        }
        if(await Hash(source)!=await Hash(output)) throw new InvalidDataException("Copied audio payload changed");
    }
    private static async Task Checked(string exe,IEnumerable<string> args,CancellationToken ct)
    {
        var r=await ProcessRunner.RunAsync(exe,args,ct);
        if(r.ExitCode!=0) throw new InvalidOperationException(r.Error);
    }
}