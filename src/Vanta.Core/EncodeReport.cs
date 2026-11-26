namespace Vanta.Core;

// Per-job wall time. No persistence, background sampling, or external telemetry.
public sealed record EncodeReport(TimeSpan Preparation,TimeSpan Encoding,TimeSpan OutputProbe,TimeSpan TimelineCheck,TimeSpan AudioHashCheck,TimeSpan FullDecode,TimeSpan Publication,TimeSpan Total,long OutputBytes,long InputBytes)
{
    // Separate stage durations may overlap in research mode; report actual wall time.
    public TimeSpan? ConcurrentTimelineDecode {get;init;}
    public TimeSpan Validation => OutputProbe+AudioHashCheck+(ConcurrentTimelineDecode??(TimelineCheck+FullDecode));
}
