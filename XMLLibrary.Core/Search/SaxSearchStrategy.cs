using System.Text;
using System.Xml;
using XMLLibrary.Core.Model;

namespace XMLLibrary.Core.Search;

/// <summary>
/// Event-based reading in the spirit of SAX. .NET has no classic SAX parser that calls our methods;
/// its streaming equivalent is <see cref="XmlReader"/>: we ask it for the next node in a loop and react to
/// the same events SAX reports — the start of an element, its text, the end of an element.
/// The document is never held in memory as a whole: only the book being read right now.
/// </summary>
/// <remarks>
/// The depth of every node is checked, so the strategy reads exactly the same parts of the file as the
/// DOM and LINQ strategies: &lt;book&gt; at depth 1 (a child of the root), its fields at depth 2 and
/// readers at depth 3 inside &lt;readers&gt;. Elements from other namespaces are ignored everywhere.
/// </remarks>
public sealed class SaxSearchStrategy : IXmlSearchStrategy
{
    private const int BookDepth = 1;
    private const int FieldDepth = 2;
    private const int ReaderDepth = 3;

    /// <inheritdoc />
    public string Name => "SAX";

    /// <inheritdoc />
    public string Description => "потокове читання";

    /// <inheritdoc />
    public IReadOnlyList<Book> Search(Stream xml, SearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(criteria);

        var state = new ParserState(criteria);

        using XmlReader reader = XmlReader.Create(xml, XmlSettings.CreateReaderSettings());
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:                      // SAX: startElement
                    OnStartElement(reader, state);
                    break;

                case XmlNodeType.Text:                         // SAX: characters
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                    state.Current?.AppendText(reader.Value);
                    break;

                case XmlNodeType.EndElement:                   // SAX: endElement
                    OnEndElement(reader, state);
                    break;
            }
        }

        return state.Found;
    }

    /// <summary>Reacts to the start of an element according to how deep it is in the document.</summary>
    private static void OnStartElement(XmlReader reader, ParserState state)
    {
        switch (reader.Depth)
        {
            case 0:
                state.InsideLibrary = IsElement(reader, LibraryXml.Root);
                break;

            case BookDepth when state.InsideLibrary && IsElement(reader, LibraryXml.Book):
                Dictionary<string, string> attributes = ReadAttributes(reader);

                // The attributes are known right away: a book that does not match is not built at all,
                // and all its inner events are skipped because Current stays null.
                state.Current = state.Criteria.MatchesAttributes(attributes) ? new BookBuilder(attributes) : null;

                if (reader.IsEmptyElement)                     // <book .../> has no end element
                {
                    state.FinishBook();
                }

                break;

            case FieldDepth when state.Current is not null && reader.NamespaceURI.Length == 0:
                state.Current.StartField(reader.LocalName, reader.IsEmptyElement);
                break;

            case ReaderDepth when state.Current is not null && state.Current.InsideReaders && IsElement(reader, LibraryXml.Reader):
                Dictionary<string, string> readerAttributes = ReadAttributes(reader);
                state.Current.AddReader(new Reader(
                    readerAttributes.GetValueOrDefault(LibraryXml.ReaderName, string.Empty),
                    readerAttributes.GetValueOrDefault(LibraryXml.ReaderFaculty, string.Empty),
                    readerAttributes.GetValueOrDefault(LibraryXml.ReaderPosition, string.Empty)));
                break;
        }
    }

    /// <summary>Reacts to the end of an element: closes a field or finishes a book.</summary>
    private static void OnEndElement(XmlReader reader, ParserState state)
    {
        switch (reader.Depth)
        {
            case BookDepth:
                state.FinishBook();
                break;

            case FieldDepth:
                // Only one element of depth 2 can be open at a time, so its end closes whatever was open.
                state.Current?.EndField();
                break;
        }
    }

    /// <summary>Tells whether the reader stands on an element with this name and without a namespace.</summary>
    private static bool IsElement(XmlReader reader, string localName)
        => reader.LocalName == localName && reader.NamespaceURI.Length == 0;

    /// <summary>
    /// Reads the attributes of the current element. Namespace declarations and attributes from other
    /// namespaces are skipped (the same rule the DOM and LINQ strategies follow).
    /// </summary>
    private static Dictionary<string, string> ReadAttributes(XmlReader reader)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.MoveToNextAttribute())
        {
            if (reader.NamespaceURI.Length == 0)
            {
                attributes[reader.LocalName] = reader.Value;
            }
        }

        reader.MoveToElement();                                // back from the last attribute to the element itself
        return attributes;
    }

    /// <summary>Everything the event handlers share while one document is read.</summary>
    private sealed class ParserState(SearchCriteria criteria)
    {
        /// <summary>What the user is looking for.</summary>
        public SearchCriteria Criteria { get; } = criteria;

        /// <summary>The books found so far, in file order.</summary>
        public List<Book> Found { get; } = [];

        /// <summary>True when the root element is &lt;library&gt;.</summary>
        public bool InsideLibrary { get; set; }

        /// <summary>The matching book being read, or null outside a book and inside a skipped book.</summary>
        public BookBuilder? Current { get; set; }

        /// <summary>Builds the current book, checks the keywords and remembers it if it matches.</summary>
        public void FinishBook()
        {
            if (Current is not null)
            {
                Book book = Current.Build();
                if (Criteria.MatchesKeyword(book))             // the attributes were already checked at the start
                {
                    Found.Add(book);
                }
            }

            Current = null;
        }
    }

    /// <summary>Collects the parts of one book while its events arrive.</summary>
    private sealed class BookBuilder(Dictionary<string, string> attributes)
    {
        private readonly StringBuilder _title = new();
        private readonly StringBuilder _author = new();
        private readonly StringBuilder _annotation = new();
        private readonly List<Reader> _readers = [];
        private readonly HashSet<string> _seenFields = new(StringComparer.Ordinal);

        private StringBuilder? _openField;

        /// <summary>True between &lt;readers&gt; and &lt;/readers&gt;.</summary>
        public bool InsideReaders { get; private set; }

        /// <summary>
        /// Starts a child element of the book. Only the first &lt;title&gt;, &lt;author&gt; and &lt;annotation&gt;
        /// count, exactly like <c>XmlNode[name]</c> in DOM and <c>XElement.Element(name)</c> in LINQ.
        /// </summary>
        /// <param name="name">The local name of the element.</param>
        /// <param name="isEmpty">True for a self-closing element, which has no text and no end event.</param>
        public void StartField(string name, bool isEmpty)
        {
            if (name == LibraryXml.Readers)
            {
                InsideReaders = !isEmpty;
                return;
            }

            StringBuilder? target = name switch
            {
                LibraryXml.Title => _title,
                LibraryXml.Author => _author,
                LibraryXml.Annotation => _annotation,
                _ => null,
            };

            if (target is not null && _seenFields.Add(name) && !isEmpty)
            {
                _openField = target;
            }
        }

        /// <summary>Closes the child element that was open.</summary>
        public void EndField()
        {
            _openField = null;
            InsideReaders = false;
        }

        /// <summary>
        /// Adds text to the open field. The text of one element may arrive in several pieces
        /// (text, CDATA, text inside nested tags), so it is appended, never assigned.
        /// </summary>
        /// <param name="text">The text of the current node.</param>
        public void AppendText(string text) => _openField?.Append(text);

        /// <summary>Adds one reader from &lt;readers&gt;.</summary>
        /// <param name="reader">The reader read from the attributes of &lt;reader&gt;.</param>
        public void AddReader(Reader reader) => _readers.Add(reader);

        /// <summary>Creates the finished book.</summary>
        public Book Build() => new(
            attributes,
            _title.ToString().Trim(),
            _author.ToString().Trim(),
            _annotation.ToString().Trim(),
            _readers.ToList());
    }
}
