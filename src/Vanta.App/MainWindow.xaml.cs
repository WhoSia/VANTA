using System.Windows;
using Vanta.Core;
using Microsoft.Win32;
namespace Vanta.App;
public partial class MainWindow : Window
{
    private CancellationTokenSource? current;
    public MainWindow()=>InitializeComponent();
    private async void OnOpen(object sender,RoutedEventArgs e)
    {
        var dlg=new OpenFileDialog{Filter="Video files|*.mp4;*.mkv;*.mov;*.webm;*.avi|All files|*.*"};
        if(dlg.ShowDialog()==true) await LoadAsync(dlg.FileName);
    }
    private async void OnDrop(object sender,DragEventArgs e)
    {
        if(e.Data.GetData(DataFormats.FileDrop) is string[] {Length:1} paths) await LoadAsync(paths[0]);
    }
    private async Task LoadAsync(string path)
    {
        current?.Cancel();
        using var cts=new CancellationTokenSource();
        current=cts;
        Plans.ItemsSource=null; Source.Text=""; Status.Text="Analyzing…";
        try
        {
            var m=await Probe.ReadAsync("ffprobe",path,cts.Token);
            if(current!=cts) return;
            Source.Text=$"{System.IO.Path.GetFileName(path)} · {m.Width}×{m.Height} · {m.Fps:F2} fps · {m.Codec} · {m.Bytes/1e6:F1} MB";
            Plans.ItemsSource=Planner.Propose(m).Select(p=>new {Name=$"{p.Name} — {p.EstimateBytes(m)/1e6:F1} MB ({p.Savings(m):P1} saved)"}).ToArray();
            Status.Text="Bitrate estimates only. Encoding is not yet enabled.";
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(current==cts) Status.Text=ex.Message;}
        finally{if(current==cts) current=null;}
    }
}