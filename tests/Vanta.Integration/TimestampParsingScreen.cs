using System.Diagnostics;
using System.Globalization;
using Vanta.Core;

internal static class TimestampParsingScreen
{
    // Preserve the prior parser as the independent differential reference.
    private static double? SplitReference(string line,string key)
    {
        var entry=line.Split('|').FirstOrDefault(s=>s.StartsWith(key+"=",StringComparison.Ordinal));
        if(entry is null)return null;
        if(!double.TryParse(entry[(key.Length+1)..],NumberStyles.Float,CultureInfo.InvariantCulture,out var t)||!double.IsFinite(t))
            throw new InvalidDataException("Missing presentation timestamp");
        return t;
    }
    private static (double? Value,bool Rejected) Outcome(Func<string,string,double?> parser,string row,string key)
    {
        try{return (parser(row,key),false);}catch(InvalidDataException){return (null,true);}
    }
    public static async Task RunAsync(string ffprobe,string source,CancellationToken ct)
    {
        const string key="best_effort_timestamp_time";
        var clock=Stopwatch.StartNew();
        var result=await ProcessRunner.RunAsync(ffprobe,["-v","error","-select_streams","v:0","-show_frames","-show_entries","frame="+key,"-of","compact=p=0:nk=0",source],ct);
        double probeSeconds=clock.Elapsed.TotalSeconds;
        if(result.ExitCode!=0||!string.IsNullOrWhiteSpace(result.Error))throw new InvalidDataException("Timestamp benchmark probe failed.");
        var rows=result.Output.Split('\n').Select(s=>s.TrimEnd('\r')).ToArray();
        var edges=new[]{"","side_data_type=H.264","best_effort_timestamp_time=0.000000","x=9|best_effort_timestamp_time=-.125|x=2","best_effort_timestamp_time= 1e-3 ","best_effort_timestamp_time=N/A","best_effort_timestamp_time=NaN","best_effort_timestamp_time=Infinity","best_effort_timestamp_time=1e999","best_effort_timestamp_time=1|best_effort_timestamp_time=2","best_effort_timestamp_time_extra=1","best_effort_timestamp_time=","|best_effort_timestamp_time=1|"};
        foreach(var row in rows.Concat(edges))
            if(Outcome(SplitReference,row,key)!=Outcome(PacketTimeline.ParseTimestamp,row,key))
                throw new InvalidDataException("Timestamp parser differential mismatch: "+row);
        int expected=rows.Count(row=>PacketTimeline.ParseTimestamp(row,key).HasValue);
        if(expected<1000)throw new InvalidDataException("Timestamp benchmark fixture too short.");
        Console.WriteLine(FormattableString.Invariant($"MANAGED_PARSE_FIXTURE frames={expected} rows={rows.Length} probe_seconds={probeSeconds:F6}"));
        // Warm both delegates and run interleaved repeated cached-text trials.
        foreach(var parser in new Func<string,string,double?>[]{SplitReference,PacketTimeline.ParseTimestamp})
            foreach(var row in rows)parser(row,key);
        for(int rep=0;rep<4;rep++)
        foreach(bool span in rep%2==0?new[]{false,true}:new[]{true,false})
        {
            ct.ThrowIfCancellationRequested();
            Func<string,string,double?> parser=span?PacketTimeline.ParseTimestamp:SplitReference;
            long before=GC.GetAllocatedBytesForCurrentThread();
            clock.Restart();
            int count=0;double sum=0;
            for(int repeat=0;repeat<100;repeat++)
            foreach(var row in rows)
                if(parser(row,key) is double value){count++;sum+=value;}
            clock.Stop();
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            if(count!=expected*100||!double.IsFinite(sum))throw new InvalidDataException("Timestamp benchmark count mismatch.");
            Console.WriteLine(FormattableString.Invariant($"MANAGED_PARSE rep={rep} span={span} seconds={clock.Elapsed.TotalSeconds:F6} allocated_bytes={allocated} count={count} checksum={sum:R}"));
        }
    }
}
