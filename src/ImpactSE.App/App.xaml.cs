using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace ImpactSE.App;

public partial class App : Application
{
    Window? _window;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash(e.ExceptionObject as Exception);
        UnhandledException += (_, e) => Crash(e.Exception);
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            Crash(ex);
        }
    }

    /// <summary>Writes the error to %LOCALAPPDATA%\ImpactSE\crash.log and shows it, instead of closing silently.</summary>
    static void Crash(Exception? ex)
    {
        var text = $"{DateTime.Now:u}\n{ex}\n\n";
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImpactSE");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "crash.log"), text);
        }
        catch { }
        MessageBoxW(IntPtr.Zero, $"Impact SE hit an error and has to close:\n\n{ex?.Message}", "Impact SE", 0x10);
        Environment.Exit(1);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);
}
