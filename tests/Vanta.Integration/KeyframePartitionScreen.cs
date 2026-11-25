using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Vanta.Core;

internal static class KeyframePartitionScreen
{
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    private static void CheckPlanner()
    {
        Check(SegmentPartition.Balanced(300,4,30,[0,60,120,180,240]) is long[] a&&a.Length==5&&a[0]==0&&a[^1]==300&&a.Zip(a.Skip(1),(x,y)=>y-x).Max()==120,"Minimax partition");
        Check(SegmentPartition.Balanced(300,4,75,[0,60,120,180,240]) is null,"Minimum interval infeasible");
        Check(SegmentPartition.Balanced(120,4,20,[0,30,60]) is null,"Too few boundaries");
        Check(SegmentPartition.Balanced(120,4,20,[0,30,60,90]) is long[] exact&&Enumerable.SequenceEqual(exact,new long[]{0,30,60,90,120}),"Equal feasible partition");
        bool rejected=false;
        try{SegmentPartition.Balanced(120,3,20,[0,60,30]);}catch(ArgumentException){rejected=true;}
        Check(rejected,"Unordered keyframe rejected");
        // Exhaustively compare the DP with an independent three-interval search.
        for(int mask=0;mask<256;mask++)
        {
            var keys=Enumerable.Range(1,8).Where(i=>(mask&(1<<(i-1)))!=0).Select(i=>(long)i).ToArray();
            long best=long.MaxValue;
            foreach(long x in keys)
            foreach(long y in keys)
                if(x>=2&&y-x>=2&&10-y>=2)best=Math.Min(best,Math.Max(x,Math.Max(y-x,10-y)));
            var actual=SegmentPartition.Balanced(10,3,2,keys);
            Check(best==long.MaxValue?actual is null:actual is not null&&actual.Zip(actual.Skip(1),(x,y)=>y-x).Max()==best,"Partition oracle mismatch");
        }
        Console.WriteLine("Keyframe minimax partition exhaustive small oracle (256 cases) passed.");
    }
    public static async Task RunAsync(Toolchain tools,string root,CancellationToken ct)
    {
        CheckPlanner();
        var source=Path.Combine(root,"keyframe high resolution.mp4");
        var fixture=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=1920x1080:r=30:d=12","-f","lavfi","-i","sine=frequency=660:sample_rate=48000:duration=12","-c:v","libx264","-preset","ultrafast","-crf","18","-g","75","-keyint_min","75","-sc_threshold","0","-threads","2","-pix_fmt","yuv420p","-c:a","aac",source],ct);
        Check(fixture.ExitCode==0&&string.IsNullOrWhiteSpace(fixture.Error),"1080p fixture: "+fixture.Error);
        var plan=new Plan("Research","libx264",1080,2_000_000,0,"medium",false);
        var modes=new[]{"continuous","equal","aligned"};
        for(int rep=0;rep<2;rep++)
        foreach(string mode in rep==0?modes:modes.Reverse().ToArray())
        {
            var path=Path.Combine(root,$"keyframe-{rep}-{mode}.mp4");
            var watch=Stopwatch.StartNew();
            var report=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,path,plan,ct,experimentalSplit:mode!="continuous",encoderThreads:4,splitSegments:4,splitWorkers:2,keyframeAligned:mode=="aligned",researchCrf:23);
            watch.Stop();
            var quality=await ProcessRunner.RunAsync(tools.Ffmpeg,["-hide_banner","-nostdin","-i",source,"-i",path,"-filter_complex","[0:v][1:v]ssim","-an","-f","null","-"],ct);
            var match=Regex.Match(quality.Error,@"All:([0-9.]+)");
            Check(quality.ExitCode==0&&match.Success&&report.OutputBytes==new FileInfo(path).Length,"1080p SSIM measurement");
            Console.WriteLine(FormattableString.Invariant($"KEYFRAME_SCREEN rep={rep} mode={mode} seconds={watch.Elapsed.TotalSeconds:F6} encode={report.Encoding.TotalSeconds:F6} bytes={report.OutputBytes} ssim={match.Groups[1].Value}"));
            File.Delete(path);
        }
        Console.WriteLine("1080p CRF research screen passed with existing publication checks.");
    }
}
