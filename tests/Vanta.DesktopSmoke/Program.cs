using System.Windows;
using System.Windows.Controls;
using Vanta.App;
using Vanta.Core;

internal static class DesktopSmoke
{
    [STAThread]
    public static void Main()
    {
        // No Show/ShowDialog, clipboard, real user settings writes or external UI automation.
        // Construct real WPF controls and exercise routed events on an STA thread.
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var main=new MainWindow();
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
        dialog.Close();main.Close();app.Shutdown();
        Console.WriteLine("WPF construction and individual editor routed-event smoke passed.");
    }
    static T Get<T>(FrameworkElement window,string name) where T:FrameworkElement
        =>window.FindName(name) as T??throw new Exception("Missing WPF element: "+name);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}