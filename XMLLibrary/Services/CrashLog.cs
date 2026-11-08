using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace XMLLibrary.Services;

/// <summary>
/// The last line of defence: an exception that nothing else caught is written to
/// %LOCALAPPDATA%\XMLLibrary\crash.log, so a problem can be investigated instead of silently closing the program.
/// </summary>
public static class CrashLog
{
    private static readonly object FileLock = new();

    /// <summary>The file the crashes are written to.</summary>
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XMLLibrary", "crash.log");

    /// <summary>Subscribes to the .NET events for unhandled exceptions of background threads and forgotten tasks.</summary>
    public static void Attach()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Write(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Appends an exception to the file. Never throws: a failing crash log must not cause a crash.</summary>
    /// <param name="exception">The exception to record.</param>
    public static void Write(Exception exception)
    {
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string time = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(FilePath, $"{time} {exception}{Environment.NewLine}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"The crash log is not written: {ex.Message}");
        }
    }
}
