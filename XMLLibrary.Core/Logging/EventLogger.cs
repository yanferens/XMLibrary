using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace XMLLibrary.Core.Logging;

/// <summary>
/// Singleton: the one event log of the program, a text file on the local disk.
/// Every line is "time level. message", e.g.
/// <c>21.09.2023 13:02:22 Фільтрація. Параметри: «faculty» – ФКНК</c>.
/// </summary>
public sealed class EventLogger
{
    /// <summary>The format of the time at the start of every line.</summary>
    public const string TimeFormat = "dd.MM.yyyy HH:mm:ss";

    // Lazy<T> creates the object on first use and is thread-safe by default:
    // two threads asking for Instance at the same moment still get the same object.
    private static readonly Lazy<EventLogger> LazyInstance = new(() => new EventLogger());

    private readonly object _fileLock = new();

    /// <summary>The constructor is private, so "new EventLogger()" outside this class does not compile.</summary>
    private EventLogger() => FilePath = DefaultFilePath;

    /// <summary>The only instance of the log.</summary>
    public static EventLogger Instance => LazyInstance.Value;

    /// <summary>%LOCALAPPDATA%\XMLLibrary\events.log on Windows.</summary>
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XMLLibrary", "events.log");

    /// <summary>The file the log is written to.</summary>
    public string FilePath { get; private set; }

    /// <summary>Writes the log to another file from now on (the tests use a temporary file).</summary>
    /// <param name="path">The full path of the log file.</param>
    public void UseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (_fileLock)
        {
            FilePath = path;
        }
    }

    /// <summary>Appends one event with the current time.</summary>
    /// <param name="level">Filtering, transformation or saving.</param>
    /// <param name="message">What happened, in words for a person.</param>
    public void Log(LogEventLevel level, string message) => Append(FormatLine(DateTime.Now, level, message));

    /// <summary>Builds one line of the log without writing it.</summary>
    /// <param name="time">The time of the event.</param>
    /// <param name="level">The level of the event.</param>
    /// <param name="message">The text of the event; line breaks are replaced, so one event is always one line.</param>
    public static string FormatLine(DateTime time, LogEventLevel level, string message)
    {
        string singleLine = (message ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        return $"{time.ToString(TimeFormat, CultureInfo.InvariantCulture)} {Caption(level)}. {singleLine}";
    }

    /// <summary>The Ukrainian name of a level as it is written in the file.</summary>
    /// <param name="level">The level.</param>
    public static string Caption(LogEventLevel level) => level switch
    {
        LogEventLevel.Filtering => "Фільтрація",
        LogEventLevel.Transformation => "Трансформація",
        LogEventLevel.Saving => "Збереження",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    /// <summary>Reads all lines of the log; an empty list if nothing was logged yet.</summary>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    public IReadOnlyList<string> ReadAll()
    {
        lock (_fileLock)
        {
            return File.Exists(FilePath) ? File.ReadAllLines(FilePath, Encoding.UTF8) : [];
        }
    }

    /// <summary>Reads the newest lines of the log, the newest first.</summary>
    /// <param name="count">How many lines at most.</param>
    public IReadOnlyList<string> ReadLatest(int count)
    {
        IReadOnlyList<string> lines = ReadAll();
        return lines.Where(line => line.Length > 0).Reverse().Take(Math.Max(0, count)).ToList();
    }

    /// <summary>
    /// Appends a line to the file. The lock keeps lines from two threads from being mixed, and a log
    /// that cannot be written never breaks the search or the saving it describes.
    /// </summary>
    private void Append(string line)
    {
        lock (_fileLock)
        {
            try
            {
                string? folder = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"The event log is not written: {ex.Message}");
            }
        }
    }
}
