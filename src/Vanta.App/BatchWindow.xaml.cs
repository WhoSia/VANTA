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
    private string? destinationDirectory;
    private sealed class Row(string input):INotifyPropertyChanged
    {
        public string Input {get;}=input;
        public string Output {get;set;}="";
        public string InputName=>System.IO.Path.GetFileName(Input);
        public string OutputName=>System.IO.Path.GetFileName(Output);
        public string Paths=>$"Source: {Input}\nOutput: {Output}";
        public string Budget {get;set;}="";
        public string State {get;set;}="Analyzing";
        public string Detail {get;set;}="";
        public MediaInfo? Media {get;set;}
        public SourceStamp? Stamp {get;set;}
        public Plan? Plan {get;set;}
        public Plan? ManualPlan {get;set;}
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Refresh()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(string.Empty));
    }
    public BatchWindow(string[]? paths=null)
    {
        initial=paths??[];
        InitializeComponent();
        Queue.ItemsSource=rows;
        ProfileBox.ItemsSource=new[]{"Quality","Balanced","Compact"};
        ProfileBox.SelectedIndex=1;
        RecommendationBox.ItemsSource=RecommendationChoices.Create();RecommendationBox.SelectedIndex=0;
        ParallelBox.ItemsSource=new[]{1,2};ParallelBox.SelectedIndex=1;
        ThreadsBox.ItemsSource=ThreadChoices.Create();ThreadsBox.SelectedIndex=0;
        initialized=true;
        Loaded+=async (_,_)=>{if(initial.Length>0)await AddAsync(initial);};
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
        if(current is not null){e.Cancel=true;current.Cancel();Summary.Text="Canceling. Close after cleanup completes.";}
    }
    private void OnClear(object sender,RoutedEventArgs e){if(current is null){rows.Clear();UpdateSummary();}}
    private void OnRemove(object sender,RoutedEventArgs e){if(current is null&&Queue.SelectedItem is Row r){rows.Remove(r);UpdateSummary();}}
    private void OnFolder(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        var dialog=new OpenFolderDialog{Title="Choose output folder",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        destinationDirectory=dialog.FolderName;DestinationText.Text="Output: "+destinationDirectory;Replan();
    }
    private void OnSourceFolder(object sender,RoutedEventArgs e)
    {
        if(current is not null)return;
        destinationDirectory=null;DestinationText.Text="Output: beside each source";Replan();
    }
    private void OnEditPlan(object sender,RoutedEventArgs e)
    {
        if(current is not null||tools is null||Queue.SelectedItem is not Row r||r.Media is null||r.State is not ("Ready" or "Skipped"))
        {Summary.Text="Select a ready or supported skipped video.";return;}
        try
        {
            r.Stamp?.RequireUnchanged(r.Input);
            var dialog=new BatchPlanDialog(r.Media,tools.Codecs,r.ManualPlan??r.Plan){Owner=this};
            if(dialog.ShowDialog()!=true||dialog.Result is null)return;
            r.ManualPlan=dialog.Result;Replan();
        }
        catch(Exception ex){Summary.Text=ex.Message;}
    }
    private void OnResetPlan(object sender,RoutedEventArgs e)
    {
        if(current is not null||Queue.SelectedItem is not Row r||r.State is not ("Ready" or "Skipped"))
        {Summary.Text="Select a ready or supported skipped video.";return;}
        r.ManualPlan=null;Replan();
    }
    private void OnRetry(object sender,RoutedEventArgs e)
    {
        if(current is not null||Queue.SelectedItem is not Row r)return;
        if(r.State is not ("Failed" or "Canceled")){Summary.Text="Select a failed or canceled job.";return;}
        if(r.Media is null){Summary.Text="Source analysis failed or was canceled. Remove and add the file again.";return;}
        try{r.Stamp?.RequireUnchanged(r.Input);}catch(Exception ex){Summary.Text=ex.Message;return;}
        r.State="Analyzing";Replan();
    }
    private void OnOpenResult(object sender,RoutedEventArgs e)
    {
        if(Queue.SelectedItem is not Row r||r.State!="Complete"||!System.IO.File.Exists(r.Output)){Summary.Text="Select an existing completed output.";return;}
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.GetDirectoryName(r.Output)!){UseShellExecute=true});}
        catch(Exception ex){Summary.Text=ex.Message;}
    }
    private void OnCopy(object sender,RoutedEventArgs e)
    {
        if(Queue.SelectedItem is not Row r)return;
        try{Clipboard.SetText($"{r.Paths}\n{r.State}\n{r.Detail}");Summary.Text="Selected job details copied.";}
        catch(Exception ex){Summary.Text=ex.Message;}
    }
    private void OnProfile(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)Replan();}
    private void OnResourceSettings(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)UpdateSummary();}
    private void SetBusy(bool busy)
    {
        AddButton.IsEnabled=ClearButton.IsEnabled=RemoveButton.IsEnabled=ProfileBox.IsEnabled=ParallelBox.IsEnabled=ThreadsBox.IsEnabled=RecommendationBox.IsEnabled=!busy;
        FolderButton.IsEnabled=SourceFolderButton.IsEnabled=RetryButton.IsEnabled=EditPlanButton.IsEnabled=ResetPlanButton.IsEnabled=!busy;
        CancelButton.IsEnabled=busy;UpdateSummary();
    }
    private void UpdateSummary()
    {
        int ready=rows.Count(r=>r.State=="Ready");
        StartButton.IsEnabled=current is null&&ready>0;
        var pending=rows.Where(r=>r.State=="Ready"&&r.Plan is not null&&r.Media is not null).ToArray();
        double inputBytes=pending.Sum(r=>(double)r.Media!.Bytes), budget=pending.Sum(r=>r.Plan!.EstimateBytes(r.Media!));
        Summary.Text=$"{rows.Count} videos · {ready} ready · {rows.Count(r=>r.State=="Complete")} complete · {rows.Count(r=>r.State=="Failed")} failed · {rows.Count(r=>r.State=="Canceled")} canceled · {rows.Count(r=>r.State=="Skipped")} skipped";
        if(pending.Length>0)Summary.Text+=$" | Ready: {inputBytes/1e6:F1} MB → {budget/1e6:F1} MB bitrate budget (not guaranteed)";
        if(current is null&&ParallelBox.SelectedItem is int parallel)
        {
            try
            {
                var count=ThreadBudget.PerJob((ThreadsBox.SelectedItem as ThreadChoice)?.Count,parallel);
                Summary.Text+=count is int n?$" | {n} requested encoder threads/job (not a CPU cap)":" | Automatic encoder threads";
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
            r.Plan=null;r.Budget="";r.Output="";
            try
            {
                var m=r.Media!;
                r.Stamp?.RequireUnchanged(r.Input);
                var p=BatchPlanSelection.Resolve(m,tools!.Codecs,(string)ProfileBox.SelectedItem,(RecommendationBox.SelectedItem as RecommendationChoice)?.Codec,r.ManualPlan);
                if(p is null){r.State="Skipped";r.Detail="Selected codec or budget tier unavailable; choose another or use single-file settings.";r.Refresh();continue;}
                r.Plan=p;r.Output=OutputNames.Suggest(r.Input,reserved,destinationDirectory);
                r.Budget=$"{p.Name} · {(p.Codec=="libx265"?"HEVC":"H.264")} · {p.Height}p · {p.VideoBitsPerSecond/1000d:F0} kbps · {p.EstimateBytes(m)/1e6:F2} MB";
                r.State="Ready";r.Detail=r.ManualPlan is null?"Shared profile; size not guaranteed.":"Individual override; size not guaranteed.";
            }
            catch(Exception ex){r.State="Failed";r.Detail=ex.Message;}
            r.Refresh();
        }
        UpdateSummary();
    }
    private async Task AddAsync(string[] paths)
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
                var row=new Row(path);rows.Add(row);
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
    private async void OnStart(object sender,RoutedEventArgs e)
    {
        if(current is not null||tools is null)return;
        var ready=rows.Where(r=>r.State=="Ready"&&r.Plan is not null).ToArray();
        if(ready.Length==0)return;
        int parallel=(int)ParallelBox.SelectedItem;
        var totalThreads=(ThreadsBox.SelectedItem as ThreadChoice)?.Count;
        try{ThreadBudget.PerJob(totalThreads,parallel);}catch(ArgumentException ex){Summary.Text=ex.Message;return;}
        var jobs=ready.Select(r=>new BatchJob(r.Input,r.Output,r.Plan!,r.Stamp)).ToArray();
        using var cts=new CancellationTokenSource();current=cts;
        foreach(var r in ready){r.State="Queued";r.Detail="";r.Refresh();}
        SetBusy(true);
        try
        {
            // Synchronous dispatch preserves order and prevents stale callbacks after completion.
            var progress=new UiProgress(x=>Dispatcher.Invoke(()=>{
                var r=ready[x.Index];
                r.State=x.Stage=="Encoding"&&new EncodeProgress(x.Stage,x.Fraction).DisplayPercent is int percent?$"Encoding {percent}%":x.Stage;
                r.Detail=x.Detail??"";r.Refresh();UpdateSummary();
            }));
            await BatchEncoder.RunAsync(tools,jobs,parallel,progress,cts.Token,totalEncoderThreads:totalThreads);
        }
        catch(OperationCanceledException){}
        catch(Exception ex){MessageBox.Show(this,ex.Message,"VANTA");}
        finally
        {
            foreach(var r in ready.Where(r=>r.State=="Queued"||r.State.StartsWith("Encoding")||r.State=="Validating"))
            {r.State=cts.IsCancellationRequested?"Canceled":"Failed";r.Refresh();}
            current=null;SetBusy(false);
        }
    }
    private sealed class UiProgress(Action<BatchUpdate> report):IProgress<BatchUpdate>{public void Report(BatchUpdate value)=>report(value);}
}