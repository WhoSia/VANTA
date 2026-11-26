using Vanta.Core;

internal static class SingleDecodeScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static async Task ReferenceAsync(Toolchain tools,string source,string output,int audioTracks,CancellationToken ct)
    {
        await PacketTimeline.ValidateAsync(tools.Ffprobe,source,output,audioTracks,ct,compareAudioPayloads:true);
        var r=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-i",output,"-map","0:V:0","-map","0:a?","-f","null","-"],ct);
        Check(r.ExitCode==0&&string.IsNullOrWhiteSpace(r.Error),"Independent strict full decode rejected candidate");
    }
    internal static async Task RunAsync(Toolchain tools,string root,string input,string output,string shifted,string changed,string vfr,string vfrOut,Plan plan,CancellationToken ct)
    {
        Check(Math.Abs(PacketTimeline.ParseStrictFrame("1 1001 1/30000",1)-.033367)<1e-12,"NTSC rational clock");
        Check(PacketTimeline.ParseStrictFrame("0 -1 1/1000",0)==-.001,"Negative clock");
        foreach(string row in new[]{"1 0 1/30","0 -9223372036854775808 1/30","0 0 1/0","0 0 -1/30","0 N/A 1/30","0 1 1/30 junk",""})
        {
            bool rejected=false;
            try{PacketTimeline.ParseStrictFrame(row,0);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Invalid strict frame accepted: "+row);
        }
        await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,input,output,1,ct);
        await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,vfr,vfrOut,2,ct);
        async Task Reject(string path,int tracks)
        {
            bool rejected=false;
            try{await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,input,path,tracks,ct);}
            catch(InvalidDataException){rejected=true;}
            Check(rejected,"Single-decode accepted negative control");
        }
        await Reject(shifted,1);
        // Payload check is separate; same-PTS substituted audio must still fail.
        await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,input,changed,1,ct);
        bool badAudio=false;
        try{await Encoder.ValidateAudioAsync(tools.Ffmpeg,input,changed,ct);}catch(InvalidDataException){badAudio=true;}
        Check(badAudio,"Independent audio hash missed substitution");
        string truncated=Path.Combine(root,"single-decode-truncated.mp4");
        File.Copy(output,truncated);
        using(var stream=File.Open(truncated,FileMode.Open,FileAccess.Write))stream.SetLength(stream.Length/2);
        await Reject(truncated,1);
        string shorter=Path.Combine(root,"single-decode-shorter.mp4");
        var cut=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-i",input,"-t","1","-map","0:v:0","-an","-c:v","libx264","-preset","veryfast",shorter],ct);
        Check(cut.ExitCode==0&&string.IsNullOrWhiteSpace(cut.Error),"Short negative fixture");
        await Reject(shorter,0);
        string dest=Path.Combine(root,"single-decode-vfr.mp4");
        var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,vfr,dest,plan,ct,singleDecodeValidation:true);
        Check(result.SingleDecodeValidation&&result.FullDecode==TimeSpan.Zero&&result.Validation<=result.Total&&File.Exists(dest),"Single-decode report and publication");
        await ReferenceAsync(tools,vfr,dest,2,ct);
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        bool canceledOk=false;
        try{await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,input,output,1,canceled.Token);}catch(OperationCanceledException){canceledOk=true;}
        Check(canceledOk,"Single-decode pre-cancel");
        Console.WriteLine("Single-decode reference, VFR, malformed row, shifted audio, changed payload, truncated/short output and cancellation controls passed.");
    }
}
