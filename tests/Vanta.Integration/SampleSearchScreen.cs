using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class SampleSearchScreen
{
    private const int Fps=24,Seconds=12;
    private const double MeanFloor=.99,WorstFloor=.985;
    private static readonly int[] Grid=[18,26,34];
    private sealed record Measurement(int Q,long Bytes,double Mean,double Worst,int Frames,double Seconds,double Encoding);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    private static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
    private static async Task<string> Run(Toolchain tools,CancellationToken ct,params string[] args)
    {
        var r=await ProcessRunner.RunAsync(tools.Ffmpeg,args,ct);
        Check(r.ExitCode==0&&string.IsNullOrWhiteSpace(r.Error),"Sampling process: "+r.Error);
        return r.Output;
    }
    private static bool Feasible(Measurement x)=>x.Mean>=MeanFloor&&x.Worst>=WorstFloor;
    private static Measurement? Best(IEnumerable<Measurement> rows)=>rows.Where(Feasible).OrderBy(x=>x.Bytes).ThenBy(x=>x.Q).FirstOrDefault();
    private static string SelectFilter(int[] windows)
    {
        Check(windows.Length==3&&windows.Distinct().Count()==3&&windows.All(w=>w>=0&&w<Seconds),"Sample bounds");
        return string.Join("+",windows.Order().Select(w=>$"between(n\\,{w*Fps}\\,{(w+1)*Fps-1})"));
    }
    private static async Task<Measurement> Measure(Toolchain tools,string src,string dest,int q,int expected,bool grouped,CancellationToken ct)
    {
        var sw=Stopwatch.StartNew();
        try
        {
            double encoding;
            if(grouped)
            {
                await Run(tools,ct,"-v","error","-xerror","-nostdin","-n","-i",src,"-map","0:v:0","-an","-c:v","libx264","-preset","fast","-threads","2","-crf",q.ToString(CultureInfo.InvariantCulture),"-g","24","-keyint_min","24","-sc_threshold","0","-fps_mode","passthrough",dest);
                encoding=sw.Elapsed.TotalSeconds;
                await PacketTimeline.ValidateAsync(tools.Ffprobe,src,dest,0,ct);
                await Run(tools,ct,"-v","error","-xerror","-nostdin","-i",dest,"-map","0:v:0","-f","null","-");
            }
            else
            {
                var report=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,src,dest,new("Sample audit","libx264",180,250000,0,"fast",false),ct,encoderThreads:2,researchCrf:q);
                encoding=report.Encoding.TotalSeconds;
                await SingleDecodeScreen.ReferenceAsync(tools,src,dest,0,ct);
            }
            var metric=await PublicCorpusScreen.Quality(tools,src,dest,Fps,ct);
            Check(metric.Frames==expected,"Sample/audit frame count");
            return new(q,new FileInfo(dest).Length,metric.Mean,metric.WorstSecond,metric.Frames,sw.Elapsed.TotalSeconds,encoding);
        }
        finally{if(File.Exists(dest))File.Delete(dest);}
    }
    private static async Task PolicyControls(CancellationToken ct)
    {
        byte[] data=new byte[TemporalProbePolicy.FrameBytes*TemporalProbePolicy.Rate*3];
        int start=TemporalProbePolicy.FrameBytes*TemporalProbePolicy.Rate;
        for(int i=start;i<start*2;i++)data[i]=(byte)(i%2==0?0:255);
        using(var raw=new MemoryStream(data))
        {
            var f=await TemporalProbePolicy.ReadAsync(raw,3,ct);
            Check(f[0].Score==0&&f[1].Score>f[2].Score&&TemporalProbePolicy.Select(f).SequenceEqual(new[]{0,1,2}),"Activity feature control");
        }
        var ties=Enumerable.Range(0,12).Select(i=>new TemporalProbePolicy.Activity(i,0,0)).ToArray();
        Check(TemporalProbePolicy.Select(ties).SequenceEqual(new[]{0,6,11}),"Deterministic tie rule");
        bool rejected=false;
        try{TemporalProbePolicy.Select([new(0,0,0),new(1,double.NaN,0),new(2,0,0)]);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Nonfinite feature rejected");
        using(var shortRaw=new MemoryStream(data[..^1]))
        {
            rejected=false;try{await TemporalProbePolicy.ReadAsync(shortRaw,3,ct);}catch(EndOfStreamException){rejected=true;}
            Check(rejected,"Truncated raw samples rejected");
        }
        using(var tooLong=new MemoryStream([..data,0]))
        {
            rejected=false;try{await TemporalProbePolicy.ReadAsync(tooLong,3,ct);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Additional raw samples rejected");
        }
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        using var canceledRaw=new MemoryStream(data);
        rejected=false;try{await TemporalProbePolicy.ReadAsync(canceledRaw,3,cancel.Token);}catch(OperationCanceledException){rejected=true;}
        Check(rejected,"Canceled feature extraction");
    }
    internal static async Task RunAsync(string reportPath,CancellationToken ct)
    {
        await PolicyControls(ct);
        var tools=await Toolchain.DiscoverAsync(ct);
        string root=Path.Combine(Path.GetTempPath(),"VANTA-sample-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        using var writer=new StreamWriter(reportPath,false){AutoFlush=true};
        void Row(object value){string line=JsonSerializer.Serialize(value);writer.WriteLine(line);Console.WriteLine("SAMPLE_SEARCH "+line);}
        try
        {
            var version=await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],ct);
            Check(version.ExitCode==0,"Tool version");
            Row(new{kind="environment",commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),ffmpeg=version.Output.Split('\n')[0],grid=Grid,MeanFloor,WorstFloor,scope="synthetic CFR silent, no release acceptance"});
            foreach(var (id,filter) in new (string,string?)[]
                {("motion",null),("rare","drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=not(between(t\\,3\\,4.99))"),("mixed","drawbox=x=0:y=0:w=iw:h=ih:color=gray:t=fill:enable=lt(t\\,6)")})
            {
                string source=Path.Combine(root,id+".mp4"),raw=Path.Combine(root,"features.raw");
                var args=new List<string>{"-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=24:d=12"};
                if(filter is not null)args.AddRange(["-vf",filter]);
                args.AddRange(["-an","-c:v","libx264","-preset","veryfast","-crf","18","-threads","2","-pix_fmt","yuv420p",source]);
                await Run(tools,ct,args.ToArray());
                string before=Hash(source);
                var sw=Stopwatch.StartNew();
                await Run(tools,ct,"-v","error","-xerror","-nostdin","-n","-i",source,"-map","0:v:0","-vf","fps=4,scale=64:36:flags=area,format=gray","-an","-f","rawvideo",raw);
                TemporalProbePolicy.Activity[] features;
                using(var file=File.OpenRead(raw))features=await TemporalProbePolicy.ReadAsync(file,Seconds,ct);
                double featureSeconds=sw.Elapsed.TotalSeconds;File.Delete(raw);
                var full=new List<Measurement>();
                foreach(int q in Grid)full.Add(await Measure(tools,source,Path.Combine(root,"audit.mp4"),q,Fps*Seconds,false,ct));
                var oracle=Best(full);
                Row(new{kind="audit",id,sourceSha=before,features,featureSeconds,full,oracle,gridSeconds=full.Sum(x=>x.Seconds)});
                foreach(var (method,windows) in new[]{("uniform",new[]{1,5,9}),("activity",TemporalProbePolicy.Select(features))})
                {
                    string sample=Path.Combine(root,"sample.mp4");
                    sw.Restart();
                    string selection=SelectFilter(windows);
                    await Run(tools,ct,"-v","error","-xerror","-nostdin","-n","-i",source,"-map","0:v:0","-vf",$"select={selection},setpts=N/({Fps}*TB)","-an","-c:v","libx264","-preset","ultrafast","-qp","0","-threads","2","-pix_fmt","yuv420p",sample);
                    // Independently compare selected decoded pixel bytes with lossless sample.
                    string expected=await Run(tools,ct,"-v","error","-xerror","-nostdin","-i",source,"-map","0:v:0","-vf",$"select={selection}","-fps_mode","passthrough","-an","-c:v","rawvideo","-f","hash","-hash","sha256","-");
                    string observed=await Run(tools,ct,"-v","error","-xerror","-nostdin","-i",sample,"-map","0:v:0","-an","-c:v","rawvideo","-f","hash","-hash","sha256","-");
                    Check(expected.Trim()==observed.Trim()&&expected.Trim().StartsWith("SHA256=",StringComparison.OrdinalIgnoreCase),"Lossless sample pixel identity");
                    double extraction=sw.Elapsed.TotalSeconds;
                    var measured=new List<Measurement>();
                    foreach(int q in Grid)measured.Add(await Measure(tools,sample,Path.Combine(root,"sample-probe.mp4"),q,Fps*3,true,ct));
                    var selected=Best(measured);
                    var audited=selected is null?null:full.Single(x=>x.Q==selected.Q);
                    double estimate=(method=="activity"?featureSeconds:0)+extraction+measured.Sum(x=>x.Seconds);
                    Row(new{kind="sample",id,method,windows,extraction,measured,selected,audited,feasible=audited is not null&&Feasible(audited),oracleMatch=audited is not null&&oracle is not null&&audited.Q==oracle.Q,searchSeconds=estimate,searchPlusFinalSeconds=estimate+(audited?.Seconds??0),costScope="replay with measured full audit; not independently timed live search"});
                    File.Delete(sample);
                }
                Check(Hash(source)==before,"Sample source changed");File.Delete(source);
            }
            Row(new{kind="summary",completed=3,interpretation="research screen only; failed sample quality or speed is evidence, not a CI infrastructure failure"});
        }
        finally{Directory.Delete(root,true);}
    }
}
