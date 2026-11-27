using System.Globalization;
using System.Diagnostics;
namespace Vanta.Core;
internal enum MotionSearchMode { Default, Diamond, DiamondSubme1 }
public static class Encoder
{
    public static Task<EncodeReport> EncodeAsync(string ffmpeg,string ffprobe,string input,string output,Plan plan,CancellationToken ct=default,IProgress<EncodeProgress>? progress=null,bool experimentalSplit=false,int? encoderThreads=null,SourceStamp? expectedSource=null)
        =>EncodeResearchAsync(ffmpeg,ffprobe,input,output,plan,ct,progress,experimentalSplit,encoderThreads,expectedSource);
    // Explicit single-job device trial. No automatic backend selection or CPU fallback.
    // A successful tiny diagnostic is necessary, not sufficient; full validation still applies.
    public static async Task<EncodeReport> EncodeHardwareTrialAsync(Toolchain tools,string input,string output,Plan plan,string backend,CancellationToken ct=default,IProgress<EncodeProgress>? progress=null,SourceStamp? expectedSource=null,bool gpuResident=false,bool fastHardware=false,int? qsvAsyncDepth=null)
    {
        ct.ThrowIfCancellationRequested();
        // Software presets do not map to hardware presets. Use backend defaults.
        var applied=plan with {Preset="medium"};
        HardwareEncoderPolicy.Validate(backend,applied,false,null,null);
        if(gpuResident)HardwareEncoderPolicy.ValidateResident(backend);
        HardwareEncoderPolicy.ValidateFast(backend,fastHardware);
        HardwareEncoderPolicy.ValidateAsyncDepth(backend,gpuResident,qsvAsyncDepth);
        input=Path.GetFullPath(input);output=Path.GetFullPath(output);
        if(string.Equals(input,output,StringComparison.OrdinalIgnoreCase)||File.Exists(output)||Directory.Exists(output))
            throw new IOException("Output must be a new file");
        if(!string.Equals(Path.GetExtension(output),".mp4",StringComparison.OrdinalIgnoreCase)||!Directory.Exists(Path.GetDirectoryName(output)))
            throw new IOException("Choose a new MP4 in an existing folder.");
        var stamp=expectedSource??SourceStamp.Capture(input);
        stamp.RequireUnchanged(input);
        var clock=Stopwatch.StartNew();
        progress?.Report(new("Qualifying hardware",null));
        var diagnostic=(await EncoderDiagnostics.CheckAsync(tools,[backend],ct)).Single();
        if(diagnostic.State!="Sample passed")
            throw new NotSupportedException(backend+" hardware qualification failed: "+diagnostic.State+" — "+diagnostic.Detail);
        stamp.RequireUnchanged(input);
        var qualification=clock.Elapsed;
        var report=await EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,input,output,applied,ct,progress,
            expectedSource:stamp,researchHardwareEncoder:backend,gpuResident:gpuResident,fastHardware:fastHardware,qsvAsyncDepth:qsvAsyncDepth);
        return report with {HardwareQualification=qualification,Preparation=report.Preparation+qualification,Total=report.Total+qualification};
    }
    // Internal test entrypoint; public callers retain the existing two-part and serial-hash defaults.
    internal static async Task<EncodeReport> EncodeResearchAsync(string ffmpeg,string ffprobe,string input,string output,Plan plan,CancellationToken ct=default,IProgress<EncodeProgress>? progress=null,bool experimentalSplit=false,int? encoderThreads=null,SourceStamp? expectedSource=null,int splitSegments=2,int splitWorkers=2,bool parallelAudioHash=false,bool keyframeAligned=false,int? researchCrf=null,bool longestFirst=false,bool fusedAudioValidation=false,string? researchHardwareEncoder=null,bool parallelDecodeValidation=false,bool singleDecodeValidation=false,bool interleavedAudioValidation=false,SemaphoreSlim? validationGate=null,SourceClockSnapshot? sourceClockSnapshot=null,bool tenBitSdr=false,bool extendedAudioResearch=false,MotionSearchMode motionSearch=MotionSearchMode.Default,bool interleavedFallback=false,bool gpuResident=false,bool fastHardware=false,int? qsvAsyncDepth=null)
    {
        var clock=Stopwatch.StartNew();
        var mark=TimeSpan.Zero;
        TimeSpan Lap()
        {
            var now=clock.Elapsed;var elapsed=now-mark;mark=now;return elapsed;
        }
        ct.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(motionSearch))throw new ArgumentOutOfRangeException(nameof(motionSearch));
        if(motionSearch!=MotionSearchMode.Default&&(plan.Codec!="libx264"||plan.Preset!="veryfast"||experimentalSplit||researchCrf.HasValue||researchHardwareEncoder is not null||tenBitSdr||parallelDecodeValidation||singleDecodeValidation||fusedAudioValidation||parallelAudioHash||interleavedAudioValidation||extendedAudioResearch))
            throw new ArgumentException("Motion search research requires ordinary continuous H.264 veryfast ABR and baseline validation.");
        if(sourceClockSnapshot is not null&&!singleDecodeValidation)throw new ArgumentException("Source clock research requires single-decode validation.");
        if(interleavedFallback&&!interleavedAudioValidation)throw new ArgumentException("Interleaved fallback requires interleaved validation.");
        if(interleavedAudioValidation&&!singleDecodeValidation)throw new ArgumentException("Interleaved audio research requires single-decode validation.");
        if(singleDecodeValidation&&(parallelDecodeValidation||fusedAudioValidation||parallelAudioHash||experimentalSplit||researchHardwareEncoder is not null))throw new ArgumentException("Single-decode research requires the independent continuous CPU validation baseline.");
        if(fusedAudioValidation&&parallelAudioHash)throw new ArgumentException("Choose one research audio validation path.");
        if(parallelDecodeValidation&&(experimentalSplit||fusedAudioValidation||parallelAudioHash||researchHardwareEncoder is not null))throw new ArgumentException("Parallel decode research requires the independent continuous CPU validation baseline.");
        HardwareEncoderPolicy.ValidateAsyncDepth(researchHardwareEncoder,gpuResident,qsvAsyncDepth);
        if(gpuResident)HardwareEncoderPolicy.ValidateResident(researchHardwareEncoder??"");
        if(fastHardware)HardwareEncoderPolicy.ValidateFast(researchHardwareEncoder??"",true);
        if(researchHardwareEncoder is not null)HardwareEncoderPolicy.Validate(researchHardwareEncoder,plan,experimentalSplit,researchCrf,encoderThreads);
        ThreadBudget.ValidateEncoder(encoderThreads,experimentalSplit&&splitSegments==2&&splitWorkers==2);
        if(researchCrf is int crf&&(crf<0||crf>51||plan.Codec!="libx264"))throw new ArgumentException("Research CRF requires H.264 and a value from 0 to 51.");
        if((keyframeAligned||longestFirst)&&!experimentalSplit)throw new ArgumentException("Research partition or scheduling requires segmented mode.");
        input=Path.GetFullPath(input); output=Path.GetFullPath(output);
        if(string.Equals(input,output,StringComparison.OrdinalIgnoreCase)||File.Exists(output)) throw new IOException("Output must be a new file");
        if(!string.Equals(Path.GetExtension(output),".mp4",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only MP4 is supported in this prototype");
        // Hold a read-only handle across probing, encoding and validation.
        // FileShare.Read denies ordinary write/delete opens on Windows.
        using var sourceLease=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read);
        var stamp=SourceStamp.Capture(input);
        expectedSource?.RequireUnchanged(input);
        sourceClockSnapshot?.RequireMetadata(ffprobe,sourceLease);
        var details=await Probe.InspectResearchAsync(ffprobe,input,ct,tenBitSdr,extendedAudioResearch);
        stamp.RequireUnchanged(input);
        if(!details.Supported) throw new NotSupportedException(details.Reason);
        var source=details.Info;
        if(gpuResident&&(source.Codec is not ("h264" or "hevc")||details.PixelFormat!="yuv420p"))throw new NotSupportedException("Resident trial requires 8-bit 4:2:0 AVC/HEVC input.");
        if(tenBitSdr&&(details.PixelFormat!="yuv420p10le"||plan.Codec!="libx265"||experimentalSplit||researchHardwareEncoder is not null||plan.Height!=source.Height))
            throw new NotSupportedException("10-bit SDR research requires full-resolution continuous software HEVC.");
        bool extendedAudio=details.Audio.Any(a=>a.Codec!="aac");
        if(extendedAudio&&experimentalSplit)throw new NotSupportedException("Extended audio copy requires continuous encoding.");
        if(interleavedAudioValidation&&(source.AudioTracks is <1 or >32))throw new NotSupportedException("Combined packet research requires 1–32 audio tracks.");
        PlanRules.Validate(plan,source);
        var dir=Path.GetDirectoryName(output)!;
        // ABR estimate only; CRF research has no bitrate-derived size contract.
        using var reservation=researchCrf.HasValue?null:ScratchBudget.Shared.Reserve(dir,plan.EstimateBytes(source),experimentalSplit);
        var temp=Path.Combine(dir,$".vanta-{Guid.NewGuid():N}.mp4");
        var common=new List<string>{"-hide_banner","-nostdin","-n","-xerror"};
        common.AddRange(HardwareEncoderPolicy.InputArguments(gpuResident,researchHardwareEncoder,source.Codec,qsvAsyncDepth));
        common.AddRange(["-i",input,"-map","0:V:0"]);
        if(researchHardwareEncoder is null)common.AddRange(["-c:v",plan.Codec,"-preset",plan.Preset,researchCrf.HasValue?"-crf":"-b:v",(researchCrf.HasValue?(long)researchCrf.Value:plan.VideoBitsPerSecond).ToString(CultureInfo.InvariantCulture)]);
        else common.AddRange(HardwareEncoderPolicy.Arguments(researchHardwareEncoder,plan,gpuResident,fastHardware,qsvAsyncDepth));
        if(motionSearch!=MotionSearchMode.Default)common.AddRange(["-x264-params",motionSearch==MotionSearchMode.Diamond?"me=dia":"me=dia:subme=1"]);
        if(tenBitSdr)common.AddRange(["-pix_fmt","yuv420p10le","-profile:v","main10"]);
        common.AddRange(["-fps_mode","passthrough"]);
        if(encoderThreads is int threads)common.AddRange(["-threads",threads.ToString(CultureInfo.InvariantCulture)]);
        if(gpuResident)common.AddRange(HardwareEncoderPolicy.ResidentFilter(plan,researchHardwareEncoder!,source.Height));
        else if(plan.Height!=source.Height) common.AddRange(["-vf",$"scale=-2:{plan.Height}"]);
        var preparation=Lap();
        try
        {
            // Continuous encoding is the default. The explicit experimental path shares every final validation gate.
            // x265 stats paths use encoder-specific escaping; keep two-pass disabled until tested.
            if(plan.TwoPass) throw new NotSupportedException("Two-pass encoding is pending Windows path and timing validation");
            if(experimentalSplit)
            {
                progress?.Report(new("Experimental split",null));
                await ExperimentalSplit.EncodeVideoAndMuxAsync(ffmpeg,ffprobe,input,temp,details,plan,ct,encoderThreads,splitSegments,splitWorkers,keyframeAligned,researchCrf,longestFirst);
            }
            else
            {
            var args=new List<string>(common);
            args.AddRange(["-map","0:a?","-c:a","copy","-tag:v",plan.Codec=="libx265"?"hvc1":"avc1","-movflags","+faststart",temp]);
            args.InsertRange(0,["-progress","pipe:1","-nostats","-loglevel","error"]);
            progress?.Report(new("Encoding",0));
            var parser=new ProgressParser(source.Seconds,progress);
            var encoded=await ProcessRunner.RunAsync(ffmpeg,args,ct,parser.Read);
            if(encoded.ExitCode!=0||!string.IsNullOrWhiteSpace(encoded.Error))throw new InvalidOperationException("Encoding failed: "+encoded.Error);
            }
            var encoding=Lap();
            TimeSpan admissionWait=TimeSpan.Zero;
            if(validationGate is not null)progress?.Report(new("Waiting for validation",null));
            using var validationLease=await ValidationLease.EnterAsync(validationGate,ct);
            if(validationGate is not null)admissionWait=Lap();
            void VerifyPhase(VerificationPhase phase)=>progress?.Report(new EncodeProgress("Validating",null){Verification=phase});
            VerifyPhase(VerificationPhase.Metadata);
            var actualDetails=await Probe.InspectResearchAsync(ffprobe,temp,ct,tenBitSdr,extendedAudioResearch);
            if(!actualDetails.Supported) throw new InvalidDataException(actualDetails.Reason);
            var actual=actualDetails.Info;
            if(actual.Height!=plan.Height || actual.Codec!=(plan.Codec=="libx265"?"hevc":"h264")) throw new InvalidDataException("Output codec or height mismatch");
            if(details.Visual is null||actualDetails.Visual is null)throw new InvalidDataException("Visual metadata unavailable");
            details.Visual.ValidateOutput(actualDetails.Visual);
            if(actualDetails.PixelFormat!=details.PixelFormat)throw new InvalidDataException("Pixel format or bit depth changed");
            AudioMetadata.ValidateOutput(details.Audio,actualDetails.Audio);
            Timeline.Validate(details.Timing,actualDetails.Timing,source.Fps);
            if(actual.AudioTracks!=source.AudioTracks || Math.Abs(actual.Seconds-source.Seconds)>0.25) throw new InvalidDataException("Output stream count or duration mismatch");
            var outputProbe=Lap();
            TimeSpan timeline,decode,audioHash=TimeSpan.Zero;
            TimeSpan? overlap=null;
            int? audioPending=null;
            Task CheckTimeline(CancellationToken token)=>PacketTimeline.ValidateAsync(ffprobe,input,temp,source.AudioTracks,token,compareAudioPayloads:fusedAudioValidation||extendedAudio);
            Task CheckDecode(CancellationToken token)=>Checked(ffmpeg,["-v","error","-xerror","-nostdin","-i",temp,"-map","0:V:0","-map","0:a?","-f","null","-"],token);
            if(singleDecodeValidation)
            {
                VerifyPhase(VerificationPhase.Combined);
                if(interleavedAudioValidation)
                    audioPending=interleavedFallback
                        ?await PacketTimeline.ValidateSingleDecodeInterleavedWithFallbackAsync(ffmpeg,ffprobe,input,temp,details.AudioStreamIndices,actualDetails.AudioStreamIndices,ct,sourceClockSnapshot)
                        :await PacketTimeline.ValidateSingleDecodeInterleavedAsync(ffmpeg,ffprobe,input,temp,details.AudioStreamIndices,actualDetails.AudioStreamIndices,ct,sourceClockSnapshot);
                else await PacketTimeline.ValidateSingleDecodeAsync(ffmpeg,ffprobe,input,temp,source.AudioTracks,ct,sourceClockSnapshot,compareAudioPayloads:extendedAudio);
                timeline=Lap();decode=TimeSpan.Zero;
                if(source.AudioTracks>0&&!interleavedAudioValidation){VerifyPhase(VerificationPhase.AudioIdentity);await ValidateAudioAsync(ffmpeg,input,temp,ct);audioHash=Lap();}
            }
            else if(parallelDecodeValidation)
            {
                VerifyPhase(VerificationPhase.Combined);
                using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
                async Task<TimeSpan> Timed(Func<CancellationToken,Task> work)
                {
                    var timer=Stopwatch.StartNew();
                    try{await work(linked.Token);return timer.Elapsed;}
                    catch{linked.Cancel();throw;}
                }
                var t=Timed(CheckTimeline);
                var d=Timed(CheckDecode);
                // Join both before leaving this scope, even when either fails.
                var durations=await Task.WhenAll(t,d);
                timeline=durations[0];decode=durations[1];overlap=Lap();
                if(source.AudioTracks>0){VerifyPhase(VerificationPhase.AudioIdentity);await ValidateAudioAsync(ffmpeg,input,temp,ct);audioHash=Lap();}
            }
            else
            {
                VerifyPhase(VerificationPhase.Timeline);
                await CheckTimeline(ct);timeline=Lap();
                if(source.AudioTracks>0&&!fusedAudioValidation)
                {
                    VerifyPhase(VerificationPhase.AudioIdentity);
                    await ValidateAudioAsync(ffmpeg,input,temp,ct,parallelAudioHash);
                    audioHash=Lap();
                }
                VerifyPhase(VerificationPhase.FullDecode);
                await CheckDecode(ct);decode=Lap();
            }
            // New codecs require exact decoded PCM too: packet bytes do not
            // capture edit-list, priming and discard effects.
            if(extendedAudio)
            {
                VerifyPhase(VerificationPhase.DecodedAudio);
                await DecodedAudioIdentity.ValidateAsync(ffmpeg,input,temp,source.AudioTracks,ct);
                audioHash+=Lap();
            }
            VerifyPhase(VerificationPhase.Publishing);
            var bytes=new FileInfo(temp).Length;
            ct.ThrowIfCancellationRequested();
            stamp.RequireUnchanged(input);
            File.Move(temp,output,false);
            var publication=Lap();
            var report=new EncodeReport(preparation,encoding,outputProbe,timeline,audioHash,decode,publication,mark,bytes,source.Bytes){ConcurrentTimelineDecode=overlap,SingleDecodeValidation=singleDecodeValidation,RequestedEncoderThreads=encoderThreads,InterleavedAudioPeakPending=audioPending,InterleavedAudioFallbackUsed=interleavedFallback&&audioPending is null,ValidationAdmissionWait=admissionWait,AppliedPlan=plan,HardwareEncoder=researchHardwareEncoder,GpuResident=gpuResident,HardwarePreset=researchHardwareEncoder is string device?HardwareEncoderPolicy.FastPreset(device,fastHardware):null,QsvAsyncDepth=qsvAsyncDepth};
            // Publication is the commit point. A completion observer cannot turn
            // an already-published verified output into a failed encode.
            try { progress?.Report(new("Complete",1)); }
            catch(Exception) { }
            return report;
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    internal static async Task ValidateAudioAsync(string exe,string source,string output,CancellationToken ct,bool parallel=false)
    {
        async Task<string> Hash(string path)
        {
            var r=await ProcessRunner.RunAsync(exe,["-v","error","-nostdin","-i",path,"-map","0:a","-c:a","copy","-f","streamhash","-hash","sha256","-"],ct);
            if(r.ExitCode!=0 || !string.IsNullOrWhiteSpace(r.Error) || string.IsNullOrWhiteSpace(r.Output)) throw new InvalidDataException("Audio payload hash unavailable");
            return r.Output.Trim();
        }
        string a,b;
        if(parallel)
        {
            var hashes=await Task.WhenAll(Hash(source),Hash(output));
            a=hashes[0];b=hashes[1];
        }
        else{a=await Hash(source);b=await Hash(output);}
        if(a!=b)throw new InvalidDataException("Copied audio payload changed");
    }
    private static async Task Checked(string exe,IEnumerable<string> args,CancellationToken ct)
    {
        var r=await ProcessRunner.RunAsync(exe,args,ct);
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error)) throw new InvalidOperationException("Validation failed: "+r.Error);
    }
}