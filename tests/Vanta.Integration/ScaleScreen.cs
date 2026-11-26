using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class ScaleScreen
{
    private sealed record Fixture(string Id,int Width,int Height,string Rate,int Seconds,int ExpectedFrames,int Tracks,long Cbr,long MinimumBytes,bool Vfr=false);
    private sealed record Trial(long Bytes,string Sha,double Total,double Encoding,double Validation,int Frames,double Mean,double Worst);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    private static async Task<string> Hash(string path,CancellationToken ct)
    {using var f=File.OpenRead(path);return Convert.ToHexString(await SHA256.HashDataAsync(f,ct));}
    private static async Task<int> Count(Toolchain tools,string path,CancellationToken ct)
    {
        var r=await ProcessRunner.RunAsync(tools.Ffprobe,["-v","error","-select_streams","v:0","-count_frames","-show_entries","stream=nb_read_frames","-of","csv=p=0",path],ct);
        if(r.ExitCode!=0||!string.IsNullOrWhiteSpace(r.Error)||!int.TryParse(r.Output.Trim(),out int n)||n<1)throw new InvalidDataException("Decoded frame count unavailable");
        return n;
    }
    private static async Task Generate(Toolchain tools,Fixture f,string path,CancellationToken ct)
    {
        var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i",$"testsrc2=s={f.Width}x{f.Height}:r={f.Rate}:d={f.Seconds}"};
        for(int a=0;a<f.Tracks;a++)args.AddRange(["-f","lavfi","-i",$"sine=frequency={440+110*a}:sample_rate=48000:duration={f.Seconds}"]);
        args.AddRange(["-map","0:v"]);for(int a=1;a<=f.Tracks;a++)args.AddRange(["-map",$"{a}:a"]);
        if(f.Vfr)args.AddRange(["-vf","select=not(mod(n\\,3))+not(mod(n\\,5))"]);
        args.AddRange(["-fps_mode","passthrough","-c:v","libx264","-preset","ultrafast","-threads","4","-pix_fmt","yuv420p"]);
        if(f.Cbr>0)
        {
            string rate=f.Cbr.ToString(System.Globalization.CultureInfo.InvariantCulture);
            args.AddRange(["-b:v",rate,"-minrate",rate,"-maxrate",rate,"-bufsize",rate,"-x264-params","nal-hrd=cbr:filler=1"]);
        }
        else args.AddRange(["-crf","18"]);
        if(f.Tracks>0)args.AddRange(["-c:a","aac","-b:a","96k"]);
        args.Add(path);
        var r=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
        Check(r.ExitCode==0&&string.IsNullOrWhiteSpace(r.Error),"Scale fixture generation failed: "+r.Error);
        Check(new FileInfo(path).Length>=f.MinimumBytes,"Physical file below declared size threshold");
    }
    private static async Task<Trial> Encode(Toolchain tools,string source,string output,MediaInfo m,Plan plan,bool optimized,int frames,CancellationToken ct,Dictionary<string,(int Frames,double Mean,double Minimum,double WorstSecond,double P05)> cache)
    {
        try
        {
            EncodeReport r=optimized?await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:4,singleDecodeValidation:true,interleavedAudioValidation:m.AudioTracks>0):
                await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:4);
            await SingleDecodeScreen.ReferenceAsync(tools,source,output,m.AudioTracks,ct);
            if(m.AudioTracks>0)await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
            var sha=await Hash(output,ct);
            if(!cache.TryGetValue(sha,out var quality)){quality=await PublicCorpusScreen.Quality(tools,source,output,m.Fps,ct);cache.Add(sha,quality);}
            Check(quality.Frames==frames,"Scale independent frame count");
            return new(r.OutputBytes,sha,r.Total.TotalSeconds,r.Encoding.TotalSeconds,r.Validation.TotalSeconds,frames,quality.Mean,quality.WorstSecond);
        }
        finally{if(File.Exists(output))File.Delete(output);}
    }
    internal static async Task RunAsync(string mode,string reportPath,CancellationToken ct)
    {
        Fixture[] fixtures=mode switch
        {
            "rates"=>[
                new("23.976",1920,1080,"24000/1001",1,24,1,0,1),
                new("24",1920,1080,"24",1,24,1,0,1),
                new("25",1920,1080,"25",1,25,1,0,1),
                new("29.97",1920,1080,"30000/1001",1,30,1,0,1),
                new("30",1920,1080,"30",1,30,1,0,1),
                new("50",1920,1080,"50",1,50,1,0,1),
                new("59.94",1920,1080,"60000/1001",1,60,1,0,1),
                new("60",1920,1080,"60",1,60,1,0,1),
                new("119.88",1920,1080,"120000/1001",1,120,1,0,1),
                new("120",1920,1080,"120",1,120,1,0,1),
                new("VFR",1920,1080,"120",1,56,1,0,1,true),
                new("portrait-60",1080,1920,"60",2,120,2,0,1),
                new("1440p-30",2560,1440,"30",2,60,0,0,1),
                new("4k-60",3840,2160,"60",2,120,1,0,1)],
            "physical"=>[
                new("1080p60-300MB",1920,1080,"60",120,7200,2,24_000_000,300_000_000),
                new("1080p30-1GB",1920,1080,"30",240,7200,1,40_000_000,1_000_000_000)],
            _=>throw new ArgumentException("Unknown scale mode")
        };
        var tools=await Toolchain.DiscoverAsync(ct);
        string root=Path.Combine(Path.GetTempPath(),"VANTA-scale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var report=new StreamWriter(reportPath,false){AutoFlush=true};
        void Row(object x){string line=JsonSerializer.Serialize(x);report.WriteLine(line);Console.WriteLine("SCALE "+line);}
        try
        {
            var version=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
            Check(version.ExitCode==0,"Tool version");
            Row(new{kind="environment",mode,commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=version.Output.Split('\n')[0],cpus=Environment.ProcessorCount,storageStress=mode=="physical"?"CBR filler, actual stored bytes; not representative natural footage":"short frame-rate coverage"});
            int completed=0;
            foreach(var f in fixtures)
            {
                string source=Path.Combine(root,f.Id+".mp4");
                await Generate(tools,f,source,ct);
                var before=await Hash(source,ct);var stamp=SourceStamp.Capture(source);
                var d=await Probe.InspectAsync(tools.Ffprobe,source,ct);
                Check(d.Supported&&d.Info.Width==f.Width&&d.Info.Height==f.Height&&d.Info.AudioTracks==f.Tracks,"Scale source: "+d.Reason);
                Check(stamp.Bytes==d.Info.Bytes&&stamp.Bytes>=f.MinimumBytes&&await Count(tools,source,ct)==f.ExpectedFrames,"Scale source count/64-bit length");
                var plan=new Plan("Scale","libx264",f.Height,8_000_000,0,"veryfast",false);
                PlanRules.Validate(plan,d.Info);
                var cache=new Dictionary<string,(int Frames,double Mean,double Minimum,double WorstSecond,double P05)>();
                Row(new{kind="fixture",f.Id,f.Width,f.Height,f.Rate,f.Seconds,f.ExpectedFrames,f.Tracks,f.Vfr,sourceBytes=stamp.Bytes,sourceSha=before,rate=plan.VideoBitsPerSecond,storageFiller=f.Cbr>0});
                Trial a,b;
                if(completed%2==0)
                {a=await Encode(tools,source,Path.Combine(root,"baseline.mp4"),d.Info,plan,false,f.ExpectedFrames,ct,cache);b=await Encode(tools,source,Path.Combine(root,"candidate.mp4"),d.Info,plan,true,f.ExpectedFrames,ct,cache);}
                else
                {b=await Encode(tools,source,Path.Combine(root,"candidate.mp4"),d.Info,plan,true,f.ExpectedFrames,ct,cache);a=await Encode(tools,source,Path.Combine(root,"baseline.mp4"),d.Info,plan,false,f.ExpectedFrames,ct,cache);}
                double delta=(double)b.Bytes/a.Bytes-1;bool matched=a.Frames==b.Frames&&Math.Abs(delta)<=.05&&Math.Abs(a.Mean-b.Mean)<=.001&&Math.Abs(a.Worst-b.Worst)<=.005;
                Row(new{kind="pair",f.Id,baseline=a,candidate=b,exact=a.Sha==b.Sha,matched,timeReduction=1-b.Total/a.Total});
                Check(matched&&await Hash(source,ct)==before,"Scale candidate integrity or quality mismatch");stamp.RequireUnchanged(source);
                completed++;File.Delete(source);
            }
            Row(new{kind="summary",completed,required=fixtures.Length,scope="one pair per fixture; coverage not speed acceptance"});
            Check(completed==fixtures.Length&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Scale incomplete or scratch leak");
        }
        finally{Directory.Delete(root,true);}
    }
    internal static async Task RunOffsetsAsync(string[] sources,string reportPath,CancellationToken ct)
    {
        Check(sources.Length==3,"Three offset fixtures required");
        var tools=await Toolchain.DiscoverAsync(ct);
        string root=Path.Combine(Path.GetTempPath(),"VANTA-offset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var report=new StreamWriter(reportPath,false){AutoFlush=true};
        void Row(object x){string line=JsonSerializer.Serialize(x);report.WriteLine(line);Console.WriteLine("SCALE_OFFSET "+line);}
        try
        {
            string? baselineSha=null;
            for(int i=0;i<sources.Length;i++)
            {
                string source=Path.GetFullPath(sources[i]);var stamp=SourceStamp.Capture(source);
                Check(i==0?stamp.Bytes<64*1024*1024:stamp.Bytes>(i==1?5L:20L)*1024*1024*1024,"64-bit logical file size");
                string before=await Hash(source,ct);
                var d=await Probe.InspectAsync(tools.Ffprobe,source,ct);
                Check(d.Supported&&d.Info.Bytes==stamp.Bytes&&d.Info.AudioTracks==1,"Offset probe");
                int frames=await Count(tools,source,ct);Check(frames==120,"Offset frame count");
                var plan=new Plan("Offset","libx264",d.Info.Height,700000,0,"fast",false);
                string output=Path.Combine(root,$"offset-{i}.mp4");
                // Ordinary production path on every input, no snapshot size cap.
                var r=await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:2,expectedSource:stamp);
                await SingleDecodeScreen.ReferenceAsync(tools,source,output,1,ct);
                await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
                var sha=await Hash(output,ct);baselineSha??=sha;
                Check(sha==baselineSha&&await Hash(source,ct)==before,"Offset output or source identity");
                stamp.RequireUnchanged(source);
                Row(new{kind="offset",index=i,logicalBytes=stamp.Bytes,sourceSha=before,outputSha=sha,frames,seconds=r.Total.TotalSeconds,scope="sparse media offsets, not physical payload throughput"});
                File.Delete(output);
            }
            Row(new{kind="summary",completed=3});
        }
        finally{Directory.Delete(root,true);}
    }
}
