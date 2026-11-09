namespace XMLLibrary.Core.Model;

/// <summary>
/// What the user is looking for: the required attribute values (chosen from the values present in the file)
/// and optional keywords searched in the title, the author and the annotation.
/// </summary>
/// <remarks>
/// The rules are kept here, in one place, and all three strategies call them,
/// so SAX, DOM and LINQ to XML can never give different answers for the same question.
/// </remarks>
public sealed class SearchCriteria
{
    private static readonly char[] Apostrophes = ['’', 'ʼ', '‘', '`', '´'];

    private readonly string[] _words;

    /// <summary>Creates the criteria.</summary>
    /// <param name="attributes">Attribute name → required value. Empty values mean "any value" and are dropped.</param>
    /// <param name="keyword">Words separated by spaces; every word must occur in the title, author or annotation.</param>
    public SearchCriteria(IReadOnlyDictionary<string, string>? attributes = null, string? keyword = null)
    {
        Attributes = (attributes ?? new Dictionary<string, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        Keyword = keyword?.Trim() ?? string.Empty;
        _words = NormalizeApostrophes(Keyword).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Criteria that match every book.</summary>
    public static SearchCriteria Empty { get; } = new();

    /// <summary>Attribute name → the value the book must have.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    /// <summary>The keywords as typed (trimmed), or an empty string for "no keywords".</summary>
    public string Keyword { get; }

    /// <summary>The separate keywords after splitting <see cref="Keyword"/> by spaces.</summary>
    public IReadOnlyList<string> Words => _words;

    /// <summary>Checks the attribute values only. The SAX strategy calls it before it reads the rest of the book.</summary>
    /// <param name="attributes">The attributes of a &lt;book&gt;.</param>
    public bool MatchesAttributes(IReadOnlyDictionary<string, string> attributes)
        => Attributes.All(pair => attributes.TryGetValue(pair.Key, out string? value) && value == pair.Value);

    /// <summary>
    /// Checks the keywords: every word must occur in the title, the author or the annotation.
    /// The letter case is ignored ("xml" finds "XML", "основи" finds "Основи"), and the typographic
    /// apostrophe ’ is treated as ' so "комп'ютер" finds "комп’ютер".
    /// </summary>
    /// <param name="book">A fully read book.</param>
    public bool MatchesKeyword(Book book)
    {
        if (_words.Length == 0)
        {
            return true;
        }

        string[] fields =
        [
            NormalizeApostrophes(book.Title),
            NormalizeApostrophes(book.Author),
            NormalizeApostrophes(book.Annotation),
        ];

        return _words.All(word => fields.Any(field => field.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Checks both the attributes and the keywords.</summary>
    /// <param name="book">A fully read book.</param>
    public bool Matches(Book book) => MatchesAttributes(book.Attributes) && MatchesKeyword(book);

    /// <summary>Describes the parameters for the event log: «faculty» – ФКНК, ключові слова – «xml».</summary>
    public string Describe()
    {
        List<string> parts = Attributes.Select(pair => $"«{pair.Key}» – {pair.Value}").ToList();
        if (Keyword.Length > 0)
        {
            parts.Add($"ключові слова – «{Keyword}»");
        }

        return parts.Count == 0 ? "без параметрів" : string.Join(", ", parts);
    }

    /// <summary>Replaces the typographic apostrophes and similar marks with the plain ' sign.</summary>
    /// <param name="text">Any text.</param>
    public static string NormalizeApostrophes(string text)
    {
        if (text.IndexOfAny(Apostrophes) < 0)
        {
            return text;
        }

        char[] characters = text.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (Array.IndexOf(Apostrophes, characters[i]) >= 0)
            {
                characters[i] = '\'';
            }
        }

        return new string(characters);
    }
}
