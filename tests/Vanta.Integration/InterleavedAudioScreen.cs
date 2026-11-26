using System.Diagnostics;
using System.Security.Cryptography;
using Vanta.Core;

internal static class InterleavedAudioScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static async Task RunAsync(Toolchain tools,string root,string source,string output,Plan plan,CancellationToken ct)
    {
        string h1="SHA256:"+new string('A',64),h2="SHA256:"+new string('B',64);
        var join=new AudioPacketJoin([2,4],[1,3],.1,4);
        join.Push(true,new(2,0,h1));join.Push(false,new(3,.1,h2));
        join.Push(true,new(4,0,h2));join.Push(false,new(1,.1,h1));join.Complete();
        Check(join.PeakPending==2,"Differing interleave matched per track");
        void Reject(Action test)
        {
            bool rejected=false;try{test();}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Interleaved negative control accepted");
        }
        Reject(()=>new AudioPacketJoin([1,1],[1,2],0));
        Reject(()=>AudioPacketJoin.Parse("packet|stream_index=1|pts_time=N/A|data_hash="+h1));
        Reject(()=>AudioPacketJoin.Parse("packet|stream_index=1|stream_index=2|pts_time=0|data_hash="+h1));
        Reject(()=>AudioPacketJoin.Parse("packet|stream_index=1|pts_time=0|data_hash=SHA256:bad"));
        Check(AudioPacketJoin.Parse("packet|stream_index=4|pts_time=-0.021333|data_hash="+h1)==new AudioPacketJoin.Packet(4,-.021333,h1),"Packet parse");
        foreach(bool payload in new[]{true,false})Reject(()=>
        {
            var j=new AudioPacketJoin([1],[2],0);
            j.Push(true,new(1,0,h1));j.Push(false,new(2,payload?0:.02,payload?h2:h1));
        });
        Reject(()=>{var j=new AudioPacketJoin([1],[2],0,1);j.Push(true,new(1,0,h1));j.Push(true,new(1,.1,h2));});
        Reject(()=>{var j=new AudioPacketJoin([1,2],[3,4],0);j.Push(true,new(1,0,h1));j.Push(false,new(3,0,h1));j.Complete();});
        Reject(()=>{var j=new AudioPacketJoin([1],[2],0);j.Push(true,new(1,0,h1));j.Complete();});
        Reject(()=>{var j=new AudioPacketJoin([1],[2],0);j.Push(true,new(1,0,h1));j.Push(false,new(2,0,h1));j.Push(false,new(2,.1,h2));j.Complete();});
        Reject(()=>{var j=new AudioPacketJoin([1],[2],0);j.Push(true,new(3,0,h1));});
        Reject(()=>{var j=new AudioPacketJoin([1],[2],0);j.Push(true,new(1,0,h1));j.Push(true,new(1,.1,h2));j.Push(false,new(2,.1,h2));});
        string sourceHash=Hash(source);
        var sourceDetails=await Probe.InspectAsync(tools.Ffprobe,source,ct);
        var outputDetails=await Probe.InspectAsync(tools.Ffprobe,output,ct);
        Check(sourceDetails.AudioStreamIndices.Count==2&&outputDetails.AudioStreamIndices.Count==2,"Two-track metadata");
        await PacketTimeline.ValidateSingleDecodeInterleavedAsync(tools.Ffmpeg,tools.Ffprobe,source,output,sourceDetails.AudioStreamIndices,outputDetails.AudioStreamIndices,ct);
        await SingleDecodeScreen.ReferenceAsync(tools,source,output,2,ct);
        var candidate=Path.Combine(root,"interleaved-vfr.mp4");
        var report=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,candidate,plan,ct,singleDecodeValidation:true,interleavedAudioValidation:true);
        Check(report.InterleavedAudioPeakPending is >=1&&report.FullDecode==TimeSpan.Zero&&report.AudioHashCheck==TimeSpan.Zero&&File.Exists(candidate),"Interleaved full encode report");
        await SingleDecodeScreen.ReferenceAsync(tools,source,candidate,2,ct);
        await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,candidate,ct);
        Check(Hash(source)==sourceHash,"Interleaved source unchanged");
        // Actual four distinct AAC streams; full reference-verified output.
        string four=Path.Combine(root,"interleaved-four.mp4"),fourOut=Path.Combine(root,"interleaved-four-out.mp4");
        var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=24:d=8"};
        for(int i=0;i<4;i++)args.AddRange(["-f","lavfi","-i",$"sine=frequency={440+i*110}:sample_rate=48000:duration=8"]);
        args.AddRange(["-map","0:v"]);for(int i=1;i<=4;i++)args.AddRange(["-map",$"{i}:a"]);
        args.AddRange(["-c:v","libx264","-preset","ultrafast","-threads","2","-pix_fmt","yuv420p","-c:a","aac","-b:a","96k",four]);
        var made=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
        Check(made.ExitCode==0&&string.IsNullOrWhiteSpace(made.Error),"Four-track fixture");
        string fourHash=Hash(four);
        await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,four,fourOut,plan with{Codec="libx264"},ct,encoderThreads:2,singleDecodeValidation:true,interleavedAudioValidation:true);
        await SingleDecodeScreen.ReferenceAsync(tools,four,fourOut,4,ct);
        await Encoder.ValidateAudioAsync(tools.Ffmpeg,four,fourOut,ct);
        foreach(var (src,dst,tracks) in new[]{(source,candidate,2),(four,fourOut,4)})
        {
            var s=await Probe.InspectAsync(tools.Ffprobe,src,ct);var o=await Probe.InspectAsync(tools.Ffprobe,dst,ct);
            for(int rep=0;rep<3;rep++)
            foreach(bool merged in rep%2==0?new[]{false,true}:new[]{true,false})
            {
                var sw=Stopwatch.StartNew();int? peak=null;
                if(merged)peak=await PacketTimeline.ValidateSingleDecodeInterleavedAsync(tools.Ffmpeg,tools.Ffprobe,src,dst,s.AudioStreamIndices,o.AudioStreamIndices,ct);
                else{await PacketTimeline.ValidateSingleDecodeAsync(tools.Ffmpeg,tools.Ffprobe,src,dst,tracks,ct);await Encoder.ValidateAudioAsync(tools.Ffmpeg,src,dst,ct);}
                Console.WriteLine("INTERLEAVED_AUDIO "+System.Text.Json.JsonSerializer.Serialize(new {tracks,rep,merged,seconds=sw.Elapsed.TotalSeconds,peak,scope="validation-only"}));
            }
        }
        // Real stream substitution/reordering must fail even if timing matches.
        string swapped=Path.Combine(root,"interleaved-swapped.mp4");
        var swap=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-i",fourOut,"-map","0:v","-map","0:a:1","-map","0:a:0","-map","0:a:2","-map","0:a:3","-c","copy",swapped],ct);
        Check(swap.ExitCode==0&&string.IsNullOrWhiteSpace(swap.Error),"Swapped audio fixture");
        var sd=await Probe.InspectAsync(tools.Ffprobe,four,ct);var od=await Probe.InspectAsync(tools.Ffprobe,swapped,ct);
        bool bad=false;try{await PacketTimeline.ValidateSingleDecodeInterleavedAsync(tools.Ffmpeg,tools.Ffprobe,four,swapped,sd.AudioStreamIndices,od.AudioStreamIndices,ct);}catch(InvalidDataException){bad=true;}
        Check(bad,"Swapped audio tracks rejected");
        using var canceled=new CancellationTokenSource();canceled.Cancel();bool stopped=false;
        try{await InterleavedAudio.ValidateAsync(tools.Ffprobe,four,fourOut,sd.AudioStreamIndices,od.AudioStreamIndices,0,canceled.Token);}catch(OperationCanceledException){stopped=true;}
        Check(stopped&&Hash(four)==fourHash,"Pre-cancel and four-track source unchanged");
        Console.WriteLine("Interleaved audio bounded join, full Encoder, independent oracle and negative controls passed.");
    }
}
