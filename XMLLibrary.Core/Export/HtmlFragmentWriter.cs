using XMLLibrary.Core.Model;
using XMLLibrary.Core.Transform;

namespace XMLLibrary.Core.Export;

/// <summary>Concrete product: writes the found books as HTML — first as an XML fragment, then through the XSL stylesheet.</summary>
/// <param name="transformer">The compiled stylesheet chosen by the user.</param>
public sealed class HtmlFragmentWriter(XslTransformer transformer) : IFragmentWriter
{
    private readonly XslTransformer _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));

    /// <inheritdoc />
    public string Extension => ".html";

    /// <inheritdoc />
    public string MimeType => "text/html";

    /// <inheritdoc />
    public void Write(IReadOnlyList<Book> books, Stream output)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(output);

        using var xml = new MemoryStream();
        new XmlFragmentWriter().Write(books, xml);
        xml.Position = 0;
        _transformer.Transform(xml, output);
    }
}
