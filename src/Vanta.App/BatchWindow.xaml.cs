using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using Vanta.Core;
namespace Vanta.App;
public partial class BatchWindow:Window
{
    private readonly ObservableCollection<Row> rows=new();
    private readonly string[] initial;
    private CancellationTokenSource? current;
    private Toolchain? tools;
    private bool initialized;
    private TimeSpan? lastBatchElapsed;
    private readonly System.Diagnostics.Stopwatch batchClock=new();
    private readonly System.Windows.Threading.DispatcherTimer batchTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    private Row[]? activeRun;
    private readonly TimingHistory batchHistory;
    private int activeParallel;
    private bool activeHardware;
    private int? activeThreads;
    private string activeToolKey="";
    private string? destinationDirectory;
    private static string PreferencesPath=>System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VANTA","batch-defaults.json");
    private sealed class Row(string input,bool korean):INotifyPropertyChanged
    {
        public string Input {get;}=input;
        public string Output {get;set;}="";
        public string InputName=>System.IO.Path.GetFileName(Input);
        public string OutputName=>System.IO.Path.GetFileName(Output);
        public string Paths=>UiLanguage.Text(Korean,$"Source: {Input}\nOutput: {Output}",$"원본: {Input}\n출력: {Output}");
        public bool Korean {get;set;}=korean;
        public string Budget=>Plan is Plan p&&Media is MediaInfo m?$"{UiLanguage.Tier(Korean,p.Name)} · {(p.Codec=="libx265"?"HEVC":"H.264")} · {p.Height}p · {DeviceRequest??UiLanguage.Preset(Korean,p.Preset)} · {p.VideoBitsPerSecond/1000d:F0} kbps · {p.EstimateBytes(m)/1e6:F2} MB":"";
        public string? DeviceRequest {get;set;}
        public EncodingEta? Eta {get;set;}
        public TimeSpan? Remaining {get;set;}
        public double? Rate {get;set;}
        public VerificationPhase? Verification {get;set;}
        public TimeSpan? AdmittedAt {get;set;}
        public string DisplayDetail=>State.StartsWith("Encoding",StringComparison.Ordinal)?(Remaining is TimeSpan remain?UiLanguage.Text(Korean,"Encoding ETA ≈ ","압축 예상 잔여 ≈ ")+UiLanguage.Duration(remain):UiLanguage.Text(Korean,"Estimating encoding time","압축 예상 시간 계산 중"))+(Rate is double speed?" · "+UiLanguage.Rate(Korean,speed):""):State=="Validating"?UiLanguage.Verification(Korean,Verification)+UiLanguage.Text(Korean," · time unknown"," · 시간 미정"):State=="Ready"?UiLanguage.Text(Korean,ManualPlan is null?"Shared profile; size not guaranteed.":"Individual override; size not guaranteed.",ManualPlan is null?"공통 플랜 · 용량 보장 없음":"개별 설정 · 용량 보장 없음"):State=="Complete"&&Report is EncodeReport r?UiLanguage.Text(Korean,Detail,$"{r.OutputBytes/1e6:F2} MB · 실제 절감률 {1-r.OutputBytes/(double)r.InputBytes:P1} · {r.Total.TotalSeconds:F1}초 (인코딩 {r.Encoding.TotalSeconds:F1}초, 검증 {r.Validation.TotalSeconds:F1}초)"):Detail;
        public string State {get;set;}="Analyzing";
        public string DisplayState=>UiLanguage.Stage(Korean,State);
        public string Detail {get;set;}="";
        public MediaInfo? Media {get;set;}
        public SourceStamp? Stamp {get;set;}
        public Plan? Plan {get;set;}
        public Plan? ManualPlan {get;set;}
        public EncodeReport? Report {get;set;}
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Refresh()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(string.Empty));
    }
    private bool korean;
    internal bool IsKorean=>korean;
    private string T(string en,string ko)=>UiLanguage.Text(korean,en,ko);
    private sealed record ProfileChoice(string Key,string Label){public override string ToString()=>Label;}
    public BatchWindow(string[]? paths=null,bool korean=false,TimingHistory? sessionHistory=null)
    {
        initial=paths??[];this.korean=korean;batchHistory=sessionHistory??new TimingHistory();
        InitializeComponent();ApplyLanguageResources();
        batchTimer.Tick+=(_,_)=>RefreshBatchProgress();
        Queue.ItemsSource=rows;
        ProfileBox.ItemsSource=new[]{"Quality","Balanced","Compact"}.Select(p=>new ProfileChoice(p,UiLanguage.Tier(korean,p))).ToArray();
        ProfileBox.SelectedIndex=1;
        UiLanguage.SetPresets(SharedPresetBox,korean);SharedPresetBox.SelectedValue="medium";
        RecommendationBox.ItemsSource=RecommendationChoices.Create(korean:korean);RecommendationBox.SelectedIndex=0;
        ParallelBox.ItemsSource=new[]{1,2};ParallelBox.SelectedIndex=1;
        BatchHardwareBox.ItemsSource=EncoderDiagnostics.HardwareCandidates;
        ThreadsBox.ItemsSource=ThreadChoices.Create(korean);ThreadsBox.SelectedIndex=0;
        string? settingsError=null;
        try
        {
            var saved=BatchPreferences.Load(PreferencesPath);
            ProfileBox.SelectedItem=ProfileBox.Items.Cast<ProfileChoice>().First(p=>p.Key==saved.Tier);
            // Bind to existing item instances rather than relying on record equality in WPF.
            RecommendationBox.SelectedItem=RecommendationBox.Items.Cast<RecommendationChoice>().First(c=>c.Codec==saved.Codec);
            SharedPresetBox.SelectedValue=saved.Preset;
            ParallelBox.SelectedItem=saved.ParallelJobs;
            var choice=ThreadsBox.Items.Cast<ThreadChoice>().FirstOrDefault(c=>c.Count==saved.EncoderThreads);
            ThreadsBox.SelectedItem=choice??ThreadsBox.Items.Cast<ThreadChoice>().First(c=>c.Count is null);
            if(choice is null)settingsError=T("Saved encoder thread budget is unavailable on this PC; using Auto.","저장된 스레드 예산을 사용할 수 없어 자동으로 설정했습니다.");
        }
        catch(Exception ex){settingsError="Saved defaults could not be loaded: "+ex.Message;}
        LanguageBox.SelectedIndex=korean?1:0;initialized=true;UpdateSummary();
        if(settingsError is not null)Summary.Text=settingsError;
        Loaded+=async (_,_)=>{if(initial.Length>0)await AddAsync(initial);};
    }
    private void ApplyLanguageResources()
    {
        UiLanguage.Apply(this,korean);
        // DataGridColumn is not in the window's visual tree; assign translated headers explicitly.
        for(int i=0;i<Queue.Columns.Count;i++)Queue.Columns[i].Header=Resources[$"Ui{46+i:000}"];
    }
    private void OnLanguage(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!initialized)return;
        korean=LanguageBox.SelectedIndex==1;
        var profile=((ProfileChoice)ProfileBox.SelectedItem).Key;
        var codec=(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec;
        var threads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        initialized=false;
        try
        {
            ApplyLanguageResources();
            ProfileBox.ItemsSource=new[]{"Quality","Balanced","Compact"}.Select(p=>new ProfileChoice(p,UiLanguage.Tier(korean,p))).ToArray();
            ProfileBox.SelectedItem=ProfileBox.Items.Cast<ProfileChoice>().First(p=>p.Key==profile);
            RecommendationBox.ItemsSource=RecommendationChoices.Create(korean:korean);
            RecommendationBox.SelectedItem=RecommendationBox.Items.Cast<RecommendationChoice>().First(c=>c.Codec==codec);
            UiLanguage.SetPresets(SharedPresetBox,korean);
            ThreadsBox.ItemsSource=ThreadChoices.Create(korean);
            ThreadsBox.SelectedItem=ThreadsBox.Items.Cast<ThreadChoice>().First(c=>c.Count==threads);
            DestinationText.Text=destinationDirectory is null?T("Output: beside each source","저장 위치: 각 원본 옆"):T("Output: ","저장 위치: ")+destinationDirectory;
            foreach(var row in rows){row.Korean=korean;row.Refresh();}
        }
        finally{initialized=true;}
        UpdateSummary();RefreshBatchProgress();
    }
    private async void OnAdd(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        var d=new OpenFileDialog{Multiselect=true,Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi|All files|*.*"};
        if(d.ShowDialog()==true)await AddAsync(d.FileNames);
    }
    private async void OnDrop(object sender,DragEventArgs e)
    {
        if(current is null&&e.Data.GetData(DataFormats.FileDrop) is string[] paths)await AddAsync(paths);
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(current is not null){e.Cancel=true;current.Cancel();Summary.Text=T("Canceling. Close after cleanup completes.","취소 중입니다. 정리가 끝난 후 닫아 주세요.");}
    }
    private void OnClear(object sender,RoutedEventArgs e){if(current is null){rows.Clear();UpdateSummary();}}
    private void OnRemove(object sender,RoutedEventArgs e){if(current is null&&Queue.SelectedItem is Row r){rows.Remove(r);UpdateSummary();}}
    private void OnFolder(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        var dialog=new OpenFolderDialog{Title="Choose output folder",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        destinationDirectory=dialog.FolderName;DestinationText.Text=T("Output: ","저장 위치: ")+destinationDirectory;Replan();
    }
    private void OnSourceFolder(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        destinationDirectory=null;DestinationText.Text=T("Output: beside each source","저장 위치: 각 원본 옆");Replan();
    }
    private void OnEditPlan(object sender,RoutedEventArgs e)
    {
        if(current is not null||tools is null||Queue.SelectedItem is not Row r||r.Media is null||!BatchRecovery.CanEdit(r.State,true))
        {Summary.Text=T("Select an analyzed, unfinished video. Editing prepares it for the next run.","분석이 끝난 미완료 영상을 선택하세요. 수정한 설정은 다음 작업에 적용됩니다.");return;}
        try
        {
            r.Stamp?.RequireUnchanged(r.Input);
            var dialog=new BatchPlanDialog(r.Media,tools.Codecs,r.ManualPlan??r.Plan,korean){Owner=this};
            if(dialog.ShowDialog()!=true||dialog.Result is null)return;
            r.ManualPlan=dialog.Result;r.State="Analyzing";Replan();
        }
        catch(Exception ex){Summary.Text=ex.Message;}
    }
    private void OnResetPlan(object sender,RoutedEventArgs e)
    {
        if(current is not null||Queue.SelectedItem is not Row r||!BatchRecovery.CanEdit(r.State,r.Media is not null))
        {Summary.Text=T("Select an analyzed, unfinished video. Reset prepares it for the next run.","분석이 끝난 미완료 영상을 선택하세요. 초기화한 설정은 다음 작업에 적용됩니다.");return;}
        r.ManualPlan=null;r.State="Analyzing";Replan();
    }
    private void OnRetry(object sender,RoutedEventArgs e)
    {
        if(current is not null||Queue.SelectedItem is not Row r)return;
        if(r.State is not ("Failed" or "Canceled")){Summary.Text=T("Select a failed or canceled job.","실패하거나 취소된 작업을 선택하세요.");return;}
        if(r.Media is null){Summary.Text=T("Source analysis failed or was canceled. Remove and add the file again.","원본 분석이 실패했거나 취소되었습니다. 목록에서 제거한 후 다시 추가하세요.");return;}
        try{r.Stamp?.RequireUnchanged(r.Input);}catch(Exception ex){Summary.Text=ex.Message;return;}
        r.State="Analyzing";Replan();
    }
    private void OnPlayResult(object sender,RoutedEventArgs e)
    {
        if(Queue.SelectedItem is not Row r||r.State!="Complete"||!System.IO.File.Exists(r.Output))
        {UpdateSelection();Summary.Text=T("Select an existing completed output.","완료된 출력 파일을 선택하세요.");return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(r.Output){UseShellExecute=true});}
        catch(Exception ex){Summary.Text=T("Could not open the video: "+ex.Message,"영상을 열지 못했습니다: "+ex.Message);}
    }
    private void OnOpenResult(object sender,RoutedEventArgs e)
    {
        if(Queue.SelectedItem is not Row r||r.State!="Complete"||!System.IO.File.Exists(r.Output)){Summary.Text=T("Select an existing completed output.","완료된 출력 파일을 선택하세요.");return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetDirectoryName(r.Output)!){UseShellExecute=true});}
        catch(Exception ex){Summary.Text=ex.Message;}
    }
    private void OnCopy(object sender,RoutedEventArgs e)
    {
        if(Queue.SelectedItem is not Row r)return;
        try{Clipboard.SetText($"{r.Paths}\n{r.DisplayState}\n{r.DisplayDetail}");Summary.Text=T("Selected job details copied.","선택한 작업의 상세 정보를 복사했습니다.");}
        catch(Exception ex){Summary.Text=ex.Message;}
    }
    private void OnSaveDefaults(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        try
        {
            new BatchPreferences(((ProfileChoice)ProfileBox.SelectedItem).Key,(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec,(int)ParallelBox.SelectedItem,(ThreadsBox.SelectedItem as ThreadChoice)?.Count,(string)SharedPresetBox.SelectedValue).Save(PreferencesPath);
            Summary.Text=T("Batch defaults saved locally. No video paths or individual overrides were stored.","공통 기본값을 저장했습니다. 영상 경로와 개별 설정은 저장하지 않았습니다.");
        }
        catch(Exception ex){Summary.Text="Could not save defaults: "+ex.Message;}
    }
    private void OnResetDefaults(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        try
        {
            new BatchPreferences().Save(PreferencesPath);
            initialized=false;
            try{ProfileBox.SelectedIndex=1;RecommendationBox.SelectedIndex=0;ParallelBox.SelectedItem=2;ThreadsBox.SelectedIndex=0;SharedPresetBox.SelectedValue="medium";}
            finally{initialized=true;}
            Replan();Summary.Text+=" | Defaults reset.";
        }
        catch(Exception ex){Summary.Text="Could not reset defaults: "+ex.Message;}
    }
    private void OnBatchSpeed(object sender,RoutedEventArgs e)=>ApplySharedFast("libx264");
    private void OnBatchHevc(object sender,RoutedEventArgs e)=>ApplySharedFast("libx265");
    private void ApplySharedFast(string codec)
    {
        if(current is not null||tools is null||!tools.Codecs.Contains(codec)||!rows.Any(r=>r.Media is not null&&r.State is "Ready" or "Skipped"))return;
        var choice=RecommendationBox.Items.Cast<RecommendationChoice>().FirstOrDefault(c=>c.Codec==codec);
        if(choice is null)return;
        initialized=false;
        try{RecommendationBox.SelectedItem=choice;SharedPresetBox.SelectedValue="veryfast";}
        finally{initialized=true;}
        Replan();
        Summary.Text+=T(" | Shared codec and preset changed; budget recalculated, individual overrides retained."," | 공통 코덱·프리셋 변경, 용량 예산 재계산, 개별 설정 유지.");
    }
    private void OnProfile(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)Replan();}
    private void OnResourceSettings(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)UpdateSummary();}
    private void OnHardwareSettings(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized)UpdateSelection();}
    private void OnHardwareToggle(object sender,RoutedEventArgs e){if(initialized)UpdateSelection();}
    private static bool FastDevice(string? backend)=>backend is not null&&(backend.EndsWith("_nvenc",StringComparison.Ordinal)||backend.EndsWith("_qsv",StringComparison.Ordinal));
    private string? HardwareProblem(string? backend,bool resident,bool fast,Row[] ready)
    {
        if(backend is null||!EncoderDiagnostics.HardwareCandidates.Contains(backend))return T("Select an explicit GPU backend.","GPU 장치를 직접 선택하세요.");
        if((ThreadsBox.SelectedItem as ThreadChoice)?.Count is not null)return T("Open resource settings and set threads to Auto.","자원 설정을 열고 스레드를 자동으로 바꾸세요.");
        if(ready.Length==0)return T("Add ready videos first.","준비된 영상을 먼저 추가하세요.");
        string family=backend.StartsWith("hevc_",StringComparison.Ordinal)?"libx265":"libx264";
        if(ready.Any(r=>r.Plan?.Codec!=family))return T("Choose a GPU with the same codec as all ready videos, or edit the individual settings.","준비된 영상과 코덱이 같은 GPU를 선택하거나 영상별 설정을 수정하세요.");
        if(fast&&!FastDevice(backend))return T("Fast preset is available for NVENC/QSV only.","속도 우선 프리셋은 NVENC/QSV만 지원합니다.");
        if(resident&&ready.Any(r=>r.Media?.Codec is not ("h264" or "hevc")||(backend.EndsWith("_amf",StringComparison.Ordinal)&&r.Plan?.Height!=r.Media?.Height)))return T("Resident trials require AVC/HEVC input; AMD preserves original height.","연속 처리 시험은 AVC/HEVC 입력이 필요하며 AMD는 원본 해상도를 유지해야 합니다.");
        return null;
    }
    private void RefreshHardwareControls(bool idle)
    {
        string? backend=BatchHardwareBox.SelectedItem as string;
        BatchHardwareBox.IsEnabled=idle;
        BatchResidentBox.IsEnabled=BatchOverlapBox.IsEnabled=idle&&backend is not null;
        BatchHardwareFastBox.IsEnabled=idle&&FastDevice(backend);
        if(idle&&!FastDevice(backend))BatchHardwareFastBox.IsChecked=false;
        var ready=rows.Where(r=>r.State=="Ready"&&r.Plan is not null).ToArray();
        string? problem=HardwareProblem(backend,BatchResidentBox.IsChecked==true,BatchHardwareFastBox.IsChecked==true,ready);
        BatchHardwareStartButton.IsEnabled=idle&&tools is not null&&problem is null;
        BatchHardwareStatus.Text=idle?(problem??T("Use the GPU batch button here. The bottom start button uses CPU. Size settings stay the same; no automatic CPU fallback or CPU time estimate.","이 영역의 GPU 일괄 시험 버튼을 누르세요. 하단 시작 버튼은 CPU를 사용합니다. 용량 설정은 유지하며 CPU 자동 전환 및 CPU 예상 시간 적용은 하지 않습니다.")):T("Settings locked during processing.","작업 중 설정 변경 불가.");
    }
    private void SetBusy(bool busy)
    {
        AddButton.IsEnabled=ClearButton.IsEnabled=RemoveButton.IsEnabled=ProfileBox.IsEnabled=SharedPresetBox.IsEnabled=ParallelBox.IsEnabled=ThreadsBox.IsEnabled=RecommendationBox.IsEnabled=!busy;
        FolderButton.IsEnabled=SourceFolderButton.IsEnabled=RetryButton.IsEnabled=EditPlanButton.IsEnabled=ResetPlanButton.IsEnabled=SaveDefaultsButton.IsEnabled=ResetDefaultsButton.IsEnabled=!busy;
        CancelButton.IsEnabled=busy;UpdateSummary();
    }
    private void OnQueueSelection(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(initialized)UpdateSelection();
    }
    private void OnQueueFirst(object sender,RoutedEventArgs e)=>MoveReady(-2);
    private void OnQueueUp(object sender,RoutedEventArgs e)=>MoveReady(-1);
    private void OnQueueDown(object sender,RoutedEventArgs e)=>MoveReady(1);
    private void MoveReady(int direction)
    {
        if(current is not null||Queue.SelectedItem is not Row row||row.State!="Ready")return;
        var ready=rows.Where(r=>r.State=="Ready").ToArray();
        int index=Array.IndexOf(ready,row),target=direction==-2?0:index+direction;
        if(index<0||target<0||target>=ready.Length||target==index)return;
        // Move only the selected ready job; do not recompute settings or output names.
        rows.Move(rows.IndexOf(row),rows.IndexOf(ready[target]));
        Queue.SelectedItem=row;
        UpdateSummary();
    }
    private void UpdateSelection()
    {
        var row=Queue.SelectedItem as Row;
        bool idle=current is null;
        RefreshHardwareControls(idle);
        bool canFast=idle&&tools is not null&&rows.Any(r=>r.Media is not null&&(r.State is "Ready" or "Skipped"));
        BatchSpeedButton.IsEnabled=canFast&&tools?.Codecs.Contains("libx264")==true;
        BatchHevcButton.IsEnabled=canFast&&tools?.Codecs.Contains("libx265")==true;
        int readyIndex=-1,readyCount=0;
        foreach(var readyRow in rows.Where(r=>r.State=="Ready"))
        {
            if(ReferenceEquals(row,readyRow))readyIndex=readyCount;
            readyCount++;
        }
        QueueFirstButton.IsEnabled=QueueUpButton.IsEnabled=idle&&readyIndex>0;
        QueueDownButton.IsEnabled=idle&&readyIndex>=0&&readyIndex<readyCount-1;
        SelectedVideoText.Text=row is null?T("No video selected","선택된 영상 없음"):$"{row.InputName} — {row.DisplayState}";
        EditPlanButton.IsEnabled=idle&&tools is not null&&row is not null&&BatchRecovery.CanEdit(row.State,row.Media is not null);
        ResetPlanButton.IsEnabled=EditPlanButton.IsEnabled&&row?.ManualPlan is not null;
        RetryButton.IsEnabled=idle&&row is not null&&BatchRecovery.CanRetry(row.State,row.Media is not null);
        RemoveButton.IsEnabled=idle&&row is not null;
        PlayResultButton.IsEnabled=OpenResultButton.IsEnabled=row is not null&&row.State=="Complete"&&System.IO.File.Exists(row.Output);
        CopyButton.IsEnabled=row is not null;
    }
    private string ToolScope(int parallel)=>tools!.Ffmpeg+"|"+tools.Ffprobe+"|parallel="+parallel;
    private static string OutputScope(Row row,string toolScope)=>toolScope+"|volume="+(System.IO.Path.GetPathRoot(row.Output)??"").ToUpperInvariant();
    private DurationRange? Reference(Row row,int? threads,string toolScope)
    {
        if(row.Media is not MediaInfo m||row.Plan is not Plan p||row.Stamp is not SourceStamp stamp||string.IsNullOrWhiteSpace(row.Output))return null;
        string key=OutputScope(row,toolScope);
        if(batchHistory.Previous(row.Input,stamp,m,p,threads,key) is TimeSpan previous)
        {
            return new(TimeSpan.FromSeconds(Math.Floor(previous.TotalSeconds)),TimeSpan.FromSeconds(Math.Max(1,Math.Ceiling(previous.TotalSeconds))));
        }
        return batchHistory.Estimate(row.Input,m,p,threads,key) is TimingRange observed?new(observed.Low,observed.High):null;
    }
    private string QueueReferenceText(DurationRange? estimate,int known,int needed,bool beforeStart)=>estimate is DurationRange range
        ?(beforeStart?T("Before start ≈ ","시작 전 예상 ≈ "):T("Queue scenario ≈ ","전체 예상 범위 ≈ "))+UiLanguage.Duration(range.Low)+"–"+UiLanguage.Duration(range.High)+T(" · prior jobs, not guaranteed"," · 이전 작업 참고, 보장 없음")
        :(beforeStart?T("Before start: time unknown","시작 전 예상 시간 미정"):T("Queue ETA unknown","전체 ETA 미정"))+T($" · references {known}/{needed}",$" · 시간 참고값 {known}/{needed}");
    private void UpdateSummary()
    {
        UpdateSelection();
        int ready=rows.Count(r=>r.State=="Ready");
        StartButton.IsEnabled=current is null&&ready>0;
        StartButton.Content=current is not null?T("Processing…","작업 중…"):ready>0?T($"Start compression · {ready} video(s)",$"영상 {ready}개 압축 시작"):T("Start batch compression","일괄 압축 시작");
        var pending=rows.Where(r=>r.State=="Ready"&&r.Plan is not null&&r.Media is not null).ToArray();
        double inputBytes=pending.Sum(r=>(double)r.Media!.Bytes), budget=pending.Sum(r=>r.Plan!.EstimateBytes(r.Media!));
        int running=rows.Count(r=>r.State.StartsWith("Encoding")||r.State is "Preparing" or "Validating" or "Waiting for encoder" or "Waiting for validation" or "Qualifying hardware"), queued=rows.Count(r=>r.State=="Queued"), analyzing=rows.Count(r=>r.State=="Analyzing");
        int complete=rows.Count(r=>r.State=="Complete"), failed=rows.Count(r=>r.State=="Failed"), canceled=rows.Count(r=>r.State=="Canceled"), skipped=rows.Count(r=>r.State=="Skipped");
        Summary.Text=T($"{rows.Count} videos · {ready} ready · {complete} complete · {running} running · {queued} queued · {analyzing} analyzing · {failed} failed · {canceled} canceled · {skipped} skipped",$"영상 {rows.Count}개 · 준비 {ready} · 완료 {complete} · 진행 {running} · 대기 {queued} · 분석 {analyzing} · 실패 {failed} · 취소 {canceled} · 건너뜀 {skipped}");
        if(pending.Length>0)Summary.Text+=T($" | Ready: {inputBytes/1e6:F1} MB → {budget/1e6:F1} MB bitrate budget (not guaranteed)",$" | 대기: {inputBytes/1e6:F1} MB → {budget/1e6:F1} MB 용량 예산 (보장 없음)");
        var totals=BatchTotals.From(rows.Where(r=>r.State=="Complete"&&r.Report is not null).Select(r=>r.Report!));
        if(lastBatchElapsed is TimeSpan elapsed)Summary.Text+=T($" | Last run elapsed {elapsed.TotalSeconds:F1}s",$" | 최근 실행 경과 시간 {elapsed.TotalSeconds:F1}초");
        if(totals.Completed>0)Summary.Text+=T($" | Completed: {totals.InputBytes/1e6:F2} → {totals.OutputBytes/1e6:F2} MB ({totals.Savings:P1} actual savings); {totals.JobTime.TotalSeconds:F1}s summed job time",$" | 완료: {totals.InputBytes/1e6:F2} → {totals.OutputBytes/1e6:F2} MB (실제 절감률 {totals.Savings:P1}); 작업 시간 합계 {totals.JobTime.TotalSeconds:F1}초");
        BatchPreflightText.Text="";BatchPreflightText.Visibility=Visibility.Collapsed;
        if(current is null&&ParallelBox.SelectedItem is int parallel)
        {
            try
            {
                var count=ThreadBudget.PerJob((ThreadsBox.SelectedItem as ThreadChoice)?.Count,parallel);
                if(pending.Length>0&&tools is not null)
                {
                    BatchPreflightText.Visibility=Visibility.Visible;
                    if(pending.Length>100)BatchPreflightText.Text=T("Before start: time unknown · large queue","시작 전 예상 시간 미정 · 대규모 대기열");
                    else
                    {
                        var refs=pending.Select(r=>Reference(r,count,ToolScope(parallel))).ToArray();
                        BatchPreflightText.Text=QueueReferenceText(QueueTiming.Estimate(parallel,[],refs),refs.Count(r=>r is not null),refs.Length,true);
                    }
                }
                Summary.Text+=count is int n?T($" | {n} requested encoder threads/job (not a CPU cap)",$" | 작업당 인코더 스레드 {n}개 요청 (CPU 상한 아님)"):T(" | Automatic encoder threads"," | 자동 인코더 스레드");
            }
            catch(ArgumentException ex){Summary.Text+=" | "+ex.Message;StartButton.IsEnabled=false;}
        }
    }
    private void RefreshBatchProgress()
    {
        if(activeRun is not Row[] run)return;
        foreach(var row in run)
        {
            if(row.Eta is not EncodingEta estimator)continue;
            var remaining=estimator.Remaining(batchClock.Elapsed);
            var rate=current?.IsCancellationRequested==true?null:estimator.RealtimeRate(batchClock.Elapsed);
            if(row.Remaining!=remaining||row.Rate!=rate){row.Remaining=remaining;row.Rate=rate;row.Refresh();}
        }
        int settled=run.Count(r=>r.State is "Complete" or "Failed" or "Canceled");
        int complete=run.Count(r=>r.State=="Complete");
        BatchProgressBar.Maximum=run.Length;BatchProgressBar.Value=settled;
        BatchProgressText.Text=T($"Settled {settled}/{run.Length} · verified {complete}",$"처리 종료 {settled}/{run.Length} · 검증 완료 {complete}");
        BatchElapsedText.Text=T("Elapsed: ","경과: ")+UiLanguage.Duration(batchClock.Elapsed);
        if(current?.IsCancellationRequested==true){BatchEtaText.Text=T("Canceling…","취소 중…");return;}
        if(activeHardware){BatchEtaText.Text=T("GPU queue ETA unknown · CPU references excluded","GPU 전체 ETA 미정 · CPU 시간 참고값 미적용");return;}
        if(run.Length>100){BatchEtaText.Text=T("Queue ETA unknown · large queue","전체 ETA 미정 · 대규모 대기열");return;}
        var active=new List<DurationRange?>();var queued=new List<DurationRange?>();
        foreach(var r in run.Where(r=>r.State is not ("Complete" or "Failed" or "Canceled")))
        {
            DurationRange? reference=Reference(r,activeThreads,activeToolKey);
            if(r.AdmittedAt is TimeSpan start)active.Add(QueueTiming.Remaining(reference,batchClock.Elapsed-start));
            else queued.Add(reference);
        }
        var queueRange=QueueTiming.Estimate(activeParallel,active,queued);
        int known=active.Concat(queued).Count(r=>r is not null),needed=active.Count+queued.Count;
        BatchEtaText.Text=QueueReferenceText(queueRange,known,needed,false);
    }
    private void Replan()
    {
        var reserved=rows.Select(r=>r.Input).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var r in rows.Where(r=>r.State=="Complete"))reserved.Add(r.Output);
        foreach(var r in rows.Where(r=>r.Media is not null&&(r.State is "Ready" or "Skipped" or "Analyzing")))
        {
            r.Plan=null;r.Output="";r.DeviceRequest=null;
            try
            {
                var m=r.Media!;
                r.Stamp?.RequireUnchanged(r.Input);
                var p=BatchPlanSelection.Resolve(m,tools!.Codecs,((ProfileChoice)ProfileBox.SelectedItem).Key,(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec,r.ManualPlan,(string)SharedPresetBox.SelectedValue);
                if(p is null){r.State="Skipped";r.Detail=T("Selected codec or budget tier unavailable; choose another or use single-file settings.","선택한 코덱 또는 플랜을 사용할 수 없습니다. 다른 플랜을 선택하거나 개별 설정을 수정하세요.");r.Refresh();continue;}
                r.Plan=p;r.Output=OutputNames.Suggest(r.Input,reserved,destinationDirectory);
                r.State="Ready";r.Detail=r.ManualPlan is null?T("Shared profile; size not guaranteed.","공통 플랜 · 용량 보장 없음"):T("Individual override; size not guaranteed.","개별 설정 · 용량 보장 없음");
            }
            catch(Exception ex){r.State="Failed";r.Detail=ex.Message;}
            r.Refresh();
        }
        UpdateSummary();
    }
    internal async Task AddAsync(string[] paths)
    {
        if(current is not null)return;
        using var cts=new CancellationTokenSource();current=cts;SetBusy(true);
        try
        {
            tools=await Toolchain.DiscoverAsync(cts.Token);
            foreach(var raw in paths)
            {
                cts.Token.ThrowIfCancellationRequested();
                var path=System.IO.Path.GetFullPath(raw);
                if(rows.Any(r=>string.Equals(r.Input,path,StringComparison.OrdinalIgnoreCase)))continue;
                var row=new Row(path,korean);rows.Add(row);
                try
                {
                    var stamp=SourceStamp.Capture(path);
                    var m=await Probe.InspectAsync(tools.Ffprobe,path,cts.Token);
                    stamp.RequireUnchanged(path);
                    if(!m.Supported){row.State="Skipped";row.Detail=m.Reason;}
                    else {row.Media=m.Info;row.Stamp=stamp;}
                }
                catch(OperationCanceledException){row.State="Canceled";row.Refresh();throw;}
                catch(Exception ex){row.State="Failed";row.Detail=ex.Message;}
                row.Refresh();
            }
            Replan();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"VANTA");}
        finally{current=null;Replan();SetBusy(false);}
    }
    private void OnCancel(object sender,RoutedEventArgs e)=>current?.Cancel();
    private async void OnStart(object sender,RoutedEventArgs e)=>await StartAsync();
    private async void OnHardwareStart(object sender,RoutedEventArgs e)
    {
        if(BatchHardwareBox.SelectedItem is string backend)await StartAsync(backend,BatchResidentBox.IsChecked==true,BatchHardwareFastBox.IsChecked==true,BatchOverlapBox.IsChecked==true);
    }
    internal async Task StartAsync(string? backend=null,bool resident=false,bool fast=false,bool overlap=false)
    {
        if(current is not null||tools is null)return;
        var ready=rows.Where(r=>r.State=="Ready"&&r.Plan is not null).ToArray();
        if(ready.Length==0)return;
        if(backend is not null&&HardwareProblem(backend,resident,fast,ready) is string problem){BatchHardwareStatus.Text=problem;return;}
        int parallel=backend is null?(int)ParallelBox.SelectedItem:overlap?2:1;
        var totalThreads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        int? effectiveThreads;
        try{effectiveThreads=ThreadBudget.PerJob(totalThreads,parallel);}catch(ArgumentException ex){Summary.Text=ex.Message;return;}
        var jobs=ready.Select(r=>new BatchJob(r.Input,r.Output,r.Plan!,r.Stamp)).ToArray();
        using var cts=new CancellationTokenSource();current=cts;
        foreach(var r in ready){r.DeviceRequest=backend is null?null:backend+(resident?" · resident":"")+(fast?(backend.EndsWith("_qsv",StringComparison.Ordinal)?" · veryfast":" · p1"):" · default");r.State="Queued";r.Detail="";r.Report=null;r.Eta=new EncodingEta(r.Media?.Seconds);r.Remaining=null;r.Rate=null;r.Verification=null;r.AdmittedAt=null;r.Refresh();}
        lastBatchElapsed=null;
        activeRun=ready;activeParallel=parallel;activeThreads=effectiveThreads;activeHardware=backend is not null;
        activeToolKey=ToolScope(parallel);
        batchClock.Restart();BatchProgressCard.Visibility=Visibility.Visible;batchTimer.Start();
        SetBusy(true);RefreshBatchProgress();
        try
        {
            // Synchronous dispatch preserves order and prevents stale callbacks after completion.
            void ApplyUpdate(BatchUpdate x)
            {
                var r=ready[x.Index];
                if(x.Stage=="Preparing"&&r.AdmittedAt is null)r.AdmittedAt=batchClock.Elapsed;
                if(!activeHardware&&x.Stage=="Complete"&&r.State!="Complete"&&x.Report is EncodeReport observed&&r.Media is MediaInfo m&&r.Plan is Plan p&&r.Stamp is SourceStamp stamp)
                    batchHistory.Record(new(r.Input,m,p,activeThreads,OutputScope(r,activeToolKey),observed.Total,stamp));
                var state=x.Stage=="Encoding"&&new EncodeProgress(x.Stage,x.Fraction).DisplayPercentText is string percent?$"Encoding {percent}":x.Stage;
                r.Eta?.Observe(new(x.Stage,x.Fraction),batchClock.Elapsed);
                var remaining=r.Eta?.Remaining(batchClock.Elapsed);
                var rate=cts.IsCancellationRequested?null:r.Eta?.RealtimeRate(batchClock.Elapsed);
                if(x.Stage is "Complete" or "Failed" or "Canceled")r.Eta=null;
                if(r.State==state&&r.Detail==(x.Detail??"")&&r.Report==x.Report&&r.Remaining==remaining&&r.Rate==rate&&r.Verification==x.Verification)return;
                r.Remaining=remaining;r.Rate=rate;r.Verification=x.Verification;
                r.State=state;r.Detail=x.Detail??"";r.Report=x.Report;r.Refresh();
            }
            var progress=new UiProgress(x=>Dispatcher.Invoke(()=>{ApplyUpdate(x);UpdateSummary();RefreshBatchProgress();}));
            var terminal=backend is null?await BatchEncoder.RunAsync(tools,jobs,parallel,progress,cts.Token,totalEncoderThreads:totalThreads)
                :await BatchEncoder.RunHardwareTrialAsync(tools,jobs,backend,progress,cts.Token,overlap,resident,fast);
            foreach(var result in terminal)ApplyUpdate(result);
        }
        catch(OperationCanceledException){}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"VANTA");}
        finally
        {
            foreach(var r in ready.Where(r=>r.State is "Queued" or "Preparing" or "Validating" or "Waiting for encoder" or "Waiting for validation" or "Qualifying hardware"||r.State.StartsWith("Encoding")))
            {r.State=cts.IsCancellationRequested?"Canceled":"Failed";r.Refresh();}
            foreach(var r in ready){r.Eta=null;r.Remaining=null;r.Rate=null;r.Verification=null;r.AdmittedAt=null;r.Refresh();}
            batchTimer.Stop();batchClock.Stop();lastBatchElapsed=batchClock.Elapsed;activeRun=null;activeHardware=false;
            BatchProgressCard.Visibility=Visibility.Collapsed;BatchProgressText.Text=BatchElapsedText.Text=BatchEtaText.Text="";
            current=null;SetBusy(false);
        }
    }
    private sealed class UiProgress(Action<BatchUpdate> report):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>report(value);}
}