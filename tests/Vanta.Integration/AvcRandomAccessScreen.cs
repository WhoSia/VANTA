using System.Globalization;
using Vanta.Core;

internal static class AvcRandomAccessScreen
{
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static string Num(double x)=>x.ToString("0.000000000",CultureInfo.InvariantCulture);
    private static async Task<string[]> Decode(string ffmpeg,string path,CancellationToken ct,double? seek=null,double? start=null)
    {
        var args=new List<string>{"-v","error","-xerror","-nostdin","-n"};
        if(seek is double at)args.AddRange(["-ss",Num(at),"-noaccurate_seek","-copyts"]);
        args.AddRange(["-i",path,"-map","0:v:0","-an"]);
        if(start is double cut)args.AddRange(["-vf",$"trim=start={Num(cut)},setpts=PTS-STARTPTS","-frames:v","60"]);
        args.AddRange(["-fps_mode","passthrough","-f","framemd5","-"]);
        var r=await ProcessRunner.RunAsync(ffmpeg,args,ct);
        Check(r.ExitCode==0&&string.IsNullOrWhiteSpace(r.Error),"Decode failed: "+r.Error);
        return r.Output.Split('\n',StringSplitOptions.RemoveEmptyEntries)
            .Where(x=>!x.StartsWith('#')).Select(x=>x.Split(',')[^1].Trim()).ToArray();
    }

    public static async Task RunAsync(Toolchain tools,string root,CancellationToken ct)
    {
        Check(AvcRandomAccess.IsIdrSample(Convert.FromHexString("000000026588"),4),"IDR sample");
        Check(!AvcRandomAccess.IsIdrSample(Convert.FromHexString("000000026188"),4),"Non-IDR sample");
        Check(!AvcRandomAccess.IsIdrSample(Convert.FromHexString("000000026588000000026188"),4),"Mixed VCL sample");
        Check(AvcRandomAccess.IsIdrSample(Convert.FromHexString("026588"),1),"One-byte length");
        Check(AvcRandomAccess.IsIdrSample(Convert.FromHexString("00026588"),2),"Two-byte length");
        foreach(string malformed in new[]{"00000000","000000056588","00000002E588","00"})
        {
            bool rejected=false;
            try{AvcRandomAccess.IsIdrSample(Convert.FromHexString(malformed),4);}
            catch(InvalidDataException){rejected=true;}
            Check(rejected,"Malformed sample accepted");
        }
        Check(AvcRandomAccess.PreviousIdr([new(2,false),new(4,true)],3) is null,"No prior IDR");
        for(int open=0;open<=1;open++)
        {
            var path=Path.Combine(root,$"avc-random-access-{open}.mp4");
            var fixture=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=8.008","-c:v","libx264","-preset","fast","-crf","18","-g","60","-keyint_min","60","-sc_threshold","0","-x264-params",$"open-gop={open}","-threads","2","-pix_fmt","yuv420p",path],ct);
            Check(fixture.ExitCode==0&&string.IsNullOrWhiteSpace(fixture.Error),"AVC fixture failed");
            var candidates=await AvcRandomAccess.ReadAsync(tools.Ffprobe,path,ct);
            Check(candidates.Length==4&&candidates.Count(x=>x.IsIdr)==(open==0?4:1),"Unexpected AVC anchor classification");
            var reference=await Decode(tools.Ffmpeg,path,ct);
            Check(reference.Length==240,"Reference frame count");
            for(int i=1;i<=3;i++)
            {
                double target=i*2.002;
                double? anchor=AvcRandomAccess.PreviousIdr(candidates,target+.000001);
                Check(anchor.HasValue&&Math.Abs(anchor.Value-(open==0?target:0))<.000001,"IDR anchor choice");
                var actual=await Decode(tools.Ffmpeg,path,ct,anchor.GetValueOrDefault(),target-(1001d/30000)/2);
                Check(Enumerable.SequenceEqual(actual,reference.Skip(i*60).Take(60)),"IDR-anchored decode differs from linear reference");
                Console.WriteLine(FormattableString.Invariant($"AVC_ANCHOR open={open} target={target:F3} anchor={anchor.GetValueOrDefault():F3} frames={actual.Length} match=true"));
            }
        }
        Console.WriteLine("AVC IDR candidate and linear-reference frame checks passed.");
    }
}
