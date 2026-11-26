using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class AccelerationCohortScreen
{
    private sealed record Fixture(string Id,int Width,int Height,string Rate,double Fps,int Seconds,int Tracks,string Codec,string Filter);
    private sealed record Trial(double Seconds,long Bytes,string Sha,int Frames,double Mean,double Worst,double P05);
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    private static string Str(JsonElement e,string key)=>e.GetProperty(key).GetString()??throw new InvalidDataException("Missing "+key);
    internal static async Task RunAsync(string reportPath,CancellationToken ct,string? publicManifest=null)
    {
        string root=Path.Combine(Path.GetTempPath(),"VANTA-cohort-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var writer=new StreamWriter(reportPath,false){AutoFlush=true};
        void Row(object value){string line=JsonSerializer.Serialize(value);writer.WriteLine(line);Console.WriteLine("COHORT "+line);}
        try
        {
            var tools=await Toolchain.DiscoverAsync(ct);
            var ff=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
            var fp=await ProcessRunner.RunAsync(tools.Ffprobe,["-version"],ct);
            Check(ff.ExitCode==0&&fp.ExitCode==0,"Tool versions");
            Row(new{kind="environment",commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=ff.Output.Split('\n')[0],ffprobe=fp.Output.Split('\n')[0],ffmpegImageBytes=new FileInfo(tools.Ffmpeg).Length,ffprobeImageBytes=new FileInfo(tools.Ffprobe).Length,ffmpegSha=Hash(tools.Ffmpeg),ffprobeSha=Hash(tools.Ffprobe),cpuCount=Environment.ProcessorCount,resourceScope="direct child only; launchers may hide descendants",pairsPerFixture=5,cohort=publicManifest is null?"synthetic":"public-confirmation"});
            Fixture[] fixtures=[
                new("ntsc-720p-two-audio",1280,720,"30000/1001",30000d/1001,20,2,"libx264","testsrc2"),
                new("mixed-1080p-hevc",1920,1080,"30",30,16,1,"libx265","testsrc2"),
                new("silent-4k",3840,2160,"30",30,4,0,"libx264","testsrc2")
            ];
            var records=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
            if(publicManifest is not null)
            {
                using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(publicManifest,ct));
                string[] ids=["animation-bbb","waterfall-portrait","screen-recording","slow-motion-drops","earth-1080p60","earth-4k60"];
                foreach(var item in manifest.RootElement.EnumerateArray())
                {
                    string id=Str(item,"id");
                    Check(ids.Contains(id)&&records.TryAdd(id,item.Clone()),"Unexpected or duplicate public fixture");
                    Check(Str(item,"origin")=="public"&&Str(item,"status")=="ready","Public acquisition incomplete: "+id);
                    Check(string.Equals(Hash(Str(item,"raw")),Str(item,"sha256"),StringComparison.OrdinalIgnoreCase)&&
                          string.Equals(Str(item,"raw_sha256"),Str(item,"sha256"),StringComparison.OrdinalIgnoreCase),"Pinned raw identity: "+id);
                    Check(string.Equals(Hash(Str(item,"fixture")),Str(item,"fixture_sha256"),StringComparison.OrdinalIgnoreCase),"Fixture identity: "+id);
                }
                Check(records.Count==ids.Length,"All six public fixtures required");
                var publicFixtures=new List<Fixture>();
                foreach(string id in ids)
                {
                    var m=(await Probe.InspectAsync(tools.Ffprobe,Str(records[id],"fixture"),ct)).Info;
                    publicFixtures.Add(new(id,m.Width,m.Height,"",m.Fps,(int)Math.Ceiling(m.Seconds),m.AudioTracks,
                        id is "screen-recording" or "slow-motion-drops"?"libx265":"libx264",""));
                }
                fixtures=publicFixtures.ToArray();
            }
            int completed=0,qualified=0;
            foreach(var f in fixtures)
            {
                ct.ThrowIfCancellationRequested();
                bool isPublic=publicManifest is not null;
                string source=isPublic?Str(records[f.Id],"fixture"):Path.Combine(root,f.Id+".mp4");
                if(!isPublic)
                {
                var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i",$"{f.Filter}=s={f.Width}x{f.Height}:r={f.Rate}:d={f.Seconds}"};
                for(int a=0;a<f.Tracks;a++)args.AddRange(["-f","lavfi","-i",$"sine=frequency={440+110*a}:sample_rate=48000:duration={f.Seconds}"]);
                args.AddRange(["-map","0:v"]);for(int a=1;a<=f.Tracks;a++)args.AddRange(["-map",$"{a}:a"]);
                if(f.Id=="mixed-1080p-hevc")args.AddRange(["-vf","drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,5)"]);
                args.AddRange(["-c:v","libx264","-preset","ultrafast","-crf","18","-threads","4","-g","60","-pix_fmt","yuv420p"]);
                if(f.Tracks>0)args.AddRange(["-c:a","aac","-b:a","96k"]);
                args.Add(source);
                var gen=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
                Check(gen.ExitCode==0&&string.IsNullOrWhiteSpace(gen.Error),"Fixture generation failed: "+gen.Error);
                }
                var details=await Probe.InspectAsync(tools.Ffprobe,source,ct);
                Check(details.Supported&&details.Info.AudioTracks==f.Tracks,"Fixture eligibility: "+details.Reason);
                var inputSha=Hash(source);
                var clock=await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,source,ct);
                long rate=(long)Math.Clamp((details.Info.VideoStreamRate??details.Info.Bytes*8d/details.Info.Seconds)*.65,PlanRules.MinimumVideoRate,PlanRules.MaximumVideoRate);
                var plan=new Plan("Cohort",f.Codec,f.Height,rate,0,"fast",false);
                Row(new{kind="fixture",id=f.Id,sourceSha=inputSha,sourceBytes=details.Info.Bytes,details.Info.Seconds,f.Width,f.Height,f.Fps,f.Tracks,f.Codec,expectedFrames=clock.Count,rate,threads=4,preset=plan.Preset,origin=isPublic?"normalized-public":"synthetic",contentGroup=f.Id.StartsWith("earth-")?"earth":f.Id,rawSha=isPublic?Str(records[f.Id],"raw_sha256"):null,ordinalClock=isPublic&&records[f.Id].TryGetProperty("fixture_clock",out var policy)&&policy.GetString()=="ordinal-cfr"});
                var memo=new Dictionary<string,(int Frames,double Mean,double Minimum,double WorstSecond,double P05)>();
                var reductions=new List<double>();int matched=0,exact=0;
                async Task<Trial> Run(bool candidate,int rep,bool traced)
                {
                    string output=Path.Combine(root,$"{f.Id}-{rep}-{candidate}-{traced}.mp4");
                    ProcessResourceTrace? trace=traced?ProcessResourceTrace.Begin():null;
                    try
                    {
                        var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:4,singleDecodeValidation:candidate,interleavedAudioValidation:candidate&&f.Tracks>0);
                        if(trace is not null)
                        {
                            var rows=trace.Snapshot();
                            Row(new{kind="resource",id=f.Id,candidate,seconds=result.Total.TotalSeconds,omitted=trace.Omitted,rows});
                            trace.Dispose();trace=null;
                        }
                        // All outputs receive the same independent oracle, outside timing.
                        await SingleDecodeScreen.ReferenceAsync(tools,source,output,f.Tracks,ct);
                        if(f.Tracks>0)await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
                        string sha=Hash(output);
                        bool hit=memo.TryGetValue(sha,out var q);
                        if(!hit){q=await PublicCorpusScreen.Quality(tools,source,output,details.Info.Fps,ct);memo.Add(sha,q);}
                        Check(q.Frames==clock.Count,"Independent decoded source count mismatch");
                        Check(Hash(source)==inputSha,"Source bytes changed");
                        Row(new{kind=traced?"traced-trial":"trial",id=f.Id,rep,candidate,seconds=result.Total.TotalSeconds,encoding=result.Encoding.TotalSeconds,validation=result.Validation.TotalSeconds,bytes=result.OutputBytes,sha,qualityCacheHit=hit,q.Frames,q.Mean,q.WorstSecond,q.P05,pending=result.InterleavedAudioPeakPending});
                        return new(result.Total.TotalSeconds,result.OutputBytes,sha,q.Frames,q.Mean,q.WorstSecond,q.P05);
                    }
                    finally{trace?.Dispose();if(File.Exists(output))File.Delete(output);}
                }
                for(int rep=0;rep<5;rep++)
                {
                    Trial a,b;
                    if(rep%2==0){a=await Run(false,rep,false);b=await Run(true,rep,false);}
                    else{b=await Run(true,rep,false);a=await Run(false,rep,false);}
                    double delta=(double)b.Bytes/a.Bytes-1, reduction=1-b.Seconds/a.Seconds;
                    bool same=a.Sha==b.Sha;
                    bool comparable=a.Frames==b.Frames&&Math.Abs(delta)<=.05&&Math.Abs(a.Mean-b.Mean)<=.001&&Math.Abs(a.Worst-b.Worst)<=.005;
                    if(same)exact++;if(comparable)matched++;
                    reductions.Add(reduction);
                    Row(new{kind="pair",id=f.Id,rep,matched=comparable,exact=same,byteDelta=delta,meanSsimDelta=b.Mean-a.Mean,worstSecondDelta=b.Worst-a.Worst,timeReduction=reduction});
                }
                double median=reductions.Order().ElementAt(2);
                bool screeningGate=matched==5&&median>=.15;
                if(screeningGate)qualified++;
                Row(new{kind="fixture-summary",id=f.Id,matched,exact,median,min=reductions.Min(),max=reductions.Max(),screeningGate,interpretation=isPublic?"previously observed public derivative confirmation, not unseen content or release authorization":"fixture-level synthetic screen only, not release authorization"});
                // Trace separately, not part of the five uninstrumented pairs.
                if(f.Tracks==2){await Run(false,5,true);await Run(true,5,true);}
                completed++;
                if(isPublic)
                {
                    Check(Hash(source)==inputSha&&string.Equals(Hash(Str(records[f.Id],"raw")),Str(records[f.Id],"sha256"),StringComparison.OrdinalIgnoreCase),"Public raw/fixture changed");
                }
                else File.Delete(source);
            }
            Row(new{kind="summary",completed,qualified,required=fixtures.Length,interpretation="completion tests evidence integrity, not promotion"});
            Check(completed==fixtures.Length&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Incomplete cohort or scratch leak");
        }
        finally{Directory.Delete(root,true);}
    }
}
