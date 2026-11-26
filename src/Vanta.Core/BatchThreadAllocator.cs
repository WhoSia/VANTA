namespace Vanta.Core;

// Research-only admission budget. An active job keeps its initial encoder
// thread request until completion; never resize a running encoder. This
// does not limit decoder, filter, x265 pool or OS thread counts.
internal sealed class BatchThreadAllocator
{
    private readonly int total,parallelism;
    private readonly object gate=new();
    private int unclaimed,active,held;
    internal BatchThreadAllocator(int total,int parallelism,int jobs)
    {
        ThreadBudget.PerJob(total,parallelism);
        if(jobs<0)throw new ArgumentOutOfRangeException(nameof(jobs));
        this.total=total;this.parallelism=parallelism;unclaimed=jobs;
    }
    internal Lease Acquire()
    {
        lock(gate)
        {
            if(unclaimed==0||active>=parallelism)throw new InvalidOperationException("No available batch admission");
            int slots=Math.Min(parallelism-active,unclaimed);
            int count=(total-held)/slots;
            if(count<1)throw new InvalidOperationException("Encoder request budget exhausted");
            active++;unclaimed--;held+=count;
            return new Lease(this,count);
        }
    }
    private void Release(int count)
    {
        lock(gate){active--;held-=count;}
    }
    internal sealed class Lease(BatchThreadAllocator owner,int count):IDisposable
    {
        internal int Count=>count;
        private int released;
        public void Dispose(){if(Interlocked.Exchange(ref released,1)==0)owner.Release(count);}
    }
}
