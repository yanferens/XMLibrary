using System.Xml;
using System.Xml.Linq;
using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Search;

/// <summary>
/// LINQ to XML: the document is loaded as an <see cref="XDocument"/>, and the query is assembled
/// from <c>Where</c> calls — one per chosen attribute — before it is executed.
/// </summary>
public sealed class LinqSearchStrategy : IXmlSearchStrategy
{
    /// <inheritdoc />
    public string Name => "LINQ to XML";

    /// <inheritdoc />
    public string Description => "запити LINQ";

    /// <inheritdoc />
    public IReadOnlyList<Book> Search(Stream xml, SearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(criteria);

        XDocument document;
        using (XmlReader reader = XmlReader.Create(xml, XmlSettings.CreateReaderSettings()))
        {
            document = XDocument.Load(reader);
        }

        if (document.Root is not { } root || root.Name != XName.Get(LibraryXml.Root))
        {
            return [];
        }

        IEnumerable<XElement> query = root.Elements(LibraryXml.Book);

        // Dynamic query generation: nothing runs yet, every Where only adds one more condition.
        foreach ((string name, string value) in criteria.Attributes)
        {
            query = query.Where(book => (string?)book.Attribute(name) == value);
        }

        return query
            .Select(ToBook)
            .Where(criteria.MatchesKeyword)
            .ToList();                                         // here the query actually runs
    }

    /// <summary>Converts a &lt;book&gt; element into a <see cref="Book"/>.</summary>
    /// <param name="element">A &lt;book&gt; element.</param>
    public static Book ToBook(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        Dictionary<string, string> attributes = element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace == XNamespace.None)
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value, StringComparer.Ordinal);

        List<Reader> readers = element.Elements(LibraryXml.Readers).Elements(LibraryXml.Reader)
            .Select(reader => new Reader(
                (string?)reader.Attribute(LibraryXml.ReaderName) ?? string.Empty,
                (string?)reader.Attribute(LibraryXml.ReaderFaculty) ?? string.Empty,
                (string?)reader.Attribute(LibraryXml.ReaderPosition) ?? string.Empty))
            .ToList();

        return new Book(
            attributes,
            Text(element, LibraryXml.Title),
            Text(element, LibraryXml.Author),
            Text(element, LibraryXml.Annotation),
            readers);
    }

    /// <summary>The trimmed text of the first child element with this name, or an empty string.</summary>
    private static string Text(XElement parent, string child)
        => ((string?)parent.Element(child) ?? string.Empty).Trim();
}
