using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class HeavyFactorialScreen
{
    private sealed record Fixture(int Width,int Height,int Fps,int Seconds,int Tracks,long Rate,string? Filter);
    private sealed record Lane(string Codec,string Preset,bool Combined);
    private sealed record Trial(double Total,double Encoding,double Validation,long Bytes,string Sha,int Frames,double Mean,double Worst,bool Fallback);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    private static async Task<string> Hash(string path,CancellationToken ct)
    {
        using var file=File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(file,ct));
    }
    internal static async Task RunAsync(string id,string reportPath,CancellationToken ct)
    {
        Fixture f=id switch
        {
            "mixed-1080p30"=>new(1920,1080,30,60,2,8_000_000,"drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=between(t\\,20\\,39.999)"),
            "motion-1080p60"=>new(1920,1080,60,24,1,8_000_000,null),
            "texture-4k30"=>new(3840,2160,30,12,0,16_000_000,"noise=alls=5:allf=t+u:all_seed=73"),
            _=>throw new ArgumentException("Unknown heavy fixture")
        };
        Lane[] lanes=[new("libx265","medium",false),new("libx265","medium",true),new("libx264","veryfast",false),new("libx264","veryfast",true)];
        // Five paired repetitions, rotate then reverse. Same fixture, fixed settings.
        int[][] order=[[0,1,3,2],[1,2,0,3],[2,3,1,0],[3,0,2,1],[2,3,1,0]];
        var root=Path.Combine(Path.GetTempPath(),"VANTA-heavy-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var writer=new StreamWriter(reportPath,false){AutoFlush=true};
        void Row(object value){var line=JsonSerializer.Serialize(value);writer.WriteLine(line);Console.WriteLine("HEAVY "+line);}
        try
        {
            var tools=await Toolchain.DiscoverAsync(ct);
            var version=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
            Check(version.ExitCode==0,"Tool version");
            Row(new{kind="environment",id,commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=version.Output.Split('\n')[0],ffmpegSha=await Hash(tools.Ffmpeg,ct),ffprobeSha=await Hash(tools.Ffprobe,ct),cpuCount=Environment.ProcessorCount,threads=4,repetitions=5,scope="synthetic physical stored bytes, no CBR filler, not independent natural content"});
            string source=Path.Combine(root,"source.mp4");
            var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i",$"testsrc2=s={f.Width}x{f.Height}:r={f.Fps}:d={f.Seconds}"};
            for(int a=0;a<f.Tracks;a++)args.AddRange(["-f","lavfi","-i",$"sine=frequency={440+110*a}:sample_rate=48000:duration={f.Seconds}"]);
            args.AddRange(["-map","0:v"]);for(int a=1;a<=f.Tracks;a++)args.AddRange(["-map",$"{a}:a"]);
            if(f.Filter is not null)args.AddRange(["-vf",f.Filter]);
            args.AddRange(["-c:v","libx264","-preset","ultrafast","-crf","18","-threads","4","-pix_fmt","yuv420p","-g","60"]);
            if(f.Tracks>0)args.AddRange(["-c:a","aac","-b:a","128k"]);
            args.Add(source);
            var gen=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
            Check(gen.ExitCode==0&&string.IsNullOrWhiteSpace(gen.Error),"Fixture generation failed: "+gen.Error);
            var details=await Probe.InspectAsync(tools.Ffprobe,source,ct);
            Check(details.Supported&&details.Info.Width==f.Width&&details.Info.Height==f.Height&&details.Info.AudioTracks==f.Tracks,"Source eligibility: "+details.Reason);
            var stamp=SourceStamp.Capture(source);var hash=await Hash(source,ct);
            int frames=f.Seconds*f.Fps;
            Row(new{kind="fixture",id,f.Width,f.Height,f.Fps,f.Seconds,f.Tracks,f.Rate,frames,sourceBytes=stamp.Bytes,sourceSha=hash,storageFiller=false});
            var quality=new Dictionary<string,(int Frames,double Mean,double Minimum,double WorstSecond,double P05)>();
            var reductions=new List<double>[2]{new(),new()};
            int[] exact=[0,0],matched=[0,0];int macroMatched=0,macroWins=0,completed=0;
            for(int rep=0;rep<5;rep++)
            {
                var trials=new Trial[4];
                foreach(int n in order[rep])
                {
                    var lane=lanes[n];
                    var plan=new Plan("Heavy factorial",lane.Codec,f.Height,f.Rate,0,lane.Preset,false);
                    string output=Path.Combine(root,$"{rep}-{n}.mp4");
                    try
                    {
                        var r=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:4,expectedSource:stamp,
                            singleDecodeValidation:lane.Combined,interleavedAudioValidation:lane.Combined&&f.Tracks>0,interleavedFallback:lane.Combined&&f.Tracks>0);
                        await SingleDecodeScreen.ReferenceAsync(tools,source,output,f.Tracks,ct);
                        if(f.Tracks>0)await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
                        string sha=await Hash(output,ct);
                        bool cached=quality.TryGetValue(sha,out var q);
                        if(!cached){q=await PublicCorpusScreen.Quality(tools,source,output,details.Info.Fps,ct);quality.Add(sha,q);}
                        Check(q.Frames==frames&&await Hash(source,ct)==hash,"Independent frame count or source identity");
                        stamp.RequireUnchanged(source);
                        var t=new Trial(r.Total.TotalSeconds,r.Encoding.TotalSeconds,r.Validation.TotalSeconds,r.OutputBytes,sha,q.Frames,q.Mean,q.WorstSecond,r.InterleavedAudioFallbackUsed);
                        trials[n]=t;completed++;
                        Row(new{kind="trial",id,rep,lane=n,lane.Codec,lane.Preset,lane.Combined,result=t,qualityCacheHit=cached});
                    }
                    catch(Exception ex){Row(new{kind="failure",id,rep,lane=n,type=ex.GetType().Name,error=ex.Message});throw;}
                    finally{if(File.Exists(output))File.Delete(output);}
                }
                bool Match(Trial a,Trial b)=>a.Frames==b.Frames&&Math.Abs((double)b.Bytes/a.Bytes-1)<=.05&&Math.Abs(b.Mean-a.Mean)<=.001&&Math.Abs(b.Worst-a.Worst)<=.005;
                void Pair(string label,int ai,int bi)
                {
                    Trial a=trials[ai],b=trials[bi];bool comparable=Match(a,b),same=a.Sha==b.Sha;double reduction=1-b.Total/a.Total;
                    Row(new{kind="pair",id,rep,label,anchor=ai,candidate=bi,matched=comparable,exact=same,timeReduction=reduction,validationReduction=1-b.Validation/a.Validation,byteDelta=(double)b.Bytes/a.Bytes-1,meanDelta=b.Mean-a.Mean,worstDelta=b.Worst-a.Worst});
                    if(ai==0&&bi==1||ai==2&&bi==3)
                    {
                        int group=ai/2;reductions[group].Add(reduction);
                        if(same)exact[group]++;if(comparable)matched[group]++;
                    }
                    if(ai==0&&bi==3){if(comparable)macroMatched++;if(comparable&&reduction>=.15)macroWins++;}
                }
                Pair("hevc-validation",0,1);Pair("avc-validation",2,3);
                Pair("codec-baseline",0,2);Pair("codec-combined",1,3);Pair("macro-plus-validation",0,3);
            }
            for(int g=0;g<2;g++)
            {
                double median=reductions[g].Order().ElementAt(2);
                Row(new{kind="validation-summary",id,codec=lanes[g*2].Codec,exact=exact[g],matched=matched[g],median,min=reductions[g].Min(),max=reductions[g].Max(),screeningGate=exact[g]==5&&matched[g]==5&&median>=.15});
            }
            Row(new{kind="summary",id,completed,macroMatched,macroWins,interpretation="Synthetic fixture gate only; no default or release authorization; no aggregate CPU/RAM claim"});
            Check(completed==20&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Incomplete heavy cohort or leaked staging");
        }
        finally{Directory.Delete(root,true);}
    }
}
