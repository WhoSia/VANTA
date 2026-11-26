using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class PresetFrontierScreen
{
    private sealed record Trial(double Seconds,double Encoding,long Bytes,double Mean,double Worst,int Frames,string Sha);
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private static string Str(JsonElement e,string key)=>e.GetProperty(key).GetString()??throw new InvalidDataException("Missing "+key);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    internal static async Task RunAsync(string? manifest,string reportPath,CancellationToken ct)
    {
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
            Row(new{kind="environment",commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=ff.Output.Split('\n')[0],ffmpegSha=Hash(tools.Ffmpeg),ffprobeSha=Hash(tools.Ffprobe),cpuCount=Environment.ProcessorCount,origin=input is null?"synthetic":"public-derivative"});
            // Predeclare contrasting known public content, not selected by measured results.
            string[] ids=input is null?["motion-1080p60","mixed-1080p30","high-rate-1080p120"]:["waterfall-portrait","screen-recording","earth-1080p60"];
            (string Codec,string Preset)[] modes=input is null?[("libx265","medium"),("libx264","fast"),("libx264","veryfast"),("libx264","superfast"),("libx265","fast"),("libx265","veryfast"),("libx265","superfast")]:[("libx265","medium"),("libx264","fast"),("libx265","veryfast"),("libx264","medium"),("libx265","fast"),("libx264","veryfast")];
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
                    Check(string.Equals(Hash(source),Str(item,"fixture_sha256"),StringComparison.OrdinalIgnoreCase)&&string.Equals(Hash(raw),rawSha,StringComparison.OrdinalIgnoreCase),"Source identity");
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
                    var trials=new Dictionary<(string,string),Trial>();
                    foreach(var mode in rep==0?modes:modes.Reverse().ToArray())
                    {
                        string dest=Path.Combine(root,$"{id}-{rep}-{mode.Codec}-{mode.Preset}.mp4");
                        try
                        {
                            var plan=new Plan("Preset frontier",mode.Codec,m.Height,rate,0,mode.Preset,false);
                            // Same verification algorithm for every setting; isolate encoder choice.
                            var result=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,dest,plan,ct,encoderThreads:4);
                            await PacketTimeline.ValidateAsync(tools.Ffprobe,source,dest,m.AudioTracks,ct,compareAudioPayloads:true);
                            string sha=Hash(dest);
                            if(!qualityCache.TryGetValue(sha,out var q)){q=await PublicCorpusScreen.Quality(tools,source,dest,m.Fps,ct);qualityCache.Add(sha,q);}
                            Check(q.Frames==clock.Count&&Hash(source)==before,"Source or frame count changed");
                            trials[mode]=new(result.Total.TotalSeconds,result.Encoding.TotalSeconds,result.OutputBytes,q.Mean,q.WorstSecond,q.Frames,sha);
                            Row(new{kind="trial",id,rep,codec=mode.Codec,preset=mode.Preset,seconds=result.Total.TotalSeconds,encoding=result.Encoding.TotalSeconds,validation=result.Validation.TotalSeconds,bytes=result.OutputBytes,q.Mean,q.WorstSecond,q.P05,q.Frames,sha});
                        }
                        finally{if(File.Exists(dest))File.Delete(dest);}
                    }
                    var a=trials[("libx265","medium")];
                    foreach(var mode in modes.Skip(1))
                    {
                        var b=trials[mode];double delta=(double)b.Bytes/a.Bytes-1;
                        bool matched=a.Frames==b.Frames&&Math.Abs(delta)<=.05&&Math.Abs(b.Mean-a.Mean)<=.001&&Math.Abs(b.Worst-a.Worst)<=.005;
                        bool paretoNoWorse=b.Bytes<=a.Bytes&&b.Mean>=a.Mean&&b.Worst>=a.Worst&&b.Seconds<=a.Seconds;
                        Row(new{kind="pair",id,rep,codec=mode.Codec,preset=mode.Preset,matched,paretoNoWorse,byteDelta=delta,meanDelta=b.Mean-a.Mean,worstDelta=b.Worst-a.Worst,encodingReduction=1-b.Encoding/a.Encoding,timeReduction=1-b.Seconds/a.Seconds});
                    }
                }
                Check(Hash(source)==before&&(rawSha is null||string.Equals(Hash(raw),rawSha,StringComparison.OrdinalIgnoreCase)),"Source changed");
                if(input is null)File.Delete(source);
            }
            Row(new{kind="summary",fixtures=ids.Length,trials=ids.Length*modes.Length*2,pairs=ids.Length*(modes.Length-1)*2,interpretation="fixed-rate screen only; no automatic codec or preset promotion"});
        }
        finally{Directory.Delete(root,true);}
    }
}
