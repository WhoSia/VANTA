using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class PublicCorpusScreen
{
    private static string Hash(string path)
    {
        using var file=File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    }
    private static string Str(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
    private static async Task<(int Frames,double Mean,double Minimum,double WorstSecond,double P05)> Quality(Toolchain tools,string source,string output,double fps,CancellationToken ct)
    {
        int frames=0, bucketCount=0;
        double sum=0,min=1,bucketSum=0,worst=1;
        int bucketSize=Math.Max(1,(int)Math.Round(fps));
        var scores=new List<double>();
        var r=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-i",source,"-i",output,
            "-filter_complex","[0:v][1:v]ssim=stats_file=-:shortest=1:repeatlast=0","-an","-f","null","-"],ct,line=>
        {
            var all=line.Split(' ',StringSplitOptions.RemoveEmptyEntries).SingleOrDefault(x=>x.StartsWith("All:",StringComparison.Ordinal));
            if(all is null||!double.TryParse(all.AsSpan(4),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value)||value is <0 or >1)
                throw new InvalidDataException("Missing SSIM frame score");
            if(scores.Count>=100_000)throw new InvalidDataException("Quality screen frame limit exceeded");
            scores.Add(value);
            frames++;sum+=value;min=Math.Min(min,value);bucketCount++;bucketSum+=value;
            if(bucketCount==bucketSize){worst=Math.Min(worst,bucketSum/bucketCount);bucketCount=0;bucketSum=0;}
        });
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error)||frames==0)throw new InvalidDataException("SSIM failed: "+r.Error);
        if(bucketCount>0)worst=Math.Min(worst,bucketSum/bucketCount);
        scores.Sort();
        double p05=scores[Math.Max(0,(int)Math.Ceiling(.05*frames)-1)];
        return(frames,sum/frames,min,worst,p05);
    }

    internal static async Task RunAsync(string manifest,string reportPath,CancellationToken ct)
    {
        var tools=await Toolchain.DiscoverAsync(ct);
        var ff=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
        var fp=await ProcessRunner.RunAsync(tools.Ffprobe,["-version"],ct);
        if(ff.ExitCode!=0||fp.ExitCode!=0)throw new InvalidDataException("Tool version unavailable");
        using var document=JsonDocument.Parse(await File.ReadAllTextAsync(manifest,ct));
        using var report=new StreamWriter(reportPath,false);
        report.AutoFlush=true;
        void Row(object data){var line=JsonSerializer.Serialize(data);report.WriteLine(line);Console.WriteLine("CORPUS "+line);}
        Row(new {kind="environment",ffmpeg=ff.Output.Split('\n')[0],ffprobe=fp.Output.Split('\n')[0],cpus=Environment.ProcessorCount,commit=Environment.GetEnvironmentVariable("GITHUB_SHA")});
        int ready=0,failed=0;
        foreach(var item in document.RootElement.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            string id=Str(item,"id"),origin=Str(item,"origin");
            if(Str(item,"status")!="ready"){Row(new{kind="intake-failure",id,origin,error=Str(item,"error")});failed++;continue;}
            ready++;
            string raw=Str(item,"raw"),source=Str(item,"fixture");
            try
            {
                if(Hash(raw)!=Str(item,"raw_sha256")||Hash(source)!=Str(item,"fixture_sha256"))throw new InvalidDataException("Corpus input hash mismatch");
                // Native compatibility is a separate observation; normalization cannot count as native support.
                try
                {
                    var native=await Probe.InspectAsync(tools.Ffprobe,raw,ct);
                    Row(new{kind="native-probe",id,origin,supported=native.Supported,reason=native.Reason,info=native.Info});
                    if(native.Supported&&native.Info.Seconds<=120&&Math.Max(native.Info.Width,native.Info.Height)<=1920)
                    {
                        string nativeOut=Path.Combine(Path.GetDirectoryName(source)!,id+"-native-output.mp4");
                        var m=native.Info;
                        long rate=(long)Math.Clamp((m.VideoStreamRate??m.Bytes*8d/m.Seconds)*.75,PlanRules.MinimumVideoRate,PlanRules.MaximumVideoRate);
                        try
                        {
                            var nativeReport=await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,raw,nativeOut,new("Native","libx264",m.Height,rate,0,"veryfast",false),ct,encoderThreads:4);
                            Row(new{kind="native-encode",id,origin,status="pass",seconds=nativeReport.Total.TotalSeconds,bytes=nativeReport.OutputBytes});
                        }
                        finally{if(File.Exists(nativeOut))File.Delete(nativeOut);}
                    }
                    else Row(new{kind="native-encode-skip",id,origin,reason=native.Supported?"Native screen limit: 120 seconds, maximum dimension 1920":native.Reason});
                }
                catch(Exception ex) when(ex is not OperationCanceledException)
                {Row(new{kind="native-error",id,origin,error=ex.Message});}
                var d=await Probe.InspectAsync(tools.Ffprobe,source,ct);
                if(!d.Supported)throw new InvalidDataException(d.Reason);
                var media=d.Info;
                var video=item.GetProperty("fixture_probe").GetProperty("streams").EnumerateArray().Single(s=>Str(s,"codec_type")=="video");
                int expectedFrames=int.Parse(video.GetProperty("nb_frames").ToString(),CultureInfo.InvariantCulture);
                if(expectedFrames<1||expectedFrames>100_000)throw new InvalidDataException("Invalid expected corpus frame count");
                var plan=new Plan("Corpus","libx264",media.Height,2_000_000,0,"medium",false);
                var trials=new Dictionary<string,(double Seconds,long Bytes,double Mean,double Worst,int Frames,double P05)>();
                string[] modes=media.AudioTracks>0?["continuous","aligned","continuous-auto","continuous-fast","continuous-fused"]:["continuous","aligned","continuous-auto","continuous-fast"];
                for(int rep=0;rep<2;rep++)
                {
                    foreach(string mode in rep==0?modes:modes.Reverse().ToArray())
                    {
                        string output=Path.Combine(Path.GetDirectoryName(source)!,id+"-"+rep+"-"+mode+".mp4");
                        try
                        {
                            var trialPlan=mode=="continuous-fast"?plan with {Preset="fast"}:plan;
                            int? threads=mode=="continuous-auto"?null:4;
                            var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,trialPlan,ct,experimentalSplit:mode=="aligned",encoderThreads:threads,splitSegments:4,splitWorkers:2,keyframeAligned:mode=="aligned",researchCrf:23,fusedAudioValidation:mode=="continuous-fused");
                            // Shadow oracle outside the timed candidate path: do not accept a weaker validator.
                            if(mode=="continuous-fused")await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
                            var q=await Quality(tools,source,output,media.Fps,ct);
                            if(q.Frames!=expectedFrames)throw new InvalidDataException("SSIM frame count differs from normalized source");
                            trials[mode]=(result.Total.TotalSeconds,result.OutputBytes,q.Mean,q.WorstSecond,q.Frames,q.P05);
                            Row(new{kind="trial",id,origin,rep,mode,width=media.Width,height=media.Height,fps=media.Fps,seconds=result.Total.TotalSeconds,encoding=result.Encoding.TotalSeconds,timeline=result.TimelineCheck.TotalSeconds,audio=result.AudioHashCheck.TotalSeconds,decode=result.FullDecode.TotalSeconds,bytes=result.OutputBytes,frames=q.Frames,ssim=q.Mean,minFrame=q.Minimum,worstSecond=q.WorstSecond,p05=q.P05,threads,preset=trialPlan.Preset});
                        }
                        finally{if(File.Exists(output))File.Delete(output);}
                    }
                    var a=trials["continuous"];
                    foreach(var candidate in modes.Skip(1))
                    {
                        var b=trials[candidate];
                        double byteDelta=(double)b.Bytes/a.Bytes-1;
                        bool matched=a.Frames==b.Frames&&Math.Abs(byteDelta)<=.05&&Math.Abs(a.Mean-b.Mean)<=.001&&Math.Abs(a.Worst-b.Worst)<=.005;
                        Row(new{kind="pair",id,origin,rep,baseline="continuous",candidate,matched,byteDelta,meanSsimDelta=b.Mean-a.Mean,worstSecondDelta=b.Worst-a.Worst,p05Delta=b.P05-a.P05,timeReduction=1-b.Seconds/a.Seconds});
                    }
                }
                if(Hash(raw)!=Str(item,"raw_sha256")||Hash(source)!=Str(item,"fixture_sha256"))throw new InvalidDataException("Corpus source modified");
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {failed++;Row(new{kind="fixture-error",id,origin,error=ex.Message});}
        }
        Row(new{kind="summary",ready,failed});
        if(ready==0||failed!=0)throw new InvalidDataException("Incomplete corpus screen; inspect JSONL");
    }
}
