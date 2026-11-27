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
        Check(HardwareEncoderPolicy.InputArguments(true,"h264_nvenc","h264").SequenceEqual(new[]{"-hwaccel","cuda","-hwaccel_output_format","cuda"})&&HardwareEncoderPolicy.InputArguments(false).Length==0,"Resident input option isolation");
        Check(HardwareEncoderPolicy.ResidentFilter(plan,"h264_nvenc",plan.Height+2).SequenceEqual(new[]{"-vf",$"scale_cuda=-2:{plan.Height}:format=nv12","-pix_fmt","+cuda"}),"Explicit device frames and no implicit software conversion");
        Check(!HardwareEncoderPolicy.Arguments("h264_nvenc",plan,true).Contains("yuv420p"),"Resident encoder does not force CPU pixel format");
        string bad=Path.Combine(root,"hardware-invalid.mp4");
        Check(HardwareEncoderPolicy.ResidentFilter(plan,"h264_nvenc",plan.Height).SequenceEqual(new[]{"-pix_fmt","+cuda"}),"No redundant GPU scale for original height");
        Check(HardwareEncoderPolicy.InputArguments(true,"hevc_qsv","h264").Contains("h264_qsv")&&HardwareEncoderPolicy.ResidentFilter(plan,"hevc_qsv",plan.Height).SequenceEqual(new[]{"-pix_fmt","+qsv"}),"QSV input decoder selected independently of output family");
        if(OperatingSystem.IsWindows())Check(HardwareEncoderPolicy.InputArguments(true,"h264_amf","h264").Contains("d3d11")&&HardwareEncoderPolicy.ResidentFilter(plan,"h264_amf",plan.Height).SequenceEqual(new[]{"-pix_fmt","+d3d11"}),"AMF D3D11 original-resolution path");
        Check(HardwareEncoderPolicy.Arguments("h264_nvenc",plan,true,true).TakeLast(2).SequenceEqual(new[]{"-preset","p1"}),"NVENC P1 isolated from software preset");
        bool badResident=false;
        try{await Encoder.EncodeHardwareTrialAsync(tools,source,bad,plan,"h264_amf",ct,fastHardware:true);}catch(ArgumentException){badResident=true;}
        Check(badResident&&!File.Exists(bad),"Unsupported AMF fast preset rejected");
        Check(HardwareEncoderPolicy.Arguments("h264_qsv",plan,true,true).TakeLast(2).SequenceEqual(new[]{"-preset","veryfast"}),"Intel speed preset isolated");
        foreach(int depth in new[]{8,16})
        {
            Check(HardwareEncoderPolicy.InputArguments(true,"hevc_qsv","h264",depth).TakeLast(2).SequenceEqual(new[]{"-extra_hw_frames",(2*depth).ToString(System.Globalization.CultureInfo.InvariantCulture)}),"Bounded QSV input pool");
            Check(HardwareEncoderPolicy.Arguments("h264_qsv",plan,true,true,depth).TakeLast(2).SequenceEqual(new[]{"-async_depth",depth.ToString(System.Globalization.CultureInfo.InvariantCulture)}),"QSV async depth output option");
        }
        foreach(var option in new[]{("h264_qsv",true,0),("h264_qsv",true,32),("h264_qsv",false,8),("h264_nvenc",true,8)})
        {
            bool rejected=false;
            try{await Encoder.EncodeHardwareTrialAsync(tools,source,bad,plan,option.Item1,ct,gpuResident:option.Item2,qsvAsyncDepth:option.Item3);}catch(ArgumentException){rejected=true;}
            Check(rejected&&!File.Exists(bad),"Invalid depth refuses before qualification/publication");
        }
        bool invalidRejected=false;
        try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,bad,plan,ct,researchHardwareEncoder:"unknown");}
        catch(ArgumentException){invalidRejected=true;}
        Check(invalidRejected&&!File.Exists(bad),"Invalid backend must not publish");
        string before=Hash(source);
        string trialBad=Path.Combine(root,"trial-invalid.mp4");
        bool invalidTrial=false;
        try{await Encoder.EncodeHardwareTrialAsync(tools,source,trialBad,plan,"unknown",ct);}
        catch(ArgumentException){invalidTrial=true;}
        Check(invalidTrial&&!File.Exists(trialBad),"Invalid explicit trial must not publish");
        invalidTrial=false;
        try{await Encoder.EncodeHardwareTrialAsync(tools,source,trialBad,plan,"hevc_nvenc",ct);}
        catch(ArgumentException){invalidTrial=true;}
        Check(invalidTrial&&!File.Exists(trialBad),"Mismatched hardware family rejected before qualification");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();bool refused=false;
            try{await Encoder.EncodeHardwareTrialAsync(tools,source,trialBad,plan,"h264_nvenc",canceled.Token);}
            catch(OperationCanceledException){refused=true;}
            Check(refused&&!File.Exists(trialBad),"Canceled explicit trial cannot publish");
        }
        var unavailable=diagnostics.FirstOrDefault(d=>d.State!="Sample passed");
        if(unavailable is not null)
        {
            bool refused=false;
            try{await Encoder.EncodeHardwareTrialAsync(tools,source,trialBad,plan with{Codec=HardwareEncoderPolicy.SoftwareFamily(unavailable.Encoder)},unavailable.Encoder,ct);}
            catch(NotSupportedException){refused=true;}
            // If device availability changes, do not label a verified encode as a failure.
            if(File.Exists(trialBad)){File.Delete(trialBad);Console.WriteLine("Hardware availability changed on repeat qualification.");}
            else Check(refused,"Unavailable device must fail qualification without publication");
        }
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
                var result=await Encoder.EncodeHardwareTrialAsync(tools,source,output,p,d.Encoder,deadline.Token);
                Check(result.HardwareEncoder==d.Encoder&&result.HardwareQualification>TimeSpan.Zero,"Explicit backend and qualification timing recorded");
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
        foreach(var d in diagnostics.Where(d=>d.State=="Sample passed"&&(OperatingSystem.IsWindows()||!d.Encoder.EndsWith("_amf",StringComparison.Ordinal))))
        {
            string output=Path.Combine(root,d.Encoder+"-resident.mp4");
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(40));
            try
            {
                var result=await Encoder.EncodeHardwareTrialAsync(tools,source,output,plan with{Codec=HardwareEncoderPolicy.SoftwareFamily(d.Encoder)},d.Encoder,deadline.Token,gpuResident:true);
                Check(result.GpuResident&&result.HardwareEncoder==d.Encoder&&File.Exists(output),"Resident path report");
                await PacketTimeline.ValidateAsync(tools.Ffprobe,source,output,m.AudioTracks,deadline.Token,compareAudioPayloads:true);
                Console.WriteLine(FormattableString.Invariant($"HARDWARE_RESIDENT {d.Encoder} verified seconds={result.Total.TotalSeconds:F6} bytes={result.OutputBytes}"));
            }
            catch(Exception ex) when(!ct.IsCancellationRequested)
            {
                if(File.Exists(output))throw;
                Console.WriteLine($"HARDWARE_RESIDENT {d.Encoder} rejected: {ex.GetType().Name}: {ex.Message}");
            }
            finally{if(File.Exists(output))File.Delete(output);}
            Check(!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Resident trial leaked temporary output");
        }
        Check(Hash(source)==before,"Hardware research altered source");
        Console.WriteLine("Hardware research argument and no-publication controls passed.");
    }
}
