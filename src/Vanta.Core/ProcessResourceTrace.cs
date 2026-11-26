using System.Diagnostics;
using System.ComponentModel;
namespace Vanta.Core;

// Explicit, local research scope only. No collection on normal desktop paths,
// no command arguments or file paths recorded, no persistent/external telemetry.
// Per-direct-child sampled working set and CPU counters are not tree-wide peaks.
internal sealed class ProcessResourceTrace:IDisposable
{
    internal sealed record Row(string Role,int Samples,long? MaxSampledWorkingSetBytes,long? ReportedPeakWorkingSetBytes,double? CpuSeconds,string CpuBasis,int CounterErrors);
    private static readonly AsyncLocal<ProcessResourceTrace?> current=new();
    private readonly object gate=new();
    private readonly List<Row> rows=[];
    private int started,active,omitted;
    private bool disposed;
    internal static ProcessResourceTrace Begin()
    {
        if(current.Value is not null)throw new InvalidOperationException("Nested resource traces are not supported");
        var trace=new ProcessResourceTrace();current.Value=trace;return trace;
    }
    internal static Observation? Track(Process process,string role)
    {
        var trace=current.Value;if(trace is null)return null;
        lock(trace.gate)
        {
            if(trace.disposed)return null;
            if(trace.started>=4096){trace.omitted++;return null;}
            trace.started++;trace.active++;
        }
        return new Observation(trace,process,role);
    }
    internal int Omitted{get{lock(gate)return omitted;}}
    internal IReadOnlyList<Row> Snapshot()
    {
        lock(gate){if(active!=0)throw new InvalidOperationException("Resource observations still active");return rows.ToArray();}
    }
    private void Finish(Row row){lock(gate){rows.Add(row);active--;}}
    public void Dispose()
    {
        lock(gate){if(disposed)return;if(active!=0)throw new InvalidOperationException("Resource trace disposed before children joined");disposed=true;}
        if(ReferenceEquals(current.Value,this))current.Value=null;
    }
    internal sealed class Observation:IAsyncDisposable
    {
        private readonly ProcessResourceTrace owner;
        private readonly Process original;
        private readonly string role;
        private readonly CancellationTokenSource stop=new();
        private readonly Task sampler;
        private long? maxWorking,peak;
        private double? sampledCpu;
        private int samples,errors,finished;
        private static bool CounterError(Exception e)=>e is InvalidOperationException or Win32Exception or NotSupportedException or ArgumentException or UnauthorizedAccessException;
        internal Observation(ProcessResourceTrace owner,Process original,string role)
        {
            this.owner=owner;this.original=original;this.role=role;
            // Separate Process wrapper: sampling never Refresh()es the runner's
            // process object concurrently with WaitForExitAsync.
            sampler=SampleAsync();
        }
        private async Task SampleAsync()
        {
            try
            {
                DateTime identity=original.StartTime;
                using var p=Process.GetProcessById(original.Id);
                if(p.StartTime!=identity){errors++;return;}
                while(!stop.IsCancellationRequested)
                {
                    p.Refresh();
                    if(p.HasExited||p.StartTime!=identity)break;
                    bool valid=false;
                    try{long v=p.WorkingSet64;if(v>0){maxWorking=Math.Max(maxWorking??0,v);valid=true;}}
                    catch(Exception ex) when(CounterError(ex)){errors++;}
                    try{long v=p.PeakWorkingSet64;if(v>0){peak=Math.Max(peak??0,v);valid=true;}}
                    catch(Exception ex) when(CounterError(ex)){errors++;}
                    try{double v=p.TotalProcessorTime.TotalSeconds;if(double.IsFinite(v)&&v>=0){sampledCpu=Math.Max(sampledCpu??0,v);valid=true;}}
                    catch(Exception ex) when(CounterError(ex)){errors++;}
                    if(valid)samples++;
                    await Task.Delay(25,stop.Token);
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested){}
            catch(Exception ex) when(CounterError(ex)){errors++;}
        }
        public async ValueTask DisposeAsync()
        {
            if(Interlocked.Exchange(ref finished,1)!=0)return;
            stop.Cancel();
            try
            {
                await sampler;
                double? cpu=sampledCpu;string basis=cpu.HasValue?"sampled-lower-bound":"unavailable";
                // Final counter may be unsupported after process exit; preserve
                // the sampled lower bound rather than inventing a zero.
                try{double v=original.TotalProcessorTime.TotalSeconds;if(double.IsFinite(v)&&v>=0){cpu=Math.Max(cpu??0,v);basis=original.HasExited?"exit-counter":"sampled-lower-bound";}}
                catch(Exception ex) when(CounterError(ex)){errors++;}
                try{long v=original.PeakWorkingSet64;if(v>0)peak=Math.Max(peak??0,v);}
                catch(Exception ex) when(CounterError(ex)){errors++;}
                owner.Finish(new(role,samples,maxWorking,peak,cpu,basis,errors));
            }
            finally{stop.Dispose();}
        }
    }
}
