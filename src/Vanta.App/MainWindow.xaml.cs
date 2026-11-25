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
    private SourceStamp? sourceStamp;
    private Plan? activePlan;
    private bool applying,encoding;
    private string? completedOutput;
    private EncodeReport? completedReport;
    private sealed record PlanRow(Plan Plan,string Name,string Size,string Settings,string Savings);
    public MainWindow()
    {
        InitializeComponent();
        CodecBox.ItemsSource=PlanRules.Codecs;
        PresetBox.ItemsSource=PlanRules.Presets;
        ThreadsBox.ItemsSource=ThreadChoices.Create();ThreadsBox.SelectedIndex=0;
    }
    private void ClearResult()
    {
        completedOutput=null;completedReport=null;ResultPath.Text="";
        ResultFolderButton.IsEnabled=CopyResultButton.IsEnabled=false;
    }
    private void OnResultFolder(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||!System.IO.File.Exists(completedOutput))
        {Status.Text="The completed output is no longer available.";return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetDirectoryName(completedOutput)!){UseShellExecute=true});}
        catch(Exception ex){Status.Text=ex.Message;}
    }
    private void OnCopyResult(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||completedReport is null)return;
        try
        {
            var r=completedReport;
            Clipboard.SetText($"{completedOutput}\n{r.InputBytes/1e6:F2} MB → {r.OutputBytes/1e6:F2} MB · {1-r.OutputBytes/(double)r.InputBytes:P1} actual savings\n{r.Total.TotalSeconds:F1}s elapsed · {r.Validation.TotalSeconds:F1}s validation");
            Status.Text="Completed result copied.";
        }
        catch(Exception ex){Status.Text=ex.Message;}
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(current is not null){e.Cancel=true;current.Cancel();Status.Text="Canceling. Close after cleanup completes.";}
    }
    private async void OnOpen(object sender,RoutedEventArgs e)
    {
        if(encoding)return;
        var dlg=new OpenFileDialog{Multiselect=true,Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi|All files|*.*"};
        if(dlg.ShowDialog()!=true)return;
        if(dlg.FileNames.Length==1)await LoadAsync(dlg.FileName);
        else if(current is null)new BatchWindow(dlg.FileNames){Owner=this}.ShowDialog();
        else Status.Text="Finish or cancel analysis before opening a batch.";
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
    private async void OnDiagnostics(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        using var cts=new CancellationTokenSource();current=cts;
        encoding=true;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=EncodeButton.IsEnabled=false;
        Editor.IsEnabled=Plans.IsEnabled=RecommendationBox.IsEnabled=false;CancelButton.IsEnabled=true;
        BusyBar.IsIndeterminate=true;BusyBar.Visibility=Visibility.Visible;
        Status.Text="Testing listed hardware encoders on synthetic frames…";
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            var results=await EncoderDiagnostics.CheckAsync(resolved,EncoderDiagnostics.HardwareCandidates,cts.Token);
            Status.Text=string.Join(Environment.NewLine,results.Select(r=>$"{r.Encoder}: {r.State} — {r.Detail}"))+Environment.NewLine+"Diagnostic only. Hardware encoding is not enabled.";
        }
        catch(OperationCanceledException){Status.Text="Diagnostics canceled or timed out.";}
        catch(Exception ex){Status.Text=ex.Message;}
        finally
        {
            current=null;encoding=false;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=Plans.IsEnabled=true;
            Editor.IsEnabled=RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();
        }
    }
    private async Task LoadAsync(string path)
    {
        current?.Cancel();ClearResult();
        using var cts=new CancellationTokenSource();current=cts;
        media=null;tools=null;sourcePath=null;sourceStamp=null;RecommendationBox.IsEnabled=false;RecommendationBox.ItemsSource=null;TargetBox.Clear();SplitBox.IsChecked=false;activePlan=null;Plans.ItemsSource=null;Editor.IsEnabled=false;EncodeButton.IsEnabled=false;Source.Text="";Estimate.Text="";Evidence.Text="";Status.Text="Analyzing…";
        CancelButton.IsEnabled=true;BusyBar.IsIndeterminate=true;BusyBar.Visibility=Visibility.Visible;
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            var stamp=SourceStamp.Capture(path);
            var details=await Probe.InspectAsync(resolved.Ffprobe,path,cts.Token);
            stamp.RequireUnchanged(path);
            if(current!=cts)return;
            var m=details.Info;
            Source.Text=$"{System.IO.Path.GetFileName(path)} · {m.Width}×{m.Height} · {m.Fps:F2} fps · {m.Codec} · {m.Bytes/1e6:F1} MB";
            if(!details.Supported){Status.Text=details.Reason;return;}
            tools=resolved;media=m;sourcePath=path;sourceStamp=stamp;
            HeightChoicesBox.ItemsSource=QuickSettings.Heights(m);
            RateChoicesBox.ItemsSource=QuickSettings.Rates(m).Select(r=>(r/1000m).ToString("0.###",CultureInfo.InvariantCulture)).ToArray();
            CodecBox.ItemsSource=resolved.Codecs;
            RecommendationBox.ItemsSource=RecommendationChoices.Create(resolved.Codecs);
            RecommendationBox.SelectedIndex=0;
            Editor.IsEnabled=true;
            RebuildPlans();
        }
        catch(OperationCanceledException){if(current==cts)Status.Text="Analysis canceled.";}
        catch(Exception ex){if(current==cts)Status.Text=ex.Message;}
        finally{if(current==cts){current=null;RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();}}
    }
    private void OnRecommendation(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(current is null&&media is not null&&tools is not null)RebuildPlans();
    }
    private void RebuildPlans()
    {
        if(media is not MediaInfo m||tools is not Toolchain resolved)return;
        var preferred=(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec;
        TargetBox.Clear();SplitBox.IsChecked=false;
        var rows=Planner.Propose(m,resolved.Codecs,preferred).Select(p=>new PlanRow(p,p.Name,$"{p.EstimateBytes(m)/1e6:F1} MB",$"{p.Height}p · {(p.Codec=="libx265"?"HEVC":"H.264")} · {p.VideoBitsPerSecond/1000d:F0} kbps",$"{p.Savings(m):P1} budget savings")).ToArray();
        Plans.ItemsSource=rows;
        if(rows.Length>0)Plans.SelectedIndex=Math.Min(1,rows.Length-1);
        else
        {
            applying=true;
            try{CodecBox.SelectedItem=preferred??resolved.Codecs[0];PresetBox.SelectedItem="medium";HeightBox.Text=m.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text="";}
            finally{applying=false;}
            RefreshEstimate();
        }
        Status.Text=rows.Length<3?"Fewer than three positive bitrate-budget savings estimates. Custom settings are available.":"Select or edit a budget tier. Actual size and quality are not guaranteed.";
    }
    private void OnPlan(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(Plans.SelectedItem is PlanRow row)Apply(row.Plan);
    }
    private void Apply(Plan p)
    {
        applying=true;
        try{TargetBox.Clear();CodecBox.SelectedItem=p.Codec;PresetBox.SelectedItem=p.Preset;HeightBox.Text=p.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text=(p.VideoBitsPerSecond/1000d).ToString("0.###",CultureInfo.InvariantCulture);}
        finally{applying=false;}
        RefreshEstimate();
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
        if(current is not null||media is null||activePlan is null)return;
        if(QuickSettings.MoreCompression(activePlan,media) is Plan next)
        {
            Plans.SelectedIndex=-1;Apply(next);Status.Text="Lower video bitrate budget applied. Quality may decrease; no encoding started.";
        }
    }
    private void OnSettings(object sender,RoutedEventArgs e){if(!applying){if(Plans is not null)Plans.SelectedIndex=-1;if(TargetBox is not null)TargetBox.Clear();RefreshEstimate();}}
    private void RefreshEstimate()
    {
        activePlan=null;
        if(MoreCompressionButton is not null)MoreCompressionButton.IsEnabled=false;
        if(EncodeButton is null||media is null)return;
        EncodeButton.IsEnabled=false;
        try
        {
            if(!int.TryParse(HeightBox.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int h))throw new ArgumentException("Enter a valid height.");
            var p=new Plan("Custom",CodecBox.SelectedItem as string??"",h,PlanRules.ParseVideoKbps(RateBox.Text),0,PresetBox.SelectedItem as string??"",false);
            var v=CandidateEvaluation.Evaluate(p,media);
            activePlan=p;
            Estimate.Text=$"Bitrate budget {p.EstimateBytes(media)/1e6:F2} MB · {p.Savings(media):P1} budget savings";
            Evidence.Text=$"Pixel retention {v.PixelRetention:P1} · quality not measured · actual size may be much smaller or larger; not a limit";
            EncodeButton.IsEnabled=current is null&&!encoding;
            MoreCompressionButton.IsEnabled=EncodeButton.IsEnabled&&p.VideoBitsPerSecond>PlanRules.MinimumVideoRate;
        }
        catch(Exception ex){Estimate.Text=ex.Message;Evidence.Text="";}
    }
    private void OnTarget(object sender,RoutedEventArgs e)
    {
        if(media is null)return;
        try
        {
            if(!double.TryParse(TargetBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double mb))throw new ArgumentException("Enter target MB.");
            var targetText=TargetBox.Text;
            RateBox.Text=(PlanRules.TargetRate(media,mb*1e6)/1000d).ToString("0.###",CultureInfo.InvariantCulture);
            TargetBox.Text=targetText;
            Status.Text="Target applied as bitrate budget, not exact file size.";
        }
        catch(Exception ex){Status.Text=ex.Message;}
    }
    private void OnCancel(object sender,RoutedEventArgs e)=>current?.Cancel();
    private async void OnEncode(object sender,RoutedEventArgs e)
    {
        if(current is not null||activePlan is null||sourcePath is null||tools is null||sourceStamp is null)return;
        var p=activePlan;var input=sourcePath;var resolved=tools;var stamp=sourceStamp;
        try{stamp.RequireUnchanged(input);}catch(Exception ex){Status.Text=ex.Message;return;}
        bool split=SplitBox.IsChecked==true;
        var threads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        try{ThreadBudget.ValidateEncoder(threads,split);}catch(ArgumentException ex){Status.Text=ex.Message;return;}
        if(split&&(p.Codec!="libx264"||media?.Height!=p.Height||media?.Codec!="h264"||!string.Equals(System.IO.Path.GetExtension(input),".mp4",StringComparison.OrdinalIgnoreCase)))
        {Status.Text="Experimental split requires H.264 MP4 input, H.264 output, original height and CFR. Uncheck it for normal encoding.";return;}
        SaveFileDialog dlg;
        try
        {
            dlg=new SaveFileDialog{Filter="MP4 video|*.mp4",DefaultExt=".mp4",FileName=System.IO.Path.GetFileName(OutputNames.Suggest(input)),InitialDirectory=System.IO.Path.GetDirectoryName(input),OverwritePrompt=false};
            if(dlg.ShowDialog()!=true)return;
            if(System.IO.File.Exists(dlg.FileName)||System.IO.Directory.Exists(dlg.FileName))
            {Status.Text="Choose a new filename; VANTA does not overwrite existing files.";return;}
        }
        catch(Exception ex){Status.Text=ex.Message;return;}
        ClearResult();
        using var cts=new CancellationTokenSource();current=cts;encoding=true;
        OpenButton.IsEnabled=false;Editor.IsEnabled=false;Plans.IsEnabled=false;RecommendationBox.IsEnabled=false;EncodeButton.IsEnabled=false;CancelButton.IsEnabled=true;BusyBar.Visibility=Visibility.Visible;Status.Text="Encoding and validating…";
        try
        {
            var progress=new Progress<EncodeProgress>(x=>
            {
                if(current!=cts||cts.IsCancellationRequested)return;
                BusyBar.IsIndeterminate=x.Fraction is null;
                BusyBar.Value=x.DisplayPercent??0;
                Status.Text=x.DisplayPercent is int percent?$"{x.Stage} · {percent}%":x.Stage+"…";
            });
            var report=await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,dlg.FileName,p,cts.Token,progress,experimentalSplit:split,encoderThreads:threads,expectedSource:stamp);
            completedOutput=System.IO.Path.GetFullPath(dlg.FileName);completedReport=report;
            ResultPath.Text=completedOutput;ResultFolderButton.IsEnabled=CopyResultButton.IsEnabled=true;
            var bytes=report.OutputBytes;
            Status.Text=$"Validated output: {bytes/1e6:F2} MB · {1-bytes/(double)report.InputBytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (validation {report.Validation.TotalSeconds:F1}s)";
        }
        catch(OperationCanceledException){Status.Text="Canceled; no new output published.";}
        catch(Exception ex){Status.Text=ex.Message;}
        finally{current=null;encoding=false;OpenButton.IsEnabled=true;Editor.IsEnabled=true;Plans.IsEnabled=true;RecommendationBox.IsEnabled=true;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();}
    }
}