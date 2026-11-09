using System.Text;
using System.Xml;

namespace XMLLibrary.Core.Model;

/// <summary>The reader settings shared by every place that parses XML or XSL in the program.</summary>
public static class XmlSettings
{
    /// <summary>
    /// Registers the legacy code pages once, so files saved as <c>encoding="windows-1251"</c>
    /// (common for Ukrainian texts) open instead of failing with "encoding is not supported".
    /// </summary>
    static XmlSettings() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// Creates safe reader settings. DTDs are forbidden: a file from the Internet could otherwise make
    /// the parser read local files or expand "billion laughs" entities. Comments, processing instructions
    /// and formatting whitespace are skipped because the program never needs them.
    /// </summary>
    /// <remarks>A new object is returned every time, so one caller can never change the settings of another.</remarks>
    public static XmlReaderSettings CreateReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersFromEntities = 1024,
    };
}
