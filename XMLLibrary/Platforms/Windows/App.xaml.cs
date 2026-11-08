using XMLLibrary.Services;

namespace XMLLibrary.WinUI;

/// <summary>The Windows entry point of the MAUI application.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initializes the Windows application object; the first line of our code that runs.</summary>
    public App()
    {
        InitializeComponent();

        // An exception on the UI thread that nothing caught: write it to the crash log and keep the
        // program open instead of closing the window under the user's hands.
        UnhandledException += (_, e) =>
        {
            CrashLog.Write(e.Exception);
            e.Handled = true;
        };
    }

    /// <summary>Creates the MAUI part of the application.</summary>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
