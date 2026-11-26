using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vanta.Core;

internal static class ResourceTraceScreen
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static async Task RunAsync(Toolchain tools,string root,string source,Plan plan,CancellationToken ct)
    {
        string inputHash=Hash(source);string? referenceHash=null;
        // Equal encoding settings; only validation algorithm changes.
        for(int rep=0;rep<2;rep++)
        foreach(bool combined in rep==0?new[]{false,true}:new[]{true,false})
        {
            string output=Path.Combine(root,$"resource-trace-{rep}-{combined}.mp4");
            IReadOnlyList<ProcessResourceTrace.Row> rows;
            EncodeReport report;
            using(var trace=ProcessResourceTrace.Begin())
            {
                report=await Encoder.EncodeResearchAsync(tools.Ffmpeg,tools.Ffprobe,source,output,plan,ct,encoderThreads:2,singleDecodeValidation:combined,interleavedAudioValidation:combined);
                rows=trace.Snapshot();
                Check(trace.Omitted==0&&rows.Count>0&&rows.All(r=>r.Samples>=0&&r.CounterErrors>=0&&r.MaxSampledWorkingSetBytes is null or >0&&r.ReportedPeakWorkingSetBytes is null or >0&&r.CpuSeconds is null or >=0),"Resource rows and joined lifetime");
                Check(rows.Any(r=>r.CpuSeconds.HasValue)&&rows.Any(r=>r.MaxSampledWorkingSetBytes.HasValue),"At least one actual CPU and RSS observation");
                Check(rows.Any(r=>r.Role==(combined?"output-strict-decode":"output-video-clock")),"Output clock path observed");
                Check(rows.Count(r=>r.Role.Contains("audio-packets")||r.Role.Contains("interleaved-audio"))==(combined?2:4),"Expected audio probe process count");
            }
            await SingleDecodeScreen.ReferenceAsync(tools,source,output,2,ct);
            await Encoder.ValidateAudioAsync(tools.Ffmpeg,source,output,ct);
            var sha=Hash(output);referenceHash??=sha;
            Check(sha==referenceHash&&Hash(source)==inputHash,"Resource trace output bytes and source unchanged");
            Console.WriteLine("RESOURCE_TRACE "+JsonSerializer.Serialize(new{rep,combined,seconds=report.Total.TotalSeconds,processes=rows.Count,cpuObserved=rows.Count(r=>r.CpuSeconds.HasValue),exitCpu=rows.Count(r=>r.CpuBasis=="exit-counter"),cpuSumObserved=rows.Where(r=>r.CpuSeconds.HasValue).Sum(r=>r.CpuSeconds!.Value),maxChildSampledRss=rows.Max(r=>r.MaxSampledWorkingSetBytes),sha,rows}));
            File.Delete(output);
        }
        // Normal calls have no collector. Pre-cancellation creates no process.
        using(var trace=ProcessResourceTrace.Begin())
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();bool rejected=false;
            try{await ProcessRunner.RunAsync(tools.Ffmpeg,["-version"],canceled.Token);}catch(OperationCanceledException){rejected=true;}
            Check(rejected&&trace.Snapshot().Count==0,"Canceled trace has no children");
        }
        // Cancel after a real child emits progress; the trace must join its sampler
        // after the runner has terminated the process, not leave a detached observer.
        using(var trace=ProcessResourceTrace.Begin())
        using(var running=CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            bool progressSeen=false,stopped=false;
            try
            {
                await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-xerror","-nostdin","-re","-f","lavfi","-i","testsrc2=s=160x90:r=24:d=10","-progress","pipe:1","-nostats","-f","null","-"],running.Token,line=>
                {
                    if(line.StartsWith("out_time_us=",StringComparison.Ordinal)){progressSeen=true;running.Cancel();}
                });
            }
            catch(OperationCanceledException){stopped=true;}
            Check(progressSeen&&stopped&&trace.Snapshot().Count==1,"Actively canceled child and sampler joined");
        }
        Console.WriteLine("Resource trace per-child CPU/RSS, byte equality, independent oracle and active/pre-cancellation passed.");
    }
}
