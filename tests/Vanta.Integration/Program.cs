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
    Console.WriteLine("All 12 codec/preset/fixture matrix encodes passed.");
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
sealed class ImmediateProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>{public void Report(EncodeProgress value)=>report(value);}
