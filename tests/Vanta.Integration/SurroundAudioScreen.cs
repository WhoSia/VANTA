using System.Security.Cryptography;
using Vanta.Core;
internal static class SurroundAudioScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
    internal static async Task RunAsync(Toolchain tools,string root,CancellationToken ct)
    {
        foreach(var (layout,channels) in new[]{("5.1",6),("7.1",8)})
        {
            string source=Path.Combine(root,$"surround-{channels}.mp4"),output=Path.Combine(root,$"surround-{channels}-output.mp4");
            string signal="aevalsrc="+string.Join("|",Enumerable.Range(0,channels).Select(i=>$"0.04*sin(2*PI*{220+47*i}*t)"))+$":s=48000:d=3:c={layout}";
            var r=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=3","-f","lavfi","-i",signal,"-map","0:v","-map","1:a","-c:v","libx264","-preset","ultrafast","-threads","2","-pix_fmt","yuv420p","-c:a","aac","-b:a","384k",source],ct);
            Check(r.ExitCode==0&&string.IsNullOrWhiteSpace(r.Error),"Surround fixture failed: "+r.Error);
            string hash=Hash(source);
            var original=await Probe.InspectAsync(tools.Ffprobe,source,ct);
            Check(original.Supported&&original.Audio.Single()==new AudioMetadata("aac",channels,48000,layout),"Surround source metadata");
            await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,new("Surround","libx264",180,250000,0,"medium",false),ct,encoderThreads:2);
            var actual=await Probe.InspectAsync(tools.Ffprobe,output,ct);
            Check(actual.Supported&&actual.Audio.Single()==original.Audio.Single(),"Surround output metadata");
            await PacketTimeline.ValidateAsync(tools.Ffprobe,source,output,1,ct,compareAudioPayloads:true);
            Check(Hash(source)==hash,"Surround source changed");
            Console.WriteLine($"SURROUND_AUDIO layout={layout} channels={channels} rate=48000 copy_verified=true");
        }
        var six=new AudioMetadata("aac",6,48000,"5.1");
        Check(!new AudioMetadata("aac",6,48000,"").Supported&&!new AudioMetadata("aac",6,48000,"5.1(side)").Supported,"Unproven surround layout must reject");
        foreach(var changed in new[]{six with {SampleRate=44100},six with {Layout="5.1(side)"},new AudioMetadata("aac",2,48000,"stereo")})
        {
            bool rejected=false;
            try{AudioMetadata.ValidateOutput([six],[changed]);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Changed audio stream metadata accepted");
        }
    }
}
