using Vanta.Core;
namespace Vanta.App;
internal sealed record ThreadChoice(int? Count,string Label)
{
    public override string ToString()=>Label;
}
internal static class ThreadChoices
{
    public static IReadOnlyList<ThreadChoice> Create()
    {
        var result=new List<ThreadChoice>{new(null,"Auto (encoder default)")};
        result.AddRange(ThreadBudget.Choices(Environment.ProcessorCount).Select(n=>new ThreadChoice(n,n.ToString())));
        return result;
    }
}
