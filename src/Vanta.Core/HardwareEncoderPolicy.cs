using System.Globalization;
namespace Vanta.Core;

// Research-only adapter. Do not transfer x264/x265 presets or assume a
// listed FFmpeg encoder can initialize on the current GPU/driver.
internal static class HardwareEncoderPolicy
{
    internal static string SoftwareFamily(string encoder)
    {
        if(!EncoderDiagnostics.HardwareCandidates.Contains(encoder))throw new ArgumentException("Unknown hardware encoder");
        return encoder.StartsWith("hevc_",StringComparison.Ordinal)?"libx265":"libx264";
    }
    internal static void Validate(string encoder,Plan plan,bool split,int? crf,int? threads)
    {
        if(SoftwareFamily(encoder)!=plan.Codec||split||crf.HasValue||threads.HasValue||plan.TwoPass||plan.Preset!="medium")
            throw new ArgumentException("Hardware research requires matching H.264/HEVC, continuous ABR, medium placeholder and automatic encoder threads.");
    }
    internal static void ValidateResident(string encoder)
    {
        _=SoftwareFamily(encoder);
        if(encoder.EndsWith("_amf",StringComparison.Ordinal)&&!OperatingSystem.IsWindows())
            throw new NotSupportedException("D3D11/AMF resident trial requires Windows.");
    }
    internal static void ValidateFast(string encoder,bool fast)
    {
        if(fast&&!encoder.EndsWith("_nvenc",StringComparison.Ordinal)&&!encoder.EndsWith("_qsv",StringComparison.Ordinal))throw new ArgumentException("Fast preset requires NVENC or QSV.");
    }
    internal static string? FastPreset(string encoder,bool fast)
    {
        ValidateFast(encoder,fast);
        return !fast?null:encoder.EndsWith("_qsv",StringComparison.Ordinal)?"veryfast":"p1";
    }
    internal static void ValidateAsyncDepth(string? encoder,bool resident,int? depth)
    {
        if(depth is not null&&(!(encoder?.EndsWith("_qsv",StringComparison.Ordinal)??false)||!resident||depth is not (8 or 16)))
            throw new ArgumentException("Explicit async depth requires resident QSV and depth 8 or 16.");
    }
    internal static string[] InputArguments(bool resident,string? encoder=null,string? sourceCodec=null,int? asyncDepth=null)
    {
        ValidateAsyncDepth(encoder,resident,asyncDepth);
        if(!resident)return [];
        ValidateResident(encoder??"");
        if(encoder!.EndsWith("_nvenc",StringComparison.Ordinal))
            return ["-hwaccel","cuda","-hwaccel_output_format","cuda"];
        if(encoder.EndsWith("_qsv",StringComparison.Ordinal))
        {
            string decoder=sourceCodec switch{"h264"=>"h264_qsv","hevc"=>"hevc_qsv",_=>throw new ArgumentException("Unsupported QSV source")};
            string device=OperatingSystem.IsWindows()?"qsv=vanta:hw,child_device_type=d3d11va":"qsv=vanta:hw";
            var args=new List<string>{"-init_hw_device",device,"-hwaccel","qsv","-hwaccel_device","vanta","-hwaccel_output_format","qsv","-c:v",decoder};
            if(asyncDepth is int depth)args.AddRange(["-extra_hw_frames",(2*depth).ToString(CultureInfo.InvariantCulture)]);
            return args.ToArray();
        }
        return ["-hwaccel","d3d11va","-hwaccel_output_format","d3d11"];
    }
    internal static string[] ResidentFilter(Plan plan,string encoder,int sourceHeight)
    {
        ValidateResident(encoder);
        if(encoder.EndsWith("_nvenc",StringComparison.Ordinal))
            return plan.Height==sourceHeight?["-pix_fmt","+cuda"]:["-vf",$"scale_cuda=-2:{plan.Height}:format=nv12","-pix_fmt","+cuda"];
        if(encoder.EndsWith("_qsv",StringComparison.Ordinal))
            return plan.Height==sourceHeight?["-pix_fmt","+qsv"]:["-vf",$"vpp_qsv=w=-2:h={plan.Height}","-pix_fmt","+qsv"];
        if(plan.Height!=sourceHeight)throw new NotSupportedException("D3D11/AMF resident trial preserves original resolution.");
        return ["-pix_fmt","+d3d11"];
    }
    internal static string[] Arguments(string encoder,Plan plan,bool resident=false,bool fast=false,int? asyncDepth=null)
    {
        Validate(encoder,plan,false,null,null);
        // Backend defaults are intentionally not presented as equivalent
        // quality/speed presets. The actual output must pass the same gates.
        ValidateFast(encoder,fast);
        ValidateAsyncDepth(encoder,resident,asyncDepth);
        if(resident)ValidateResident(encoder);
        var args=new List<string>{"-c:v",encoder,"-b:v",plan.VideoBitsPerSecond.ToString(CultureInfo.InvariantCulture)};
        if(FastPreset(encoder,fast) is string preset)args.AddRange(["-preset",preset]);
        if(asyncDepth is int depth)args.AddRange(["-async_depth",depth.ToString(CultureInfo.InvariantCulture)]);
        if(!resident)args.AddRange(["-pix_fmt","yuv420p"]);
        return args.ToArray();
    }
}
