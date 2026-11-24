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
    var input=Path.Combine(root,"input sample.mp4");
    var output=Path.Combine(root,"output sample.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=5","-itsoffset","0.2","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=4.8","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",input);
    var before=Hash(input);
    var source=await Probe.InspectAsync("ffprobe",input,ct);
    Check(source.Supported,source.Reason);
    var plan=new Plan("CI","libx265",180,180000,0,"medium",false,true);
    await Encoder.EncodeAsync("ffmpeg","ffprobe",input,output,plan,ct);
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
