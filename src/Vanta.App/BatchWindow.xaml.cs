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
        public string Budget=>Plan is Plan p&&Media is MediaInfo m?$"{UiLanguage.Tier(Korean,p.Name)} · {(p.Codec=="libx265"?"HEVC":"H.264")} · {p.Height}p · {UiLanguage.Preset(Korean,p.Preset)} · {p.VideoBitsPerSecond/1000d:F0} kbps · {p.EstimateBytes(m)/1e6:F2} MB":"";
        public string DisplayDetail=>State=="Ready"?UiLanguage.Text(Korean,ManualPlan is null?"Shared profile; size not guaranteed.":"Individual override; size not guaranteed.",ManualPlan is null?"공통 플랜 · 용량 보장 없음":"개별 설정 · 용량 보장 없음"):State=="Complete"&&Report is EncodeReport r?UiLanguage.Text(Korean,Detail,$"{r.OutputBytes/1e6:F2} MB · 실제 절감률 {1-r.OutputBytes/(double)r.InputBytes:P1} · {r.Total.TotalSeconds:F1}초 (검증 {r.Validation.TotalSeconds:F1}초)"):Detail;
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
    public BatchWindow(string[]? paths=null,bool korean=false)
    {
        initial=paths??[];this.korean=korean;
        InitializeComponent();ApplyLanguageResources();
        Queue.ItemsSource=rows;
        ProfileBox.ItemsSource=new[]{"Quality","Balanced","Compact"}.Select(p=>new ProfileChoice(p,UiLanguage.Tier(korean,p))).ToArray();
        ProfileBox.SelectedIndex=1;
        UiLanguage.SetPresets(SharedPresetBox,korean);SharedPresetBox.SelectedValue="medium";
        RecommendationBox.ItemsSource=RecommendationChoices.Create(korean:korean);RecommendationBox.SelectedIndex=0;
        ParallelBox.ItemsSource=new[]{1,2};ParallelBox.SelectedIndex=1;
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
        UpdateSummary();
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
    private void OnProfile(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)Replan();}
    private void OnResourceSettings(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)UpdateSummary();}
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
    private void UpdateSelection()
    {
        var row=Queue.SelectedItem as Row;
        bool idle=current is null;
        SelectedVideoText.Text=row is null?T("No video selected","선택된 영상 없음"):$"{row.InputName} — {row.DisplayState}";
        EditPlanButton.IsEnabled=idle&&tools is not null&&row is not null&&BatchRecovery.CanEdit(row.State,row.Media is not null);
        ResetPlanButton.IsEnabled=EditPlanButton.IsEnabled&&row?.ManualPlan is not null;
        RetryButton.IsEnabled=idle&&row is not null&&BatchRecovery.CanRetry(row.State,row.Media is not null);
        RemoveButton.IsEnabled=idle&&row is not null;
        PlayResultButton.IsEnabled=OpenResultButton.IsEnabled=row is not null&&row.State=="Complete"&&System.IO.File.Exists(row.Output);
        CopyButton.IsEnabled=row is not null;
    }
    private void UpdateSummary()
    {
        UpdateSelection();
        int ready=rows.Count(r=>r.State=="Ready");
        StartButton.IsEnabled=current is null&&ready>0;
        StartButton.Content=current is not null?T("Processing…","작업 중…"):ready>0?T($"Start compression · {ready} video(s)",$"영상 {ready}개 압축 시작"):T("Start batch compression","일괄 압축 시작");
        var pending=rows.Where(r=>r.State=="Ready"&&r.Plan is not null&&r.Media is not null).ToArray();
        double inputBytes=pending.Sum(r=>(double)r.Media!.Bytes), budget=pending.Sum(r=>r.Plan!.EstimateBytes(r.Media!));
        int running=rows.Count(r=>r.State.StartsWith("Encoding")||r.State=="Validating"), queued=rows.Count(r=>r.State=="Queued"), analyzing=rows.Count(r=>r.State=="Analyzing");
        int complete=rows.Count(r=>r.State=="Complete"), failed=rows.Count(r=>r.State=="Failed"), canceled=rows.Count(r=>r.State=="Canceled"), skipped=rows.Count(r=>r.State=="Skipped");
        Summary.Text=T($"{rows.Count} videos · {ready} ready · {complete} complete · {running} running · {queued} queued · {analyzing} analyzing · {failed} failed · {canceled} canceled · {skipped} skipped",$"영상 {rows.Count}개 · 준비 {ready} · 완료 {complete} · 진행 {running} · 대기 {queued} · 분석 {analyzing} · 실패 {failed} · 취소 {canceled} · 건너뜀 {skipped}");
        if(pending.Length>0)Summary.Text+=T($" | Ready: {inputBytes/1e6:F1} MB → {budget/1e6:F1} MB bitrate budget (not guaranteed)",$" | 대기: {inputBytes/1e6:F1} MB → {budget/1e6:F1} MB 용량 예산 (보장 없음)");
        var totals=BatchTotals.From(rows.Where(r=>r.State=="Complete"&&r.Report is not null).Select(r=>r.Report!));
        if(lastBatchElapsed is TimeSpan elapsed)Summary.Text+=T($" | Last run elapsed {elapsed.TotalSeconds:F1}s",$" | 최근 실행 경과 시간 {elapsed.TotalSeconds:F1}초");
        if(totals.Completed>0)Summary.Text+=T($" | Completed: {totals.InputBytes/1e6:F2} → {totals.OutputBytes/1e6:F2} MB ({totals.Savings:P1} actual savings); {totals.JobTime.TotalSeconds:F1}s summed job time",$" | 완료: {totals.InputBytes/1e6:F2} → {totals.OutputBytes/1e6:F2} MB (실제 절감률 {totals.Savings:P1}); 작업 시간 합계 {totals.JobTime.TotalSeconds:F1}초");
        if(current is null&&ParallelBox.SelectedItem is int parallel)
        {
            try
            {
                var count=ThreadBudget.PerJob((ThreadsBox.SelectedItem as ThreadChoice)?.Count,parallel);
                Summary.Text+=count is int n?T($" | {n} requested encoder threads/job (not a CPU cap)",$" | 작업당 인코더 스레드 {n}개 요청 (CPU 상한 아님)"):T(" | Automatic encoder threads"," | 자동 인코더 스레드");
            }
            catch(ArgumentException ex){Summary.Text+=" | "+ex.Message;StartButton.IsEnabled=false;}
        }
    }
    private void Replan()
    {
        var reserved=rows.Select(r=>r.Input).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var r in rows.Where(r=>r.State=="Complete"))reserved.Add(r.Output);
        foreach(var r in rows.Where(r=>r.Media is not null&&(r.State is "Ready" or "Skipped" or "Analyzing")))
        {
            r.Plan=null;r.Output="";
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
    internal async Task StartAsync()
    {
        if(current is not null||tools is null)return;
        var ready=rows.Where(r=>r.State=="Ready"&&r.Plan is not null).ToArray();
        if(ready.Length==0)return;
        int parallel=(int)ParallelBox.SelectedItem;
        var totalThreads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        try{ThreadBudget.PerJob(totalThreads,parallel);}catch(ArgumentException ex){Summary.Text=ex.Message;return;}
        var jobs=ready.Select(r=>new BatchJob(r.Input,r.Output,r.Plan!,r.Stamp)).ToArray();
        using var cts=new CancellationTokenSource();current=cts;
        foreach(var r in ready){r.State="Queued";r.Detail="";r.Report=null;r.Refresh();}
        lastBatchElapsed=null;
        var clock=System.Diagnostics.Stopwatch.StartNew();
        SetBusy(true);
        try
        {
            // Synchronous dispatch preserves order and prevents stale callbacks after completion.
            void ApplyUpdate(BatchUpdate x)
            {
                var r=ready[x.Index];
                var state=x.Stage=="Encoding"&&new EncodeProgress(x.Stage,x.Fraction).DisplayPercent is int percent?$"Encoding {percent}%":x.Stage;
                if(r.State==state&&r.Detail==(x.Detail??"")&&r.Report==x.Report)return;
                r.State=state;r.Detail=x.Detail??"";r.Report=x.Report;r.Refresh();
            }
            var progress=new UiProgress(x=>Dispatcher.Invoke(()=>{ApplyUpdate(x);UpdateSummary();}));
            var terminal=await BatchEncoder.RunAsync(tools,jobs,parallel,progress,cts.Token,totalEncoderThreads:totalThreads);
            foreach(var result in terminal)ApplyUpdate(result);
        }
        catch(OperationCanceledException){}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"VANTA");}
        finally
        {
            foreach(var r in ready.Where(r=>r.State=="Queued"||r.State.StartsWith("Encoding")||r.State=="Validating"))
            {r.State=cts.IsCancellationRequested?"Canceled":"Failed";r.Refresh();}
            lastBatchElapsed=clock.Elapsed;current=null;SetBusy(false);
        }
    }
    private sealed class UiProgress(Action<BatchUpdate> report):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>report(value);}
}