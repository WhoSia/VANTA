using System.Diagnostics;
using System.Text.RegularExpressions;
using Vanta.Core;
internal static class MultiSplitScreen
{
    public static async Task RunAsync(Toolchain tools,string root,CancellationToken ct)
    {
        var source=Path.Combine(root,"multi split source.mp4");
        var fixture=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=960x540:r=30:d=8","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=8","-c:v","libx264","-preset","ultrafast","-crf","18","-g","60","-keyint_min","60","-sc_threshold","0","-threads","2","-pix_fmt","yuv420p","-c:a","aac",source],ct);
        if(fixture.ExitCode!=0||!string.IsNullOrWhiteSpace(fixture.Error))throw new Exception("Multi-split fixture failed: "+fixture.Error);
        var plan=new Plan("Research","libx264",540,900000,0,"medium",false);
        var configs=new[]{(1,1),(2,2),(4,2),(4,4),(8,2),(8,4)};
        for(int rep=0;rep<2;rep++)
        foreach(var (segments,workers) in rep==0?configs:configs.Reverse().ToArray())
        {
            var path=Path.Combine(root,$"multi-split-{rep}-{segments}-{workers}.mp4");
            var clock=Stopwatch.StartNew();
            var report=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,path,plan,ct,experimentalSplit:segments>1,encoderThreads:4,splitSegments:segments,splitWorkers:workers);
            clock.Stop();
            var quality=await ProcessRunner.RunAsync(tools.Ffmpeg,["-hide_banner","-nostdin","-i",source,"-i",path,"-filter_complex","[0:v][1:v]ssim","-an","-f","null","-"],ct);
            var match=Regex.Match(quality.Error,@"All:([0-9.]+)");
            if(quality.ExitCode!=0||!match.Success||report.OutputBytes!=new FileInfo(path).Length)throw new Exception("Multi-split measurement failed.");
            Console.WriteLine(FormattableString.Invariant($"MULTI_SPLIT rep={rep} segments={segments} workers={workers} seconds={clock.Elapsed.TotalSeconds:F6} encode={report.Encoding.TotalSeconds:F6} bytes={report.OutputBytes} ssim={match.Groups[1].Value}"));
            File.Delete(path);
        }
        // Same production encoder, active cancellation while bounded workers own staging.
        using var canceled=CancellationTokenSource.CreateLinkedTokenSource(ct);
        var canceledOutput=Path.Combine(root,"multi split canceled.mp4");
        var task=Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,canceledOutput,plan,canceled.Token,experimentalSplit:true,encoderThreads:4,splitSegments:8,splitWorkers:2);
        bool sawWork=false;
        while(!task.IsCompleted)
        {
            if(Directory.EnumerateDirectories(root,".vanta-split-*").Any()){sawWork=true;canceled.Cancel();break;}
            await Task.Delay(5,ct);
        }
        bool stopped=false;
        try{await task;}catch(OperationCanceledException) when(canceled.IsCancellationRequested){stopped=true;}
        if(!sawWork||!stopped||File.Exists(canceledOutput)||Directory.EnumerateFileSystemEntries(root,".vanta-*").Any())throw new Exception("Multi-split cancellation failed.");
        Console.WriteLine("Multi-split matched-workload screen and active cancellation passed.");
    }
}
