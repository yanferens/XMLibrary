using System.Xml.Linq;

namespace XMLLibrary.Core.Model;

/// <summary>
/// Names of the elements and attributes of the library XML file, kept in one place
/// so that the three search strategies, the catalog and the writers never disagree about them.
/// </summary>
public static class LibraryXml
{
    /// <summary>The root element: &lt;library&gt;.</summary>
    public const string Root = "library";

    /// <summary>The main node: one &lt;book&gt; per book, a direct child of the root.</summary>
    public const string Book = "book";

    /// <summary>The child element with the title of a book.</summary>
    public const string Title = "title";

    /// <summary>The child element with the author of a book.</summary>
    public const string Author = "author";

    /// <summary>The child element with a short description of a book.</summary>
    public const string Annotation = "annotation";

    /// <summary>The second level of the structure: the list of readers of a book.</summary>
    public const string Readers = "readers";

    /// <summary>One reader inside &lt;readers&gt;.</summary>
    public const string Reader = "reader";

    /// <summary>The unique id of a book. Every value is different, so it is useless as a search filter.</summary>
    public const string Id = "id";

    /// <summary>The attribute of &lt;reader&gt; with the reader's full name.</summary>
    public const string ReaderName = "name";

    /// <summary>The attribute of &lt;reader&gt; with the reader's faculty.</summary>
    public const string ReaderFaculty = "faculty";

    /// <summary>The attribute of &lt;reader&gt; with the reader's position (student, lecturer, ...).</summary>
    public const string ReaderPosition = "position";

    private static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
    {
        ["faculty"] = "Факультет",
        ["department"] = "Кафедра",
        ["kind"] = "Вид видання",
        ["year"] = "Рік",
        ["language"] = "Мова",
        ["pages"] = "Сторінок",
        ["publisher"] = "Видавництво",
    };

    /// <summary>
    /// Returns the Ukrainian caption of an attribute for the user interface.
    /// An attribute the program does not know keeps its own name, so any XML file still works.
    /// </summary>
    /// <param name="attribute">The attribute name as written in the file, e.g. "faculty".</param>
    public static string Caption(string attribute) => Captions.GetValueOrDefault(attribute, attribute);

    /// <summary>
    /// Tells whether an attribute of a &lt;book&gt; takes part in the search:
    /// namespace declarations, attributes from other namespaces and the unique id do not.
    /// </summary>
    /// <param name="attribute">An attribute of a &lt;book&gt; element.</param>
    public static bool IsSearchable(XAttribute attribute)
        => !attribute.IsNamespaceDeclaration
           && attribute.Name.Namespace == XNamespace.None
           && attribute.Name.LocalName != Id;
}
