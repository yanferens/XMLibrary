using System.Text;
using System.Xml;
using System.Xml.Xsl;
using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Transform;

/// <summary>Turns an XML document into HTML with an XSL stylesheet (XSLT 1.0).</summary>
/// <remarks>The compiled stylesheet is thread-safe, so one object can be used from a background thread.</remarks>
public sealed class XslTransformer
{
    private readonly XslCompiledTransform _transform = new();

    /// <summary>Compiles a stylesheet.</summary>
    /// <param name="name">The file name for messages and captions.</param>
    /// <param name="xsl">The stylesheet content.</param>
    /// <exception cref="InvalidDataException">The file is empty, too big or not a valid XSLT stylesheet.</exception>
    public XslTransformer(string name, byte[] xsl)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(xsl);

        if (xsl.Length == 0)
        {
            throw new InvalidDataException($"Файл «{name}» порожній.");
        }

        if (xsl.LongLength > FileKinds.MaxFileBytes)
        {
            throw new InvalidDataException($"Файл «{name}» завеликий для XSL-шаблону.");
        }

        Name = name;
        try
        {
            using XmlReader reader = XmlReader.Create(new MemoryStream(xsl, writable: false), XmlSettings.CreateReaderSettings());

            // XsltSettings.Default: no document() function and no embedded scripts — a stylesheet
            // downloaded from Drive must not be able to read other files or run code. The resolver is null,
            // so xsl:include and xsl:import of other files are refused as well.
            _transform.Load(reader, XsltSettings.Default, stylesheetResolver: null);
        }
        catch (Exception ex) when (ex is XmlException or XsltException or DecoderFallbackException or ArgumentException)
        {
            throw new InvalidDataException($"Файл «{name}» не є коректним XSL-документом: {ex.Message}", ex);
        }
    }

    /// <summary>The file name of the stylesheet, e.g. "library.xsl".</summary>
    public string Name { get; }

    /// <summary>
    /// Transforms <paramref name="xml"/> and writes the result to <paramref name="output"/> as bytes.
    /// The encoding comes from &lt;xsl:output&gt; (utf-8), and the &lt;meta charset&gt; that XSLT adds
    /// to the HTML names the same encoding, so Ukrainian letters are shown correctly.
    /// </summary>
    /// <param name="xml">The source document.</param>
    /// <param name="output">Where the result is written; the stream is left open.</param>
    /// <exception cref="InvalidDataException">The stylesheet failed while it ran (for example, &lt;xsl:message terminate="yes"&gt;).</exception>
    public void Transform(Stream xml, Stream output)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(output);

        try
        {
            using XmlReader reader = XmlReader.Create(xml, XmlSettings.CreateReaderSettings());
            _transform.Transform(reader, arguments: null, output);
        }
        catch (Exception ex) when (ex is XmlException or XsltException)
        {
            throw new InvalidDataException($"Не вдалося перетворити документ за шаблоном «{Name}»: {ex.Message}", ex);
        }
    }

    /// <summary>Transforms the document and returns the result as text, for the preview.</summary>
    /// <param name="xml">The source document.</param>
    public string TransformToString(Stream xml)
    {
        using var buffer = new MemoryStream();
        Transform(xml, buffer);
        buffer.Position = 0;

        // detectEncodingFromByteOrderMarks also drops the byte order mark if the stylesheet wrote one.
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
