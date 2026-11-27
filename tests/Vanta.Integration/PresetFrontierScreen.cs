using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class PresetFrontierScreen
{
    private sealed record Trial(double Seconds,double Encoding,long Bytes,double Mean,double Worst,int Frames,string Sha);
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private static string Str(JsonElement e,string key)=>e.GetProperty(key).GetString()??throw new InvalidDataException("Missing "+key);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    internal static async Task RunAsync(string? manifest,string reportPath,CancellationToken ct,string? publicFixture=null)
    {
        if(publicFixture is not null&&manifest is null)throw new ArgumentException("Explicit public fixture requires manifest");
        var tools=await Toolchain.DiscoverAsync(ct);
        using var input=manifest is null?null:JsonDocument.Parse(await File.ReadAllTextAsync(manifest,ct));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var report=new StreamWriter(reportPath,false){AutoFlush=true};
        string root=Path.Combine(Path.GetTempPath(),"VANTA-frontier-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Row(object x){string line=JsonSerializer.Serialize(x);report.WriteLine(line);Console.WriteLine("FRONTIER "+line);}
        try
        {
            var ff=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
            Check(ff.ExitCode==0,"Tool version");
            Row(new{kind="environment",commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=ff.Output.Split('\n')[0],ffmpegSha=Hash(tools.Ffmpeg),ffprobeSha=Hash(tools.Ffprobe),cpuCount=Environment.ProcessorCount,origin=input is null?"synthetic":"public-derivative",requestedFixture=publicFixture});
            // Predeclare contrasting known public content, not selected by measured results.
            string[] ids=input is null?["motion-1080p60","mixed-1080p30","high-rate-1080p120"]:["waterfall-portrait","screen-recording","earth-1080p60"];
            (string Codec,string Preset,int RateScale)[] modes=input is null?[("libx265","medium",10000),("libx264","fast",10000),("libx264","veryfast",10000),("libx264","superfast",10000),("libx265","fast",10000),("libx265","veryfast",10000),("libx265","superfast",10000)]:[("libx265","medium",10000),("libx264","fast",10000),("libx265","veryfast",10000),("libx264","medium",10000),("libx265","fast",10000),("libx264","veryfast",10000)];
            if(publicFixture is not null)
            {
                string[] allIds=["animation-bbb","waterfall-portrait","screen-recording","slow-motion-drops","earth-1080p60","earth-4k60"];
                Check(allIds.Contains(publicFixture),"Unknown public fixture");
                Check(input!.RootElement.ValueKind==JsonValueKind.Array&&input.RootElement.GetArrayLength()==1,"Exactly one public fixture required");
                Check(Str(input.RootElement[0],"id")==publicFixture,"Wrong public fixture");
                ids=[publicFixture];
                // Fixed before seeing these shard results, not an adaptive selector.
                // Keep the six original modes; test three additional rate ratios.
                modes=[..modes,("libx264","veryfast",5000),("libx264","veryfast",6875),("libx264","veryfast",8500)];
            }
            foreach(string id in ids)
            {
                string source,raw;
                string? rawSha=null;
                if(input is null)
                {
                    source=Path.Combine(root,id+".mp4");raw=source;
                    var (fps,duration)=id switch{"motion-1080p60"=>(60,8),"mixed-1080p30"=>(30,12),_=>(120,4)};
                    var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i",$"testsrc2=s=1920x1080:r={fps}:d={duration}","-f","lavfi","-i",$"sine=frequency=440:sample_rate=48000:duration={duration}","-map","0:v","-map","1:a"};
                    if(id=="mixed-1080p30")args.AddRange(["-vf","drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,6)"]);
                    args.AddRange(["-c:v","libx264","-preset","ultrafast","-crf","18","-threads","4","-pix_fmt","yuv420p","-c:a","aac","-b:a","96k",source]);
                    var made=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
                    Check(made.ExitCode==0&&string.IsNullOrWhiteSpace(made.Error),"Synthetic fixture: "+made.Error);
                }
                else
                {
                    var item=input.RootElement.EnumerateArray().Single(x=>Str(x,"id")==id);
                    Check(Str(item,"origin")=="public"&&Str(item,"status")=="ready","Incomplete pinned fixture");
                    source=Str(item,"fixture");raw=Str(item,"raw");rawSha=Str(item,"sha256");
                    Check(string.Equals(Hash(source),Str(item,"fixture_sha256"),StringComparison.OrdinalIgnoreCase)&&string.Equals(Hash(raw),rawSha,StringComparison.OrdinalIgnoreCase)&&string.Equals(Str(item,"raw_sha256"),rawSha,StringComparison.OrdinalIgnoreCase),"Source identity");
                }
                string before=Hash(source);
                var d=await Probe.InspectAsync(tools.Ffprobe,source,ct);Check(d.Supported,d.Reason);
                var m=d.Info;
                var clock=await PacketTimeline.CaptureSourceClockAsync(tools.Ffprobe,source,ct);
                long rate=input is null?8_000_000:(long)Math.Clamp((m.VideoStreamRate??m.Bytes*8d/m.Seconds)*.65,PlanRules.MinimumVideoRate,PlanRules.MaximumVideoRate);
                Row(new{kind="fixture",id,rawSha,sourceSha=before,m.Width,m.Height,m.Fps,m.Seconds,m.AudioTracks,rate,threads=4,frames=clock.Count});
                var qualityCache=new Dictionary<string,(int Frames,double Mean,double Minimum,double WorstSecond,double P05)>();
                for(int rep=0;rep<2;rep++)
                {
                    var trials=new Dictionary<(string,string,int),Trial>();
                    foreach(var mode in rep==0?modes:modes.Reverse().ToArray())
                    {
                        string dest=Path.Combine(root,$"{id}-{rep}-{mode.Codec}-{mode.Preset}-{mode.RateScale}.mp4");
                        try
                        {
                            long appliedRate=Math.Clamp(rate*mode.RateScale/10000,PlanRules.MinimumVideoRate,PlanRules.MaximumVideoRate);
                            var plan=new Plan("Preset frontier",mode.Codec,m.Height,appliedRate,0,mode.Preset,false);
                            // Same verification algorithm for every setting; isolate encoder choice.
                            var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,dest,plan,ct,encoderThreads:4);
                            await PacketTimeline.ValidateAsync(tools.Ffprobe,source,dest,m.AudioTracks,ct,compareAudioPayloads:true);
                            string sha=Hash(dest);
                            if(!qualityCache.TryGetValue(sha,out var q)){q=await PublicCorpusScreen.Quality(tools,source,dest,m.Fps,ct);qualityCache.Add(sha,q);}
                            Check(q.Frames==clock.Count&&Hash(source)==before,"Source or frame count changed");
                            trials[mode]=new(result.Total.TotalSeconds,result.Encoding.TotalSeconds,result.OutputBytes,q.Mean,q.WorstSecond,q.Frames,sha);
                            Row(new{kind="trial",id,rep,codec=mode.Codec,preset=mode.Preset,rateScale=mode.RateScale,appliedRate,seconds=result.Total.TotalSeconds,encoding=result.Encoding.TotalSeconds,validation=result.Validation.TotalSeconds,bytes=result.OutputBytes,q.Mean,q.WorstSecond,q.P05,q.Frames,sha});
                        }
                        finally{if(File.Exists(dest))File.Delete(dest);}
                    }
                    var a=trials[("libx265","medium",10000)];
                    foreach(var mode in modes.Skip(1))
                    {
                        var b=trials[mode];double delta=(double)b.Bytes/a.Bytes-1;
                        bool matched=a.Frames==b.Frames&&Math.Abs(delta)<=.05&&Math.Abs(b.Mean-a.Mean)<=.001&&Math.Abs(b.Worst-a.Worst)<=.005;
                        bool paretoNoWorse=b.Bytes<=a.Bytes&&b.Mean>=a.Mean&&b.Worst>=a.Worst&&b.Seconds<=a.Seconds;
                        Row(new{kind="pair",id,rep,codec=mode.Codec,preset=mode.Preset,rateScale=mode.RateScale,appliedRate=Math.Clamp(rate*mode.RateScale/10000,PlanRules.MinimumVideoRate,PlanRules.MaximumVideoRate),matched,paretoNoWorse,byteDelta=delta,meanDelta=b.Mean-a.Mean,worstDelta=b.Worst-a.Worst,encodingReduction=1-b.Encoding/a.Encoding,timeReduction=1-b.Seconds/a.Seconds});
                    }
                }
                Check(Hash(source)==before&&(rawSha is null||string.Equals(Hash(raw),rawSha,StringComparison.OrdinalIgnoreCase)),"Source changed");
                if(input is null)File.Delete(source);
            }
            Row(new{kind="summary",fixtures=ids.Length,trials=ids.Length*modes.Length*2,pairs=ids.Length*(modes.Length-1)*2,interpretation=publicFixture is null?"fixed-rate screen only; no automatic codec or preset promotion":"fixed predeclared rate grid on one public derivative; no automatic selection or aggregate acceptance"});
        }
        finally{Directory.Delete(root,true);}
    }
}
