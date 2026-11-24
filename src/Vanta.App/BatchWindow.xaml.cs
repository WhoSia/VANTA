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
    private sealed class Row(string input):INotifyPropertyChanged
    {
        public string Input {get;}=input;
        public string Output {get;set;}="";
        public string Budget {get;set;}="";
        public string State {get;set;}="Analyzing";
        public string Detail {get;set;}="";
        public MediaInfo? Media {get;set;}
        public Plan? Plan {get;set;}
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
        ParallelBox.ItemsSource=new[]{1,2};ParallelBox.SelectedIndex=1;
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
    private void OnProfile(object sender,System.Windows.Controls.SelectionChangedEventArgs e){if(initialized&&current is null)Replan();}
    private void SetBusy(bool busy)
    {
        AddButton.IsEnabled=ClearButton.IsEnabled=RemoveButton.IsEnabled=ProfileBox.IsEnabled=ParallelBox.IsEnabled=!busy;
        CancelButton.IsEnabled=busy;UpdateSummary();
    }
    private void UpdateSummary()
    {
        int ready=rows.Count(r=>r.State=="Ready");
        StartButton.IsEnabled=current is null&&ready>0;
        Summary.Text=$"{rows.Count} videos · {ready} ready · {rows.Count(r=>r.State=="Complete")} complete · {rows.Count(r=>r.State=="Failed")} failed · {rows.Count(r=>r.State=="Canceled")} canceled";
    }
    private void Replan()
    {
        var reserved=rows.Select(r=>r.Input).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var r in rows.Where(r=>r.State=="Complete"))reserved.Add(r.Output);
        foreach(var r in rows.Where(r=>r.Media is not null&&r.State!="Complete"))
        {
            r.Plan=null;r.Budget="";r.Output="";
            try
            {
                var m=r.Media!;
                var p=Planner.Propose(m).FirstOrDefault(p=>p.Name==(string)ProfileBox.SelectedItem);
                if(p is null){r.State="Skipped";r.Detail="Selected profile unavailable; choose another profile or use single-file settings.";r.Refresh();continue;}
                if(!tools!.Codecs.Contains(p.Codec))p=p with {Codec=tools.Codecs[0]};
                r.Plan=p;r.Output=OutputNames.Suggest(r.Input,reserved);
                r.Budget=$"{p.Name} · {p.EstimateBytes(m)/1e6:F2} MB";
                r.State="Ready";r.Detail="Bitrate budget, not guaranteed size.";
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
                    var m=await Probe.InspectAsync(tools.Ffprobe,path,cts.Token);
                    if(!m.Supported){row.State="Skipped";row.Detail=m.Reason;}
                    else row.Media=m.Info;
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
        var jobs=ready.Select(r=>new BatchJob(r.Input,r.Output,r.Plan!)).ToArray();
        using var cts=new CancellationTokenSource();current=cts;
        foreach(var r in ready){r.State="Queued";r.Detail="";r.Refresh();}
        SetBusy(true);
        try
        {
            // Synchronous dispatch preserves order and prevents stale callbacks after completion.
            var progress=new UiProgress(x=>Dispatcher.Invoke(()=>{
                var r=ready[x.Index];
                r.State=x.Stage=="Encoding"&&x.Fraction is double f?$"Encoding {f:P0}":x.Stage;
                r.Detail=x.Detail??"";r.Refresh();UpdateSummary();
            }));
            await BatchEncoder.RunAsync(tools,jobs,(int)ParallelBox.SelectedItem,progress,cts.Token);
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