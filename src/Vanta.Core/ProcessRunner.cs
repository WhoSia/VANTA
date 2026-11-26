using System.Diagnostics;
namespace Vanta.Core;
public sealed record ProcessResult(int ExitCode,string Output,string Error);
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> args, CancellationToken ct=default, Action<string>? onOutputLine=null)
    {
        ct.ThrowIfCancellationRequested();
        var psi=new ProcessStartInfo(executable) { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true };
        foreach(var arg in args) psi.ArgumentList.Add(arg);
        using var p=Process.Start(psi) ?? throw new InvalidOperationException("Could not start process");
        await using var resource=ProcessResourceTrace.Track(p,Path.GetFileName(executable));
        var output=onOutputLine is null?BoundedProcessText.ReadOutputAsync(p.StandardOutput):DrainLinesAsync(p.StandardOutput,onOutputLine);
        var error=BoundedProcessText.ReadDiagnosticAsync(p.StandardError);
        try { await p.WaitForExitAsync(ct); }
        catch(OperationCanceledException) { try { if(!p.HasExited) p.Kill(entireProcessTree:true); } catch(InvalidOperationException) {} await p.WaitForExitAsync(); await Task.WhenAll(output,error); throw; }
        return new(p.ExitCode,await output,await error);
    }
    private static async Task<string> DrainLinesAsync(StreamReader reader,Action<string> onLine)
    {
        // Drain even when an observer throws, then surface the observer failure.
        Exception? failure=null;
        while(await reader.ReadLineAsync() is string line)
        {
            if(failure is null){try{onLine(line);}catch(Exception ex){failure=ex;}}
        }
        if(failure is not null)throw new InvalidOperationException("Progress observer failed",failure);
        return "";
    }
}