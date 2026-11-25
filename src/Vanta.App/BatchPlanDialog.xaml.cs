using System.Globalization;
using System.Windows;
using Vanta.Core;
namespace Vanta.App;
public partial class BatchPlanDialog:Window
{
    private readonly MediaInfo media;
    private readonly IReadOnlyCollection<string> available;
    private bool initialized;
    private Plan? candidate;
    public Plan? Result {get;private set;}
    private readonly bool korean;
    private string T(string en,string ko)=>UiLanguage.Text(korean,en,ko);
    public BatchPlanDialog(MediaInfo media,IReadOnlyCollection<string> available,Plan? seed,bool korean=false)
    {
        this.media=media;this.available=available;this.korean=korean;
        InitializeComponent();UiLanguage.Apply(this,korean);
        SourceInfo.Text=$"{media.Width}×{media.Height} · {media.Fps:F2} fps · {media.Bytes/1e6:F2} MB";
        CodecBox.ItemsSource=available;
        CodecBox.SelectedItem=seed?.Codec??available.FirstOrDefault();
        UiLanguage.SetPresets(PresetBox,korean);
        PresetBox.SelectedValue=seed?.Preset??"medium";
        HeightBox.Text=(seed?.Height??media.Height).ToString(CultureInfo.InvariantCulture);
        RateBox.Text=seed is null?"":(seed.VideoBitsPerSecond/1000d).ToString("0.###",CultureInfo.InvariantCulture);
        HeightChoicesBox.ItemsSource=QuickSettings.Heights(media);
        RateChoicesBox.ItemsSource=QuickSettings.Rates(media).Select(r=>(r/1000m).ToString("0.###",CultureInfo.InvariantCulture)).ToArray();
        initialized=true;Evaluate();
    }
    private void OnQuickHeight(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(HeightChoicesBox.SelectedItem is int h){HeightBox.Text=h.ToString(CultureInfo.InvariantCulture);HeightChoicesBox.SelectedIndex=-1;}
    }
    private void OnQuickRate(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(RateChoicesBox.SelectedItem is string rate){RateBox.Text=rate;RateChoicesBox.SelectedIndex=-1;}
    }
    private void OnMoreCompression(object sender,RoutedEventArgs e)
    {
        if(candidate is not null&&QuickSettings.MoreCompression(candidate,media) is Plan next)
            RateBox.Text=(next.VideoBitsPerSecond/1000m).ToString("0.###",CultureInfo.InvariantCulture);
    }
    private void OnChanged(object sender,RoutedEventArgs e){if(initialized){TargetBox.Clear();Evaluate();}}
    private void Evaluate()
    {
        candidate=null;ApplyButton.IsEnabled=MoreCompressionButton.IsEnabled=false;
        try
        {
            if(!int.TryParse(HeightBox.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var height))
                throw new ArgumentException("Enter a valid height.");
            var plan=new Plan("Custom",CodecBox.SelectedItem as string??"",height,PlanRules.ParseVideoKbps(RateBox.Text),0,PresetBox.SelectedValue as string??"",false);
            candidate=BatchPlanSelection.Resolve(media,available,"Balanced",null,plan);
            Estimate.Text=T($"Bitrate budget: {plan.EstimateBytes(media)/1e6:F2} MB · {plan.Savings(media):P1} budget savings",$"용량 예산: {plan.EstimateBytes(media)/1e6:F2} MB · 예산 절감률 {plan.Savings(media):P1}");
            ApplyButton.IsEnabled=true;MoreCompressionButton.IsEnabled=plan.VideoBitsPerSecond>PlanRules.MinimumVideoRate;
        }
        catch(Exception ex){Estimate.Text=ex.Message;}
    }
    private void OnTarget(object sender,RoutedEventArgs e)
    {
        try
        {
            if(!double.TryParse(TargetBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double mb))throw new ArgumentException("Enter target MB.");
            var targetText=TargetBox.Text;
            RateBox.Text=(PlanRules.TargetRate(media,mb*1e6)/1000d).ToString("0.###",CultureInfo.InvariantCulture);
            TargetBox.Text=targetText;
            Evaluate();
        }
        catch(Exception ex){Estimate.Text=ex.Message;}
    }
    private void OnApply(object sender,RoutedEventArgs e)
    {
        Evaluate();
        if(candidate is null)return;
        Result=candidate;DialogResult=true;
    }
}