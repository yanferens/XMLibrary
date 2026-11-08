using System.Xml;
using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Search;

/// <summary>
/// DOM: the whole document is loaded into memory as a tree of <see cref="XmlNode"/> objects
/// (<see cref="XmlDocument"/>), and the books are selected with an XPath query that is generated
/// from the criteria at run time, e.g. <c>/library/book[@faculty='ФКНК' and @year='2021']</c>.
/// </summary>
public sealed class DomSearchStrategy : IXmlSearchStrategy
{
    /// <inheritdoc />
    public string Name => "DOM";

    /// <inheritdoc />
    public string Description => "дерево + XPath";

    /// <inheritdoc />
    public IReadOnlyList<Book> Search(Stream xml, SearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(criteria);

        var document = new XmlDocument { XmlResolver = null };
        using (XmlReader reader = XmlReader.Create(xml, XmlSettings.CreateReaderSettings()))
        {
            document.Load(reader);
        }

        XmlNodeList? nodes = document.SelectNodes(BuildXPath(criteria));
        if (nodes is null)
        {
            return [];
        }

        var found = new List<Book>();
        foreach (XmlElement element in nodes.OfType<XmlElement>())
        {
            Book book = ToBook(element);

            // XPath 1.0 has no case-insensitive search, so the keywords are checked in C#.
            if (criteria.MatchesKeyword(book))
            {
                found.Add(book);
            }
        }

        return found;
    }

    /// <summary>
    /// Dynamic query generation: one XPath condition per chosen attribute, joined with "and".
    /// Without attributes the query selects every book.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <returns>An XPath 1.0 expression such as <c>/library/book[@year='2021']</c>.</returns>
    public static string BuildXPath(SearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        string path = $"/{LibraryXml.Root}/{LibraryXml.Book}";
        if (criteria.Attributes.Count == 0)
        {
            return path;
        }

        // VerifyNCName rejects anything that is not a plain attribute name, so the query cannot be broken
        // by a strange name; the values are always written as XPath string literals.
        IEnumerable<string> conditions = criteria.Attributes.Select(pair =>
            $"@{XmlConvert.VerifyNCName(pair.Key)}={Literal(pair.Value)}");

        return $"{path}[{string.Join(" and ", conditions)}]";
    }

    /// <summary>
    /// Writes a value as an XPath string literal. XPath 1.0 has no escape characters, so a value with an
    /// apostrophe ("комп'ютерної") goes into double quotes, and a value with both kinds of quotes is glued
    /// together with <c>concat()</c>.
    /// </summary>
    /// <param name="value">Any text.</param>
    public static string Literal(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.Contains('\''))
        {
            return $"'{value}'";
        }

        if (!value.Contains('"'))
        {
            return $"\"{value}\"";
        }

        return "concat('" + value.Replace("'", "', \"'\", '") + "')";
    }

    /// <summary>Converts a &lt;book&gt; element of the DOM tree into a <see cref="Book"/>.</summary>
    private static Book ToBook(XmlElement element)
    {
        // Namespace declarations (xmlns) live in their own namespace, so the check skips them too.
        Dictionary<string, string> attributes = element.Attributes
            .Cast<XmlAttribute>()
            .Where(attribute => attribute.NamespaceURI.Length == 0)
            .ToDictionary(attribute => attribute.LocalName, attribute => attribute.Value, StringComparer.Ordinal);

        List<Reader> readers = (element.SelectNodes($"{LibraryXml.Readers}/{LibraryXml.Reader}")?.OfType<XmlElement>()
                                ?? Enumerable.Empty<XmlElement>())
            .Select(reader => new Reader(
                reader.GetAttribute(LibraryXml.ReaderName),
                reader.GetAttribute(LibraryXml.ReaderFaculty),
                reader.GetAttribute(LibraryXml.ReaderPosition)))
            .ToList();

        return new Book(
            attributes,
            Text(element, LibraryXml.Title),
            Text(element, LibraryXml.Author),
            Text(element, LibraryXml.Annotation),
            readers);
    }

    /// <summary>The trimmed text of the first child element with this name (no namespace), or an empty string.</summary>
    private static string Text(XmlElement parent, string child)
        => parent[child, string.Empty]?.InnerText.Trim() ?? string.Empty;
}
