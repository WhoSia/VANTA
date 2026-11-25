using Vanta.Core;
namespace Vanta.App;
internal sealed record ThreadChoice(int? Count,string Label)
{
    public override string ToString()=>Label;
}
internal static class ThreadChoices
{
    public static IReadOnlyList<ThreadChoice> Create(bool korean=false)
    {
        var result=new List<ThreadChoice>{new(null,UiLanguage.Text(korean,"Auto (encoder default)","자동 (인코더 기본값)"))};
        result.AddRange(ThreadBudget.Choices(Environment.ProcessorCount).Select(n=>new ThreadChoice(n,n.ToString())));
        return result;
    }
}
