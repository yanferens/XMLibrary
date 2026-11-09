using System.Text;

namespace XMLLibrary.Core.Model;

/// <summary>Turns any text typed by the user into a file name that is valid on Windows and on Google Drive.</summary>
public static class FileNames
{
    /// <summary>The longest name (without the extension) the program creates.</summary>
    public const int MaxLength = 100;

    /// <summary>The name used when nothing usable is left of the user's text.</summary>
    public const string Fallback = "fragment";

    private const string ForbiddenCharacters = "\\/:*?\"<>|";

    /// <summary>
    /// Builds "name.ext" from free text: trims it, drops the extension if the user already typed it,
    /// replaces characters Windows forbids (<c>\ / : * ? " &lt; &gt; |</c> and control characters) with "_"
    /// and limits the length.
    /// </summary>
    /// <param name="text">What the user typed, e.g. " report.xml ".</param>
    /// <param name="extension">The extension to add, with the dot: ".xml".</param>
    /// <returns>A safe file name such as "report.xml".</returns>
    public static string Build(string? text, string extension)
    {
        string name = (text ?? string.Empty).Trim();

        if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^extension.Length];
        }

        var builder = new StringBuilder(name.Length);
        foreach (char character in name)
        {
            builder.Append(char.IsControl(character) || ForbiddenCharacters.Contains(character) ? '_' : character);
        }

        // Windows does not allow a name to end with a dot or a space.
        name = builder.ToString().Trim().TrimEnd('.', ' ');
        if (name.Length > MaxLength)
        {
            // Never cut an emoji or another two-part character in half.
            int length = char.IsHighSurrogate(name[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
            name = name[..length].TrimEnd('.', ' ');
        }

        return (name.Length == 0 ? Fallback : name) + extension;
    }
}
