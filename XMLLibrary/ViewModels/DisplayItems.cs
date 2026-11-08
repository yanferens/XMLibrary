using System.Globalization;
using XMLLibrary.Core.Logging;
using XMLLibrary.Core.Model;

namespace XMLLibrary.ViewModels;

/// <summary>A found book prepared for its card: plain strings for the labels.</summary>
/// <param name="Title">The title, or a placeholder when the book has none.</param>
/// <param name="Author">The author, or a placeholder.</param>
/// <param name="Annotation">The annotation; may be empty.</param>
/// <param name="Tags">The attributes of the book as "caption: value" chips.</param>
/// <param name="ReadersText">One line about the readers.</param>
/// <param name="Monogram">The first letter of the title for the "cover".</param>
public sealed record BookItem(
    string Title,
    string Author,
    string Annotation,
    IReadOnlyList<TagItem> Tags,
    string ReadersText,
    string Monogram)
{
    /// <summary>True when there is an annotation to show.</summary>
    public bool HasAnnotation => Annotation.Length > 0;

    /// <summary>Prepares a book for display.</summary>
    /// <param name="book">A found book.</param>
    public static BookItem From(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        string title = book.Title.Length > 0 ? book.Title : "Без назви";

        List<TagItem> tags = book.Attributes
            .Where(pair => pair.Key != LibraryXml.Id && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new TagItem(LibraryXml.Caption(pair.Key), pair.Value))
            .ToList();

        string readers = book.Readers.Count == 0
            ? "Читачів немає"
            : "Читачі: " + string.Join(", ", book.Readers.Select(reader =>
                reader.Position.Length > 0 ? $"{reader.Name} ({reader.Position})" : reader.Name));

        return new BookItem(
            title,
            book.Author.Length > 0 ? book.Author : "Автор не вказаний",
            book.Annotation,
            tags,
            readers,
            FirstLetter(title));
    }

    /// <summary>The first letter or digit of the title in upper case; otherwise its first visible symbol.</summary>
    private static string FirstLetter(string title)
    {
        foreach (char character in title)
        {
            if (char.IsLetterOrDigit(character))
            {
                return char.ToUpperInvariant(character).ToString();
            }
        }

        string first = StringInfo.GetNextTextElement(title);
        return first.Length > 0 ? first : "?";
    }
}

/// <summary>One chip on a book card.</summary>
/// <param name="Caption">The attribute caption, e.g. "Рік".</param>
/// <param name="Value">The value, e.g. "2021".</param>
public sealed record TagItem(string Caption, string Value);

/// <summary>One line of the list in the Google Drive file picker.</summary>
/// <param name="Title">The file name.</param>
/// <param name="Subtitle">When it was changed and how big it is.</param>
public sealed record ChoiceItem(string Title, string Subtitle);

/// <summary>One line of the event log, split for display.</summary>
/// <param name="Time">"07.11.2026 20:41:05".</param>
/// <param name="Level">"Фільтрація", "Трансформація", "Збереження" or an empty string for an unknown line.</param>
/// <param name="Message">The rest of the line.</param>
public sealed record LogLineItem(string Time, string Level, string Message)
{
    /// <summary>A short code of the level for the colour of the badge: "filter", "transform", "save" or "other".</summary>
    public string Kind => Level switch
    {
        _ when Level == EventLogger.Caption(LogEventLevel.Filtering) => "filter",
        _ when Level == EventLogger.Caption(LogEventLevel.Transformation) => "transform",
        _ when Level == EventLogger.Caption(LogEventLevel.Saving) => "save",
        _ => "other",
    };

    /// <summary>True when the line had a known level, so the badge is shown.</summary>
    public bool HasLevel => Level.Length > 0;

    /// <summary>
    /// Splits a line written by <see cref="EventLogger"/>: "time level. message".
    /// A line in any other format is shown whole as the message.
    /// </summary>
    /// <param name="line">One line of the log file.</param>
    public static LogLineItem Parse(string line)
    {
        line ??= string.Empty;
        int timeLength = EventLogger.TimeFormat.Length;

        if (line.Length > timeLength + 1
            && DateTime.TryParseExact(line[..timeLength], EventLogger.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            string rest = line[(timeLength + 1)..];
            foreach (LogEventLevel level in Enum.GetValues<LogEventLevel>())
            {
                string caption = EventLogger.Caption(level);
                if (rest.StartsWith(caption + ".", StringComparison.Ordinal))
                {
                    return new LogLineItem(line[..timeLength], caption, rest[(caption.Length + 1)..].Trim());
                }
            }

            return new LogLineItem(line[..timeLength], string.Empty, rest.Trim());
        }

        return new LogLineItem(string.Empty, string.Empty, line);
    }
}
