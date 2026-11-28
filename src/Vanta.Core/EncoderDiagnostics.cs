using System.Text.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace Vanta.Core;

public sealed record EncoderDiagnostic(string Encoder,string State,string Detail);

// Explicit, bounded synthetic test. This does not enable hardware encoding
// or guarantee that an arbitrary source, resolution or driver will work.
public static class EncoderDiagnostics
{
    public static IReadOnlyList<string> HardwareCandidates {get;}=Array.AsReadOnly(new[]{"h264_nvenc","hevc_nvenc","h264_qsv","hevc_qsv","h264_amf","hevc_amf"});
    public static async Task<IReadOnlyList<EncoderDiagnostic>> CheckAsync(Toolchain tools,IReadOnlyCollection<string> requested,CancellationToken ct=default,bool quick=false)
    {
        ct.ThrowIfCancellationRequested();
        if(requested.Any(c=>!HardwareCandidates.Contains(c)&&!PlanRules.Codecs.Contains(c)))
            throw new ArgumentException("Unsupported diagnostic encoder.");
        var clock=Stopwatch.StartNew();
        using var listingDeadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        listingDeadline.CancelAfter(TimeSpan.FromSeconds(quick?2:10));
        var listed=await ProcessRunner.RunAsync(tools.Ffmpeg,["-hide_banner","-encoders"],listingDeadline.Token);
        if(listed.ExitCode!=0)throw new InvalidOperationException("Cannot list encoders.");
        var result=new List<EncoderDiagnostic>();
        var dir=Path.Combine(Path.GetTempPath(),"VANTA-diagnostic-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach(var codec in requested.Distinct())
            {
                ct.ThrowIfCancellationRequested();
                if(!Regex.IsMatch(listed.Output,@"(?m)^\s*V\S*\s+"+Regex.Escape(codec)+@"\s"))
                {
                    result.Add(new(codec,"Not listed","Encoder not included in this FFmpeg build."));continue;
                }
                var budget=quick?TimeSpan.FromSeconds(8)-clock.Elapsed:TimeSpan.FromSeconds(10);
                if(budget<=TimeSpan.Zero){result.Add(new(codec,"Not tested","Short check budget exhausted; retry if needed."));continue;}
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(quick&&budget>TimeSpan.FromSeconds(2)?TimeSpan.FromSeconds(2):budget);
                var output=Path.Combine(dir,codec+".mp4");
                try
                {
                    var enc=await ProcessRunner.RunAsync(tools.Ffmpeg,["-hide_banner","-nostdin","-v","error","-xerror","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=0.4","-an","-c:v",codec,"-pix_fmt","yuv420p","-threads","2","-b:v","250000",output],deadline.Token);
                    if(enc.ExitCode!=0||!File.Exists(output))
                    {
                        result.Add(new(codec,"Failed",Short(enc.Error)));continue;
                    }
                    var probe=await ProcessRunner.RunAsync(tools.Ffprobe,["-v","error","-select_streams","v:0","-count_frames","-show_entries","stream=codec_name,nb_read_frames","-of","json",output],deadline.Token);
                    if(probe.ExitCode!=0||!string.IsNullOrWhiteSpace(probe.Error))
                    {
                        result.Add(new(codec,"Failed","Output probe failed: "+Short(probe.Error)));continue;
                    }
                    using var doc=JsonDocument.Parse(probe.Output);
                    var streams=doc.RootElement.GetProperty("streams");
                    string expected=codec.Contains("265")||codec.StartsWith("hevc_")?"hevc":"h264";
                    if(streams.GetArrayLength()!=1||streams[0].GetProperty("codec_name").GetString()!=expected||streams[0].GetProperty("nb_read_frames").ToString()!="12")
                    {
                        result.Add(new(codec,"Failed","Synthetic output codec or decoded frame count did not match."));continue;
                    }
                    var decode=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-i",output,"-map","0:v:0","-f","null","-"],deadline.Token);
                    result.Add(decode.ExitCode==0&&string.IsNullOrWhiteSpace(decode.Error)?new(codec,"Sample passed","12 synthetic frames encoded and decoded; not a production compatibility guarantee."):new(codec,"Failed",Short(decode.Error)));
                }
                catch(OperationCanceledException) when(!ct.IsCancellationRequested){result.Add(new(codec,"Timed out",quick?"Short check time budget reached; device support is unknown.":"10 second diagnostic limit."));}
                catch(Exception ex) when(ex is not OperationCanceledException){result.Add(new(codec,"Failed",Short(ex.Message)));}
                finally{if(File.Exists(output))File.Delete(output);}
            }
            return result.AsReadOnly();
        }
        finally{Directory.Delete(dir,true);}
    }
    private static string Short(string text)
    {
        var line=text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).FirstOrDefault()??"Encoder did not produce a valid output.";
        return line.Length<=350?line:line[..350];
    }
}