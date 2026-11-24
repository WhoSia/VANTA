namespace Vanta.Core;

// Explicit FFmpeg encoder thread request, not a CPU affinity or process-wide limit.
// null keeps the existing encoder-specific automatic behavior.
public static class ThreadBudget
{
    public const int Maximum=64;

    public static int? PerJob(int? total,int parallelism)
    {
        if(parallelism is <1 or >2)throw new ArgumentOutOfRangeException(nameof(parallelism));
        if(total is null)return null;
        if(total<parallelism||total>Maximum)
            throw new ArgumentException("Thread budget must be between the number of active jobs and 64.");
        return total.Value/parallelism;
    }

    public static void ValidateEncoder(int? threads,bool split)
    {
        if(threads is null)return;
        if(threads<1||threads>Maximum||(split&&(threads<2||threads%2!=0)))
            throw new ArgumentException("Choose 1–64 encoder threads; experimental split requires an even total of at least 2.");
    }

    public static IReadOnlyList<int> Choices(int logicalProcessors)
    {
        int limit=Math.Clamp(logicalProcessors,1,Maximum);
        return Enumerable.Range(1,limit).Where(n=>n==1||n%2==0||n==limit).ToArray();
    }
}
