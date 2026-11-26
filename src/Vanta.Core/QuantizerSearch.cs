namespace Vanta.Core;

// Bounded discrete quality search, not a proof of a global optimum.
// CRF and quality usually trend oppositely, but unmeasured reversals remain possible.
internal sealed record QualityObservation(int Quantizer,double Quality,long Bytes);
internal sealed record QuantizerResult(IReadOnlyList<QualityObservation> Observations,QualityObservation? Best,bool ObservedReversal,string StopReason);
internal static class QuantizerSearch
{
    internal static async Task<QuantizerResult> RunAsync(int minimum,int maximum,double target,long? maxBytes,int budget,
        Func<int,CancellationToken,Task<QualityObservation>> measure,CancellationToken ct)
    {
        if(minimum<0||maximum>51||minimum>=maximum||!double.IsFinite(target)||target<0||target>1||
           maxBytes is <=0||budget<2||budget>maximum-minimum+1)throw new ArgumentException("Invalid quantizer search bounds");
        ArgumentNullException.ThrowIfNull(measure);
        var observations=new SortedDictionary<int,QualityObservation>();
        var order=new List<QualityObservation>();
        async Task Probe(int q)
        {
            ct.ThrowIfCancellationRequested();
            if(observations.ContainsKey(q)||order.Count>=budget)throw new InvalidOperationException("Repeated or over-budget quantizer");
            var p=await measure(q,ct);ct.ThrowIfCancellationRequested();
            if(p.Quantizer!=q||!double.IsFinite(p.Quality)||p.Quality is <-1 or >1||p.Bytes<=0)
                throw new InvalidDataException("Invalid quality observation");
            observations.Add(q,p);order.Add(p);
        }
        bool Reversal()
        {
            var a=observations.Values.ToArray();
            return a.Zip(a.Skip(1)).Any(x=>x.Second.Quality>x.First.Quality+1e-9||x.Second.Bytes>x.First.Bytes);
        }
        await Probe(minimum);await Probe(maximum);
        string reason="Budget";
        while(order.Count<budget)
        {
            var a=observations.Values.ToArray();
            bool reverse=Reversal();
            int next;
            if(reverse)
            {
                // Once a measured monotonicity assumption fails, spend remaining
                // budget on the largest unresolved interval, not a false bracket.
                var gap=a.Zip(a.Skip(1)).OrderByDescending(x=>x.Second.Quantizer-x.First.Quantizer).First();
                if(gap.Second.Quantizer-gap.First.Quantizer<=1){reason="Exhausted";break;}
                next=gap.First.Quantizer+(gap.Second.Quantizer-gap.First.Quantizer)/2;
            }
            else
            {
                var lower=a.LastOrDefault(x=>x.Quality>=target);
                if(lower is null||lower.Quantizer==maximum){reason="Endpoint under monotonicity";break;}
                var upper=a.First(x=>x.Quantizer>lower.Quantizer);
                int span=upper.Quantizer-lower.Quantizer;
                if(span<=1){reason="Bracket under monotonicity";break;}
                double prediction=lower.Quantizer+span*(lower.Quality-target)/(lower.Quality-upper.Quality);
                int guard=Math.Max(1,(span+3)/4);
                next=Math.Clamp((int)Math.Round(prediction,MidpointRounding.AwayFromZero),lower.Quantizer+guard,upper.Quantizer-guard);
            }
            await Probe(next);
        }
        ct.ThrowIfCancellationRequested();
        var best=order.Where(x=>x.Quality>=target&&(maxBytes is null||x.Bytes<=maxBytes.Value))
            .OrderBy(x=>x.Bytes).ThenByDescending(x=>x.Quality).ThenBy(x=>x.Quantizer).FirstOrDefault();
        return new(order.AsReadOnly(),best,Reversal(),reason);
    }
}
