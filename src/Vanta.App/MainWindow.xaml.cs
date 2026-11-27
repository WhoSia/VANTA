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
    private string? appliedTarget;
    private bool applying,encoding;
    private string? completedOutput;
    private readonly System.Diagnostics.Stopwatch jobClock=new();
    private readonly System.Windows.Threading.DispatcherTimer etaTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    private EncodingEta? eta;
    private EncodeProgress? jobProgress;
    private sealed record TimingKey(string Path,SourceStamp Stamp,Plan Plan,int? Threads,bool Split,string Ffmpeg,string Ffprobe);
    private readonly Dictionary<TimingKey,EncodeReport> timings=new();
    private TimeSpan? previousTail,validationStarted;
    private readonly TimingHistory timingHistory=new();
    private readonly TimingHistory batchSessionHistory=new();
    private TimingKey? CurrentTimingKey(Plan p)=>sourcePath is not null&&sourceStamp is not null&&tools is not null
        ?new(sourcePath,sourceStamp,p with{Name=""},(ThreadsBox.SelectedItem as ThreadChoice)?.Count,SplitBox.IsChecked==true,tools.Ffmpeg,tools.Ffprobe):null;
    private static string DurationText(TimeSpan value)
    {
        long seconds=(long)Math.Ceiling(Math.Max(0,value.TotalSeconds));
        return seconds>=3600?$"{seconds/3600}:{seconds%3600/60:00}:{seconds%60:00}":$"{seconds/60:00}:{seconds%60:00}";
    }
    private void StartJobProgress(bool split,string stage="Preparing")
    {
        if(HardwareBenchButton is not null)HardwareBenchButton.IsEnabled=false;
        jobClock.Restart();eta=split?null:new EncodingEta(media?.Seconds);jobProgress=new(stage,null);validationStarted=null;previousTail=null;
        ProgressCard.Visibility=Visibility.Visible;etaTimer.Start();RefreshJobProgress();
    }
    private void RefreshJobProgress()
    {
        if(jobProgress is not EncodeProgress p)return;
        ProgressStage.Text=p.Stage=="Validating"?UiLanguage.Verification(korean,p.Verification):UiLanguage.Stage(korean,p.Stage);
        ProgressPercent.Text=p.DisplayPercentText??"";
        BusyBar.IsIndeterminate=p.DisplayPercent is null;BusyBar.Value=p.DisplayPercent==100?100:p.Fraction is double fraction&&double.IsFinite(fraction)?Math.Clamp(fraction*100,0,99):0;
        ElapsedText.Text=T("Elapsed: ","경과: ")+DurationText(jobClock.Elapsed);
        ThroughputText.Text=current?.IsCancellationRequested!=true&&p.Stage=="Encoding"&&eta?.RealtimeRate(jobClock.Elapsed) is double rate?UiLanguage.Rate(korean,rate):"";
        EtaText.Text=current?.IsCancellationRequested==true?T("Canceling…","취소 중…"):
            p.Stage=="Validating"?(validationStarted is TimeSpan started&&CompletionReference.ValidationRemaining(previousTail,jobClock.Elapsed-started) is TimeSpan tail
                ?T("Remaining ≈ ","예상 잔여 ≈ ")+DurationText(tail)+T(" · previous run reference"," · 이전 실행 참고")
                :T("Verification · remaining time unknown","검증 중 · 남은 시간 미정")):
            p.Stage=="Complete"?T("Verified","검증 완료"):
            p.Stage is "Analyzing" or "Diagnostics" or "Qualifying hardware"?T("Remaining time unknown","남은 시간 미정"):
            eta is null?T("Remaining time unknown for split encoding","분할 압축 · 남은 시간 미정"):
            eta?.Remaining(jobClock.Elapsed) is TimeSpan remaining
                ?(CompletionReference.TotalRemaining(remaining,previousTail) is TimeSpan total
                    ?T("Total ETA ≈ ","전체 예상 잔여 ≈ ")+DurationText(total)+T(" · previous verification time"," · 이전 검증 시간 참고")
                    :T("Encoding ETA ≈ ","압축 예상 잔여 ≈ ")+DurationText(remaining))
                :T("Estimating · verification time excluded","예상 시간 계산 중 · 검증 시간 제외");
    }
    private void StopJobProgress()
    {
        etaTimer.Stop();jobClock.Stop();eta=null;jobProgress=null;previousTail=null;validationStarted=null;
        ProgressCard.Visibility=Visibility.Collapsed;EtaText.Text="";ElapsedText.Text="";ThroughputText.Text="";ProgressPercent.Text="";ProgressStage.Text="";
    }
    private void RefreshPreflightEta()
    {
        if(PreflightEta is null)return;

        PreflightEta.Text=activePlan is Plan p?TimingText(p,false):"";
        if(ActivePlanSummary is not null)
        {
            ActivePlanSummary.Visibility=activePlan is null?Visibility.Collapsed:Visibility.Visible;
            ActivePlanSummary.Text=activePlan is Plan applied
                ?T("Applied: ","적용 설정: ")+(applied.Codec=="libx265"?"HEVC":"H.264")+" · "+UiLanguage.Preset(korean,applied.Preset)+$" · {applied.Height}p · {applied.VideoBitsPerSecond/1000d:0.###} kbps"+(SplitBox.IsChecked==true?T(" · experimental split"," · 실험적 분할"):"")
                :"";
        }
    }
    private string TimingText(Plan p,bool compact)
    {
        if(CurrentTimingKey(p) is not TimingKey k||media is null)return "";
        if(timings.TryGetValue(k,out var t))
            return (compact?T("Previous: ","이전: "):T("Previous same-setting total: ","동일 설정 이전 전체 시간: "))+DurationText(t.Total)+T(" · reference only"," · 참고값");
        if(!k.Split&&timingHistory.Estimate(k.Path,media,k.Plan,k.Threads,k.Ffmpeg+"|"+k.Ffprobe) is TimingRange range)
            return T("Observed range ≈ ","관측 범위 ≈ ")+DurationText(range.Low)+"–"+DurationText(range.High)+T($" ({range.Sources} sources; not guaranteed)",$" (영상 {range.Sources}개 · 보장 없음)");
        return compact?T("Time not measured","시간 미측정"):T("Time estimate available after encoding starts.","예상 시간은 압축 시작 후 표시됩니다.");
    }
    private void RefreshPlanTimes()
    {
        RefreshPreflightEta();
        if(applying||media is not MediaInfo m||Plans.ItemsSource is not IEnumerable<PlanRow> rows)return;
        var selected=Plans.SelectedIndex;
        var snapshot=rows.Select(row=>MakeRow(row.Plan,m)).ToArray();
        applying=true;
        try{Plans.ItemsSource=snapshot;Plans.SelectedIndex=selected;}
        finally{applying=false;}
    }
    private EncodeReport? completedReport;
    private sealed record PlanRow(Plan Plan,string Name,string Size,string Settings,string Savings,string Timing);
    private bool korean,languageReady;
    private readonly bool persistLanguage;
    private string statusEn="Open an 8-bit SDR video with AAC audio or no audio.",statusKo="AAC 오디오가 있거나 무음인 8비트 SDR 영상을 열어 주세요.";
    private string T(string en,string ko)=>UiLanguage.Text(korean,en,ko);
    public MainWindow():this(UiLanguage.Load(),true){}
    public MainWindow(bool korean,bool persistLanguage=false)
    {
        this.korean=korean;this.persistLanguage=persistLanguage;
        InitializeComponent();UiLanguage.Apply(this,korean);
        etaTimer.Tick+=(_,_)=>RefreshJobProgress();
        CodecBox.ItemsSource=PlanRules.Codecs;
        UiLanguage.SetPresets(PresetBox,korean);
        ThreadsBox.ItemsSource=ThreadChoices.Create(korean);ThreadsBox.SelectedIndex=0;
        LanguageBox.SelectedIndex=korean?1:0;languageReady=true;SetStatus(statusEn,statusKo);
        HardwareBox.ItemsSource=EncoderDiagnostics.HardwareCandidates;
        ThreadsBox.SelectionChanged+=(_,_)=>{RefreshPlanTimes();RefreshHardwareButton();};
        SplitBox.Checked+=(_,_)=>{RefreshPlanTimes();RefreshHardwareButton();};SplitBox.Unchecked+=(_,_)=>{RefreshPlanTimes();RefreshHardwareButton();};
    }
    private void SetStatus(string en,string? ko=null){statusEn=en;statusKo=ko??en;Status.Text=T(statusEn,statusKo);}
    private void OnLanguage(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!languageReady)return;
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
            UiLanguage.SetPresets(PresetBox,korean);
            ThreadsBox.ItemsSource=ThreadChoices.Create(korean);
            ThreadsBox.SelectedItem=ThreadsBox.Items.Cast<ThreadChoice>().First(c=>c.Count==previousThreads);
        }
        finally{applying=false;}
        RefreshEstimate();RefreshResult();RefreshJobProgress();Status.Text=T(statusEn,statusKo);
        if(persistLanguage){try{UiLanguage.Save(korean);}catch(Exception ex){SetStatus("Could not save language: "+ex.Message,"언어 설정을 저장하지 못했습니다: "+ex.Message);}}
    }
    private PlanRow MakeRow(Plan p,MediaInfo m)=>new(p,UiLanguage.Tier(korean,p.Name),$"{p.EstimateBytes(m)/1e6:F1} MB",$"{p.Height}p · {(p.Codec=="libx265"?"HEVC":"H.264")} · {UiLanguage.Preset(korean,p.Preset)} · {p.VideoBitsPerSecond/1000d:F0} kbps",T($"{p.Savings(m):P1} budget savings",$"예산 절감률 {p.Savings(m):P1}"),TimingText(p,true));
    private void ClearResult()
    {
        completedOutput=null;completedReport=null;RefreshResult();
    }
    private string ResultText(EncodeReport r)=>T(
        $"{r.InputBytes/1e6:F2} → {r.OutputBytes/1e6:F2} MB · {1-r.OutputBytes/(double)r.InputBytes:P1} actual savings · {r.Total.TotalSeconds:F1}s (encoding {r.Encoding.TotalSeconds:F1}s, validation {r.Validation.TotalSeconds:F1}s)",
        $"{r.InputBytes/1e6:F2} → {r.OutputBytes/1e6:F2} MB · 실제 절감률 {1-r.OutputBytes/(double)r.InputBytes:P1} · {r.Total.TotalSeconds:F1}초 (인코딩 {r.Encoding.TotalSeconds:F1}초, 검증 {r.Validation.TotalSeconds:F1}초)")+(r.HardwareEncoder is string backend?T($"\n{backend} · {(r.GpuResident?"GPU-resident":"CPU decode")} · device defaults · {r.AppliedPlan?.Height}p · {r.AppliedPlan?.VideoBitsPerSecond/1000d:F0} kbps · qualification {r.HardwareQualification.TotalSeconds:F1}s",$"\n{backend} · {(r.GpuResident?"GPU 연속 처리":"CPU 디코딩")} · 장치 기본 설정 · {r.AppliedPlan?.Height}p · {r.AppliedPlan?.VideoBitsPerSecond/1000d:F0} kbps · 장치 검사 {r.HardwareQualification.TotalSeconds:F1}초"):T($"\n{r.AppliedPlan?.Codec??"—"} · {r.AppliedPlan?.Preset??"—"} · {r.AppliedPlan?.Height}p · {r.AppliedPlan?.VideoBitsPerSecond/1000d:F0} kbps · threads {r.RequestedEncoderThreads?.ToString()??"auto"}",$"\n{r.AppliedPlan?.Codec??"—"} · {UiLanguage.Preset(true,r.AppliedPlan?.Preset??"—")} · {r.AppliedPlan?.Height}p · {r.AppliedPlan?.VideoBitsPerSecond/1000d:F0} kbps · 스레드 {r.RequestedEncoderThreads?.ToString()??"자동"}"));
    private void RefreshResult()
    {
        ResultPath.Text=completedOutput??"";
        ResultSummary.Text=completedReport is EncodeReport report?ResultText(report):"";
        ResultSummary.Visibility=completedReport is null?Visibility.Collapsed:Visibility.Visible;
        bool available=completedOutput is not null&&System.IO.File.Exists(completedOutput);
        PlayResultButton.IsEnabled=ResultFolderButton.IsEnabled=available;
        CopyResultButton.IsEnabled=completedReport is not null;
    }
    private void OnPlayResult(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||!System.IO.File.Exists(completedOutput))
        {RefreshResult();SetStatus("The completed output is no longer available.","완료된 출력 파일을 찾을 수 없습니다.");return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(completedOutput){UseShellExecute=true});}
        catch(Exception ex){SetStatus("Could not open the video: "+ex.Message,"영상을 열지 못했습니다: "+ex.Message);}
    }
    private void OnResultFolder(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||!System.IO.File.Exists(completedOutput))
        {RefreshResult();SetStatus("The completed output is no longer available.","완료된 출력 파일을 찾을 수 없습니다.");return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetDirectoryName(completedOutput)!){UseShellExecute=true});}
        catch(Exception ex){SetStatus(ex.Message);}
    }
    private void OnCopyResult(object sender,RoutedEventArgs e)
    {
        if(completedOutput is null||completedReport is null)return;
        try
        {
            var r=completedReport;
            Clipboard.SetText($"{completedOutput}\n{ResultText(r)}");
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
        else if(current is null)ShowBatch(dlg.FileNames);
        else SetStatus("Finish or cancel analysis before opening a batch.","일괄 압축을 열기 전에 분석을 완료하거나 취소해 주세요.");
    }
    private async void OnDrop(object sender,DragEventArgs e)
    {
        if(encoding||e.Data.GetData(DataFormats.FileDrop) is not string[] paths)return;
        if(paths.Length==1)await LoadAsync(paths[0]);
        else if(paths.Length>1&&current is null)ShowBatch(paths);
    }
    private void OnBatch(object sender,RoutedEventArgs e)
    {
        if(current is null)ShowBatch();
    }
    private void ShowBatch(string[]? paths=null)
    {
        var batch=new BatchWindow(paths,korean,batchSessionHistory){Owner=this};
        batch.ShowDialog();
        LanguageBox.SelectedIndex=batch.IsKorean?1:0;
    }
    private async void OnDiagnostics(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        using var cts=new CancellationTokenSource();current=cts;
        encoding=true;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=EncodeButton.IsEnabled=false;
        Editor.IsEnabled=Plans.IsEnabled=RecommendationBox.IsEnabled=false;CancelButton.IsEnabled=true;
        StartJobProgress(true,"Diagnostics");
        SpeedFirstButton.IsEnabled=FastHevcButton.IsEnabled=HardwareTrialButton.IsEnabled=ResidentTrialButton.IsEnabled=false;
        SetStatus("Testing listed hardware encoders on synthetic frames…","시험 영상으로 하드웨어 인코더를 검사 중입니다…");
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            var results=await EncoderDiagnostics.CheckAsync(resolved,EncoderDiagnostics.HardwareCandidates,cts.Token);
            var detail=string.Join(Environment.NewLine,results.Select(r=>$"{r.Encoder}: {r.State} — {r.Detail}"));SetStatus(detail+Environment.NewLine+"Diagnostic only. Hardware encoding is not enabled.",detail+Environment.NewLine+"진단 전용입니다. 하드웨어 압축은 활성화되지 않았습니다.");
        }
        catch(OperationCanceledException){SetStatus("Diagnostics canceled or timed out.","진단이 취소되었거나 제한 시간을 초과했습니다.");}
        catch(Exception ex){SetStatus(ex.Message);}
        finally
        {
            current=null;encoding=false;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=Plans.IsEnabled=true;
            Editor.IsEnabled=RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;StopJobProgress();RefreshEstimate();
        }
    }
    internal async Task LoadAsync(string path)
    {
        current?.Cancel();ClearResult();appliedTarget=null;
        using var cts=new CancellationTokenSource();current=cts;
        media=null;tools=null;sourcePath=null;sourceStamp=null;HardwareBox.SelectedIndex=-1;RecommendationBox.IsEnabled=false;RecommendationBox.ItemsSource=null;TargetBox.Clear();SplitBox.IsChecked=false;activePlan=null;Plans.ItemsSource=null;Editor.IsEnabled=false;EncodeButton.IsEnabled=false;Source.Text="";Estimate.Text="";Evidence.Text="";SetStatus("Analyzing…","분석 중…");
        CancelButton.IsEnabled=true;SpeedFirstButton.IsEnabled=FastHevcButton.IsEnabled=HardwareTrialButton.IsEnabled=ResidentTrialButton.IsEnabled=false;StartJobProgress(true,"Analyzing");RefreshPreflightEta();
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            var stamp=SourceStamp.Capture(path);
            var details=await Probe.InspectAsync(resolved.Ffprobe,path,cts.Token);
            stamp.RequireUnchanged(path);
            if(current!=cts)return;
            var m=details.Info;
            Source.Text=$"{System.IO.Path.GetFileName(path)} · {m.Width}×{m.Height} · {m.Fps:F2} fps · {m.Codec} · {m.Bytes/1e6:F1} MB"+(double.IsFinite(m.Seconds)&&m.Seconds>0&&m.Seconds<=TimeSpan.FromDays(30).TotalSeconds?" · "+DurationText(TimeSpan.FromSeconds(m.Seconds)):"");
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
        finally{if(current==cts){current=null;RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;StopJobProgress();RefreshEstimate();}}
    }
    private void OnRecommendation(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!applying&&current is null&&media is not null&&tools is not null)RebuildPlans();
    }
    private void RebuildPlans()
    {
        if(media is not MediaInfo m||tools is not Toolchain resolved)return;
        var preferred=(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec;
        appliedTarget=null;TargetBox.Clear();SplitBox.IsChecked=false;
        var rows=Planner.Propose(m,resolved.Codecs,preferred).Select(p=>MakeRow(p,m)).ToArray();
        Plans.ItemsSource=rows;
        if(rows.Length>0)Plans.SelectedIndex=Math.Min(1,rows.Length-1);
        else
        {
            applying=true;
            try{CodecBox.SelectedItem=preferred??resolved.Codecs[0];PresetBox.SelectedValue="medium";HeightBox.Text=m.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text="";}
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
        try{appliedTarget=null;TargetBox.Clear();CodecBox.SelectedItem=p.Codec;PresetBox.SelectedValue=p.Preset;HeightBox.Text=p.Height.ToString(CultureInfo.InvariantCulture);RateBox.Text=(p.VideoBitsPerSecond/1000d).ToString("0.###",CultureInfo.InvariantCulture);}
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
    private bool HardwareAllowed(string? backend)=>backend is not null&&EncoderDiagnostics.HardwareCandidates.Contains(backend)&&
        current is null&&!encoding&&!TargetPending&&activePlan is Plan p&&
        p.Codec==(backend.StartsWith("hevc_",StringComparison.Ordinal)?"libx265":"libx264")&&
        SplitBox.IsChecked!=true&&(ThreadsBox.SelectedItem as ThreadChoice)?.Count is null;
    private void OnHardwareChoice(object sender,System.Windows.Controls.SelectionChangedEventArgs e)=>RefreshHardwareButton();
    private bool ResidentAllowed(string? backend)=>HardwareAllowed(backend)&&(backend is "h264_nvenc" or "hevc_nvenc")&&media?.Codec is "h264" or "hevc";
    private void RefreshHardwareButton()
    {
        if(HardwareTrialButton is not null)HardwareTrialButton.IsEnabled=HardwareAllowed(HardwareBox?.SelectedItem as string);
        if(ResidentTrialButton is not null)ResidentTrialButton.IsEnabled=ResidentAllowed(HardwareBox?.SelectedItem as string);
        if(HardwareBenchButton is not null)HardwareBenchButton.IsEnabled=current is null&&!encoding&&HardwareBox?.SelectedItem is string selected&&EncoderDiagnostics.HardwareCandidates.Contains(selected);
    }
    private async void OnHardwareBenchmark(object sender,RoutedEventArgs e)
    {
        if(current is not null||encoding||HardwareBox.SelectedItem is not string backend||!EncoderDiagnostics.HardwareCandidates.Contains(backend))return;
        using var cts=new CancellationTokenSource();current=cts;encoding=true;
        OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=EncodeButton.IsEnabled=false;
        Editor.IsEnabled=Plans.IsEnabled=RecommendationBox.IsEnabled=HardwareBox.IsEnabled=false;
        CancelButton.IsEnabled=true;
        SpeedFirstButton.IsEnabled=FastHevcButton.IsEnabled=HardwareTrialButton.IsEnabled=ResidentTrialButton.IsEnabled=false;
        StartJobProgress(true,"Diagnostics");
        SetStatus("Synthetic device benchmark; no user media is read.","합성 영상 장치 비교 중 · 사용자 영상은 읽지 않습니다.");
        DeviceBenchmarkResult? result=null;
        try
        {
            var resolved=await Toolchain.DiscoverAsync(cts.Token);
            result=await DeviceBenchmark.RunAsync(resolved,backend,cts.Token,new Progress<string>(stage=>
            {
                if(current==cts&&!cts.IsCancellationRequested)SetStatus("Device screen: "+stage,"장치 비교: "+stage);
            }));
            SetStatus("Device screen finished. Results are synthetic, not a prediction for your video.","장치 비교 완료. 합성 영상 결과이며 사용자 영상의 예상 시간이 아닙니다.");
        }
        catch(OperationCanceledException){SetStatus("Device screen canceled; temporary files removed.","장치 비교 취소 · 임시 파일 정리.");}
        catch(Exception ex){SetStatus(ex.Message);}
        finally
        {
            current=null;encoding=false;OpenButton.IsEnabled=BatchButton.IsEnabled=DiagnosticsButton.IsEnabled=Plans.IsEnabled=HardwareBox.IsEnabled=true;
            Editor.IsEnabled=RecommendationBox.IsEnabled=media is not null;CancelButton.IsEnabled=false;StopJobProgress();RefreshEstimate();
        }
        if(result is not null)ShowDeviceReport(result);
    }
    private void ShowDeviceReport(DeviceBenchmarkResult result)
    {
        var dialog=new Window{Owner=this,Title=T("VANTA · Device screen","VANTA · 장치 성능 비교"),Width=820,Height=540,MinWidth=480,MinHeight=320,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233,237,242))};
        var panel=new System.Windows.Controls.DockPanel{Margin=new Thickness(16)};
        var copy=new System.Windows.Controls.Button{Content=T("Copy full JSON report","전체 JSON 결과 복사"),Padding=new Thickness(12,8),Margin=new Thickness(0,8,0,0)};
        System.Windows.Controls.DockPanel.SetDock(copy,System.Windows.Controls.Dock.Bottom);panel.Children.Add(copy);
        copy.Click+=(_,_)=>{try{Clipboard.SetText(result.Json());copy.Content=T("Copied","복사됨");}catch(Exception ex){MessageBox.Show(dialog,ex.Message,"VANTA");}};
        var header=new System.Windows.Controls.TextBlock{Text=T("810p · 30fps · 3s · 1791kbps · one synthetic trial per path","810p · 30fps · 3초 · 1791kbps · 경로당 합성 영상 1회"),FontSize=16,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
        System.Windows.Controls.DockPanel.SetDock(header,System.Windows.Controls.Dock.Top);panel.Children.Add(header);
        string detail=string.Join(Environment.NewLine+Environment.NewLine,result.Rows.Select(row=>row.Report is EncodeReport r
            ?$"{row.Mode} — {row.State}\n"+T($"Total {r.Total.TotalSeconds:F2}s · encode {r.Encoding.TotalSeconds:F2}s · validate {r.Validation.TotalSeconds:F2}s",$"전체 {r.Total.TotalSeconds:F2}초 · 압축 {r.Encoding.TotalSeconds:F2}초 · 검증 {r.Validation.TotalSeconds:F2}초")+
             $"\n{r.OutputBytes/1e6:F3} MB · SSIM {row.MeanSsim:F5} / worst 1s {row.WorstSecond:F5}\n"+T($"Fixed output check: {row.OrdinaryCheckSeconds:F2}s ordinary / {row.CombinedCheckSeconds:F2}s combined",$"고정 출력 검사: 기존 {row.OrdinaryCheckSeconds:F2}초 / 통합 {row.CombinedCheckSeconds:F2}초")
            :$"{row.Mode} — {row.State}\n{row.Detail}"));
        var body=new System.Windows.Controls.TextBox{Text=detail,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto,Padding=new Thickness(12),FontSize=14};
        panel.Children.Add(body);dialog.Content=panel;dialog.ShowDialog();
    }
    private async void OnResidentTrial(object sender,RoutedEventArgs e)
    {
        if(HardwareBox.SelectedItem is string backend&&ResidentAllowed(backend))await SaveAndEncodeAsync(backend,true);
    }
    private async void OnHardwareTrial(object sender,RoutedEventArgs e)
    {
        if(HardwareBox.SelectedItem is string backend&&HardwareAllowed(backend))await SaveAndEncodeAsync(backend);
    }
    private void OnSpeedFirst(object sender,RoutedEventArgs e)=>ApplyFastPath("libx264");
    private void OnFastHevc(object sender,RoutedEventArgs e)=>ApplyFastPath("libx265");
    private void ApplyFastPath(string codec)
    {
        if(current is not null||encoding||TargetPending||media is null||activePlan is not Plan p||tools is null||!tools.Codecs.Contains(codec))return;
        applying=true;
        try
        {
            Plans.SelectedIndex=-1;
            CodecBox.SelectedItem=codec;PresetBox.SelectedValue="veryfast";
            SplitBox.IsChecked=false;
        }
        finally{applying=false;}
        RefreshEstimate();
        string label=codec=="libx265"?"HEVC":"H.264";
        SetStatus(label+" veryfast selected; resolution and bitrate unchanged. Split disabled. Actual quality and size may differ. No encoding started.",
            label+" 매우 빠르게 설정을 적용했습니다. 해상도와 비트레이트는 그대로이며 분할 압축은 해제했습니다. 실제 화질과 용량은 달라질 수 있습니다. 압축은 아직 시작하지 않았습니다.");
    }
    private void OnMoreCompression(object sender,RoutedEventArgs e)
    {
        if(current is not null||media is null||activePlan is null)return;
        if(QuickSettings.MoreCompression(activePlan,media) is Plan next)
        {
            Plans.SelectedIndex=-1;Apply(next);SetStatus("Lower video bitrate budget applied. Quality may decrease; no encoding started.","영상 비트레이트 예산을 낮췄습니다. 화질이 낮아질 수 있으며 압축은 시작되지 않았습니다.");
        }
    }
    private void OnSettings(object sender,RoutedEventArgs e){if(!applying){if(Plans is not null)Plans.SelectedIndex=-1;appliedTarget=null;if(TargetBox is not null)TargetBox.Clear();RefreshEstimate();}}
    private bool TargetPending=>TargetBox is not null&&!string.IsNullOrWhiteSpace(TargetBox.Text)&&TargetBox.Text!=appliedTarget;
    private void OnTargetTextChanged(object sender,RoutedEventArgs e){if(!applying)RefreshEstimate();}
    private void RefreshEstimate()
    {
        activePlan=null;
        RefreshPreflightEta();
        if(MoreCompressionButton is not null)MoreCompressionButton.IsEnabled=false;
        if(SpeedFirstButton is not null)SpeedFirstButton.IsEnabled=false;
        if(FastHevcButton is not null)FastHevcButton.IsEnabled=false;
        if(HardwareTrialButton is not null)HardwareTrialButton.IsEnabled=false;
        if(ResidentTrialButton is not null)ResidentTrialButton.IsEnabled=false;
        if(HardwareBenchButton is not null)HardwareBenchButton.IsEnabled=current is null&&!encoding&&HardwareBox?.SelectedItem is string selected&&EncoderDiagnostics.HardwareCandidates.Contains(selected);
        if(EncodeButton is null||MoreCompressionButton is null||media is null)return;
        EncodeButton.IsEnabled=false;
        if(TargetPending)
        {
            Estimate.Text=T("Apply the new target or clear it to use the current bitrate.","새 목표 용량을 적용하거나 지워서 현재 비트레이트를 사용하세요.");
            Evidence.Text="";return;
        }
        try
        {
            if(!int.TryParse(HeightBox.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int h))throw new ArgumentException("Enter a valid height.");
            var p=new Plan("Custom",CodecBox.SelectedItem as string??"",h,PlanRules.ParseVideoKbps(RateBox.Text),0,PresetBox.SelectedValue as string??"",false);
            var v=CandidateEvaluation.Evaluate(p,media);
            activePlan=p;
            RefreshPreflightEta();
            Estimate.Text=T($"Bitrate budget {p.EstimateBytes(media)/1e6:F2} MB · {p.Savings(media):P1} budget savings",$"용량 예산 {p.EstimateBytes(media)/1e6:F2} MB · 예산 절감률 {p.Savings(media):P1}");
            Evidence.Text=T($"Pixel retention {v.PixelRetention:P1} · quality not measured · actual size may be much smaller or larger; not a limit",$"픽셀 유지 비율 {v.PixelRetention:P1} · 화질 미측정 · 실제 용량은 예산과 다를 수 있음");
            EncodeButton.IsEnabled=current is null&&!encoding;
            MoreCompressionButton.IsEnabled=EncodeButton.IsEnabled&&p.VideoBitsPerSecond>PlanRules.MinimumVideoRate;
            if(SpeedFirstButton is not null)SpeedFirstButton.IsEnabled=EncodeButton.IsEnabled&&tools?.Codecs.Contains("libx264")==true;
            if(FastHevcButton is not null)FastHevcButton.IsEnabled=EncodeButton.IsEnabled&&tools?.Codecs.Contains("libx265")==true;
            RefreshHardwareButton();
        }
        catch(Exception ex){Estimate.Text=ex.Message;Evidence.Text="";}
    }
    private void OnTarget(object sender,RoutedEventArgs e)
    {
        if(media is null||current is not null)return;
        appliedTarget=null;
        try
        {
            if(string.IsNullOrWhiteSpace(TargetBox.Text)){TargetBox.Clear();RefreshEstimate();return;}
            if(!double.TryParse(TargetBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double mb))throw new ArgumentException("Enter target MB.");
            var targetText=TargetBox.Text;
            var rate=(PlanRules.TargetRate(media,mb*1e6)/1000d).ToString("0.###",CultureInfo.InvariantCulture);
            applying=true;
            try{Plans.SelectedIndex=-1;RateBox.Text=rate;appliedTarget=targetText;}
            finally{applying=false;}
            RefreshEstimate();
            SetStatus("Target applied as bitrate budget, not exact file size.","목표 용량을 비트레이트 예산으로 적용했습니다. 정확한 용량을 보장하지 않습니다.");
        }
        catch(Exception ex){RefreshEstimate();SetStatus("Target not applied: "+ex.Message,"목표 용량을 적용하지 못했습니다. 목표를 수정하거나 지워 주세요. ("+ex.Message+")");}
    }
    private void OnCancel(object sender,RoutedEventArgs e)=>current?.Cancel();
    private bool ValidateEncodingRequest()
    {
        if(current is not null||activePlan is null||sourcePath is null||tools is null||sourceStamp is null)return false;
        try{sourceStamp.RequireUnchanged(sourcePath);}catch(Exception ex){SetStatus(ex.Message);return false;}
        bool split=SplitBox.IsChecked==true;
        var threads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        try{ThreadBudget.ValidateEncoder(threads,split);}catch(ArgumentException ex){SetStatus(ex.Message);return false;}
        if(split&&(activePlan.Codec!="libx264"||media?.Height!=activePlan.Height||media?.Codec!="h264"||!string.Equals(System.IO.Path.GetExtension(sourcePath),".mp4",StringComparison.OrdinalIgnoreCase)))
        {SetStatus("Experimental split requires H.264 MP4 input, H.264 output, original height and CFR. Uncheck it for normal encoding.","분할 압축에는 H.264 MP4 입력과 H.264 출력, 원본 높이, 고정 프레임률이 필요합니다. 일반 압축은 체크를 해제하세요.");return false;}
        return true;
    }
    private async void OnEncode(object sender,RoutedEventArgs e)=>await SaveAndEncodeAsync(null);
    private async Task SaveAndEncodeAsync(string? backend,bool resident=false)
    {
        if(!ValidateEncodingRequest()||backend is not null&&!HardwareAllowed(backend)||resident&&!ResidentAllowed(backend))return;
        var input=sourcePath!;
        SaveFileDialog dlg;
        try
        {
            dlg=new SaveFileDialog{Filter="MP4 video|*.mp4",DefaultExt=".mp4",FileName=System.IO.Path.GetFileName(OutputNames.Suggest(input)),InitialDirectory=System.IO.Path.GetDirectoryName(input),OverwritePrompt=false};
            if(dlg.ShowDialog()!=true)return;
            if(System.IO.File.Exists(dlg.FileName)||System.IO.Directory.Exists(dlg.FileName))
            {SetStatus("Choose a new filename; VANTA does not overwrite existing files.","새 파일 이름을 선택해 주세요. 기존 파일은 덮어쓰지 않습니다.");return;}
        }
        catch(Exception ex){SetStatus(ex.Message);return;}
        await EncodeToAsync(dlg.FileName,backend,resident);
    }
    internal async Task EncodeToAsync(string output,string? hardware=null,bool resident=false)
    {
        if(!ValidateEncodingRequest()||hardware is not null&&!HardwareAllowed(hardware)||resident&&!ResidentAllowed(hardware))return;
        var p=activePlan!;var input=sourcePath!;var resolved=tools!;var stamp=sourceStamp!;
        bool split=SplitBox.IsChecked==true;
        var threads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        if(System.IO.File.Exists(output)||System.IO.Directory.Exists(output))
        {SetStatus("Choose a new filename; VANTA does not overwrite existing files.","새 파일 이름을 선택해 주세요. 기존 파일은 덮어쓰지 않습니다.");return;}
        ClearResult();
        var timingKey=hardware is null?CurrentTimingKey(p):null;
        using var cts=new CancellationTokenSource();current=cts;encoding=true;
        StartJobProgress(split,hardware is null?"Preparing":"Qualifying hardware");
        HardwareBox.IsEnabled=false;
        SpeedFirstButton.IsEnabled=FastHevcButton.IsEnabled=HardwareTrialButton.IsEnabled=ResidentTrialButton.IsEnabled=false;
        if(!split&&timingKey is not null&&timings.TryGetValue(timingKey,out var prior))previousTail=CompletionReference.Tail(prior);
        OpenButton.IsEnabled=false;Editor.IsEnabled=false;Plans.IsEnabled=false;RecommendationBox.IsEnabled=false;EncodeButton.IsEnabled=false;CancelButton.IsEnabled=true;ProgressCard.Visibility=Visibility.Visible;SetStatus("Encoding and validating…","압축 및 검증 중…");
        try
        {
            var progress=new Progress<EncodeProgress>(x=>
            {
                if(current!=cts||cts.IsCancellationRequested)return;
                if(x.Stage=="Validating"&&validationStarted is null)validationStarted=jobClock.Elapsed;
                jobProgress=x;eta?.Observe(x,jobClock.Elapsed);RefreshJobProgress();
                var suffix=x.DisplayPercentText is string percent?$" · {percent}":"…";SetStatus(x.Stage+suffix,UiLanguage.Stage(true,x.Stage)+suffix);
            });
            var report=hardware is null
                ?await Encoder.EncodeAsync(resolved.Ffmpeg,resolved.Ffprobe,input,output,p,cts.Token,progress,experimentalSplit:split,encoderThreads:threads,expectedSource:stamp)
                :await Encoder.EncodeHardwareTrialAsync(resolved,input,output,p,hardware,cts.Token,progress,stamp,resident);
            completedOutput=System.IO.Path.GetFullPath(output);completedReport=report;
            if(timingKey is not null){if(timings.Count>=16&&!timings.ContainsKey(timingKey))timings.Remove(timings.Keys.First());timings[timingKey]=report;
                if(!split&&media is MediaInfo measured)timingHistory.Record(new(timingKey.Path,measured,timingKey.Plan,timingKey.Threads,timingKey.Ffmpeg+"|"+timingKey.Ffprobe,report.Total));}
            RefreshPlanTimes();
            RefreshResult();
            var bytes=report.OutputBytes;
            SetStatus($"Validated output: {bytes/1e6:F2} MB · {1-bytes/(double)report.InputBytes:P1} actual savings · {report.Total.TotalSeconds:F1}s (validation {report.Validation.TotalSeconds:F1}s)",$"검증 완료: {bytes/1e6:F2} MB · 실제 절감률 {1-bytes/(double)report.InputBytes:P1} · {report.Total.TotalSeconds:F1}초 (검증 {report.Validation.TotalSeconds:F1}초)");
        }
        catch(OperationCanceledException){SetStatus("Canceled; no new output published.","취소되었습니다. 새 출력 파일은 저장되지 않았습니다.");}
        catch(Exception ex){SetStatus(ex.Message);}
        finally{StopJobProgress();current=null;encoding=false;OpenButton.IsEnabled=true;Editor.IsEnabled=true;Plans.IsEnabled=true;RecommendationBox.IsEnabled=true;HardwareBox.IsEnabled=true;CancelButton.IsEnabled=false;RefreshEstimate();}
    }
}