namespace XMLLibrary.Core.Model;

/// <summary>The kinds of files the program opens, and the limits that keep it responsive.</summary>
public static class FileKinds
{
    /// <summary>Extensions of library documents.</summary>
    public static IReadOnlyList<string> XmlExtensions { get; } = [".xml"];

    /// <summary>Extensions of XSL stylesheets (both spellings are common).</summary>
    public static IReadOnlyList<string> XslExtensions { get; } = [".xsl", ".xslt"];

    /// <summary>The largest file the program agrees to open: 20 MB is far more than any lab document.</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    /// <summary>Tells whether a file name ends with one of the given extensions, ignoring the letter case.</summary>
    /// <param name="fileName">A file name such as "Library.XML".</param>
    /// <param name="extensions">Extensions with the dot: ".xml", ".xsl".</param>
    public static bool HasExtension(string? fileName, IEnumerable<string> extensions)
        => fileName is not null
           && extensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
