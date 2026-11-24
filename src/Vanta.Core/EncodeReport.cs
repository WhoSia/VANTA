namespace Vanta.Core;

// Per-job wall time. No persistence, background sampling, or external telemetry.
public sealed record EncodeReport(TimeSpan Preparation,TimeSpan Encoding,TimeSpan OutputProbe,TimeSpan TimelineCheck,TimeSpan AudioHashCheck,TimeSpan FullDecode,TimeSpan Publication,TimeSpan Total,long OutputBytes)
{
    public TimeSpan Validation => OutputProbe+TimelineCheck+AudioHashCheck+FullDecode;
}
