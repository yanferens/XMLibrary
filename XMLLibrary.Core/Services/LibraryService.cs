using XMLLibrary.Core.Logging;
using XMLLibrary.Core.Model;
using XMLLibrary.Core.Search;
using XMLLibrary.Core.Transform;

namespace XMLLibrary.Core.Services;

/// <summary>The operations the user interface calls: search with a chosen strategy and transform to HTML, both logged.</summary>
public sealed class LibraryService
{
    /// <summary>All strategies the user can choose from, in the order they are shown.</summary>
    public IReadOnlyList<IXmlSearchStrategy> Strategies { get; } =
    [
        new SaxSearchStrategy(),
        new DomSearchStrategy(),
        new LinqSearchStrategy(),
    ];

    /// <summary>Searches the document with the chosen strategy and writes a "Фільтрація" line to the log.</summary>
    /// <param name="document">The opened library.</param>
    /// <param name="strategy">SAX, DOM or LINQ to XML.</param>
    /// <param name="criteria">What to look for.</param>
    /// <returns>The matching books in file order.</returns>
    public IReadOnlyList<Book> Search(LibraryDocument document, IXmlSearchStrategy strategy, SearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(criteria);

        using Stream xml = document.OpenRead();
        IReadOnlyList<Book> books = strategy.Search(xml, criteria);

        EventLogger.Instance.Log(LogEventLevel.Filtering,
            $"Документ: {document.Name}. Параметри: {criteria.Describe()}. Спосіб: {strategy.Name}. Знайдено: {books.Count}");
        return books;
    }

    /// <summary>Transforms the whole document to HTML for the preview and writes a "Трансформація" line to the log.</summary>
    /// <param name="document">The opened library.</param>
    /// <param name="transformer">The opened stylesheet.</param>
    public string TransformToHtml(LibraryDocument document, XslTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(transformer);

        using Stream xml = document.OpenRead();
        string html = transformer.TransformToString(xml);

        EventLogger.Instance.Log(LogEventLevel.Transformation,
            $"Документ {document.Name} перетворено в HTML за шаблоном {transformer.Name}");
        return html;
    }
}
