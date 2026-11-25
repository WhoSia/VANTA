using System.Windows;
using Vanta.Core;

namespace Vanta.App;
public partial class App:Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==1&&e.Args[0]=="--check-dependencies")
        {
            // No window, no preferences, no installation or network activity.
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try{Toolchain.DiscoverAsync(deadline.Token).GetAwaiter().GetResult();Shutdown(0);}
            catch(Exception){Shutdown(2);}
            return;
        }
        MainWindow=new MainWindow();MainWindow.Show();
    }
}
