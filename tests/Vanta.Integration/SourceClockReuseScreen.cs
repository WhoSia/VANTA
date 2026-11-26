using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class SourceClockReuseScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static async Task RunAsync(Toolchain tools,string root,string source,string output,string vfr,Plan plan,CancellationToken ct)
    {
        string before=Hash(source),outBefore=Hash(output);
        var timer=Stopwatch.StartNew();
        var snapshot=await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,source,ct);
        double capture=timer.Elapsed.TotalSeconds;
        Check(snapshot.Count==3597,"120s NTSC source clock count");
        var comparisons=new List<(bool Reuse,double Seconds)>();
        for(int rep=0;rep<3;rep++)
        foreach(bool reuse in rep%2==0?new[]{false,true}:new[]{true,false})
        {
            timer.Restart();
            await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,1,ct,reuse?snapshot:null);
            await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
            double seconds=timer.Elapsed.TotalSeconds;comparisons.Add((reuse,seconds));
            Console.WriteLine("SOURCE_CLOCK "+JsonSerializer.Serialize(new {rep,reuse,seconds,captureSeconds=capture,frames=snapshot.Count,scope="validation-only"}));
        }
        Console.WriteLine("SOURCE_CLOCK_AMORTIZED "+JsonSerializer.Serialize(new {captureSeconds=capture,freshTotal=comparisons.Where(x=>!x.Reuse).Sum(x=>x.Seconds),reuseTotal=capture+comparisons.Where(x=>x.Reuse).Sum(x=>x.Seconds)}));
        Check(Hash(source)==before&&Hash(output)==outBefore,"Repeated clock comparison leaves media unchanged");
        var vfrClock=await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,vfr,ct);
        string vfrBefore=Hash(vfr),dest=Path.Combine(root,"snapshot-vfr.mp4");
        await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,vfr,dest,plan,ct,encoderThreads:2,singleDecodeValidation:true,interleavedAudioValidation:true,sourceClockSnapshot:vfrClock);
        await SingleDecodeScreen.ReferenceAsync(tools,vfr,dest,2,ct);
        await Encoder.ValidateAudioAsync(tools.Ffmpeg,vfr,dest,ct);
        Check(Hash(vfr)==vfrBefore,"Snapshot full encode leaves source unchanged");
        // A different path with identical bytes is deliberately not eligible.
        string copy=Path.Combine(root,"snapshot-copy.mp4");
        File.Copy(vfr,copy);
        using(var lease=new FileStream(copy,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool rejected=false;
            try{await vfrClock.RequireUnchangedAsync(tools.Ffprobe,lease,ct);}catch(IOException){rejected=true;}
            Check(rejected,"Other source path rejected");
        }
        var copied=await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,copy,ct);
        var stamp=SourceStamp.Capture(copy);
        using(var edit=new FileStream(copy,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            edit.Position=edit.Length-1;int value=edit.ReadByte();edit.Position=edit.Length-1;edit.WriteByte((byte)(value^1));
        }
        File.SetLastWriteTimeUtc(copy,stamp.LastWriteUtc);
        File.SetCreationTimeUtc(copy,stamp.CreationUtc);
        Check(SourceStamp.Capture(copy)==stamp,"Same-length mutation with restored timestamps");
        using(var lease=new FileStream(copy,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool rejected=false;
            try{await copied.RequireUnchangedAsync(tools.Ffprobe,lease,ct);}catch(IOException ex){rejected=ex.Message.Contains("byte identity");}
            Check(rejected,"Exact byte check rejects metadata-preserving mutation");
        }
        string badOutput=Path.Combine(root,"snapshot-stale.mp4");
        bool stale=false;
        try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,copy,badOutput,plan,ct,singleDecodeValidation:true,sourceClockSnapshot:copied);}
        catch(Exception ex) when(ex is IOException or InvalidOperationException or NotSupportedException){stale=true;}
        Check(stale&&!File.Exists(badOutput)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Stale snapshot cannot publish");
        using var canceled=new CancellationTokenSource();canceled.Cancel();bool stopped=false;
        try{await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,source,canceled.Token);}catch(OperationCanceledException){stopped=true;}
        Check(stopped,"Capture pre-cancellation");
        Console.WriteLine("Source clock snapshot bounds, long comparison, full VFR oracle, byte mutation and cancellation passed.");
    }
}
