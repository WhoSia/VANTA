using System.Text.Json;
namespace Vanta.Core;

// Bounded compatibility probe, not a throughput benchmark. Never publishes an output.
internal static class ResidentQualification
{
    internal const int Frames=12;
    internal static bool Eligible(MediaDetails details,Plan plan,string backend)=>
        details.Supported&&details.PixelFormat=="yuv420p"&&(details.Info.Codec is "h264" or "hevc")&&
        (!backend.EndsWith("_amf",StringComparison.Ordinal)||(OperatingSystem.IsWindows()&&plan.Height==details.Info.Height));

    internal static async Task<string> CheckAsync(Toolchain tools,string input,Plan plan,string backend,SourceStamp stamp,bool fast,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        string dir=Path.Combine(Path.GetTempPath(),"VANTA-resident-check-"+Guid.NewGuid().ToString("N"));
        try
        {
            using var sourceLease=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read);
            stamp.RequireUnchanged(input);
            var details=await Probe.InspectAsync(tools.Ffprobe,input,limit.Token);
            if(!Eligible(details,plan,backend))return "Not eligible";
            PlanRules.Validate(plan,details.Info);
            Directory.CreateDirectory(dir);
            string sample=Path.Combine(dir,"sample.mp4");
            var args=new List<string>{"-hide_banner","-nostdin","-v","error","-xerror","-n"};
            args.AddRange(HardwareEncoderPolicy.InputArguments(true,backend,details.Info.Codec));
            args.AddRange(["-i",input,"-map","0:V:0","-an","-frames:v",Frames.ToString(System.Globalization.CultureInfo.InvariantCulture),"-fps_mode","passthrough"]);
            args.AddRange(HardwareEncoderPolicy.Arguments(backend,plan,true,fast));
            args.AddRange(HardwareEncoderPolicy.ResidentFilter(plan,backend,details.Info.Height));
            args.Add(sample);
            var result=await ProcessRunner.RunAsync(tools.Ffmpeg,args,limit.Token);
            stamp.RequireUnchanged(input);
            if(result.ExitCode!=0||!string.IsNullOrWhiteSpace(result.Error)||!File.Exists(sample))return "Sample refused";
            bool passed=await VerifySampleAsync(tools,sample,plan,limit.Token);
            stamp.RequireUnchanged(input);
            return passed?"Sample passed":"Sample refused";
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){stamp.RequireUnchanged(input);return "Timed out";}
        finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }

    internal static async Task<bool> VerifySampleAsync(Toolchain tools,string path,Plan plan,CancellationToken ct)
    {
        var probe=await ProcessRunner.RunAsync(tools.Ffprobe,["-v","error","-select_streams","v:0","-count_frames","-show_entries","stream=codec_name,nb_read_frames,height","-of","json",path],ct);
        if(probe.ExitCode!=0||!string.IsNullOrWhiteSpace(probe.Error))return false;
        try
        {
            using var doc=JsonDocument.Parse(probe.Output);
            var streams=doc.RootElement.GetProperty("streams");
            if(streams.GetArrayLength()!=1||streams[0].GetProperty("codec_name").GetString()!=(plan.Codec=="libx265"?"hevc":"h264")||
                streams[0].GetProperty("height").GetInt32()!=plan.Height||streams[0].GetProperty("nb_read_frames").ToString()!=Frames.ToString(System.Globalization.CultureInfo.InvariantCulture))return false;
        }
        catch(JsonException){return false;}
        catch(KeyNotFoundException){return false;}
        var decode=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-i",path,"-map","0:v:0","-f","null","-"],ct);
        return decode.ExitCode==0&&string.IsNullOrWhiteSpace(decode.Error);
    }
}
