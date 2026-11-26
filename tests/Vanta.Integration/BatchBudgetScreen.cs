using System.Diagnostics;
using System.Security.Cryptography;
using Vanta.Core;

internal static class BatchBudgetScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private sealed class IgnoreProgress:IProgress<BatchUpdate>{public void Report(BatchUpdate value){}}
    private sealed class CallbackProgress(Action<BatchUpdate> action):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>action(value);}
    internal static async Task RunAsync(Toolchain tools,string root,string[] sources,CancellationToken ct)
    {
        var budget=new BatchThreadAllocator(5,2,3);
        using var a=budget.Acquire();using var b=budget.Acquire();
        Check(a.Count==2&&b.Count==3,"Odd request budget is fully allocated");
        bool denied=false;try{using var extra=budget.Acquire();}catch(InvalidOperationException){denied=true;}
        Check(denied,"No third active lease");
        a.Dispose();a.Dispose();
        using var c=budget.Acquire();
        Check(c.Count==2,"Released request budget available while peer active");
        b.Dispose();c.Dispose();
        denied=false;try{using var extra=budget.Acquire();}catch(InvalidOperationException){denied=true;}
        Check(denied,"No duplicate queued admission");
        var tail=new BatchThreadAllocator(5,2,3);
        using(var first=tail.Acquire())using(var second=tail.Acquire())Check(first.Count+second.Count==5,"Initial shared budget");
        using(var last=tail.Acquire())Check(last.Count==5,"Isolated tail may receive full request");
        using(var only=new BatchThreadAllocator(5,2,1).Acquire())Check(only.Count==5,"Single job receives full request");
        var before=sources.Select(Hash).ToArray();
        var media=new MediaInfo[sources.Length];
        for(int i=0;i<sources.Length;i++)media[i]=(await Probe.InspectAsync(tools.Ffprobe,sources[i],ct)).Info;
        var plan=new Plan("Batch resource research","libx264",180,250000,0,"fast",false);
        string[] modes=["serial","fixed","adaptive"];
        var qualityCache=new Dictionary<(int,string),(int Frames,double Mean,double Minimum,double WorstSecond,double P05)>();
        for(int rep=0;rep<2;rep++)
        {
            var observations=new Dictionary<string,(double Seconds,(long Bytes,double Quality,double Worst,int Frames)[] Outputs)>();
            foreach(var mode in rep==0?modes:modes.Reverse().ToArray())
            {
                var jobs=sources.Select((src,i)=>new BatchJob(src,Path.Combine(root,$"resource-{rep}-{mode}-{i}.mp4"),plan)).ToArray();
                var timer=Stopwatch.StartNew();
                var terminal=mode=="adaptive"?await BatchEncoder.RunAdaptiveResearchAsync(tools,jobs,2,new IgnoreProgress(),5,ct):
                    await BatchEncoder.RunAsync(tools,jobs,mode=="serial"?1:2,new IgnoreProgress(),ct,totalEncoderThreads:5);
                double seconds=timer.Elapsed.TotalSeconds;
                Check(terminal.Count==jobs.Length&&terminal.All(x=>x.Stage=="Complete"&&x.Report is not null),"Batch resource completion");
                var outputs=new (long Bytes,double Quality,double Worst,int Frames)[jobs.Length];
                for(int i=0;i<jobs.Length;i++)
                {
                    var report=terminal[i].Report!;
                    int? threads=report.RequestedEncoderThreads;
                    Check(mode=="serial"?threads==5:mode=="fixed"?threads==2:threads is >=1 and <=5,"Requested thread report");
                    string sha=Hash(jobs[i].Output);
                    if(!qualityCache.TryGetValue((i,sha),out var q)){q=await PublicCorpusScreen.Quality(tools,jobs[i].Input,jobs[i].Output,media[i].Fps,ct);qualityCache[(i,sha)]=q;}
                    await PacketTimeline.ValidateAsync(tools.Ffprobe,jobs[i].Input,jobs[i].Output,media[i].AudioTracks,ct,compareAudioPayloads:true);
                    outputs[i]=(report.OutputBytes,q.Mean,q.WorstSecond,q.Frames);
                    Console.WriteLine("BATCH_RESOURCE_JOB "+System.Text.Json.JsonSerializer.Serialize(new {rep,mode,index=i,threads,bytes=report.OutputBytes,seconds=report.Total.TotalSeconds,q.Mean,q.WorstSecond,q.Frames,sha}));
                    File.Delete(jobs[i].Output);
                }
                observations[mode]=(seconds,outputs);
                Console.WriteLine("BATCH_RESOURCE "+System.Text.Json.JsonSerializer.Serialize(new {rep,mode,seconds,totalRequestedBudget=5,parallelism=mode=="serial"?1:2}));
            }
            var baseline=observations["serial"];
            foreach(var mode in modes.Skip(1))
            {
                var current=observations[mode];
                bool matched=baseline.Outputs.Zip(current.Outputs).All(x=>x.First.Frames==x.Second.Frames&&Math.Abs((double)x.Second.Bytes/x.First.Bytes-1)<=.05&&Math.Abs(x.Second.Quality-x.First.Quality)<=.001&&Math.Abs(x.Second.Worst-x.First.Worst)<=.005);
                Console.WriteLine("BATCH_RESOURCE_PAIR "+System.Text.Json.JsonSerializer.Serialize(new {rep,mode,matched,timeReduction=1-current.Seconds/baseline.Seconds}));
            }
        }
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        var cancelJobs=sources.Select((src,i)=>new BatchJob(src,Path.Combine(root,$"adaptive-canceled-{i}.mp4"),plan)).ToArray();
        var canceledResult=await BatchEncoder.RunAdaptiveResearchAsync(tools,cancelJobs,2,new IgnoreProgress(),5,canceled.Token);
        Check(canceledResult.All(x=>x.Stage=="Canceled")&&cancelJobs.All(x=>!File.Exists(x.Output)),"Pre-canceled adaptive jobs not published");
        // Failed admission must return its lease so the next queued job can run.
        var failureJobs=sources.Select((src,i)=>new BatchJob(src,Path.Combine(root,$"adaptive-failure-{i}.mp4"),i==0?plan with {Height=181}:plan)).ToArray();
        var failedResult=await BatchEncoder.RunAdaptiveResearchAsync(tools,failureJobs,2,new IgnoreProgress(),5,ct);
        Check(failedResult[0].Stage=="Failed"&&!File.Exists(failureJobs[0].Output)&&failedResult.Skip(1).All(x=>x.Stage=="Complete"),"Failed adaptive job releases budget to subsequent jobs");
        foreach(var job in failureJobs.Skip(1))File.Delete(job.Output);
        using(var activeCancel=CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            int started=0;
            var activeJobs=sources.Select((src,i)=>new BatchJob(src,Path.Combine(root,$"adaptive-active-cancel-{i}.mp4"),plan)).ToArray();
            var result=await BatchEncoder.RunAdaptiveResearchAsync(tools,activeJobs,2,new CallbackProgress(x=>
            {
                if(x.Stage=="Encoding"){Interlocked.Increment(ref started);activeCancel.Cancel();}
            }),5,activeCancel.Token);
            Check(started>0&&result.All(x=>x.Stage=="Canceled")&&activeJobs.All(x=>!File.Exists(x.Output)),"Active adaptive cancellation joins workers and publishes nothing");
        }
        Check(sources.Select(Hash).SequenceEqual(before)&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Batch source and cleanup");
        Console.WriteLine("Batch thread admission lease, tail, actual encoder, quality, failed-job lease return and active/pre-cancel controls passed.");
    }
}
