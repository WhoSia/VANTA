namespace Vanta.Core;

public static class OutputNames
{
    public static string Suggest(string input,ISet<string>? reserved=null,string? destinationDirectory=null)
    {
        input=Path.GetFullPath(input);
        var dir=destinationDirectory is null?Path.GetDirectoryName(input)!:Path.GetFullPath(destinationDirectory);
        if(!Directory.Exists(dir))throw new DirectoryNotFoundException(dir);
        var stem=Path.GetFileNameWithoutExtension(input)+"_vanta";
        for(int i=1;i<=10000;i++)
        {
            var path=Path.Combine(dir,stem+(i==1?"":$"_{i}")+".mp4");
            if(!File.Exists(path)&&!Directory.Exists(path)&&(reserved is null||!reserved.Contains(path))&&!string.Equals(path,input,StringComparison.OrdinalIgnoreCase))
            {
                reserved?.Add(path);
                return path;
            }
        }
        throw new IOException("No available output filename.");
    }
}
public sealed record BatchJob(string Input,string Output,Plan Plan,SourceStamp? ExpectedSource=null);
public sealed record BatchUpdate(int Index,string Stage,double? Fraction,string? Detail=null,EncodeReport? Report=null)
{
    public VerificationPhase? Verification {get;init;}
}
public static class BatchEncoder
{
    public static Task<IReadOnlyList<BatchUpdate>> RunAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,CancellationToken ct=default,int? totalEncoderThreads=null)
        =>RunCoreAsync(tools,jobs,parallelism,progress,ct,totalEncoderThreads,false);
    internal static Task<IReadOnlyList<BatchUpdate>> RunAdaptiveResearchAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,int totalEncoderThreads,CancellationToken ct)
        =>RunCoreAsync(tools,jobs,parallelism,progress,ct,totalEncoderThreads,true);
    internal static Task<IReadOnlyList<BatchUpdate>> RunGatedResearchAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,IProgress<BatchUpdate> progress,int totalEncoderThreads,CancellationToken ct)
        =>RunCoreAsync(tools,jobs,2,progress,ct,totalEncoderThreads,false,true);
    // Explicit research only. At most two in-flight jobs, one encoder (including
    // device qualification) and one verifier. No automatic backend fallback.
    internal static Task<IReadOnlyList<BatchUpdate>> RunPipelineResearchAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,IProgress<BatchUpdate> progress,CancellationToken ct,bool overlap,int? threads=null,string? backend=null,bool resident=false,bool fast=false)
    {
        if(backend is not null)
        {
            _=HardwareEncoderPolicy.SoftwareFamily(backend);
            HardwareEncoderPolicy.ValidateFast(backend,fast);
            if(resident)HardwareEncoderPolicy.ValidateResident(backend);
            if(threads.HasValue)throw new ArgumentException("Device trials require automatic threads.");
        }
        else if(resident||fast)throw new ArgumentException("Device options require an explicit backend.");
        return RunCoreAsync(tools,jobs,overlap?2:1,progress,ct,threads,false,overlap,overlap,backend,resident,fast);
    }
    private static async Task<IReadOnlyList<BatchUpdate>> RunCoreAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,CancellationToken ct,int? totalEncoderThreads,bool adaptive,bool serializeValidation=false,bool stagePipeline=false,string? backend=null,bool resident=false,bool fast=false)
    {
        if(parallelism is <1 or >2)throw new ArgumentOutOfRangeException(nameof(parallelism));
        var jobThreads=ThreadBudget.PerJob(totalEncoderThreads,stagePipeline?1:parallelism);
        using var encoderGate=stagePipeline?new SemaphoreSlim(1,1):null;
        using var validationGate=serializeValidation?new SemaphoreSlim(1,1):null;
        var allocator=adaptive?new BatchThreadAllocator(totalEncoderThreads!.Value,parallelism,jobs.Count):null;
        var inputs=jobs.Select(j=>Path.GetFullPath(j.Input)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var j in jobs)
        {
            var path=Path.GetFullPath(j.Output);
            if(inputs.Contains(path)||!outputs.Add(path))throw new ArgumentException("Batch output overlaps an input or another output.");
        }
        var terminal=new BatchUpdate?[jobs.Count];
        void Notify(BatchUpdate update)
        {
            // Notifications are advisory; the returned terminal results are authoritative.
            try{progress.Report(update);}catch(Exception){}
        }
        void Finish(BatchUpdate update)
        {
            terminal[update.Index]=update;
            Notify(update);
        }
        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0,jobs.Count),new ParallelOptions{MaxDegreeOfParallelism=parallelism,CancellationToken=ct},async (i,token)=>
            {
                var j=jobs[i];
                EncodeReport report;
                try
                {
                    using var lease=allocator?.Acquire();
                    Notify(new(i,"Preparing",null));
                    var forwarded=new InlineProgress(x=>
                    {
                        if(x.Stage!="Complete")Notify(new BatchUpdate(i,x.Stage,x.Fraction){Verification=x.Verification});
                    });
                    if(backend is not null)
                        report=await Encoder.EncodeHardwareTrialCoreAsync(tools,j.Input,j.Output,j.Plan,backend,token,forwarded,j.ExpectedSource,resident,fast,null,encoderGate,validationGate);
                    else
                    {
                        if(encoderGate is not null)Notify(new(i,"Waiting for encoder",null));
                        using var encodeLease=await ValidationLease.EnterAsync(encoderGate,token);
                        report=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,j.Input,j.Output,j.Plan,token,forwarded,encoderThreads:lease?.Count??jobThreads,expectedSource:j.ExpectedSource,validationGate:validationGate,encodeLease:encodeLease);
                    }
                }
                catch(OperationCanceledException) when(token.IsCancellationRequested){Finish(new(i,"Canceled",null));return;}
                catch(Exception ex){Finish(new(i,"Failed",null,ex.Message));return;}
                Finish(new(i,"Complete",1,$"{report.OutputBytes/1e6:F2} MB · {1-report.OutputBytes/(double)report.InputBytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (encoding {report.Encoding.TotalSeconds:F1}s, validation {report.Validation.TotalSeconds:F1}s)",report));
            });
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){}
        // All workers have joined. Jobs never started due to cancellation still get one terminal result.
        for(int i=0;i<terminal.Length;i++)
            if(terminal[i] is null)Finish(new(i,"Canceled",null));
        return Array.AsReadOnly(terminal.Select(x=>x!).ToArray());
    }
    private sealed class InlineProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)=>report(value);
    }
}