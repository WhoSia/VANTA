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
        if(encoder is not ("h264_nvenc" or "hevc_nvenc"))throw new ArgumentException("GPU-resident trial requires NVIDIA NVENC.");
    }
    internal static string[] InputArguments(bool resident)=>resident?["-hwaccel","cuda","-hwaccel_output_format","cuda"]:[];
    internal static string[] ResidentFilter(Plan plan)=>["-vf",$"scale_cuda=-2:{plan.Height}:format=nv12","-pix_fmt","+cuda"];
    internal static string[] Arguments(string encoder,Plan plan,bool resident=false)
    {
        Validate(encoder,plan,false,null,null);
        // Backend defaults are intentionally not presented as equivalent
        // quality/speed presets. The actual output must pass the same gates.
        if(resident){ValidateResident(encoder);return ["-c:v",encoder,"-b:v",plan.VideoBitsPerSecond.ToString(CultureInfo.InvariantCulture)];}
        return ["-c:v",encoder,"-b:v",plan.VideoBitsPerSecond.ToString(CultureInfo.InvariantCulture),"-pix_fmt","yuv420p"];
    }
}
