using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace XMLLibrary.Core.Model;

/// <summary>
/// An XML file opened by the user, from the disk or from Google Drive. The raw bytes are kept,
/// so every search strategy can read the document from the very beginning in its own way.
/// </summary>
public sealed class LibraryDocument
{
    private readonly byte[] _content;

    private LibraryDocument(string name, byte[] content, int bookCount, AttributeCatalog catalog)
    {
        Name = name;
        _content = content;
        BookCount = bookCount;
        Catalog = catalog;
    }

    /// <summary>The file name shown to the user, e.g. "library.xml".</summary>
    public string Name { get; }

    /// <summary>How many &lt;book&gt; elements the file has.</summary>
    public int BookCount { get; }

    /// <summary>The attributes and values for the drop-down lists.</summary>
    public AttributeCatalog Catalog { get; }

    /// <summary>
    /// Checks the bytes and creates a document. Everything that can be wrong with a file is reported as
    /// <see cref="InvalidDataException"/> with a message for the user, so the caller needs only one catch.
    /// </summary>
    /// <param name="name">The file name for messages and captions.</param>
    /// <param name="content">The whole file.</param>
    /// <exception cref="InvalidDataException">The file is empty, too big, not XML or not a library.</exception>
    public static LibraryDocument Load(string name, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            throw new InvalidDataException($"Файл «{name}» порожній.");
        }

        if (content.LongLength > FileKinds.MaxFileBytes)
        {
            throw new InvalidDataException($"Файл «{name}» завеликий: можна відкрити файл до {FileKinds.MaxFileBytes / 1024 / 1024} МБ.");
        }

        XDocument document = Parse(name, content);
        XElement? root = document.Root;

        // The name is compared together with its namespace: <library xmlns="..."> is a different element,
        // and the XPath and LINQ queries of the strategies would silently find nothing in it.
        if (root is null || root.Name != XName.Get(LibraryXml.Root))
        {
            throw new InvalidDataException(
                $"Файл «{name}» не схожий на бібліотеку: кореневий елемент має бути <{LibraryXml.Root}> без простору імен.");
        }

        int bookCount = root.Elements(LibraryXml.Book).Count();
        return new LibraryDocument(name, content, bookCount, AttributeCatalog.FromXml(document));
    }

    /// <summary>Opens a new read-only stream over the file content, positioned at the beginning.</summary>
    public Stream OpenRead() => new MemoryStream(_content, writable: false);

    /// <summary>Parses the bytes with the safe settings and turns every parser failure into a user message.</summary>
    private static XDocument Parse(string name, byte[] content)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(new MemoryStream(content, writable: false), XmlSettings.CreateReaderSettings());
            return XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"Файл «{name}» не є коректним XML (рядок {ex.LineNumber}, позиція {ex.LinePosition}): {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException or NotSupportedException)
        {
            // Bytes that do not match the declared encoding, or an encoding .NET does not know.
            throw new InvalidDataException($"Файл «{name}» має невідоме або пошкоджене кодування: {ex.Message}", ex);
        }
    }
}
