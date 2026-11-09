using System.Xml.Linq;

namespace XMLLibrary.Core.Model;

/// <summary>
/// The attributes that occur on the &lt;book&gt; elements of one file, each with all its different values.
/// The user interface builds one drop-down list per attribute from it ("data loading"), so the user
/// can only choose values that really exist in the opened file.
/// </summary>
public sealed class AttributeCatalog
{
    private AttributeCatalog(IReadOnlyList<AttributeValues> attributes) => Attributes = attributes;

    /// <summary>The attributes in the order they first appear in the file.</summary>
    public IReadOnlyList<AttributeValues> Attributes { get; }

    /// <summary>
    /// Collects the catalog from a parsed library document. Only the books the strategies search
    /// (direct children of the root) are looked at; empty values and the unique id are skipped.
    /// </summary>
    /// <param name="document">A document whose root is &lt;library&gt;.</param>
    public static AttributeCatalog FromXml(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IEnumerable<XElement> books = document.Root?.Elements(LibraryXml.Book) ?? Enumerable.Empty<XElement>();

        List<AttributeValues> attributes = books
            .SelectMany(book => book.Attributes())
            .Where(attribute => LibraryXml.IsSearchable(attribute) && !string.IsNullOrWhiteSpace(attribute.Value))
            .GroupBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal)
            .Select(group => new AttributeValues(
                group.Key,
                group.Select(attribute => attribute.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Order(NaturalValueComparer.Instance)
                    .ToList()))
            .ToList();

        return new AttributeCatalog(attributes);
    }
}

/// <summary>One attribute name with its distinct values, e.g. "year": 2018, 2019, 2021.</summary>
/// <param name="Name">The attribute name as written in the file.</param>
/// <param name="Values">The distinct non-empty values, sorted for a drop-down list.</param>
public sealed record AttributeValues(string Name, IReadOnlyList<string> Values);
