using System.Diagnostics;
namespace Vanta.Core;
public sealed record ProcessResult(int ExitCode,string Output,string Error);
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> args, CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();
        var psi=new ProcessStartInfo(executable) { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, CreateNoWindow=true };
        foreach(var arg in args) psi.ArgumentList.Add(arg);
        using var p=Process.Start(psi) ?? throw new InvalidOperationException("Could not start process");
        var output=p.StandardOutput.ReadToEndAsync();
        var error=p.StandardError.ReadToEndAsync();
        try { await p.WaitForExitAsync(ct); }
        catch(OperationCanceledException) { try { if(!p.HasExited) p.Kill(entireProcessTree:true); } catch(InvalidOperationException) {} await p.WaitForExitAsync(); await Task.WhenAll(output,error); throw; }
        return new(p.ExitCode,await output,await error);
    }
}