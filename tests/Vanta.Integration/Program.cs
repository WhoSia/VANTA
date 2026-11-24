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
    Console.WriteLine("Windows FFmpeg integration checks passed (delayed AAC, Unicode paths, source preservation, no overwrite, pre-cancel).");
}
finally { Directory.Delete(root,true); }
