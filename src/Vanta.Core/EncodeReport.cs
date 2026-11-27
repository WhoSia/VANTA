namespace Vanta.Core;

// Per-job wall time. No persistence, background sampling, or external telemetry.
public sealed record EncodeReport(TimeSpan Preparation,TimeSpan Encoding,TimeSpan OutputProbe,TimeSpan TimelineCheck,TimeSpan AudioHashCheck,TimeSpan FullDecode,TimeSpan Publication,TimeSpan Total,long OutputBytes,long InputBytes)
{
    // Separate stage durations may overlap in research mode; report actual wall time.
    public TimeSpan? ConcurrentTimelineDecode {get;init;}
    // In single-decode research, TimelineCheck includes strict full output decode.
    public bool SingleDecodeValidation {get;init;}
    // Requested encoder threads, not actual process threads or CPU affinity.
    public int? RequestedEncoderThreads {get;init;}
    public Plan? AppliedPlan {get;init;}
    public string? HardwareEncoder {get;init;}
    public bool GpuResident {get;init;}
    // Explicit device qualification is included in Preparation and Total.
    public TimeSpan HardwareQualification {get;init;}
    // When set, per-track payload and timestamp checks are folded into TimelineCheck.
    public int? InterleavedAudioPeakPending {get;init;}
    // True only after a resource-limit refusal and complete per-track retry.
    public bool InterleavedAudioFallbackUsed {get;init;}
    // Wall time spent waiting for the research validation gate; included in Total, not Validation.
    public TimeSpan ValidationAdmissionWait {get;init;}
    public TimeSpan Validation => OutputProbe+AudioHashCheck+(ConcurrentTimelineDecode??(TimelineCheck+FullDecode));
}
