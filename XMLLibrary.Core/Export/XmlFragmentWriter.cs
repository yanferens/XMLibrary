using System.Text;
using System.Xml;
using System.Xml.Linq;
using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Export;

/// <summary>Concrete product: writes the found books as a new library XML document with the same structure as the source.</summary>
public sealed class XmlFragmentWriter : IFragmentWriter
{
    /// <inheritdoc />
    public string Extension => ".xml";

    /// <inheritdoc />
    public string MimeType => "application/xml";

    /// <inheritdoc />
    public void Write(IReadOnlyList<Book> books, Stream output)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(output);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            CloseOutput = false,
        };

        using XmlWriter writer = XmlWriter.Create(output, settings);
        ToDocument(books).Save(writer);
    }

    /// <summary>Builds the XML document of the fragment: &lt;library&gt; with one &lt;book&gt; per found book.</summary>
    /// <param name="books">The found books.</param>
    public static XDocument ToDocument(IReadOnlyList<Book> books)
    {
        ArgumentNullException.ThrowIfNull(books);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(LibraryXml.Root,
                books.Select(book => new XElement(LibraryXml.Book,
                    book.Attributes.Select(pair => new XAttribute(pair.Key, pair.Value)),
                    new XElement(LibraryXml.Title, book.Title),
                    new XElement(LibraryXml.Author, book.Author),
                    new XElement(LibraryXml.Annotation, book.Annotation),
                    new XElement(LibraryXml.Readers,
                        book.Readers.Select(reader => new XElement(LibraryXml.Reader,
                            new XAttribute(LibraryXml.ReaderName, reader.Name),
                            new XAttribute(LibraryXml.ReaderFaculty, reader.Faculty),
                            new XAttribute(LibraryXml.ReaderPosition, reader.Position))))))));
    }
}
