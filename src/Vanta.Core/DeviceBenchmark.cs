using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
namespace Vanta.Core;

public sealed record DeviceBenchmarkRow(string Mode,string State,string? Detail,EncodeReport? Report,double? MeanSsim,double? WorstSecond,double? OrdinaryCheckSeconds,double? CombinedCheckSeconds);
public sealed record DeviceBenchmarkResult(string Backend,string FfmpegVersion,string SourceSha,int LogicalProcessors,IReadOnlyList<DeviceBenchmarkRow> Rows)
{
    public string Json()=>JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true});
}
// Opt-in, bounded synthetic screen. Never reads the selected user video, never
// saves an output video, never changes the actual compression configuration.
public static class DeviceBenchmark
{
    private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    private static async Task<string> Checked(string exe,string[] args,CancellationToken ct)
    {
        var result=await ProcessRunner.RunAsync(exe,args,ct);
        if(result.ExitCode!=0||!string.IsNullOrWhiteSpace(result.Error))throw new InvalidDataException("Benchmark command failed: "+result.Error);
        return result.Output;
    }
    internal static (double Mean,double Worst) ParseSsim(string text)
    {
        var values=new List<double>();
        foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            var fields=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);
            var frame=fields.SingleOrDefault(f=>f.StartsWith("n:",StringComparison.Ordinal));
            var all=fields.SingleOrDefault(f=>f.StartsWith("All:",StringComparison.Ordinal));
            if(frame is null||!int.TryParse(frame[2..],NumberStyles.None,CultureInfo.InvariantCulture,out int ordinal)||ordinal!=values.Count+1||
               all is null||!double.TryParse(all[4..],NumberStyles.Float,CultureInfo.InvariantCulture,out double quality)||!double.IsFinite(quality)||quality<0||quality>1)
                throw new InvalidDataException("Invalid SSIM frame record");
            values.Add(quality);
        }
        if(values.Count!=90)throw new InvalidDataException("Expected 90 SSIM frames");
        return (values.Average(),values.Chunk(30).Min(v=>v.Average()));
    }
    public static async Task<DeviceBenchmarkResult> RunAsync(Toolchain tools,string backend,CancellationToken ct=default,IProgress<string>? progress=null)
    {
        ct.ThrowIfCancellationRequested();
        string family=HardwareEncoderPolicy.SoftwareFamily(backend);
        if(!tools.Codecs.Contains(family))throw new NotSupportedException("Matching CPU encoder unavailable");
        string root=Path.Combine(Path.GetTempPath(),"VANTA-device-screen-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source=Path.Combine(root,"synthetic.mp4");
            using(var setup=CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                setup.CancelAfter(TimeSpan.FromSeconds(45));progress?.Report("Fixture");
                await Checked(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=1440x810:r=30:d=3","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=3","-map","0:v","-map","1:a","-c:v","libx264","-preset","veryfast","-threads","4","-b:v","2238750","-pix_fmt","yuv420p","-c:a","aac","-b:a","128k",source],setup.Token);
            }
            var stamp=SourceStamp.Capture(source);string before=Hash(source);
            var version=(await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct)).Output.Split('\n')[0].Trim();
            var rows=new List<DeviceBenchmarkRow>();
            string[] modes=backend.EndsWith("_nvenc",StringComparison.Ordinal)?["CPU medium","CPU veryfast","GPU encode","GPU resident"]:["CPU medium","CPU veryfast","GPU encode"];
            foreach(string mode in modes)
            {
                ct.ThrowIfCancellationRequested();progress?.Report(mode);
                string output=Path.Combine(root,rows.Count+".mp4");
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(90));
                EncodeReport? result=null;
                try
                {
                    var plan=new Plan("Device screen",family,810,1791000,0,mode=="CPU veryfast"?"veryfast":"medium",false);
                    result=mode.StartsWith("CPU",StringComparison.Ordinal)
                        ?await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,deadline.Token,expectedSource:stamp)
                        :await Encoder.EncodeHardwareTrialAsync(tools,source,output,plan,backend,deadline.Token,expectedSource:stamp,gpuResident:mode=="GPU resident");
                    string outputSha=Hash(output);
                    var quality=ParseSsim(await Checked(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-i",source,"-i",output,"-lavfi","ssim=stats_file=-","-an","-f","null","-"],deadline.Token));
                    // Validation-only proxy on the very same output; no promotion.
                    // Both lanes independently compare source clocks and packet payloads,
                    // and strictly decode all output streams. Separate from product total.
                    var timer=Stopwatch.StartNew();
                    await PacketTimeline.ValidateAsync(tools.Ffprobe,source,output,1,deadline.Token,compareAudioPayloads:true);
                    await Checked(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-i",output,"-map","0:V:0","-map","0:a?","-f","null","-"],deadline.Token);
                    double ordinary=timer.Elapsed.TotalSeconds;timer.Restart();
                    await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,1,deadline.Token,compareAudioPayloads:true);
                    double combined=timer.Elapsed.TotalSeconds;
                    if(Hash(output)!=outputSha||Hash(source)!=before)throw new IOException("Benchmark file identity changed");
                    rows.Add(new(mode,"Verified",null,result,quality.Mean,quality.Worst,ordinary,combined));
                }
                catch(OperationCanceledException) when(!ct.IsCancellationRequested)
                {rows.Add(new(mode,"Timed out","90 second lane limit; no comparable result",null,null,null,null,null));}
                catch(Exception ex) when(ex is not OperationCanceledException)
                {rows.Add(new(mode,"Rejected",ex.Message.Length>1200?ex.Message[..1200]:ex.Message,null,null,null,null,null));}
                finally
                {
                    if(File.Exists(output))File.Delete(output);
                    stamp.RequireUnchanged(source);
                    if(Hash(source)!=before)throw new IOException("Synthetic source changed");
                }
            }
            ct.ThrowIfCancellationRequested();
            return new(backend,version,before,Environment.ProcessorCount,rows.AsReadOnly());
        }
        finally{Directory.Delete(root,true);}
    }
}
