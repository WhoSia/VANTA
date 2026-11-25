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
    private bool korean,languageReady;
    private readonly bool persistLanguage;
    private string statusEn="Open an 8-bit SDR video with AAC audio or no audio.",statusKo="AAC 오디오가 있거나 무음인 8비트 SDR 영상을 열어 주세요.";
    private string T(string en,string ko)=>UiLanguage.Text(korean,en,ko);
    public MainWindow():this(UiLanguage.Load(),true){}
    public MainWindow(bool korean,bool persistLanguage=false)
    {
        this.korean=korean;this.persistLanguage=persistLanguage;
        InitializeComponent();UiLanguage.Apply(this,korean);
        CodecBox.ItemsSource=PlanRules.Codecs;
        PresetBox.ItemsSource=PlanRules.Presets;
        ThreadsBox.ItemsSource=ThreadChoices.Create(korean);ThreadsBox.SelectedIndex=0;
        LanguageBox.SelectedIndex=korean?1:0;languageReady=true;SetStatus(statusEn,statusKo);
    }
    private void SetStatus(string en,string? ko=null){statusEn=en;statusKo=ko??en;Status.Text=T(statusEn,statusKo);}
    private void OnLanguage(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!languageReady||current is not null)return;
        korean=LanguageBox.SelectedIndex==1;UiLanguage.Apply(this,korean);
        var selected=Plans.SelectedIndex;
        var previousPlans=Plans.ItemsSource is IEnumerable<PlanRow> rows?rows.Select(r=>r.Plan).ToArray():[];
        var previousCodec=(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec;
        var previousThreads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        applying=true;
        try
        {
            if(media is MediaInfo m&&tools is Toolchain resolved)
            {
                RecommendationBox.ItemsSource=RecommendationChoices.Create(resolved.Codecs,korean);
                RecommendationBox.SelectedItem=RecommendationBox.Items.Cast<RecommendationChoice>().First(c=>c.Codec==previousCodec);
                Plans.ItemsSource=previousPlans.Select(p=>MakeRow(p,m)).ToArray();Plans.SelectedIndex=selected;
            }
            ThreadsBox.ItemsSource=ThreadChoices.Create(korean);
            ThreadsBox.SelectedItem=ThreadsBox.Items.Cast<ThreadChoice>().First(c=>c.Count==previousThreads);
        }
        finally{applying=false;}
        RefreshEstimate();Status.Text=T(statusEn,statusKo);
        if(persistLanguage){try{UiLanguage.Save(korean);}catch(Exception ex){SetStatus("Could not save language: "+ex.Message,"언어 설정을 저장하지 못했습니다: "+ex.Message);}}
    }
    private PlanRow MakeRow(Plan p,MediaInfo m)=>new(p,UiLanguage.Tier(korean,p.Name),$"{p.EstimateBytes(m)/1e6:F1} MB",$"{p.Height}p · {(p.Codec=="libx265"?"HEVC":"H.264")} · {p.VideoBitsPerSecond/1000d:F0} kbps",T($"{p.Savings(m):P1} budget savings",$"예산 절감률 {p.Savings(m):P1}"));
    private void ClearResult()
    {
        completedOutput=null;completedReport=null;ResultPath.Text="";
        ResultFolderButton.IsEnabled=CopyResultButton.IsEnabled=false;
    }
    private void OnResultFolder(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||!System.IO.File.Exists(completedOutput))
        {SetStatus("The completed output is no longer available.","완료된 출력 파일을 찾을 수 없습니다.");return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetDirectoryName(completedOutput)!){UseShellExecute=true});}
        catch(Exception ex){SetStatus(ex.Message);}
    }
    private void OnCopyResult(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||completedReport is null)return;
        try
        {
            var r=completedReport;
            Clipboard.SetText($"{completedOutput}\n{r.InputBytes/1e6:F2} MB → {r.OutputBytes/1e6:F2} MB · {1-r.OutputBytes/(double)r.InputBytes:P1} actual savings\n{r.Total.TotalSeconds:F1}s elapsed · {r.Validation.TotalSeconds:F1}s validation");
            SetStatus("Completed result copied.","완료 결과를 복사했습니다.");
        }
        catch(Exception ex){SetStatus(ex.Message);}
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(current is not null){e.Cancel=true;current.Cancel();SetStatus("Canceling. Close after cleanup completes.","취소 중입니다. 정리가 끝난 후 닫아 주세요.");}
    }
    private async void OnOpen(object sender,RoutedEventArgs e)
    {
        if(encoding)return;
        var dlg=new OpenFileDialog{Multiselect=true,Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi|All files|*.*"};
        if(dlg.ShowDialog()!=true)return;
        if(dlg.FileNames.Length==1)await LoadAsync(dlg.FileName);
        else if(current is null)new BatchWindow(dlg.FileNames,korean){Owner=this}.ShowDialog();
        else SetStatus("Finish or cancel analysis before opening a batch.","일괄 압축을 열기 전에 분석을 완료하거나 취소해 주세요.");
    }
    private async void OnDrop(object sender,DragEventArgs e)
    {
        if(encoding||e.Data.GetData(DataFormats.FileDrop) is not string[] paths)return;
        if(paths.Length==1)await LoadAsync(paths[0]);
        else if(paths.Length>1&&current is null)new BatchWindow(paths,korean){Owner=this}.ShowDialog();
    }
    private void OnBatch(object sender,RoutedEventArgs e)
    {
        if(current is null)new BatchWindow(korean:korean){Owner=this}.ShowDialog();
    }
    private async void OnDiagnostics(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        using var cts=new CancellationTokenSource();current=cts;LanguageBox.IsEnabled=false;
        encoding=true;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=EncodeButton.IsEnabled=false;
        Editor.IsEnabled=Plans.IsEnabled=RecommendationBox.IsEnabled=false;CancelButton.IsEnabled=true;
        BusyBar.IsIndeterminate=true;BusyBar.Visibility=Visibility.Visible;
        SetStatus("Testing listed hardware encoders on synthetic frames…","시험 영상으로 하드웨어 인코더를 검사 중입니다…");
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            var results=await EncoderDiagnostics.CheckAsync(resolved,EncoderDiagnostics.HardwareCandidates,cts.Token);
            Status.Text=string.Join(Environment.NewLine,results.Select(r=>$"{r.Encoder}: {r.State} — {r.Detail}"))+Environment.NewLine+"Diagnostic only. Hardware encoding is not enabled.";
        }
        catch(OperationCanceledException){SetStatus("Diagnostics canceled or timed out.","진단이 취소되었거나 제한 시간을 초과했습니다.");}
        catch(Exception ex){SetStatus(ex.Message);}
        finally
        {
            current=null;encoding=false;LanguageBox.IsEnabled=true;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=Plans.IsEnabled=true;
            Editor.IsEnabled=RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();
        }
    }
    private async Task LoadAsync(string path)
    {
        current?.Cancel();ClearResult();
        using var cts=new CancellationTokenSource();current=cts;LanguageBox.IsEnabled=false;
        media=null;tools=null;sourcePath=null;sourceStamp=null;RecommendationBox.IsEnabled=false;RecommendationBox.ItemsSource=null;TargetBox.Clear();SplitBox.IsChecked=false;activePlan=null;Plans.ItemsSource=null;Editor.IsEnabled=false;EncodeButton.IsEnabled=false;Source.Text="";Estimate.Text="";Evidence.Text="";SetStatus("Analyzing…","분석 중…");
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
            if(!details.Supported){SetStatus(details.Reason);return;}
            tools=resolved;media=m;sourcePath=path;sourceStamp=stamp;
            HeightChoicesBox.ItemsSource=QuickSettings.Heights(m);
            RateChoicesBox.ItemsSource=QuickSettings.Rates(m).Select(r=>(r/1000m).ToString("0.###",CultureInfo.InvariantCulture)).ToArray();
            CodecBox.ItemsSource=resolved.Codecs;
            RecommendationBox.ItemsSource=RecommendationChoices.Create(resolved.Codecs,korean);
            RecommendationBox.SelectedIndex=0;
            Editor.IsEnabled=true;
            RebuildPlans();
        }
        catch(OperationCanceledException){if(current==cts)SetStatus("Analysis canceled.","분석이 취소되었습니다.");}
        catch(Exception ex){if(current==cts)SetStatus(ex.Message);}
        finally{if(current==cts){current=null;LanguageBox.IsEnabled=true;RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();}}
    }
    private void OnRecommendation(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!applying&&current is null&&media is not null&&tools is not null)RebuildPlans();
    }
    private void RebuildPlans()
    {
        if(media is not MediaInfo m||tools is not Toolchain resolved)return;
        var preferred=(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec;
        TargetBox.Clear();SplitBox.IsChecked=false;
        var rows=Planner.Propose(m,resolved.Codecs,preferred).Select(p=>MakeRow(p,m)).ToArray();
        Plans.ItemsSource=rows;
        if(rows.Length>0)Plans.SelectedIndex=Math.Min(1,rows.Length-1);
        else
        {
            applying=true;
            try{CodecBox.SelectedItem=preferred??resolved.Codecs[0];PresetBox.SelectedItem="medium";HeightBox.Text=m.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text="";}
            finally{applying=false;}
            RefreshEstimate();
        }
        if(rows.Length<3)SetStatus("Fewer than three positive bitrate-budget savings estimates. Custom settings are available.","용량 절감 예산이 가능한 플랜이 3개 미만입니다. 직접 설정할 수 있습니다.");
        else SetStatus("Select or edit a budget tier. Actual size and quality are not guaranteed.","플랜을 선택하거나 수정하세요. 실제 용량과 화질은 보장되지 않습니다.");
    }
    private void OnPlan(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!applying&&Plans.SelectedItem is PlanRow row)Apply(row.Plan);
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
            Plans.SelectedIndex=-1;Apply(next);SetStatus("Lower video bitrate budget applied. Quality may decrease; no encoding started.","영상 비트레이트 예산을 낮췄습니다. 화질이 낮아질 수 있으며 압축은 시작되지 않았습니다.");
        }
    }
    private void OnSettings(object sender,RoutedEventArgs e){if(!applying){if(Plans is not null)Plans.SelectedIndex=-1;if(TargetBox is not null)TargetBox.Clear();RefreshEstimate();}}
    private void RefreshEstimate()
    {
        activePlan=null;
        if(MoreCompressionButton is not null)MoreCompressionButton.IsEnabled=false;
        if(EncodeButton is null||MoreCompressionButton is null||media is null)return;
        EncodeButton.IsEnabled=false;
        try
        {
            if(!int.TryParse(HeightBox.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int h))throw new ArgumentException("Enter a valid height.");
            var p=new Plan("Custom",CodecBox.SelectedItem as string??"",h,PlanRules.ParseVideoKbps(RateBox.Text),0,PresetBox.SelectedItem as string??"",false);
            var v=CandidateEvaluation.Evaluate(p,media);
            activePlan=p;
            Estimate.Text=T($"Bitrate budget {p.EstimateBytes(media)/1e6:F2} MB · {p.Savings(media):P1} budget savings",$"용량 예산 {p.EstimateBytes(media)/1e6:F2} MB · 예산 절감률 {p.Savings(media):P1}");
            Evidence.Text=T($"Pixel retention {v.PixelRetention:P1} · quality not measured · actual size may be much smaller or larger; not a limit",$"픽셀 유지 비율 {v.PixelRetention:P1} · 화질 미측정 · 실제 용량은 예산과 다를 수 있음");
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
            SetStatus("Target applied as bitrate budget, not exact file size.","목표 용량을 비트레이트 예산으로 적용했습니다. 정확한 용량을 보장하지 않습니다.");
        }
        catch(Exception ex){SetStatus(ex.Message);}
    }
    private void OnCancel(object sender,RoutedEventArgs e)=>current?.Cancel();
    private async void OnEncode(object sender,RoutedEventArgs e)
    {
        if(current is not null||activePlan is null||sourcePath is null||tools is null||sourceStamp is null)return;
        var p=activePlan;var input=sourcePath;var resolved=tools;var stamp=sourceStamp;
        try{stamp.RequireUnchanged(input);}catch(Exception ex){SetStatus(ex.Message);return;}
        bool split=SplitBox.IsChecked==true;
        var threads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        try{ThreadBudget.ValidateEncoder(threads,split);}catch(ArgumentException ex){SetStatus(ex.Message);return;}
        if(split&&(p.Codec!="libx264"||media?.Height!=p.Height||media?.Codec!="h264"||!string.Equals(System.IO.Path.GetExtension(input),".mp4",StringComparison.OrdinalIgnoreCase)))
        {SetStatus("Experimental split requires H.264 MP4 input, H.264 output, original height and CFR. Uncheck it for normal encoding.","분할 압축에는 H.264 MP4 입력과 H.264 출력, 원본 높이, 고정 프레임률이 필요합니다. 일반 압축은 체크를 해제하세요.");return;}
        SaveFileDialog dlg;
        try
        {
            dlg=new SaveFileDialog{Filter="MP4 video|*.mp4",DefaultExt=".mp4",FileName=System.IO.Path.GetFileName(OutputNames.Suggest(input)),InitialDirectory=System.IO.Path.GetDirectoryName(input),OverwritePrompt=false};
            if(dlg.ShowDialog()!=true)return;
            if(System.IO.File.Exists(dlg.FileName)||System.IO.Directory.Exists(dlg.FileName))
            {SetStatus("Choose a new filename; VANTA does not overwrite existing files.","새 파일 이름을 선택해 주세요. 기존 파일은 덮어쓰지 않습니다.");return;}
        }
        catch(Exception ex){SetStatus(ex.Message);return;}
        ClearResult();
        using var cts=new CancellationTokenSource();current=cts;encoding=true;LanguageBox.IsEnabled=false;
        OpenButton.IsEnabled=false;Editor.IsEnabled=false;Plans.IsEnabled=false;RecommendationBox.IsEnabled=false;EncodeButton.IsEnabled=false;CancelButton.IsEnabled=true;BusyBar.Visibility=Visibility.Visible;SetStatus("Encoding and validating…","압축 및 검증 중…");
        try
        {
            var progress=new Progress<EncodeProgress>(x=>
            {
                if(current!=cts||cts.IsCancellationRequested)return;
                BusyBar.IsIndeterminate=x.Fraction is null;
                BusyBar.Value=x.DisplayPercent??0;
                var suffix=x.DisplayPercent is int percent?$" · {percent}%":"…";SetStatus(x.Stage+suffix,UiLanguage.Stage(true,x.Stage)+suffix);
            });
            var report=await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,dlg.FileName,p,cts.Token,progress,experimentalSplit:split,encoderThreads:threads,expectedSource:stamp);
            completedOutput=System.IO.Path.GetFullPath(dlg.FileName);completedReport=report;
            ResultPath.Text=completedOutput;ResultFolderButton.IsEnabled=CopyResultButton.IsEnabled=true;
            var bytes=report.OutputBytes;
            SetStatus($"Validated output: {bytes/1e6:F2} MB · {1-bytes/(double)report.InputBytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (validation {report.Validation.TotalSeconds:F1}s)",$"검증 완료: {bytes/1e6:F2} MB · 실제 절감률 {1-bytes/(double)report.InputBytes:P1} · {report.Total.TotalSeconds:F1}초 (검증 {report.Validation.TotalSeconds:F1}초)");
        }
        catch(OperationCanceledException){SetStatus("Canceled; no new output published.","취소되었습니다. 새 출력 파일은 저장되지 않았습니다.");}
        catch(Exception ex){SetStatus(ex.Message);}
        finally{current=null;encoding=false;LanguageBox.IsEnabled=true;OpenButton.IsEnabled=true;Editor.IsEnabled=true;Plans.IsEnabled=true;RecommendationBox.IsEnabled=true;CancelButton.IsEnabled=false;BusyBar.Visibility=Visibility.Collapsed;RefreshEstimate();}
    }
}