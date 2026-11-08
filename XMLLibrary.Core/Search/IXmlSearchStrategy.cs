using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Search;

/// <summary>
/// Strategy pattern: one way of searching an XML document. The rest of the program knows only this
/// interface, so SAX, DOM and LINQ to XML are interchangeable at run time, and a fourth way can be added
/// without changing a single line of the code that uses it.
/// </summary>
public interface IXmlSearchStrategy
{
    /// <summary>The name shown in the UI and in the log: "SAX", "DOM", "LINQ to XML".</summary>
    string Name { get; }

    /// <summary>A few words for the UI about how this strategy reads the file.</summary>
    string Description { get; }

    /// <summary>
    /// Reads the whole document from <paramref name="xml"/> and returns the matching books in file order.
    /// The &lt;book&gt; elements are the direct children of the &lt;library&gt; root.
    /// </summary>
    /// <param name="xml">A stream positioned at the beginning of a library document.</param>
    /// <param name="criteria">What to look for.</param>
    /// <exception cref="System.Xml.XmlException">The stream is not well-formed XML.</exception>
    IReadOnlyList<Book> Search(Stream xml, SearchCriteria criteria);
}
