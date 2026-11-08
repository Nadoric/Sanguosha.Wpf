using System.Windows;
using System.IO;

namespace Sanguosha.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Length == 2 && e.Args[0] is
                "--verify-ui" or "--verify-audio" or "--verify-effects" or "--verify-skins")
            {
                try
                {
                    MainWindow window = new();
                    if (e.Args[0] == "--verify-ui")
                    {
                        window.RunUiVerification(Path.GetFullPath(e.Args[1]));
                    }
                    else if (e.Args[0] == "--verify-skins")
                    {
                        window.RunSkinVerification(Path.GetFullPath(e.Args[1]));
                    }
                    else if (e.Args[0] == "--verify-effects")
                    {
                        window.RunEffectsVerification(Path.GetFullPath(e.Args[1]));
                    }
                    else
                    {
                        window.RunAudioVerification(Path.GetFullPath(e.Args[1]));
                    }
                    window.Close();
                    Shutdown(0);
                }
                catch (Exception exception)
                {
                    Directory.CreateDirectory(e.Args[1]);
                    File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), exception.ToString());
                    Shutdown(1);
                }
            }
            else
            {
                MainWindow window = new();
                window.Show();
            }
        }
    }
}
