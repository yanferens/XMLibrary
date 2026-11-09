namespace XMLLibrary.Core.Model;

/// <summary>
/// One &lt;book&gt; element of the library. The attributes are kept as a dictionary because
/// different books have a different set of attributes (one has "language", another has "pages").
/// </summary>
/// <param name="Attributes">Every attribute of the &lt;book&gt; element: name → value.</param>
/// <param name="Title">The text of &lt;title&gt;, trimmed; empty if the book has no title.</param>
/// <param name="Author">The text of &lt;author&gt;, trimmed; empty if there is none.</param>
/// <param name="Annotation">The text of &lt;annotation&gt;, trimmed; empty if there is none.</param>
/// <param name="Readers">The readers listed in &lt;readers&gt;, in file order.</param>
public sealed record Book(
    IReadOnlyDictionary<string, string> Attributes,
    string Title,
    string Author,
    string Annotation,
    IReadOnlyList<Reader> Readers)
{
    /// <summary>Returns the value of an attribute, or <c>null</c> when this book does not have it.</summary>
    /// <param name="name">The attribute name, e.g. "year".</param>
    public string? GetAttribute(string name) => Attributes.GetValueOrDefault(name);
}

/// <summary>One &lt;reader&gt; of a book.</summary>
/// <param name="Name">The reader's full name.</param>
/// <param name="Faculty">The reader's faculty.</param>
/// <param name="Position">Who the reader is: student, postgraduate, lecturer, ...</param>
public sealed record Reader(string Name, string Faculty, string Position);
