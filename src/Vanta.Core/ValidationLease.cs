namespace Vanta.Core;

// Research-only gate with cancellation-safe ownership. A waiting task never
// owns a permit until WaitAsync succeeds. Dispose is idempotent.
internal sealed class ValidationLease:IDisposable
{
    private SemaphoreSlim? gate;
    private ValidationLease(SemaphoreSlim gate)=>this.gate=gate;
    internal static async Task<ValidationLease?> EnterAsync(SemaphoreSlim? gate,CancellationToken ct)
    {
        if(gate is null)return null;
        await gate.WaitAsync(ct);
        return new ValidationLease(gate);
    }
    public void Dispose()=>Interlocked.Exchange(ref gate,null)?.Release();
}
