using Vanta.Core;
using System.Security.Cryptography;
var root=Path.Combine(Path.GetTempPath(),"VANTA CI 한글 "+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));
var ct=timeout.Token;
async Task Run(params string[] args)
{
    var r=await ProcessRunner.RunAsync("ffmpeg",args,ct);
    if(r.ExitCode!=0) throw new Exception(r.Error);
}
static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
static string Hash(string path) { using var s=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
try
{
    var resolved=await Toolchain.DiscoverAsync(ct);
    Check(Path.IsPathFullyQualified(resolved.Ffmpeg)&&Path.IsPathFullyQualified(resolved.Ffprobe)&&resolved.Codecs.Contains("libx264")&&resolved.Codecs.Contains("libx265"),"Toolchain resolution");
    var previousDir=Environment.GetEnvironmentVariable("VANTA_FFMPEG_DIR");
    try
    {
        Environment.SetEnvironmentVariable("VANTA_FFMPEG_DIR",Path.GetDirectoryName(resolved.Ffmpeg));
        var explicitPair=await Toolchain.DiscoverAsync(ct);
        Check(explicitPair.Ffmpeg==resolved.Ffmpeg,"Explicit trusted toolchain");
        Environment.SetEnvironmentVariable("VANTA_FFMPEG_DIR",root);
        bool missing=false;
        try{await Toolchain.DiscoverAsync(ct);}catch(InvalidOperationException){missing=true;}
        Check(missing,"Explicit missing toolchain must not silently fall back");
    }
    finally{Environment.SetEnvironmentVariable("VANTA_FFMPEG_DIR",previousDir);}
    var input=Path.Combine(root,"input sample.mp4");
    var output=Path.Combine(root,"output sample.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=5","-itsoffset","0.2","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=4.8","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",input);
    var before=Hash(input);
    var source=await Probe.InspectAsync("ffprobe",input,ct);
    Check(source.Supported,source.Reason);
    var plan=new Plan("CI","libx265",180,180000,0,"medium",false,true);
    var events=new List<EncodeProgress>();
    await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,output,plan,ct,new ImmediateProgress(events.Add));
    Check(events.Any(x=>x.Stage=="Encoding"&&x.Fraction>0)&&events.Any(x=>x.Stage=="Validating")&&events.Last().Stage=="Complete","Actual progress stages");
    Check(File.Exists(output),"Output not published");
    Check(Hash(input)==before,"Source changed");
    var dest=await Probe.InspectAsync("ffprobe",output,ct);
    Timeline.Validate(source.Timing,dest.Timing,source.Info.Fps);
    var priorOutput=Hash(output);
    bool rejected=false;
    try { await Encoder.EncodeAsync("ffmpeg","ffprobe",input,output,plan,ct); } catch(IOException) { rejected=true; }
    Check(rejected && Hash(output)==priorOutput,"Existing output overwritten");
    rejected=false;
    try { await Encoder.EncodeAsync("ffmpeg","ffprobe",input,input,plan,ct); } catch(IOException) { rejected=true; }
    Check(rejected && Hash(input)==before,"Source overwrite not rejected");
    using var canceled=new CancellationTokenSource();
    canceled.Cancel();
    var canceledPath=Path.Combine(root,"canceled.mp4");
    rejected=false;
    try { await Encoder.EncodeAsync("ffmpeg","ffprobe",input,canceledPath,plan,canceled.Token); } catch(OperationCanceledException) { rejected=true; }
    Check(rejected && !File.Exists(canceledPath),"Pre-canceled encode published a file");
    Check(!Directory.EnumerateFiles(root,".vanta-*").Any(),"Temporary output leaked");
    Console.WriteLine("Delayed AAC, Unicode paths, source preservation, no overwrite, pre-cancel passed.");
    var shifted=Path.Combine(root,"shifted audio.mp4");
    await Run("-v","error","-nostdin","-n","-itsoffset","0.02","-i",input,"-i",input,"-map","1:v:0","-map","0:a:0","-c","copy",shifted);
    var shiftedInfo=await Probe.InspectAsync("ffprobe",shifted,ct);
    Timeline.Validate(source.Timing,shiftedInfo.Timing,source.Info.Fps); // Old endpoint tolerance accepts a 20 ms shift.
    rejected=false;
    try { await PacketTimeline.ValidateAsync("ffprobe",input,shifted,1,ct); } catch(InvalidDataException) { rejected=true; }
    Check(rejected,"Constant relative audio shift escaped timeline guard");
    Console.WriteLine("20 ms constant audio offset counterexample rejected.");
    var silent=Path.Combine(root,"silent source.mp4");
    var silentOut=Path.Combine(root,"silent compressed.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=3","-c:v","libx264","-pix_fmt","yuv420p",silent);
    await Encoder.EncodeAsync("ffmpeg","ffprobe",silent,silentOut,plan with { Height=90 },ct);
    var silentInfo=await Probe.InspectAsync("ffprobe",silentOut,ct);
    Check(silentInfo.Info.AudioTracks==0 && silentInfo.Info.Height==90,"Silent scaled output incorrect");
    Console.WriteLine("Silent video and scaling passed.");
    var avcOut=Path.Combine(root,"avc fast.mp4");
    await Encoder.EncodeAsync("ffmpeg","ffprobe",input,avcOut,plan with {Codec="libx264",Preset="fast"},ct);
    var avc=await Probe.InspectAsync("ffprobe",avcOut,ct);
    Check(avc.Info.Codec=="h264","H.264 output mismatch");
    Console.WriteLine("H.264 fast preset and copied audio timeline passed.");
    var flat=Path.Combine(root,"flat source.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","color=c=gray:s=320x180:r=30:d=6","-c:v","libx264","-pix_fmt","yuv420p",flat);
    foreach(var (label,fixture) in new[]{("flat",flat),("motion",silent)})
    {
        var metadata=await Probe.ReadAsync(resolved.Ffprobe,fixture,ct);
        foreach(var codec in PlanRules.Codecs)
        foreach(var preset in PlanRules.Presets)
        {
            var candidate=plan with {Codec=codec,Preset=preset};
            var destination=Path.Combine(root,$"matrix-{label}-{codec}-{preset}.mp4");
            await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,fixture,destination,candidate,ct);
            var estimate=candidate.EstimateBytes(metadata);
            var size=new FileInfo(destination).Length;
            Check(size>0&&double.IsFinite(estimate)&&estimate>0,"Invalid matrix result");
            Console.WriteLine(FormattableString.Invariant($"ABR_MATRIX {label} {codec} {preset} predicted={estimate:F0} actual={size} error_pct={(size/estimate-1)*100:F2}"));
        }
    }
    Console.WriteLine("All 16 codec/preset/fixture matrix encodes passed.");
    var batchA=OutputNames.Suggest(input);
    var batchB=OutputNames.Suggest(silent);
    var updates=new System.Collections.Concurrent.ConcurrentQueue<BatchUpdate>();
    await BatchEncoder.RunAsync(resolved,[new(input,batchA,plan),new(silent,batchB,plan),new(input,output,plan)],2,new BatchProgress(updates.Enqueue),ct);
    Check(File.Exists(batchA)&&File.Exists(batchB)&&Hash(input)==before&&Hash(output)==priorOutput,"Parallel output safety");
    Check(updates.Count(x=>x.Stage=="Complete")==2&&updates.Count(x=>x.Stage=="Failed")==1,"Batch failure isolation");
    Console.WriteLine("Parallel batch, collision refusal and per-job failure isolation passed.");
    var vfr=Path.Combine(root,"vfr multiple audio.mp4");
    var vfrOut=Path.Combine(root,"vfr compressed.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=12","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=12","-itsoffset","0.15","-f","lavfi","-i","sine=frequency=880:sample_rate=48000:duration=11.85","-map","0:v","-map","1:a","-map","2:a","-vf","select=not(mod(n\\,2))+not(mod(n\\,5))","-fps_mode","vfr","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",vfr);
    var vf=await Probe.InspectAsync("ffprobe",vfr,ct);
    Check(vf.Supported && vf.Info.AudioTracks==2,vf.Reason);
    await Encoder.EncodeAsync("ffmpeg","ffprobe",vfr,vfrOut,plan,ct);
    Check(File.Exists(vfrOut),"VFR output absent");
    Console.WriteLine("VFR and two independently offset audio streams passed.");
    var longInput=Path.Combine(root,"long source.mp4");
    var longOutput=Path.Combine(root,"long compressed.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=120","-f","lavfi","-i","sine=frequency=660:sample_rate=48000:duration=120","-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p","-c:a","aac",longInput);
    await Encoder.EncodeAsync("ffmpeg","ffprobe",longInput,longOutput,plan,ct);
    Check(File.Exists(longOutput),"Long output absent");
    Console.WriteLine("120-second NTSC-rate timeline passed.");
    var splitOut=Path.Combine(root,"experimental split.mp4");
    var splitPlan=plan with {Codec="libx264"};
    var splitWatch=System.Diagnostics.Stopwatch.StartNew();
    await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,longInput,splitOut,splitPlan,ct,experimentalSplit:true);
    Check(File.Exists(splitOut),"Experimental split output absent");
    Console.WriteLine(FormattableString.Invariant($"Experimental split 120s NTSC passed, end-to-end seconds={splitWatch.Elapsed.TotalSeconds:F3}, bytes={new FileInfo(splitOut).Length}"));
    var rejectedSplit=Path.Combine(root,"rejected VFR split.mp4");
    rejected=false;
    try{await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,vfr,rejectedSplit,splitPlan,ct,experimentalSplit:true);}
    catch(NotSupportedException){rejected=true;}
    Check(rejected&&!File.Exists(rejectedSplit)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"VFR split failed closed");
    Console.WriteLine("Experimental VFR split rejected; no leaked output or segments.");
    var delayedSplit=Path.Combine(root,"delayed split.mp4");
    await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,delayedSplit,splitPlan,ct,experimentalSplit:true);
    Check(File.Exists(delayedSplit)&&Hash(input)==before,"Delayed audio split");
    Console.WriteLine("Experimental delayed AAC split passed.");

    // Each of 121 frames carries a unique 8-bit spatial ID; odd count exercises
    // the asymmetric split. High bitrate isolates ordering from marker loss.
    var marker=Path.Combine(root,"frame IDs.mp4");
    var markerOut=Path.Combine(root,"frame IDs split.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","nullsrc=s=160x96:r=30","-vf","geq=lum='if(bitand(N,pow(2,floor(X/20))),235,16)':cb=128:cr=128","-frames:v","121","-c:v","libx264","-threads","2","-pix_fmt","yuv420p",marker);
    async Task<string[]> MarkerHashes(string path)
    {
        var rr=await ProcessRunner.RunAsync(resolved.Ffmpeg,["-v","error","-xerror","-nostdin","-i",path,"-map","0:v:0","-vf","scale=8:2:flags=area,format=gray,lut=y='if(gte(val,128),255,0)'","-an","-f","framemd5","-"],ct);
        Check(rr.ExitCode==0&&string.IsNullOrWhiteSpace(rr.Error),"Marker extraction failed");
        return rr.Output.Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(s=>!s.StartsWith('#')&&s.Contains(',')).Select(s=>s.Split(',').Last().Trim()).ToArray();
    }
    var ids=await MarkerHashes(marker);
    Check(ids.Length==121&&ids.Distinct().Count()==121,"Fixture frame IDs not unique");
    await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,marker,markerOut,splitPlan with {Height=96,VideoBitsPerSecond=2_000_000},ct,experimentalSplit:true,encoderThreads:4);
    Check(ids.SequenceEqual(await MarkerHashes(markerOut)),"Frame content identity changed at split");
    Console.WriteLine("All 121 unique frame IDs preserved across odd split.");
    var reversed=Path.Combine(root,"reversed frame IDs.mp4");
    await Run("-v","error","-nostdin","-n","-i",marker,"-vf","reverse","-c:v","libx264","-crf","0","-pix_fmt","yuv420p",reversed);
    await PacketTimeline.ValidateAsync(resolved.Ffprobe,marker,reversed,0,ct);
    Check(!ids.SequenceEqual(await MarkerHashes(reversed)),"Marker test failed to detect reordering");
    Console.WriteLine("PTS-preserving reordered content counterexample detected by marker test.");

    // Compare same codec, source, preset and encoder-thread budget, with all
    // validation included. Two repetitions in alternating order; no speed gate.
    for(int repeat=0;repeat<2;repeat++)
    {
        foreach(bool splitMode in repeat==0?new[]{false,true}:new[]{true,false})
        {
            var destPath=Path.Combine(root,$"timed-{repeat}-{splitMode}.mp4");
            var watch=System.Diagnostics.Stopwatch.StartNew();
            await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,longInput,destPath,splitPlan,ct,experimentalSplit:splitMode,encoderThreads:4);
            watch.Stop();
            var quality=await ProcessRunner.RunAsync(resolved.Ffmpeg,["-hide_banner","-nostdin","-i",longInput,"-i",destPath,"-filter_complex","[0:v][1:v]ssim","-an","-f","null","-"],ct);
            var match=System.Text.RegularExpressions.Regex.Match(quality.Error,@"All:([0-9.]+)");
            Check(quality.ExitCode==0&&match.Success,"SSIM measurement missing");
            Console.WriteLine(FormattableString.Invariant($"SPLIT_BENCH rep={repeat} split={splitMode} encoder_threads=4 seconds={watch.Elapsed.TotalSeconds:F3} bytes={new FileInfo(destPath).Length} ssim={match.Groups[1].Value}"));
        }
    }
    var canceledSplit=Path.Combine(root,"active split cancellation.mp4");
    using(var splitCancel=CancellationTokenSource.CreateLinkedTokenSource(ct))
    {
        var splitTask=Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,longInput,canceledSplit,splitPlan,splitCancel.Token,experimentalSplit:true);
        bool sawWork=false;
        while(!splitTask.IsCompleted)
        {
            if(Directory.EnumerateDirectories(root,".vanta-split-*").Any()){sawWork=true;splitCancel.Cancel();break;}
            await Task.Delay(10,ct);
        }
        rejected=false;
        try{await splitTask;}catch(OperationCanceledException){rejected=true;}
        Check(sawWork&&rejected&&!File.Exists(canceledSplit)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Active split cancellation cleanup");
    }
    Console.WriteLine("Active split cancellation passed.");
    var activeOutput=Path.Combine(root,"active cancellation.mp4");
    using var active=CancellationTokenSource.CreateLinkedTokenSource(ct);
    var task=Encoder.EncodeAsync("ffmpeg","ffprobe",longInput,activeOutput,plan,active.Token);
    bool observed=false;
    while(!task.IsCompleted)
    {
        if(Directory.EnumerateFiles(root,".vanta-*").Any()){observed=true;active.Cancel();break;}
        await Task.Delay(10,ct);
    }
    rejected=false;
    try{await task;}catch(OperationCanceledException){rejected=true;}
    Check(observed && rejected && !File.Exists(activeOutput),"Active cancellation failed");
    Check(!Directory.EnumerateFiles(root,".vanta-*").Any(),"Active cancellation leaked temporary output");
    Console.WriteLine("Active cancellation and cleanup passed.");
    Console.WriteLine("Windows FFmpeg integration checks passed.");
}
finally { Directory.Delete(root,true); }
sealed class BatchProgress(Action<BatchUpdate> report):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>report(value);}
sealed class ImmediateProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>{public void Report(EncodeProgress value)=>report(value);}
