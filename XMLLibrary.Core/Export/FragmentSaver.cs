using XMLLibrary.Core.Cloud;
using XMLLibrary.Core.Logging;
using XMLLibrary.Core.Model;
using XMLLibrary.Core.Transform;

namespace XMLLibrary.Core.Export;

/// <summary>
/// Factory Method, the creator. The base class holds everything that is the same for every format:
/// it opens the file, lets the product write into it, uploads the file to the cloud and logs the event.
/// Which product writes the data is decided by a subclass in <see cref="CreateWriter"/>.
/// </summary>
public abstract class FragmentSaver
{
    /// <summary>The factory method: creates the product that writes the chosen format.</summary>
    protected abstract IFragmentWriter CreateWriter();

    /// <summary>The level this format is logged with.</summary>
    protected abstract LogEventLevel EventLevel { get; }

    /// <summary>Builds the log message about a saved file.</summary>
    /// <param name="fileName">The name the file got in the cloud.</param>
    /// <param name="bookCount">How many books the fragment has.</param>
    protected abstract string DescribeSaved(string fileName, int bookCount);

    /// <summary>
    /// Opens the file the product writes into. The content is collected in memory and then uploaded in one
    /// request, so a failed upload never leaves half a file in the cloud.
    /// </summary>
    protected virtual Stream OpenFile() => new MemoryStream();

    /// <summary>Writes the fragment in this saver's format and uploads it.</summary>
    /// <param name="books">The filtered fragment.</param>
    /// <param name="name">The file name typed by the user; it is cleaned and gets the right extension.</param>
    /// <param name="cloud">Where to save.</param>
    /// <param name="cancellationToken">Stops the upload.</param>
    /// <returns>The created cloud file.</returns>
    public async Task<CloudFile> SaveAsync(
        IReadOnlyList<Book> books, string name, ICloudStorage cloud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(cloud);

        IFragmentWriter writer = CreateWriter();
        string fileName = FileNames.Build(name, writer.Extension);

        await using Stream file = OpenFile();
        writer.Write(books, file);
        file.Position = 0;

        CloudFile saved = await cloud.UploadAsync(fileName, writer.MimeType, file, cancellationToken);

        EventLogger.Instance.Log(EventLevel, DescribeSaved(saved.Name, books.Count));
        return saved;
    }
}

/// <summary>Concrete creator: saves the fragment as XML.</summary>
public sealed class XmlFragmentSaver : FragmentSaver
{
    /// <inheritdoc />
    protected override IFragmentWriter CreateWriter() => new XmlFragmentWriter();

    /// <inheritdoc />
    protected override LogEventLevel EventLevel => LogEventLevel.Saving;

    /// <inheritdoc />
    protected override string DescribeSaved(string fileName, int bookCount)
        => $"Відфільтрований фрагмент (книг: {bookCount}) збережено у файл {fileName} на Google Drive";
}

/// <summary>Concrete creator: saves the fragment as HTML, transformed with the given XSL stylesheet.</summary>
/// <param name="transformer">The stylesheet that turns the fragment into HTML.</param>
public sealed class HtmlFragmentSaver(XslTransformer transformer) : FragmentSaver
{
    private readonly XslTransformer _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));

    /// <inheritdoc />
    protected override IFragmentWriter CreateWriter() => new HtmlFragmentWriter(_transformer);

    /// <inheritdoc />
    protected override LogEventLevel EventLevel => LogEventLevel.Transformation;

    /// <inheritdoc />
    protected override string DescribeSaved(string fileName, int bookCount)
        => $"Фрагмент (книг: {bookCount}) перетворено за шаблоном {_transformer.Name} і збережено у файл {fileName} на Google Drive";
}
