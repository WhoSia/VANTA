using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using Vanta.App;
using Vanta.Core;

internal static class DesktopSmoke
{
    [STAThread]
    public static void Main(string[] args)
    {
        // No Show/ShowDialog, clipboard, real user settings writes or external UI automation.
        // Construct real WPF controls and exercise routed events on an STA thread.
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        if(args.Contains("--media")){RunMediaWorkflow(app);return;}
        var main=new MainWindow(false);
        Check(!Get<Button>(main,"EncodeButton").IsEnabled,"No encode before source");
        Check(!Get<Button>(main,"ResultFolderButton").IsEnabled&&!Get<Button>(main,"CopyResultButton").IsEnabled,"No result before successful encode");
        Check(Get<CheckBox>(main,"SplitBox").IsChecked!=true,"Split disabled initially");
        var media=new MediaInfo(10_000_000,60,1920,1080,30,"h264",0,0);
        var dialog=new BatchPlanDialog(media,["libx264","libx265"],new Plan("Custom","libx264",720,300_000,0,"fast",false));
        var apply=Get<Button>(dialog,"ApplyButton");
        Check(apply.IsEnabled,"Valid initial individual plan");
        Get<TextBox>(dialog,"HeightBox").Text="721";
        Check(!apply.IsEnabled,"Invalid odd height disables apply");
        Get<TextBox>(dialog,"HeightBox").Text="720";
        Check(apply.IsEnabled,"Corrected height enables apply");
        Get<TextBox>(dialog,"RateBox").Text="invalid";
        Check(!apply.IsEnabled,"Invalid bitrate disables apply");
        Get<TextBox>(dialog,"RateBox").Text="500";
        Check(apply.IsEnabled,"Valid bitrate enables apply");
        Get<TextBox>(dialog,"RateBox").Text="500.0001";
        Check(!apply.IsEnabled,"Fractional-bit bitrate rejected");
        Get<TextBox>(dialog,"RateBox").Text="500.001";
        Check(apply.IsEnabled,"Exact single-bit resolution accepted");
        Get<TextBox>(dialog,"TargetBox").Text="5";
        Get<TextBox>(dialog,"RateBox").Text="600";
        Check(Get<TextBox>(dialog,"TargetBox").Text=="","Manual edit clears stale target");
        Get<ComboBox>(dialog,"HeightChoicesBox").SelectedItem=480;
        Check(Get<TextBox>(dialog,"HeightBox").Text=="480","Quick height updates actual editor");
        Get<ComboBox>(dialog,"RateChoicesBox").SelectedItem="500";
        Check(Get<TextBox>(dialog,"RateBox").Text=="500","Quick rate updates actual editor");
        Get<Button>(dialog,"MoreCompressionButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Get<TextBox>(dialog,"RateBox").Text=="375","More compression changes only budget");
        Get<TextBox>(dialog,"RateBox").Text="64";
        Check(!Get<Button>(dialog,"MoreCompressionButton").IsEnabled,"More compression stops at minimum");
        Check(Get<ListBox>(main,"Plans").ItemTemplate is not null,"Recommendation tiles expose separate fields");
        var batch=new BatchWindow();
        Check(!Get<Button>(batch,"EditPlanButton").IsEnabled&&!Get<Button>(batch,"RemoveButton").IsEnabled&&!Get<Button>(batch,"OpenResultButton").IsEnabled&&!Get<Button>(batch,"StartButton").IsEnabled,"Empty queue disables row actions and encoding");
        Check(Get<TextBlock>(batch,"SelectedVideoText").Text=="No video selected","Selection is explicit");
        var language=Get<ComboBox>(main,"LanguageBox");
        var beforeThreads=Get<ComboBox>(main,"ThreadsBox").SelectedIndex;
        language.SelectedIndex=1;
        Check((string)Get<Button>(main,"OpenButton").Content=="영상 열기… (여러 개 가능)","Korean main captions");
        Check(Get<ComboBox>(main,"ThreadsBox").SelectedIndex==beforeThreads&&!Get<Button>(main,"EncodeButton").IsEnabled,"Language switch preserves idle state and thread choice");
        language.SelectedIndex=0;
        Check((string)Get<Button>(main,"OpenButton").Content=="Open videos… (one or many)","English main captions restored");
        var koreanBatch=new BatchWindow(korean:true);
        Check(Get<TextBlock>(koreanBatch,"Summary").Text.Contains("영상 0개 · 준비 0"),"Korean queue summary");
        Check((string)Get<Button>(koreanBatch,"StartButton").Content=="일괄 압축 시작","Batch has distinct primary action");
        Check((string)Get<ComboBox>(koreanBatch,"SharedPresetBox").SelectedItem=="medium"||PlanRules.Presets.Contains((string)Get<ComboBox>(koreanBatch,"SharedPresetBox").SelectedItem),"Shared speed is valid");
        Check((string)Get<Button>(koreanBatch,"AddButton").Content=="영상 추가…"&&!Get<Button>(koreanBatch,"StartButton").IsEnabled,"Korean batch labels and idle gate");
        var koreanDialog=new BatchPlanDialog(media,["libx264"],new Plan("Custom","libx264",720,300_000,0,"fast",false),true);
        Check((string)Get<Button>(koreanDialog,"ApplyButton").Content=="설정 적용"&&Get<Button>(koreanDialog,"ApplyButton").IsEnabled,"Korean individual editor");
        koreanDialog.Close();koreanBatch.Close();batch.Close();dialog.Close();main.Close();app.Shutdown();
        Console.WriteLine("WPF construction and individual editor routed-event smoke passed.");
    }
    private static void RunMediaWorkflow(Application app)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;
        var frame=new DispatcherFrame();
        var task=dispatcher.InvokeAsync(async ()=>await MediaWorkflow()).Task.Unwrap();
        _=task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
        app.Shutdown();
        Console.WriteLine("Real media load, loaded-language state, verified output and collision UI workflow passed.");
    }
    private static async Task MediaWorkflow()
    {
        var root=Path.Combine(Path.GetTempPath(),"VANTA-desktop-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var main=new MainWindow(false);
        try
        {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var tools=await Toolchain.DiscoverAsync(deadline.Token);
            var source=Path.Combine(root,"source clip.mp4");
            var output=Path.Combine(root,"output clip.mp4");
            var fixture=await ProcessRunner.RunAsync(tools.Ffmpeg,["-v","error","-nostdin","-n","-f","lavfi","-i","testsrc2=s=640x360:r=30:d=3","-c:v","libx264","-crf","18","-threads","2","-pix_fmt","yuv420p",source],deadline.Token);
            Check(fixture.ExitCode==0&&File.Exists(source),"Actual desktop fixture");
            await main.LoadAsync(source);
            var plans=Get<ListBox>(main,"Plans");
            var height=Get<TextBox>(main,"HeightBox");
            var rate=Get<TextBox>(main,"RateBox");
            var language=Get<ComboBox>(main,"LanguageBox");
            var codec=Get<ComboBox>(main,"CodecBox");
            var threads=Get<ComboBox>(main,"ThreadsBox");
            var target=Get<TextBox>(main,"TargetBox");
            var split=Get<CheckBox>(main,"SplitBox");
            var encode=Get<Button>(main,"EncodeButton");
            Check(plans.Items.Count>0&&encode.IsEnabled,"Real source yields a usable plan");
            var planIndex=plans.SelectedIndex;
            var originalHeight=height.Text;var originalRate=rate.Text;
            language.SelectedIndex=1;
            Check(plans.SelectedIndex==planIndex&&height.Text==originalHeight&&rate.Text==originalRate&&encode.IsEnabled,"Selected recommendation survives localization");
            codec.SelectedItem="libx264";height.Text="360";rate.Text="250.001";
            threads.SelectedIndex=1;target.Text="0.1";split.IsChecked=true;
            language.SelectedIndex=0;
            Check(plans.SelectedIndex==-1&&height.Text=="360"&&rate.Text=="250.001"&&codec.SelectedItem as string=="libx264"&&threads.SelectedIndex==1&&target.Text=="0.1"&&split.IsChecked==true&&encode.IsEnabled,"Manual settings, target text, split and thread choice survive localization");
            split.IsChecked=false;
            var before=await File.ReadAllBytesAsync(source);
            await main.EncodeToAsync(output);
            Check(File.Exists(output)&&Get<Button>(main,"ResultFolderButton").IsEnabled&&Get<Button>(main,"CopyResultButton").IsEnabled&&Get<TextBlock>(main,"ResultPath").Text==output&&encode.IsEnabled,"Validated output reaches real desktop controls");
            var outputBytes=await File.ReadAllBytesAsync(output);
            await main.EncodeToAsync(output);
            Check((await File.ReadAllBytesAsync(output)).SequenceEqual(outputBytes),"Existing output remains unchanged");
            Check((await File.ReadAllBytesAsync(source)).SequenceEqual(before),"Source bytes remain unchanged");
            Check(!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Desktop workflow leaves no temporary files");
            language.SelectedIndex=1;
            Check(Get<TextBlock>(main,"ResultPath").Text==output&&Get<Button>(main,"ResultFolderButton").IsEnabled,"Completed result survives localization");
            Check((string)encode.Content=="압축 파일 저장…","Single action is a save command");
            var second=Path.Combine(root,"second source.mp4");File.Copy(source,second);
            var batch=new BatchWindow(korean:true);
            try
            {
                Get<ComboBox>(batch,"SharedPresetBox").SelectedItem="veryfast";
                Get<ComboBox>(batch,"ParallelBox").SelectedItem=2;
                Get<ComboBox>(batch,"ThreadsBox").SelectedIndex=0;
                await batch.AddAsync([source,second]);
                var start=Get<Button>(batch,"StartButton");
                Check(start.IsEnabled&&(string)start.Content=="영상 2개 압축 시작","Batch action counts ready files");
                await batch.StartAsync();
                Check(File.Exists(Path.Combine(root,"source clip_vanta.mp4"))&&File.Exists(Path.Combine(root,"second source_vanta.mp4")),"Batch produces separate validated outputs");
                Check(!start.IsEnabled&&Get<TextBlock>(batch,"Summary").Text.Contains("완료 2")&&Get<TextBlock>(batch,"Summary").Text.Contains("최근 실행 경과 시간"),"Batch completion and wall time reflected in command bar");
                var third=Path.Combine(root,"third source.mp4");File.Copy(source,third);
                var fourth=Path.Combine(root,"fourth source.mp4");File.Copy(source,fourth);
                Get<ComboBox>(batch,"ParallelBox").SelectedItem=1;
                await batch.AddAsync([third,fourth]);
                var run=batch.StartAsync();
                Get<Button>(batch,"CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await run;
                Check(Get<TextBlock>(batch,"Summary").Text.Contains("완료 2")&&Get<TextBlock>(batch,"Summary").Text.Contains("취소 2")&&!Get<Button>(batch,"CancelButton").IsEnabled,"Cancel retains previous completed results and settles pending rows");
                Check(!File.Exists(Path.Combine(root,"third source_vanta.mp4"))&&!File.Exists(Path.Combine(root,"fourth source_vanta.mp4"))&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Canceled batch does not publish or leak staging files");
            }
            finally{batch.Close();}
        }
        finally {main.Close();Directory.Delete(root,true);}
    }
    static T Get<T>(FrameworkElement window,string name) where T:FrameworkElement
        =>window.FindName(name) as T??throw new Exception("Missing WPF element: "+name);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}