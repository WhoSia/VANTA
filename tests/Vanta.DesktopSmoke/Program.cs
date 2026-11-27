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
        Check(!Get<Button>(main,"HardwareTrialButton").IsEnabled&&Get<ComboBox>(main,"HardwareBox").SelectedIndex==-1,"No automatic hardware path");
        Check(Get<TextBlock>(main,"ActivePlanSummary").Visibility==Visibility.Collapsed,"No applied settings before source");
        Check(Get<FrameworkElement>(main,"ProgressCard").Visibility==Visibility.Collapsed&&Get<TextBlock>(main,"EtaText").Text==""&&Get<TextBlock>(main,"ThroughputText").Text=="","No stale ETA or speed on startup");
        Check(!Get<Button>(main,"ResultFolderButton").IsEnabled&&!Get<Button>(main,"CopyResultButton").IsEnabled&&!Get<Button>(main,"PlayResultButton").IsEnabled&&Get<TextBlock>(main,"ResultSummary").Visibility==Visibility.Collapsed,"No result before successful encode");
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
        var targetEditor=Get<TextBox>(dialog,"TargetBox");
        var targetApply=Get<Button>(dialog,"TargetApplyButton");
        var oldRate=Get<TextBox>(dialog,"RateBox").Text;
        targetEditor.Text="invalid";
        Check(!apply.IsEnabled,"Unapplied target blocks stale plan");
        targetApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!apply.IsEnabled&&Get<TextBox>(dialog,"RateBox").Text==oldRate,"Invalid target cannot replace or apply prior plan");
        apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(dialog.Result is null,"Routed apply cannot bypass invalid target");
        targetEditor.Text="5";
        targetApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(apply.IsEnabled&&targetEditor.Text=="5","Valid target enables apply");
        targetEditor.Text="6";
        Check(!apply.IsEnabled,"Edited target must be applied again");
        targetEditor.Clear();
        Check(apply.IsEnabled,"Clear target restores current bitrate");
        targetEditor.Text="5";
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
        Check(!Get<Button>(batch,"QueueFirstButton").IsEnabled&&!Get<Button>(batch,"QueueUpButton").IsEnabled&&!Get<Button>(batch,"QueueDownButton").IsEnabled&&!Get<DataGrid>(batch,"Queue").CanUserSortColumns,"Empty queue and display order guard");
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
        Check((string)Get<ComboBox>(koreanBatch,"SharedPresetBox").SelectedValue=="medium"||PlanRules.Presets.Contains((string)Get<ComboBox>(koreanBatch,"SharedPresetBox").SelectedValue),"Shared speed is valid");
        Check((string)Get<Button>(koreanBatch,"AddButton").Content=="영상 추가…"&&!Get<Button>(koreanBatch,"StartButton").IsEnabled,"Korean batch labels and idle gate");
        Check((string)Get<DataGrid>(koreanBatch,"Queue").Columns[0].Header=="원본","Korean grid header");
        Get<ComboBox>(koreanBatch,"LanguageBox").SelectedIndex=0;
        Check((string)Get<DataGrid>(koreanBatch,"Queue").Columns[0].Header=="Source","Grid header follows live language");
        Get<ComboBox>(koreanBatch,"LanguageBox").SelectedIndex=1;
        Check(System.Windows.Automation.AutomationProperties.GetName(Get<ComboBox>(koreanBatch,"SharedPresetBox"))=="공통 압축 속도","Localized accessible field name");
        Check(UiLanguage.Preset(true,"medium")=="균형"&&UiLanguage.Preset(true,"veryfast")=="매우 빠르게"&&UiLanguage.Preset(true,"superfast")=="초고속","Korean speed labels");
        var koreanDialog=new BatchPlanDialog(media,["libx264"],new Plan("Custom","libx264",720,300_000,0,"fast",false),true);
        Check((string)Get<Button>(koreanDialog,"ApplyButton").Content=="설정 적용"&&Get<Button>(koreanDialog,"ApplyButton").IsEnabled,"Korean individual editor");
        CheckLayout(main,"Status","EncodeButton",560,480);
        CheckLayout(main,"Status","LanguageBox",560,480);
        CheckLayout(koreanBatch,"Summary","StartButton",720,580);
        CheckLayout(batch,"Summary","StartButton",720,580);
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
            var speed=Get<Button>(main,"SpeedFirstButton");
            var oldHeight=height.Text;var oldRate=rate.Text;
            split.IsChecked=true;
            Check(speed.IsEnabled,"Speed shortcut available");
            speed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(codec.SelectedItem as string=="libx264"&&Get<ComboBox>(main,"PresetBox").SelectedValue as string=="veryfast"&&height.Text==oldHeight&&rate.Text==oldRate&&split.IsChecked==false&&plans.SelectedIndex==-1&&encode.IsEnabled,"Speed shortcut changes only codec/preset and clears split");
            var fastHevc=Get<Button>(main,"FastHevcButton");
            Check(fastHevc.IsEnabled,"HEVC speed path available");
            fastHevc.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(codec.SelectedItem as string=="libx265"&&Get<ComboBox>(main,"PresetBox").SelectedValue as string=="veryfast"&&height.Text==oldHeight&&rate.Text==oldRate&&encode.IsEnabled,"HEVC shortcut retains rate and dimensions");
            var hardware=Get<ComboBox>(main,"HardwareBox");var hardwareTrial=Get<Button>(main,"HardwareTrialButton");
            hardware.SelectedItem="h264_nvenc";Check(!hardwareTrial.IsEnabled,"Mismatched hardware family disabled");
            hardware.SelectedItem="hevc_nvenc";Check(hardwareTrial.IsEnabled,"Matching explicit device choice available");
            split.IsChecked=true;Check(!hardwareTrial.IsEnabled,"Split excludes hardware trial");split.IsChecked=false;
            threads.SelectedIndex=1;Check(!hardwareTrial.IsEnabled,"Manual thread budget excludes device trial");threads.SelectedIndex=0;
            target.Text="invalid";Check(!hardwareTrial.IsEnabled,"Pending target excludes hardware trial");
            await main.EncodeToAsync(Path.Combine(root,"blocked-hardware.mp4"),"hevc_nvenc");
            Check(!File.Exists(Path.Combine(root,"blocked-hardware.mp4")),"Invalid target cannot launch hardware trial");
            Check(!fastHevc.IsEnabled,"Pending target disables HEVC speed shortcut");
            Check(!speed.IsEnabled,"Pending target disables speed shortcut");
            speed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(target.Text=="invalid"&&!encode.IsEnabled,"Speed shortcut cannot bypass target gate");
            target.Clear();
            plans.SelectedIndex=0;
            var planIndex=plans.SelectedIndex;
            var originalHeight=height.Text;var originalRate=rate.Text;
            language.SelectedIndex=1;
            Check(plans.SelectedIndex==planIndex&&height.Text==originalHeight&&rate.Text==originalRate&&encode.IsEnabled,"Selected recommendation survives localization");
            codec.SelectedItem="libx264";height.Text="360";rate.Text="250.001";
            threads.SelectedIndex=1;target.Text="0.1";split.IsChecked=true;
            language.SelectedIndex=0;
            Check(plans.SelectedIndex==-1&&height.Text=="360"&&rate.Text=="250.001"&&codec.SelectedItem as string=="libx264"&&threads.SelectedIndex==1&&target.Text=="0.1"&&split.IsChecked==true&&!encode.IsEnabled,"Manual settings and unapplied target gate survive localization");
            target.Text="invalid";
            Get<Button>(main,"TargetApplyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!encode.IsEnabled&&rate.Text=="250.001","Invalid target blocks old plan");
            var rejectedOutput=Path.Combine(root,"invalid target.mp4");
            await main.EncodeToAsync(rejectedOutput);
            Check(!File.Exists(rejectedOutput),"Invalid target cannot encode even through shared entry point");
            language.SelectedIndex=1;
            Check(!encode.IsEnabled,"Language cannot revive stale target");
            target.Clear();
            Check(encode.IsEnabled,"Clearing target restores encoding");
            language.SelectedIndex=0;
            split.IsChecked=false;
            Check(Get<TextBlock>(main,"ActivePlanSummary").Visibility==Visibility.Visible&&Get<TextBlock>(main,"ActivePlanSummary").Text.Contains("H.264")&&Get<TextBlock>(main,"ActivePlanSummary").Text.Contains("kbps"),"Applied plan visible near save command");
            var before=await File.ReadAllBytesAsync(source);
            var encodingTask=main.EncodeToAsync(output);
            Check(language.IsEnabled&&!encode.IsEnabled,"Language available during single encode");
            Check(Get<FrameworkElement>(main,"ProgressCard").Visibility==Visibility.Visible&&Get<TextBlock>(main,"ElapsedText").Text.StartsWith("Elapsed:"),"Live progress card at encode start");
            language.SelectedIndex=1;
            Check((string)encode.Content=="압축 파일 저장…"&&Get<ComboBox>(main,"PresetBox").SelectedValue is string&&!encode.IsEnabled,"Active encode settings preserved on language switch");
            await encodingTask;
            language.SelectedIndex=0;
            Check(Get<FrameworkElement>(main,"ProgressCard").Visibility==Visibility.Collapsed&&Get<TextBlock>(main,"EtaText").Text==""&&Get<TextBlock>(main,"ThroughputText").Text==""&&Get<TextBlock>(main,"PreflightEta").Text.Contains("Previous same-setting total"),"Live ETA cleared and same-setting total retained");
            Check(File.Exists(output)&&Get<Button>(main,"ResultFolderButton").IsEnabled&&Get<Button>(main,"CopyResultButton").IsEnabled&&Get<TextBlock>(main,"ResultPath").Text==output&&encode.IsEnabled,"Validated output reaches real desktop controls");
            Check(Get<Button>(main,"PlayResultButton").IsEnabled&&Get<TextBlock>(main,"ResultSummary").Text.Contains("actual savings")&&Get<TextBlock>(main,"ResultSummary").Text.Contains("encoding")&&Get<TextBlock>(main,"ResultSummary").Text.Contains("libx264"),"Playable output and persistent actual summary");
            var outputBytes=await File.ReadAllBytesAsync(output);
            await main.EncodeToAsync(output);
            Check((await File.ReadAllBytesAsync(output)).SequenceEqual(outputBytes),"Existing output remains unchanged");
            Check((await File.ReadAllBytesAsync(source)).SequenceEqual(before),"Source bytes remain unchanged");
            Check(!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Desktop workflow leaves no temporary files");
            language.SelectedIndex=1;
            Check(Get<TextBlock>(main,"ResultPath").Text==output&&Get<Button>(main,"ResultFolderButton").IsEnabled,"Completed result survives localization");
            Check((string)encode.Content=="압축 파일 저장…","Single action is a save command");
            Check(Get<TextBlock>(main,"ResultSummary").Text.Contains("실제 절감률")&&(string)Get<Button>(main,"PlayResultButton").Content=="결과 재생","Persistent result localizes after unrelated status changes");
            var second=Path.Combine(root,"second source.mp4");File.Copy(source,second);
            var batchHistory=new TimingHistory();
            var batch=new BatchWindow(korean:true,sessionHistory:batchHistory);
            try
            {
                Get<ComboBox>(batch,"SharedPresetBox").SelectedValue="veryfast";
                Get<ComboBox>(batch,"ParallelBox").SelectedItem=2;
                Get<ComboBox>(batch,"ThreadsBox").SelectedIndex=0;
                await batch.AddAsync([source,second]);
                var batchFast=Get<Button>(batch,"BatchSpeedButton");
                Check(batchFast.IsEnabled,"Shared fast path available for analyzed files");
                batchFast.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Get<ComboBox>(batch,"RecommendationBox").SelectedItem?.ToString()=="H.264"&&Get<ComboBox>(batch,"SharedPresetBox").SelectedValue as string=="veryfast","Batch fast path selects AVC veryfast");
                // Restore the initial shared codec to keep the prior timing-context scenario comparable.
                Get<ComboBox>(batch,"RecommendationBox").SelectedIndex=0;
                var start=Get<Button>(batch,"StartButton");
                Check(start.IsEnabled&&(string)start.Content=="영상 2개 압축 시작","Batch action counts ready files");
                Check(Get<TextBlock>(batch,"BatchPreflightText").Text.Contains("0/2"),"First batch preflight does not invent time");
                var batchLanguage=Get<ComboBox>(batch,"LanguageBox");
                var orderGrid=Get<DataGrid>(batch,"Queue");
                var first=orderGrid.Items[0];var secondRow=orderGrid.Items[1];
                orderGrid.SelectedItem=secondRow;
                Check(Get<Button>(batch,"QueueFirstButton").IsEnabled&&Get<Button>(batch,"QueueUpButton").IsEnabled,"Ready row can move ahead");
                Get<Button>(batch,"QueueFirstButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(ReferenceEquals(orderGrid.Items[0],secondRow)&&ReferenceEquals(orderGrid.SelectedItem,secondRow),"Move first preserves row selection and submission order");
                Get<Button>(batch,"QueueDownButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(ReferenceEquals(orderGrid.Items[0],first),"Move down restores original order");
                Get<Button>(batch,"QueueUpButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(ReferenceEquals(orderGrid.Items[0],secondRow),"Move up changes submission order");
                var batchRun=batch.StartAsync();
                Check(!Get<Button>(batch,"QueueDownButton").IsEnabled,"Running queue cannot reorder");
                Check(!batchFast.IsEnabled&&!Get<Button>(batch,"BatchHevcButton").IsEnabled,"Running batch disables shared fast paths");
                Get<Button>(batch,"QueueDownButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(ReferenceEquals(orderGrid.Items[0],secondRow),"Routed reorder event cannot bypass running guard");
                Check(batchLanguage.IsEnabled&&!start.IsEnabled,"Language available during batch encode");
                Check(Get<FrameworkElement>(batch,"BatchProgressCard").Visibility==Visibility.Visible&&Get<TextBlock>(batch,"BatchProgressText").Text.Contains("0/2"),"Batch progress card starts with current run count");
                Check(Get<TextBlock>(batch,"BatchEtaText").Text.Contains("0/2"),"Unknown queue time reports absent references");
                batchLanguage.SelectedIndex=0;
                Check((string)start.Content=="Processing…"&&(string)Get<ComboBox>(batch,"SharedPresetBox").SelectedValue=="veryfast","Batch language change preserves running settings");
                batchLanguage.SelectedIndex=1;
                await batchRun;
                Check(Get<FrameworkElement>(batch,"BatchProgressCard").Visibility==Visibility.Collapsed&&Get<TextBlock>(batch,"BatchEtaText").Text=="","Batch progress cleared at terminal settlement");
                Check(File.Exists(Path.Combine(root,"source clip_vanta.mp4"))&&File.Exists(Path.Combine(root,"second source_vanta.mp4")),"Batch produces separate validated outputs");
                Check(!start.IsEnabled&&Get<TextBlock>(batch,"Summary").Text.Contains("완료 2")&&Get<TextBlock>(batch,"Summary").Text.Contains("최근 실행 경과 시간"),"Batch completion and wall time reflected in command bar");
                var queue=Get<DataGrid>(batch,"Queue");queue.SelectedIndex=0;
                Check(Get<Button>(batch,"PlayResultButton").IsEnabled,"Completed batch row enables playback");
                var third=Path.Combine(root,"third source.mp4");File.Copy(source,third);
                var fourth=Path.Combine(root,"fourth source.mp4");File.Copy(source,fourth);
                Get<ComboBox>(batch,"ParallelBox").SelectedItem=1;
                await batch.AddAsync([third,fourth]);
                var run=batch.StartAsync();
                Get<Button>(batch,"CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await run;
                Check(Get<FrameworkElement>(batch,"BatchProgressCard").Visibility==Visibility.Collapsed&&Get<TextBlock>(batch,"BatchEtaText").Text=="","Canceled batch clears ETA card");
                Check(Get<TextBlock>(batch,"Summary").Text.Contains("완료 2")&&Get<TextBlock>(batch,"Summary").Text.Contains("취소 2")&&!Get<Button>(batch,"CancelButton").IsEnabled,"Cancel retains previous completed results and settles pending rows");
                Check(!File.Exists(Path.Combine(root,"third source_vanta.mp4"))&&!File.Exists(Path.Combine(root,"fourth source_vanta.mp4"))&&!Directory.EnumerateFileSystemEntries(root,".vanta-*").Any(),"Canceled batch does not publish or leak staging files");
            }
            finally{batch.Close();}
            Check(batchHistory.Count==2,"Only successful batch jobs recorded, including short inputs");
            var reopened=new BatchWindow(korean:false,sessionHistory:batchHistory);
            try
            {
                Get<ComboBox>(reopened,"SharedPresetBox").SelectedValue="veryfast";
                Get<ComboBox>(reopened,"ParallelBox").SelectedItem=2;
                Get<ComboBox>(reopened,"ThreadsBox").SelectedIndex=0;
                await reopened.AddAsync([source,second]);
                Check(Get<TextBlock>(reopened,"BatchPreflightText").Text.StartsWith("Before start ≈ "), "Reopened batch uses previous matching verified job times");
                Get<ComboBox>(reopened,"ParallelBox").SelectedItem=1;
                Check(Get<TextBlock>(reopened,"BatchPreflightText").Text.Contains("0/2"),"Changed parallelism withholds prior timings");
                Get<ComboBox>(reopened,"ParallelBox").SelectedItem=2;
                Check(Get<TextBlock>(reopened,"BatchPreflightText").Text.StartsWith("Before start ≈ "),"Restoring matching settings restores references");
                Get<ComboBox>(reopened,"LanguageBox").SelectedIndex=1;
                Check(Get<TextBlock>(reopened,"BatchPreflightText").Text.StartsWith("시작 전 예상 ≈ "),"Preflight ETA localizes");
            }
            finally{reopened.Close();}
            File.Delete(output);
            Get<Button>(main,"PlayResultButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!Get<Button>(main,"PlayResultButton").IsEnabled&&Get<TextBlock>(main,"ResultSummary").Visibility==Visibility.Visible,"Deleted output cannot launch; historical summary remains");
            await main.LoadAsync(second);
            Check(Get<TextBlock>(main,"ResultSummary").Visibility==Visibility.Collapsed&&!Get<Button>(main,"PlayResultButton").IsEnabled,"New source clears completed result");
        }
        finally {main.Close();Directory.Delete(root,true);}
    }
    private static void CheckLayout(Window window,string messageName,string commandName,double width,double height)
    {
        var message=Get<TextBlock>(window,messageName);
        message.Text=string.Join(Environment.NewLine,Enumerable.Repeat("Long diagnostic / 긴 진단 메시지",60));
        var content=(FrameworkElement)window.Content;
        content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
        var command=Get<FrameworkElement>(window,commandName);
        var bounds=command.TransformToAncestor(content).TransformBounds(new Rect(command.RenderSize));
        Check(bounds.Height>0&&bounds.Top>=0&&bounds.Bottom<=height+1,"Primary command stays in viewport with long diagnostics");
    }
    static T Get<T>(FrameworkElement window,string name) where T:FrameworkElement
        =>window.FindName(name) as T??throw new Exception("Missing WPF element: "+name);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}