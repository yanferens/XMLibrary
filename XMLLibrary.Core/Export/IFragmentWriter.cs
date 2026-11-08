using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Export;

/// <summary>
/// The product of the Factory Method: an object that knows how to write the found books
/// into an already opened file in one particular format.
/// </summary>
public interface IFragmentWriter
{
    /// <summary>The file extension with the dot: ".xml", ".html".</summary>
    string Extension { get; }

    /// <summary>The MIME type the cloud gets for the file.</summary>
    string MimeType { get; }

    /// <summary>Writes the books into the opened file.</summary>
    /// <param name="books">The filtered fragment.</param>
    /// <param name="output">The opened file; it is left open.</param>
    void Write(IReadOnlyList<Book> books, Stream output);
}
