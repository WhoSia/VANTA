using System.Windows;
using System.Globalization;
using System.ComponentModel;
using Vanta.Core;
using Microsoft.Win32;
namespace Vanta.App;
public partial class MainWindow : Window
{
    private CancellationTokenSource? current;
    private MediaInfo? media;
    private Toolchain? tools;
    private string? sourcePath;
    private Plan? activePlan;
    private bool applying,encoding;
    private sealed record PlanRow(Plan Plan,string Label);
    public MainWindow()
    {
        InitializeComponent();
        CodecBox.ItemsSource=PlanRules.Codecs;
        PresetBox.ItemsSource=PlanRules.Presets;
        ThreadsBox.ItemsSource=ThreadChoices.Create();ThreadsBox.SelectedIndex=0;
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(current is not null){e.Cancel=true;current.Cancel();Status.Text="Canceling. Close after cleanup completes.";}
    }
    private async void OnOpen(object sender,RoutedEventArgs e)
    {
        if(encoding)return;
        var dlg=new OpenFileDialog{Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi|All files|*.*"};
        if(dlg.ShowDialog()==true) await LoadAsync(dlg.FileName);
    }
    private async void OnDrop(object sender,DragEventArgs e)
    {
        if(encoding||e.Data.GetData(DataFormats.FileDrop) is not string[] paths)return;
        if(paths.Length==1)await LoadAsync(paths[0]);
        else if(paths.Length>1&&current is null)new BatchWindow(paths){Owner=this}.ShowDialog();
    }
    private void OnBatch(object sender,RoutedEventArgs e)
    {
        if(current is null)new BatchWindow{Owner=this}.ShowDialog();
    }
    private async Task LoadAsync(string path)
    {
        current?.Cancel();
        using var cts=new CancellationTokenSource();current=cts;
        media=null;tools=null;sourcePath=null;SplitBox.IsChecked=false;activePlan=null;Plans.ItemsSource=null;Editor.IsEnabled=false;EncodeButton.IsEnabled=false;Source.Text="";Estimate.Text="";Evidence.Text="";Status.Text="Analyzing…";
        CancelButton.IsEnabled=true;BusyBar.IsIndeterminate=true;BusyBar.Visibility=Visibility.Visible;
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            var details=await Probe.InspectAsync(resolved.Ffprobe,path,cts.Token);
            if(current!=cts)return;
            var m=details.Info;
            Source.Text=$"{System.IO.Path.GetFileName(path)} · {m.Width}×{m.Height} · {m.Fps:F2} fps · {m.Codec} · {m.Bytes/1e6:F1} MB";
            if(!details.Supported){Status.Text=details.Reason;return;}
            tools=resolved;media=m;sourcePath=path;
            CodecBox.ItemsSource=resolved.Codecs;
            var rows=Planner.Propose(m).Select(p=>resolved.Codecs.Contains(p.Codec)?p:p with {Codec=resolved.Codecs[0]}).Select(p=>new PlanRow(p,$"{p.Name} · {p.Height}p · {p.EstimateBytes(m)/1e6:F1} MB ({p.Savings(m):P1} saved)")).ToArray();
            Plans.ItemsSource=rows;
            Editor.IsEnabled=true;
            if(rows.Length>0)Plans.SelectedIndex=Math.Min(1,rows.Length-1);
            else
            {
                applying=true;
                try{CodecBox.SelectedIndex=0;PresetBox.SelectedItem="medium";HeightBox.Text=m.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text="";}
                finally{applying=false;}
                RefreshEstimate();
            }
            Status.Text=rows.Length<3?"Fewer than three feasible savings estimates. Custom settings are available.":"Select or edit a plan. Actual output size may differ substantially.";
        }
        catch(OperationCanceledException){if(current==cts)Status.Text="Analysis canceled.";}
        catch(Exception ex){if(current==cts)Status.Text=ex.Message;}
        finally{if(current==cts){current=null;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();}}
    }
    private void OnPlan(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(Plans.SelectedItem is PlanRow row)Apply(row.Plan);
    }
    private void Apply(Plan p)
    {
        applying=true;
        try{CodecBox.SelectedItem=p.Codec;PresetBox.SelectedItem=p.Preset;HeightBox.Text=p.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text=(p.VideoBitsPerSecond/1000d).ToString("0.###",CultureInfo.InvariantCulture);}
        finally{applying=false;}
        RefreshEstimate();
    }
    private void OnSettings(object sender,RoutedEventArgs e){if(!applying){if(Plans is not null)Plans.SelectedIndex=-1;RefreshEstimate();}}
    private void RefreshEstimate()
    {
        activePlan=null;
        if(EncodeButton is null||media is null)return;
        EncodeButton.IsEnabled=false;
        try
        {
            if(!int.TryParse(HeightBox.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int h)||!decimal.TryParse(RateBox.Text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal kb)||kb<64||kb>100000)throw new ArgumentException("Enter a valid height and bitrate.");
            var p=new Plan("Custom",CodecBox.SelectedItem as string??"",h,(long)(kb*1000),0,PresetBox.SelectedItem as string??"",false);
            var v=CandidateEvaluation.Evaluate(p,media);
            activePlan=p;
            Estimate.Text=$"Bitrate budget {p.EstimateBytes(media)/1e6:F2} MB · {p.Savings(media):P1} saved";
            Evidence.Text=$"Pixel retention {v.PixelRetention:P1} · quality not measured · actual size may be much smaller or larger; not a limit";
            EncodeButton.IsEnabled=current is null&&!encoding;
        }
        catch(Exception ex){Estimate.Text=ex.Message;Evidence.Text="";}
    }
    private void OnTarget(object sender,RoutedEventArgs e)
    {
        if(media is null)return;
        try
        {
            if(!double.TryParse(TargetBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double mb))throw new ArgumentException("Enter target MB.");
            RateBox.Text=(PlanRules.TargetRate(media,mb*1e6)/1000d).ToString("0.###",CultureInfo.InvariantCulture);
            Status.Text="Target applied as bitrate budget, not exact file size.";
        }
        catch(Exception ex){Status.Text=ex.Message;}
    }
    private void OnCancel(object sender,RoutedEventArgs e)=>current?.Cancel();
    private async void OnEncode(object sender,RoutedEventArgs e)
    {
        if(current is not null||activePlan is null||sourcePath is null||tools is null)return;
        var p=activePlan;var input=sourcePath;var resolved=tools;
        bool split=SplitBox.IsChecked==true;
        var threads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        try{ThreadBudget.ValidateEncoder(threads,split);}catch(ArgumentException ex){Status.Text=ex.Message;return;}
        if(split&&(p.Codec!="libx264"||media?.Height!=p.Height||media?.Codec!="h264"||!string.Equals(System.IO.Path.GetExtension(input),".mp4",StringComparison.OrdinalIgnoreCase)))
        {Status.Text="Experimental split requires H.264 MP4 input, H.264 output, original height and CFR. Uncheck it for normal encoding.";return;}
        var dlg=new SaveFileDialog{Filter="MP4 video|*.mp4",DefaultExt=".mp4",FileName=System.IO.Path.GetFileName(OutputNames.Suggest(input)),InitialDirectory=System.IO.Path.GetDirectoryName(input),OverwritePrompt=true};
        if(dlg.ShowDialog()!=true)return;
        using var cts=new CancellationTokenSource();current=cts;encoding=true;
        OpenButton.IsEnabled=false;Editor.IsEnabled=false;Plans.IsEnabled=false;EncodeButton.IsEnabled=false;CancelButton.IsEnabled=true;BusyBar.Visibility=Visibility.Visible;Status.Text="Encoding and validating…";
        try
        {
            var progress=new Progress<EncodeProgress>(x=>
            {
                if(current!=cts||cts.IsCancellationRequested)return;
                BusyBar.IsIndeterminate=x.Fraction is null;
                BusyBar.Value=(x.Fraction??0)*100;
                Status.Text=x.Fraction is double fraction?$"{x.Stage} · {fraction:P0}":x.Stage+"…";
            });
            var report=await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,dlg.FileName,p,cts.Token,progress,experimentalSplit:split,encoderThreads:threads);
            var bytes=report.OutputBytes;
            Status.Text=$"Validated output: {bytes/1e6:F2} MB · {1-bytes/(double)media!.Bytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (validation {report.Validation.TotalSeconds:F1}s)";
        }
        catch(OperationCanceledException){Status.Text="Canceled; no new output published.";}
        catch(Exception ex){Status.Text=ex.Message;}
        finally{current=null;encoding=false;OpenButton.IsEnabled=true;Editor.IsEnabled=true;Plans.IsEnabled=true;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();}
    }
}