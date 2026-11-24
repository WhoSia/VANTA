namespace Vanta.App;
internal sealed record RecommendationChoice(string? Codec,string Label)
{
    public override string ToString()=>Label;
}
internal static class RecommendationChoices
{
    public static IReadOnlyList<RecommendationChoice> Create(IReadOnlyCollection<string>? available=null)
    {
        var result=new List<RecommendationChoice>{new(null,"Auto (prefer HEVC)")};
        if(available is null||available.Contains("libx264"))result.Add(new("libx264","H.264"));
        if(available is null||available.Contains("libx265"))result.Add(new("libx265","HEVC"));
        return result;
    }
}