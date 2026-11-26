using System.Security.Cryptography;
using Vanta.Core;

internal static class HardwareResearchScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static async Task RunAsync(Toolchain tools,string root,string source,IReadOnlyList<EncoderDiagnostic> diagnostics,CancellationToken ct)
    {
        var m=(await Probe.InspectAsync(tools.Ffprobe,source,ct)).Info;
        var plan=new Plan("Hardware research","libx264",m.Height,250000,0,"medium",false);
        foreach(var encoder in EncoderDiagnostics.HardwareCandidates)
        {
            var p=plan with {Codec=HardwareEncoderPolicy.SoftwareFamily(encoder)};
            var args=HardwareEncoderPolicy.Arguments(encoder,p);
            Check(args.SequenceEqual(new[]{"-c:v",encoder,"-b:v","250000","-pix_fmt","yuv420p"})&&!args.Contains("-preset"),"Hardware argument isolation");
            foreach(var invalid in new[]{p with {Codec=p.Codec=="libx264"?"libx265":"libx264"},p with {Preset="fast"},p with {TwoPass=true}})
            {
                bool rejected=false;
                try{HardwareEncoderPolicy.Validate(encoder,invalid,false,null,null);}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid hardware plan accepted");
            }
            foreach(var options in new[]{(true,(int?)null,(int?)null),(false,(int?)23,(int?)null),(false,(int?)null,(int?)4)})
            {
                bool rejected=false;
                try{HardwareEncoderPolicy.Validate(encoder,p,options.Item1,options.Item2,options.Item3);}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid hardware research option accepted");
            }
        }
        string bad=Path.Combine(root,"hardware-invalid.mp4");
        bool invalidRejected=false;
        try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,bad,plan,ct,researchHardwareEncoder:"unknown");}
        catch(ArgumentException){invalidRejected=true;}
        Check(invalidRejected&&!File.Exists(bad),"Invalid backend must not publish");
        string before=Hash(source);
        foreach(var d in diagnostics)
        {
            ct.ThrowIfCancellationRequested();
            if(d.State!="Sample passed")
            {Console.WriteLine($"HARDWARE_FULL {d.Encoder} skipped: {d.State}");continue;}
            var p=plan with {Codec=HardwareEncoderPolicy.SoftwareFamily(d.Encoder)};
            string output=Path.Combine(root,d.Encoder+"-full.mp4");
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(40));
            try
            {
                var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,p,deadline.Token,researchHardwareEncoder:d.Encoder);
                Check(File.Exists(output),"Successful hardware encode missing output");
                await PacketTimeline.ValidateAsync(tools.Ffprobe,source,output,m.AudioTracks,deadline.Token,compareAudioPayloads:true);
                Console.WriteLine(FormattableString.Invariant($"HARDWARE_FULL {d.Encoder} verified seconds={result.Total.TotalSeconds:F6} bytes={result.OutputBytes}"));
            }
            catch(Exception ex) when(!ct.IsCancellationRequested)
            {
                // If a post-publication independent check failed, fail the CI
                // rather than misclassify a published result as a driver skip.
                if(File.Exists(output))throw;
                Console.WriteLine($"HARDWARE_FULL {d.Encoder} rejected: {ex.GetType().Name}: {ex.Message}");
            }
            finally{if(File.Exists(output))File.Delete(output);}
            Check(!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Hardware research leaked temporary output");
        }
        Check(Hash(source)==before,"Hardware research altered source");
        Console.WriteLine("Hardware research argument and no-publication controls passed.");
    }
}
