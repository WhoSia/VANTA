using System.Diagnostics;
using System.Security.Cryptography;
using Vanta.Core;

internal static class QuantizerScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static async Task RunAsync(Toolchain tools,string root,CancellationToken ct)
    {
        // Deterministic monotone curves, exact endpoint and plateau cases.
        for(int shape=0;shape<3;shape++)
        for(int k=0;k<=17;k++)
        {
            var curve=Enumerable.Range(18,17).Select(q=>new QualityObservation(q,shape switch
            {
                0=>1-(q-18)*.002,
                1=>1-Math.Pow((q-18)/16d,2)*.04,
                _=>1-((q-18)/4)*.008
            },100000-(q-18)*3000)).ToArray();
            double target=1-k*.003;
            var result=await QuantizerSearch.RunAsync(18,34,target,null,10,(q,_)=>Task.FromResult(curve[q-18]),ct);
            var oracle=curve.Where(x=>x.Quality>=target).OrderBy(x=>x.Bytes).FirstOrDefault();
            Check(result.Best==oracle&&result.Observations.Count<=10&&result.Observations.Select(x=>x.Quantizer).Distinct().Count()==result.Observations.Count,"Monotone search oracle mismatch");
        }
        var reversed=await QuantizerSearch.RunAsync(18,34,.99,null,5,(q,_)=>Task.FromResult(new QualityObservation(q,q==18?.98:1,50000+q)),ct);
        Check(reversed.ObservedReversal&&reversed.Observations.Count==5&&reversed.Best is not null,"Observed nonmonotone curve uses remaining budget");
        var infeasible=await QuantizerSearch.RunAsync(18,34,.99,1,5,(q,_)=>Task.FromResult(new QualityObservation(q,1,100)),ct);
        Check(infeasible.Best is null,"Measured byte budget may be infeasible");
        bool invalid=false;
        try{await QuantizerSearch.RunAsync(18,34,.99,null,5,(q,_)=>Task.FromResult(new QualityObservation(q,double.NaN,100)),ct);}
        catch(InvalidDataException){invalid=true;}
        Check(invalid,"Nonfinite measurement rejected");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();bool stopped=false;int probes=0;
            try{await QuantizerSearch.RunAsync(18,34,.99,null,5,(q,_)=>{probes++;return Task.FromResult(new QualityObservation(q,1,100));},canceled.Token);}
            catch(OperationCanceledException){stopped=true;}
            Check(stopped&&probes==0,"Canceled search cannot start probe");
        }
        Console.WriteLine("Quantizer search: 54 deterministic oracle cases, reversal, infeasible size, invalid and cancellation controls passed.");
        // Full verified Encoder first; then fill only unmeasured settings for
        // the exhaustive audit. Audit time is not attributed to adaptive search.
        string source=Path.Combine(root,"quantizer-source.mp4");
        var generated=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-n","-f","lavfi","-i","testsrc2=s=320x180:r=24:d=2","-an","-c:v","libx264","-preset","veryfast","-crf","18","-threads","2","-pix_fmt","yuv420p",source],ct);
        Check(generated.ExitCode==0&&string.IsNullOrWhiteSpace(generated.Error),"Quantizer source generation failed");
        var stamp=Hash(source);
        var plan=new Plan("Quantizer research","libx264",180,250000,0,"fast",false);
        var observed=new Dictionary<int,(QualityObservation Sample,double Seconds)>();
        async Task<QualityObservation> Measure(int q,CancellationToken token)
        {
            Check(!observed.ContainsKey(q),"Repeated physical probe");
            string path=Path.Combine(root,$"quantizer-{q}.mp4");
            var timer=Stopwatch.StartNew();
            try
            {
                var encoded=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,path,plan,token,encoderThreads:2,researchCrf:q);
                var quality=await PublicCorpusScreen.Quality(tools,source,path,24,token);
                Check(quality.Frames==48,"Quality frame count");
                var o=new QualityObservation(q,quality.Mean,encoded.OutputBytes);
                observed.Add(q,(o,timer.Elapsed.TotalSeconds));
                Console.WriteLine("QUANTIZER "+System.Text.Json.JsonSerializer.Serialize(new {q,quality=o.Quality,bytes=o.Bytes,seconds=observed[q].Seconds}));
                return o;
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        const double threshold=.99;
        var adaptive=await QuantizerSearch.RunAsync(18,34,threshold,null,8,Measure,ct);
        double adaptiveSeconds=adaptive.Observations.Sum(o=>observed[o.Quantizer].Seconds);
        foreach(int q in Enumerable.Range(18,17).Where(q=>!observed.ContainsKey(q)).ToArray())await Measure(q,ct);
        var best=observed.Values.Select(x=>x.Sample).Where(x=>x.Quality>=threshold).OrderBy(x=>x.Bytes).ThenByDescending(x=>x.Quality).ThenBy(x=>x.Quantizer).FirstOrDefault();
        Check(adaptive.Best is null||adaptive.Best.Quality>=threshold,"Measured target not satisfied");
        Check(observed.Count==17&&Hash(source)==stamp,"Quantizer audit incomplete or modified input");
        Console.WriteLine("QUANTIZER_SUMMARY "+System.Text.Json.JsonSerializer.Serialize(new {target=threshold,probes=adaptive.Observations.Select(x=>x.Quantizer),adaptive=adaptive.Best,oracle=best,match=adaptive.Best==best,adaptiveSeconds,gridSeconds=observed.Values.Sum(x=>x.Seconds),adaptive.ObservedReversal,adaptive.StopReason}));
    }
}
