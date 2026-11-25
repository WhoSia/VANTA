using Vanta.Core;
using System.Security.Cryptography;
// This test executable doubles as a deterministic, zero-exit fake probe only for a marked fixture.
if(args.Length==7&&args[0]=="-v"&&args[1]=="error"&&args[2]=="-show_format"&&args[3]=="-show_streams"&&args[4]=="-of"&&args[5]=="json"&&File.Exists(args[6]+".test-probe.json"))
{
    // Write ASCII JSON bytes directly, independent of the console's code page.
    using(var stdout=Console.OpenStandardOutput())
        await stdout.WriteAsync(await File.ReadAllBytesAsync(args[6]+".test-probe.json"));
    if(File.Exists(args[6]+".test-probe-error"))
        Console.Error.WriteLine("Synthetic error-level probe diagnostic");
    return;
}
if(args.Length==3&&args[0]=="--corpus")
{
    using var corpusTimeout=new CancellationTokenSource(TimeSpan.FromMinutes(24));
    await PublicCorpusScreen.RunAsync(args[1],args[2],corpusTimeout.Token);
    return;
}
if(args.Length!=0)throw new ArgumentException("Unexpected integration-test arguments.");
var root=Path.Combine(Path.GetTempPath(),"VANTA CI 한글 "+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));
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
    var softwareCheck=await EncoderDiagnostics.CheckAsync(resolved,["libx264","libx265"],ct);
    Check(softwareCheck.Count==2&&softwareCheck.All(r=>r.State=="Sample passed"),"Software diagnostic positive controls");
    var hardwareCheck=await EncoderDiagnostics.CheckAsync(resolved,EncoderDiagnostics.HardwareCandidates,ct);
    Check(hardwareCheck.Count==6&&hardwareCheck.All(r=>r.State is "Sample passed" or "Not listed" or "Failed" or "Timed out"),"Hardware diagnostic classification");
    foreach(var item in hardwareCheck)Console.WriteLine($"ENCODER_DIAG {item.Encoder} {item.State}: {item.Detail}");
    using(var preCanceled=new CancellationTokenSource())
    {
        preCanceled.Cancel();bool stopped=false;
        try{await EncoderDiagnostics.CheckAsync(resolved,["libx264"],preCanceled.Token);}catch(OperationCanceledException){stopped=true;}
        Check(stopped,"Diagnostic pre-cancel");
    }
    Console.WriteLine("Encoder diagnostic positive controls and classification passed.");
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
    var fakeProbe=Path.Combine(AppContext.BaseDirectory,"Vanta.Integration"+(OperatingSystem.IsWindows()?".exe":""));
    var capturedProbe=await ProcessRunner.RunAsync(resolved.Ffprobe,["-v","error","-show_format","-show_streams","-of","json",input],ct);
    Check(capturedProbe.ExitCode==0&&string.IsNullOrWhiteSpace(capturedProbe.Error),"Positive probe control");
    using var capturedJson=System.Text.Json.JsonDocument.Parse(capturedProbe.Output);
    string replayJson=System.Text.Json.JsonSerializer.Serialize(capturedJson.RootElement);
    Check(replayJson.All(c=>c<=127),"Probe replay JSON must be ASCII");
    await File.WriteAllTextAsync(input+".test-probe.json",replayJson,ct);
    var probeArgs=new[]{"-v","error","-show_format","-show_streams","-of","json",input};
    var cleanReplay=await ProcessRunner.RunAsync(fakeProbe,probeArgs,ct);
    Check(cleanReplay.ExitCode==0&&cleanReplay.Output==replayJson&&string.IsNullOrWhiteSpace(cleanReplay.Error),
        $"Clean probe replay failed: exit={cleanReplay.ExitCode}, stdout chars={cleanReplay.Output.Length}/{replayJson.Length}, stderr={cleanReplay.Error}");
    var replayDetails=await Probe.InspectAsync(fakeProbe,input,ct);
    Check(replayDetails.Supported&&replayDetails.Info==source.Info,"Clean replay must be accepted with matching media details");
    await File.WriteAllTextAsync(input+".test-probe-error","error",ct);
    var synthetic=await ProcessRunner.RunAsync(fakeProbe,probeArgs,ct);
    Check(synthetic.ExitCode==0&&synthetic.Output==cleanReplay.Output&&synthetic.Error.Contains("Synthetic error-level probe diagnostic"),
        $"Zero-exit error control failed: exit={synthetic.ExitCode}, stdout chars={synthetic.Output.Length}/{cleanReplay.Output.Length}, stderr={synthetic.Error}");
    bool diagnosticRejected=false;
    try{await Probe.InspectAsync(fakeProbe,input,ct);}
    catch(InvalidOperationException ex) when(ex.Message.Contains("Synthetic error-level probe diagnostic")){diagnosticRejected=true;}
    Check(diagnosticRejected,"Probe accepted error-level stderr with exit zero");
    File.Delete(input+".test-probe-error");
    File.Delete(input+".test-probe.json");
    Console.WriteLine("Clean probe replay accepted and identical zero-exit error diagnostic rejected.");
    var plan=new Plan("CI","libx265",180,180000,0,"medium",false,true);
    var events=new List<EncodeProgress>();
    var expectedStamp=SourceStamp.Capture(input);
    bool writeDenied=false;
    var firstReport=await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,output,plan,ct,new ImmediateProgress(e=>
    {
        events.Add(e);
        if(OperatingSystem.IsWindows()&&e.Stage=="Encoding"&&e.Fraction==0)
        {
            try{using var writer=new FileStream(input,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);}
            catch(IOException){writeDenied=true;}
        }
    }),expectedSource:expectedStamp);
    Check(!OperatingSystem.IsWindows()||writeDenied,"Source write lease not enforced");
    Check(firstReport.InputBytes==expectedStamp.Bytes,"Source size report");
    var stale=Path.Combine(root,"stale source.mp4");
    var staleOut=Path.Combine(root,"stale output.mp4");
    File.Copy(input,stale);
    var staleStamp=SourceStamp.Capture(stale);
    File.SetLastWriteTimeUtc(stale,staleStamp.LastWriteUtc.AddMinutes(-5));
    bool staleRejected=false;
    try{await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,stale,staleOut,plan,ct,expectedSource:staleStamp);}
    catch(IOException ex) when(ex.Message.Contains("Source changed")){staleRejected=true;}
    Check(staleRejected&&!File.Exists(staleOut)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Stale source must not publish");
    Console.WriteLine("Source stamp, Windows write lease and stale plan rejection passed.");
    Check(firstReport.OutputBytes==new FileInfo(output).Length && firstReport.Total>TimeSpan.Zero && firstReport.Validation>TimeSpan.Zero && firstReport.AudioHashCheck>TimeSpan.Zero,"Encode report fields");
    Check(firstReport.Preparation+firstReport.Encoding+firstReport.Validation+firstReport.Publication==firstReport.Total,"Phase time accounting");
    Console.WriteLine("Encode report accounting passed.");
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
    // Shadow mode only: the production Encoder still uses separate streamhash.
    await PacketTimeline.ValidateAsync(resolved.Ffprobe,input,output,1,ct,compareAudioPayloads:true);
    rejected=false;
    try{await PacketTimeline.ValidateAsync(resolved.Ffprobe,input,shifted,1,ct,compareAudioPayloads:true);}
    catch(InvalidDataException){rejected=true;}
    Check(rejected,"Shadow payload comparator accepted shifted audio");
    var otherAudio=Path.Combine(root,"changed audio source.mp4");
    var changed=Path.Combine(root,"same PTS changed audio.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=5","-itsoffset","0.2","-f","lavfi","-i","sine=frequency=880:sample_rate=48000:duration=4.8","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",otherAudio);
    await Run("-v","error","-nostdin","-n","-i",input,"-i",otherAudio,"-map","0:v:0","-map","1:a:0","-c","copy",changed);
    await PacketTimeline.ValidateAsync(resolved.Ffprobe,input,changed,1,ct);
    rejected=false;
    try{await PacketTimeline.ValidateAsync(resolved.Ffprobe,input,changed,1,ct,compareAudioPayloads:true);}
    catch(InvalidDataException ex) when(ex.Message.Contains("Audio packet payload changed")){rejected=true;}
    Check(rejected,"Shadow comparator missed same-PTS changed audio");
    Console.WriteLine("Shadow packet validation accepted copied AAC and rejected shifted and substituted audio.");
    var silent=Path.Combine(root,"silent source.mp4");
    var silentOut=Path.Combine(root,"silent compressed.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=3","-c:v","libx264","-pix_fmt","yuv420p",silent);
    var silentReport=await Encoder.EncodeAsync("ffmpeg","ffprobe",silent,silentOut,plan with { Height=90 },ct);
    Check(silentReport.AudioHashCheck==TimeSpan.Zero && silentReport.OutputBytes==new FileInfo(silentOut).Length,"Silent encode report");
    var silentInfo=await Probe.InspectAsync("ffprobe",silentOut,ct);
    Check(silentInfo.Info.AudioTracks==0 && silentInfo.Info.Height==90,"Silent scaled output incorrect");
    Console.WriteLine("Silent video and scaling passed.");
    var anamorphic=Path.Combine(root,"anamorphic.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=2","-vf","setsar=4/3","-c:v","libx264","-pix_fmt","yuv420p","-colorspace","bt709","-color_range","tv",anamorphic);
    var originalVisual=await Probe.InspectAsync(resolved.Ffprobe,anamorphic,ct);
    Check(originalVisual.Supported&&originalVisual.Visual?.DisplayRatio is double dar&&Math.Abs(dar-64d/27)<.001,"Anamorphic fixture metadata");
    foreach(var codec in PlanRules.Codecs)
    {
        var visualOut=Path.Combine(root,codec+" anamorphic.mp4");
        await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,anamorphic,visualOut,plan with {Codec=codec,Height=90},ct);
        var outputVisual=await Probe.InspectAsync(resolved.Ffprobe,visualOut,ct);
        originalVisual.Visual!.ValidateOutput(outputVisual.Visual!);
    }
    Console.WriteLine("Anamorphic DAR and declared color tags preserved for both software codecs.");
    var rotated=Path.Combine(root,"rotation 90.mp4");
    await Run("-v","error","-nostdin","-n","-display_rotation","90","-i",silent,"-map","0:v:0","-c","copy",rotated);
    var rotationInfo=await Probe.InspectAsync(resolved.Ffprobe,rotated,ct);
    Check(rotationInfo.Visual?.HasTransform==true&&!rotationInfo.Supported,"Real display matrix must be detected and rejected");
    var rotatedOut=Path.Combine(root,"rotation rejected.mp4");
    bool rotationRejected=false;
    try{await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,rotated,rotatedOut,plan,ct);}
    catch(NotSupportedException){rotationRejected=true;}
    Check(rotationRejected&&!File.Exists(rotatedOut)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Rotated source must not publish");
    Console.WriteLine("Real 90-degree display matrix rejected without output publication.");
    var avcOut=Path.Combine(root,"avc fast.mp4");
    await Encoder.EncodeAsync("ffmpeg","ffprobe",input,avcOut,plan with {Codec="libx264",Preset="fast"},ct,encoderThreads:1);
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
    var perFile=BatchPlanSelection.Resolve(await Probe.ReadAsync(resolved.Ffprobe,silent,ct),resolved.Codecs,"Balanced","libx265",plan with {Name="Custom",Codec="libx264",Height=90,Preset="veryfast"})!;
    await BatchEncoder.RunAsync(resolved,[new(input,batchA,plan),new(silent,batchB,perFile),new(input,output,plan)],2,new BatchProgress(updates.Enqueue),ct,totalEncoderThreads:4);
    var batchInfoA=await Probe.ReadAsync(resolved.Ffprobe,batchA,ct);
    var batchInfoB=await Probe.ReadAsync(resolved.Ffprobe,batchB,ct);
    Check(batchInfoA.Codec=="hevc"&&batchInfoA.Height==180&&batchInfoB.Codec=="h264"&&batchInfoB.Height==90,"Independent batch settings");
    Console.WriteLine("Mixed-codec and mixed-height per-file batch passed.");
    Check(File.Exists(batchA)&&File.Exists(batchB)&&Hash(input)==before&&Hash(output)==priorOutput,"Parallel output safety");
    Check(updates.Count(x=>x.Stage=="Complete")==2&&updates.Count(x=>x.Stage=="Failed")==1,"Batch failure isolation");
    Console.WriteLine("Parallel batch, collision refusal and per-job failure isolation passed (4 requested encoder threads shared).");
    var notificationOutput=Path.Combine(root,"notification failure.mp4");
    var notifications=new System.Collections.Concurrent.ConcurrentQueue<BatchUpdate>();
    var terminalNotifications=await BatchEncoder.RunAsync(resolved,[new(silent,notificationOutput,perFile)],1,new BatchProgress(x=>
    {
        notifications.Enqueue(x);
        if(x.Stage=="Complete")throw new InvalidOperationException("Synthetic completion observer failure");
    }),ct,totalEncoderThreads:2);
    Check(File.Exists(notificationOutput)&&notifications.Count(x=>x.Stage=="Complete")==1&&!notifications.Any(x=>x.Stage=="Failed"),"Published batch output cannot become failed due to observer");
    Check(terminalNotifications.Count==1&&terminalNotifications[0].Stage=="Complete"&&terminalNotifications[0].Report is not null,"Published outcome independent of observer");
    Console.WriteLine("Batch post-publication observer exception does not produce a false failure.");
    using(var stopAfterFirst=CancellationTokenSource.CreateLinkedTokenSource(ct))
    {
        var kept=Path.Combine(root,"batch kept.mp4");
        var never=Path.Combine(root,"batch never started.mp4");
        var terminal=await BatchEncoder.RunAsync(resolved,[new(silent,kept,perFile),new(silent,never,perFile)],1,new BatchProgress(x=>
        {
            if(x.Stage=="Complete"){stopAfterFirst.Cancel();throw new InvalidOperationException("Observer lost completion");}
        }),stopAfterFirst.Token,totalEncoderThreads:2);
        Check(terminal.Count==2&&terminal[0].Stage=="Complete"&&terminal[0].Report is not null&&terminal[1].Stage=="Canceled"&&File.Exists(kept)&&!File.Exists(never),"Cancellation after publication preserves completed result and marks queued job");
    }
    var vfr=Path.Combine(root,"vfr multiple audio.mp4");
    var vfrOut=Path.Combine(root,"vfr compressed.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30:d=12","-f","lavfi","-i","sine=frequency=440:sample_rate=48000:duration=12","-itsoffset","0.15","-f","lavfi","-i","sine=frequency=880:sample_rate=48000:duration=11.85","-map","0:v","-map","1:a","-map","2:a","-vf","select=not(mod(n\\,2))+not(mod(n\\,5))","-fps_mode","vfr","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",vfr);
    var vf=await Probe.InspectAsync("ffprobe",vfr,ct);
    Check(vf.Supported && vf.Info.AudioTracks==2,vf.Reason);
    await Encoder.EncodeAsync("ffmpeg","ffprobe",vfr,vfrOut,plan,ct);
    Check(File.Exists(vfrOut),"VFR output absent");
    Console.WriteLine("VFR and two independently offset audio streams passed.");
    await PacketTimeline.ValidateAsync(resolved.Ffprobe,vfr,vfrOut,2,ct,compareAudioPayloads:true);
    var mov=Path.Combine(root,"copied MOV.mov");
    var movOut=Path.Combine(root,"MOV compressed.mp4");
    await Run("-v","error","-nostdin","-n","-i",input,"-map","0:v:0","-map","0:a:0","-c","copy",mov);
    await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,mov,movOut,plan,ct);
    await PacketTimeline.ValidateAsync(resolved.Ffprobe,mov,movOut,1,ct,compareAudioPayloads:true);
    Console.WriteLine("Shadow packet validation passed VFR multi-audio and MOV input.");
    var longInput=Path.Combine(root,"long source.mp4");
    var longOutput=Path.Combine(root,"long compressed.mp4");
    await Run("-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=30000/1001:d=120","-f","lavfi","-i","sine=frequency=660:sample_rate=48000:duration=120","-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p","-c:a","aac",longInput);
    await Encoder.EncodeAsync("ffmpeg","ffprobe",longInput,longOutput,plan,ct);
    Check(File.Exists(longOutput),"Long output absent");
    Console.WriteLine("120-second NTSC-rate timeline passed.");
    await TimestampParsingScreen.RunAsync(resolved.Ffprobe,longInput,ct);
    await AudioHashScreen.RunAsync(resolved.Ffmpeg,longInput,longOutput,vfr,vfrOut,input,changed,ct);
    for(int repeat=0;repeat<2;repeat++)
    {
        foreach(bool fused in repeat==0?new[]{false,true}:new[]{true,false})
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            await PacketTimeline.ValidateAsync(resolved.Ffprobe,longInput,longOutput,1,ct,compareAudioPayloads:fused);
            if(!fused)
            {
                async Task<string> AudioHash(string path)
                {
                    var result=await ProcessRunner.RunAsync(resolved.Ffmpeg,["-v","error","-nostdin","-i",path,"-map","0:a","-c:a","copy","-f","streamhash","-hash","sha256","-"],ct);
                    Check(result.ExitCode==0&&!string.IsNullOrWhiteSpace(result.Output),"Reference hash failed");
                    return result.Output.Trim();
                }
                var sourceHash=await AudioHash(longInput);
                var outputHash=await AudioHash(longOutput);
                Check(sourceHash==outputHash,"Reference audio hash mismatch");
            }
            Console.WriteLine(FormattableString.Invariant($"PACKET_SHADOW rep={repeat} fused={fused} seconds={clock.Elapsed.TotalSeconds:F4}"));
        }
    }
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
    var splitIds=await MarkerHashes(markerOut);
    Check(Enumerable.SequenceEqual(ids,splitIds),"Frame content identity changed at split");
    Console.WriteLine("All 121 unique frame IDs preserved across odd split.");
    foreach(var (segments,workers) in new[]{(3,2),(4,4),(8,2),(8,4)})
    {
        var markerDest=Path.Combine(root,$"marker-{segments}-{workers}.mp4");
        await Encoder.EncodeResearchAsync(resolved.Ffmpeg,resolved.Ffprobe,marker,markerDest,splitPlan with {Height=96,VideoBitsPerSecond=2_000_000},ct,experimentalSplit:true,encoderThreads:4,splitSegments:segments,splitWorkers:workers);
        var markerIds=await MarkerHashes(markerDest);
        Check(Enumerable.SequenceEqual(ids,markerIds),$"Frame identities changed in {segments}/{workers} split");
        Check(!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Multi-split staging leaked");
    }
    var invalidSplit=Path.Combine(root,"invalid worker budget.mp4");
    rejected=false;
    try{await Encoder.EncodeResearchAsync(resolved.Ffmpeg,resolved.Ffprobe,marker,invalidSplit,splitPlan with {Height=96,VideoBitsPerSecond=2_000_000},ct,experimentalSplit:true,encoderThreads:2,splitSegments:8,splitWorkers:4);}
    catch(ArgumentException){rejected=true;}
    Check(rejected&&!File.Exists(invalidSplit)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Invalid research worker budget must fail closed");
    Console.WriteLine("Multi-split 3/4/8 frame identity and worker budget controls passed.");
    var reorderedWorkDest=Path.Combine(root,"marker-longest-first.mp4");
    await Encoder.EncodeResearchAsync(resolved.Ffmpeg,resolved.Ffprobe,marker,reorderedWorkDest,splitPlan with {Height=96,VideoBitsPerSecond=2_000_000},ct,experimentalSplit:true,encoderThreads:4,splitSegments:4,splitWorkers:2,longestFirst:true);
    var reorderedWorkIds=await MarkerHashes(reorderedWorkDest);
    Check(Enumerable.SequenceEqual(ids,reorderedWorkIds),"Longest-first work order must not change frame order");
    Console.WriteLine("Longest-first work scheduling preserved chronological frame identities.");
    await MultiSplitScreen.RunAsync(resolved,root,ct);
    await KeyframePartitionScreen.RunAsync(resolved,root,ct);
    await AvcRandomAccessScreen.RunAsync(resolved,root,ct);
    var reversed=Path.Combine(root,"reversed frame IDs.mp4");
    await Run("-v","error","-nostdin","-n","-i",marker,"-vf","reverse","-c:v","libx264","-crf","0","-pix_fmt","yuv420p",reversed);
    await PacketTimeline.ValidateAsync(resolved.Ffprobe,marker,reversed,0,ct);
    var reversedIds=await MarkerHashes(reversed);
    Check(!Enumerable.SequenceEqual(ids,reversedIds),"Marker test failed to detect reordering");
    Console.WriteLine("PTS-preserving reordered content counterexample detected by marker test.");

    // Compare same codec, source, preset and encoder-thread budget, with all
    // validation included. Two repetitions in alternating order; no speed gate.
    for(int repeat=0;repeat<2;repeat++)
    {
        foreach(bool splitMode in repeat==0?new[]{false,true}:new[]{true,false})
        {
            var destPath=Path.Combine(root,$"timed-{repeat}-{splitMode}.mp4");
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var report=await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,longInput,destPath,splitPlan,ct,experimentalSplit:splitMode,encoderThreads:4);
            watch.Stop();
            Check(report.Total<=watch.Elapsed && report.Preparation+report.Encoding+report.Validation+report.Publication==report.Total,"Benchmark phase accounting");
            Console.WriteLine(FormattableString.Invariant($"ENCODE_PHASE rep={repeat} split={splitMode} prep={report.Preparation.TotalSeconds:F4} encode={report.Encoding.TotalSeconds:F4} output_probe={report.OutputProbe.TotalSeconds:F4} timeline={report.TimelineCheck.TotalSeconds:F4} audio_hash={report.AudioHashCheck.TotalSeconds:F4} decode={report.FullDecode.TotalSeconds:F4} publish={report.Publication.TotalSeconds:F4} total={report.Total.TotalSeconds:F4}"));
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
    var batchCancelA=Path.Combine(root,"batch canceled A.mp4");
    var batchCancelB=Path.Combine(root,"batch canceled B.mp4");
    using(var batchCancel=CancellationTokenSource.CreateLinkedTokenSource(ct))
    {
        var batchEvents=new System.Collections.Concurrent.ConcurrentQueue<BatchUpdate>();
        var batchTask=BatchEncoder.RunAsync(resolved,[new(longInput,batchCancelA,plan),new(longInput,batchCancelB,plan)],1,new BatchProgress(batchEvents.Enqueue),batchCancel.Token);
        bool started=false;
        while(!batchTask.IsCompleted)
        {
            if(Directory.EnumerateFiles(root,".vanta-*.mp4").Any()){started=true;batchCancel.Cancel();break;}
            await Task.Delay(10,ct);
        }
        var terminal=await batchTask;
        Check(terminal.Count==2&&terminal.All(x=>x.Stage=="Canceled"),"All canceled jobs receive terminal results");
        Check(started&&!File.Exists(batchCancelA)&&!File.Exists(batchCancelB)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Batch active/queued cancellation cleanup");
        Check(batchEvents.Count(x=>x.Stage=="Canceled")==2&&!batchEvents.Any(x=>x.Index==1&&x.Stage=="Encoding"),"Queued job started after cancel or lacks terminal notification");
    }
    Console.WriteLine("Batch active and queued cancellation passed.");
    Console.WriteLine("Windows FFmpeg integration checks passed.");
}
finally { Directory.Delete(root,true); }
sealed class BatchProgress(Action<BatchUpdate> report):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>report(value);}
sealed class ImmediateProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>{public void Report(EncodeProgress value)=>report(value);}
