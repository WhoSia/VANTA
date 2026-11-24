namespace Vanta.Core;

// Manual per-file settings take precedence over the shared recommendation policy.
// Missing automatic tiers are not silently replaced; explicit overrides are validated.
public static class BatchPlanSelection
{
    public static Plan? Resolve(MediaInfo media,IReadOnlyCollection<string> available,string tier,string? preferredCodec,Plan? manual)
    {
        if(manual is not null)
        {
            PlanRules.Validate(manual,media);
            if(!available.Contains(manual.Codec))throw new ArgumentException("Selected manual encoder is no longer available.");
            return manual;
        }
        return Planner.Propose(media,available,preferredCodec).FirstOrDefault(p=>p.Name==tier);
    }
}