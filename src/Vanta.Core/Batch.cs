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
public sealed record BatchUpdate(int Index,string Stage,double? Fraction,string? Detail=null,EncodeReport? Report=null);
public static class BatchEncoder
{
    public static Task<IReadOnlyList<BatchUpdate>> RunAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,CancellationToken ct=default,int? totalEncoderThreads=null)
        =>RunCoreAsync(tools,jobs,parallelism,progress,ct,totalEncoderThreads,false);
    internal static Task<IReadOnlyList<BatchUpdate>> RunAdaptiveResearchAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,int totalEncoderThreads,CancellationToken ct)
        =>RunCoreAsync(tools,jobs,parallelism,progress,ct,totalEncoderThreads,true);
    private static async Task<IReadOnlyList<BatchUpdate>> RunCoreAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,CancellationToken ct,int? totalEncoderThreads,bool adaptive)
    {
        if(parallelism is <1 or >2)throw new ArgumentOutOfRangeException(nameof(parallelism));
        var jobThreads=ThreadBudget.PerJob(totalEncoderThreads,parallelism);
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
                    report=await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,j.Input,j.Output,j.Plan,token,new InlineProgress(x=>
                    {
                        if(x.Stage!="Complete")Notify(new(i,x.Stage,x.Fraction));
                    }),encoderThreads:lease?.Count??jobThreads,expectedSource:j.ExpectedSource);
                }
                catch(OperationCanceledException) when(token.IsCancellationRequested){Finish(new(i,"Canceled",null));return;}
                catch(Exception ex){Finish(new(i,"Failed",null,ex.Message));return;}
                Finish(new(i,"Complete",1,$"{report.OutputBytes/1e6:F2} MB · {1-report.OutputBytes/(double)report.InputBytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (validation {report.Validation.TotalSeconds:F1}s)",report));
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