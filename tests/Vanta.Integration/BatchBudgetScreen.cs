using System.Diagnostics;
using System.Security.Cryptography;
using Vanta.Core;

internal static class BatchBudgetScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    private sealed class IgnoreProgress:IProgress<BatchUpdate>{public void Report(BatchUpdate value){}}
    private sealed class EncodeCallback(Action<EncodeProgress> action):IProgress<EncodeProgress>{public void Report(EncodeProgress value)=>action(value);}
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
        string[] modes=["serial","fixed","adaptive","serial-equal","fixed-gated"];
        var qualityCache=new Dictionary<(int,string),(int Frames,double Mean,double Minimum,double WorstSecond,double P05)>();
        for(int rep=0;rep<2;rep++)
        {
            var observations=new Dictionary<string,(double Seconds,(long Bytes,double Quality,double Worst,int Frames,string Sha)[] Outputs)>();
            foreach(var mode in rep==0?modes:modes.Reverse().ToArray())
            {
                var jobs=sources.Select((src,i)=>new BatchJob(src,Path.Combine(root,$"resource-{rep}-{mode}-{i}.mp4"),plan)).ToArray();
                var timer=Stopwatch.StartNew();
                bool serial=mode is "serial" or "serial-equal";
                int requestedBudget=mode=="serial-equal"?2:5;
                var terminal=mode=="adaptive"?await BatchEncoder.RunAdaptiveResearchAsync(tools,jobs,2,new IgnoreProgress(),5,ct):
                    mode=="fixed-gated"?await BatchEncoder.RunGatedResearchAsync(tools,jobs,new IgnoreProgress(),5,ct):
                    await BatchEncoder.RunAsync(tools,jobs,serial?1:2,new IgnoreProgress(),ct,totalEncoderThreads:requestedBudget);
                double seconds=timer.Elapsed.TotalSeconds;
                Check(terminal.Count==jobs.Length&&terminal.All(x=>x.Stage=="Complete"&&x.Report is not null),"Batch resource completion");
                var outputs=new (long Bytes,double Quality,double Worst,int Frames,string Sha)[jobs.Length];
                for(int i=0;i<jobs.Length;i++)
                {
                    var report=terminal[i].Report!;
                    int? threads=report.RequestedEncoderThreads;
                    Check(mode=="serial"?threads==5:mode=="adaptive"?threads is >=1 and <=5:threads==2,"Requested thread report");
                    Check(report.ValidationAdmissionWait>=TimeSpan.Zero&&report.ValidationAdmissionWait+report.Validation<=report.Total,"Admission wait accounting");
                    if(mode!="fixed-gated")Check(report.ValidationAdmissionWait==TimeSpan.Zero,"Ungated baseline wait");
                    string sha=Hash(jobs[i].Output);
                    if(!qualityCache.TryGetValue((i,sha),out var q)){q=await PublicCorpusScreen.Quality(tools,jobs[i].Input,jobs[i].Output,media[i].Fps,ct);qualityCache[(i,sha)]=q;}
                    await PacketTimeline.ValidateAsync(tools.Ffprobe,jobs[i].Input,jobs[i].Output,media[i].AudioTracks,ct,compareAudioPayloads:true);
                    outputs[i]=(report.OutputBytes,q.Mean,q.WorstSecond,q.Frames,sha);
                    Console.WriteLine("BATCH_RESOURCE_JOB "+System.Text.Json.JsonSerializer.Serialize(new {rep,mode,index=i,threads,bytes=report.OutputBytes,seconds=report.Total.TotalSeconds,admissionWait=report.ValidationAdmissionWait.TotalSeconds,q.Mean,q.WorstSecond,q.Frames,sha}));
                    File.Delete(jobs[i].Output);
                }
                observations[mode]=(seconds,outputs);
                Console.WriteLine("BATCH_RESOURCE "+System.Text.Json.JsonSerializer.Serialize(new {rep,mode,seconds,totalRequestedBudget=requestedBudget,parallelism=serial?1:2}));
            }
            var baseline=observations["serial"];
            foreach(var mode in new[]{"fixed","adaptive"})
            {
                var current=observations[mode];
                bool matched=baseline.Outputs.Zip(current.Outputs).All(x=>x.First.Frames==x.Second.Frames&&Math.Abs((double)x.Second.Bytes/x.First.Bytes-1)<=.05&&Math.Abs(x.Second.Quality-x.First.Quality)<=.001&&Math.Abs(x.Second.Worst-x.First.Worst)<=.005);
                Console.WriteLine("BATCH_RESOURCE_PAIR "+System.Text.Json.JsonSerializer.Serialize(new {rep,mode,matched,timeReduction=1-current.Seconds/baseline.Seconds}));
            }
            foreach(var (reference,mode) in new[]{("serial-equal","fixed"),("fixed","fixed-gated")})
            {
                var a0=observations[reference];var a1=observations[mode];
                bool exact=a0.Outputs.Zip(a1.Outputs).All(x=>x.First.Sha==x.Second.Sha);
                bool matched=a0.Outputs.Zip(a1.Outputs).All(x=>x.First.Frames==x.Second.Frames&&Math.Abs((double)x.Second.Bytes/x.First.Bytes-1)<=.05&&Math.Abs(x.Second.Quality-x.First.Quality)<=.001&&Math.Abs(x.Second.Worst-x.First.Worst)<=.005);
                Console.WriteLine("BATCH_SCHEDULER_PAIR "+System.Text.Json.JsonSerializer.Serialize(new {rep,baseline=reference,mode,matched,exact,timeReduction=1-a1.Seconds/a0.Seconds}));
            }
        }
        // A canceled waiter must never release another job's semaphore permit.
        using(var gate=new SemaphoreSlim(1,1))
        {
            using(var held=await ValidationLease.EnterAsync(gate,ct))
            using(var waitCancel=CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var waiting=ValidationLease.EnterAsync(gate,waitCancel.Token);
                Check(!waiting.IsCompleted&&gate.CurrentCount==0,"Validation gate serializes admission");
                waitCancel.Cancel();
                bool stopped=false;
                try{using var invalid=await waiting;}catch(OperationCanceledException){stopped=true;}
                Check(stopped&&gate.CurrentCount==0,"Canceled waiter cannot release held permit");
                string dest=Path.Combine(root,"gated-wait-canceled.mp4");
                using var during=CancellationTokenSource.CreateLinkedTokenSource(ct);
                bool rejected=false;
                try{await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,sources[0],dest,plan,during.Token,new EncodeCallback(x=>{if(x.Stage=="Waiting for validation")during.Cancel();}),encoderThreads:2,validationGate:gate);}
                catch(OperationCanceledException){rejected=true;}
                Check(rejected&&gate.CurrentCount==0&&!File.Exists(dest),"Canceled validation admission publishes nothing");
            }
            Check(gate.CurrentCount==1,"Gate released after holder completion");
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
