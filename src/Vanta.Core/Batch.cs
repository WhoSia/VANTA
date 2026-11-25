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
    public static async Task RunAsync(Toolchain tools,IReadOnlyList<BatchJob> jobs,int parallelism,IProgress<BatchUpdate> progress,CancellationToken ct=default,int? totalEncoderThreads=null)
    {
        if(parallelism is <1 or >2)throw new ArgumentOutOfRangeException(nameof(parallelism));
        var jobThreads=ThreadBudget.PerJob(totalEncoderThreads,parallelism);
        var inputs=jobs.Select(j=>Path.GetFullPath(j.Input)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var j in jobs)
        {
            var path=Path.GetFullPath(j.Output);
            if(inputs.Contains(path)||!outputs.Add(path))throw new ArgumentException("Batch output overlaps an input or another output.");
        }
        await Parallel.ForEachAsync(Enumerable.Range(0,jobs.Count),new ParallelOptions{MaxDegreeOfParallelism=parallelism,CancellationToken=ct},async (i,token)=>
        {
            var j=jobs[i];
            EncodeReport report;
            try
            {
                report=await Encoder.EncodeAsync(tools.Ffmpeg,tools.Ffprobe,j.Input,j.Output,j.Plan,token,new InlineProgress(x=>
                {
                    if(x.Stage!="Complete")progress.Report(new(i,x.Stage,x.Fraction));
                }),encoderThreads:jobThreads,expectedSource:j.ExpectedSource);
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){progress.Report(new(i,"Canceled",null));return;}
            catch(Exception ex){progress.Report(new(i,"Failed",null,ex.Message));return;}
            // Encoder has already published. A failing notification is not an encoding failure.
            // Best-effort terminal progress; never issue a contradictory Failed event.
            try
            {
                progress.Report(new(i,"Complete",1,$"{report.OutputBytes/1e6:F2} MB · {1-report.OutputBytes/(double)report.InputBytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (validation {report.Validation.TotalSeconds:F1}s)",report));
            }
            catch(Exception) { }
        });
    }
    private sealed class InlineProgress(Action<EncodeProgress> report):IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)=>report(value);
    }
}