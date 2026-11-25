namespace Vanta.Core;

// Manual per-file settings take precedence over the shared recommendation policy.
// Missing automatic tiers are not silently replaced; explicit overrides are validated.
public static class BatchPlanSelection
{
    public static Plan? Resolve(MediaInfo media,IReadOnlyCollection<string> available,string tier,string? preferredCodec,Plan? manual,string sharedPreset="medium")
    {
        if(manual is not null)
        {
            PlanRules.Validate(manual,media);
            if(!available.Contains(manual.Codec))throw new ArgumentException("Selected manual encoder is no longer available.");
            return manual;
        }
        if(!PlanRules.Presets.Contains(sharedPreset))throw new ArgumentException("Invalid shared preset.");
        var plan=Planner.Propose(media,available,preferredCodec).FirstOrDefault(p=>p.Name==tier);
        return plan is null?null:plan with {Preset=sharedPreset};
    }
}