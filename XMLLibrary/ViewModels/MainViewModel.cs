using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using XMLLibrary.Core.Cloud;
using XMLLibrary.Core.Export;
using XMLLibrary.Core.Logging;
using XMLLibrary.Core.Model;
using XMLLibrary.Core.Search;
using XMLLibrary.Core.Services;
using XMLLibrary.Core.Transform;

namespace XMLLibrary.ViewModels;

/// <summary>
/// The state and the commands of the main window. It contains no MAUI types: the page only binds to it,
/// and every action goes through the Core classes, so there is no "magic pushbutton" — no button handler
/// with XML, search or Drive code in it.
/// </summary>
/// <remarks>
/// Every long operation runs through <see cref="RunAsync"/>: while it runs, the other buttons are disabled,
/// and any failure becomes a message for the user instead of a crash.
/// </remarks>
public sealed class MainViewModel : ViewModelBase
{
    /// <summary>The question the task requires before the program closes.</summary>
    public const string ExitQuestion = "Чи дійсно ви хочете завершити роботу з програмою?";

    /// <summary>How many cards are shown at most; all found books are still saved.</summary>
    public const int MaxShownResults = 300;

    /// <summary>A page longer than this is opened in the browser instead of the built-in preview.</summary>
    public const int MaxPreviewLength = 1_500_000;

    /// <summary>How many of the newest log lines the log window shows.</summary>
    public const int LogLinesShown = 300;

    private readonly LibraryService _service;
    private readonly ICloudStorage _cloud;
    private readonly IDialogService _dialogs;
    private readonly IFileService _files;
    private readonly IAppLifetime _lifetime;
    private readonly List<AsyncCommand> _guardedCommands = [];

    private LibraryDocument? _document;
    private XslTransformer? _transformer;
    private IReadOnlyList<Book> _found = [];
    private bool _hasSearched;
    private StrategyOptionViewModel _selectedOption;
    private IReadOnlyList<AttributeFilterViewModel> _filters = [];
    private IReadOnlyList<BookItem> _results = [];
    private string _keyword = string.Empty;
    private string _status = "Відкрийте XML-файл з диска або з Google Drive.";
    private bool _isBusy;
    private string _previewHtml = string.Empty;
    private string _previewTitle = string.Empty;
    private string _previewFileName = "preview.html";
    private bool _isPreviewVisible;
    private IReadOnlyList<LogLineItem> _logLines = [];
    private bool _isLogVisible;

    /// <summary>Creates the view model with everything it works with.</summary>
    /// <param name="service">Search and transformation.</param>
    /// <param name="cloud">Google Drive (or a fake in the tests).</param>
    /// <param name="dialogs">Messages, questions and choices.</param>
    /// <param name="files">The file picker and the browser.</param>
    /// <param name="lifetime">Closing the program.</param>
    public MainViewModel(LibraryService service, ICloudStorage cloud, IDialogService dialogs, IFileService files, IAppLifetime lifetime)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _cloud = cloud ?? throw new ArgumentNullException(nameof(cloud));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));

        Strategies = service.Strategies.Select(strategy => new StrategyOptionViewModel(strategy, SelectStrategy)).ToList();
        if (Strategies.Count == 0)
        {
            throw new ArgumentException("The service has no search strategies.", nameof(service));
        }

        _selectedOption = Strategies[0];
        _selectedOption.IsSelected = true;

        OpenXmlCommand = Guarded(OpenXmlAsync);
        OpenXmlFromDriveCommand = Guarded(OpenXmlFromDriveAsync);
        OpenXslCommand = Guarded(OpenXslAsync);
        OpenXslFromDriveCommand = Guarded(OpenXslFromDriveAsync);
        SearchCommand = Guarded(SearchAsync, () => _document is not null);
        ClearCommand = Guarded(ClearAsync);
        ShowHtmlCommand = Guarded(ShowHtmlAsync, () => _document is not null && _transformer is not null);
        SaveXmlCommand = Guarded(() => SaveAsync(new XmlFragmentSaver()), () => _found.Count > 0);
        SaveHtmlCommand = Guarded(SaveHtmlAsync, () => _found.Count > 0 && _transformer is not null);
        SignOutCommand = Guarded(SignOutAsync, () => _cloud.IsSignedIn);
        ShowLogCommand = Guarded(ShowLogAsync);
        OpenPreviewInBrowserCommand = Guarded(OpenPreviewInBrowserAsync, () => _previewHtml.Length > 0);
        ClosePreviewCommand = new RelayCommand(() => IsPreviewVisible = false);
        CloseLogCommand = new RelayCommand(() => IsLogVisible = false);
        ExitCommand = new AsyncCommand(ExitAsync);
    }

    // ───────────── Commands (bound to the buttons) ─────────────

    /// <summary>Opens an XML file from the disk.</summary>
    public AsyncCommand OpenXmlCommand { get; }

    /// <summary>Opens an XML file from Google Drive.</summary>
    public AsyncCommand OpenXmlFromDriveCommand { get; }

    /// <summary>Opens an XSL stylesheet from the disk.</summary>
    public AsyncCommand OpenXslCommand { get; }

    /// <summary>Opens an XSL stylesheet from Google Drive.</summary>
    public AsyncCommand OpenXslFromDriveCommand { get; }

    /// <summary>Searches with the chosen parameters and strategy.</summary>
    public AsyncCommand SearchCommand { get; }

    /// <summary>The Clear button: resets every parameter and empties the results.</summary>
    public AsyncCommand ClearCommand { get; }

    /// <summary>Transforms the whole document to HTML and shows it.</summary>
    public AsyncCommand ShowHtmlCommand { get; }

    /// <summary>Saves the found books to Google Drive as XML.</summary>
    public AsyncCommand SaveXmlCommand { get; }

    /// <summary>Saves the found books to Google Drive as HTML.</summary>
    public AsyncCommand SaveHtmlCommand { get; }

    /// <summary>Forgets the Google account.</summary>
    public AsyncCommand SignOutCommand { get; }

    /// <summary>Shows the newest lines of the event log.</summary>
    public AsyncCommand ShowLogCommand { get; }

    /// <summary>Opens the previewed page in the default browser.</summary>
    public AsyncCommand OpenPreviewInBrowserCommand { get; }

    /// <summary>Closes the HTML preview.</summary>
    public ICommand ClosePreviewCommand { get; }

    /// <summary>Closes the log window.</summary>
    public ICommand CloseLogCommand { get; }

    /// <summary>Asks "Так / Ні" and closes the program on "Так". Used by the button and by the × of the window.</summary>
    public AsyncCommand ExitCommand { get; }

    // ───────────── Search form ─────────────

    /// <summary>The segments of the "SAX | DOM | LINQ to XML" switch.</summary>
    public IReadOnlyList<StrategyOptionViewModel> Strategies { get; }

    /// <summary>The strategy the next search will use.</summary>
    public IXmlSearchStrategy SelectedStrategy => _selectedOption.Strategy;

    /// <summary>One drop-down list per attribute of the opened file.</summary>
    public IReadOnlyList<AttributeFilterViewModel> Filters
    {
        get => _filters;
        private set => SetProperty(ref _filters, value);
    }

    /// <summary>The keywords typed by the user.</summary>
    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value ?? string.Empty);
    }

    // ───────────── Sources (left panel) ─────────────

    /// <summary>True when an XML file is open.</summary>
    public bool HasDocument => _document is not null;

    /// <summary>True when no XML file is open yet.</summary>
    public bool HasNoDocument => _document is null;

    /// <summary>The name of the opened XML file.</summary>
    public string DocumentTitle => _document?.Name ?? "Файл не вибрано";

    /// <summary>A short description of the opened XML file.</summary>
    public string DocumentDetails => _document is null
        ? "Відкрийте бібліотеку з диска або з Google Drive"
        : $"{UkrainianText.Books(_document.BookCount)} · {UkrainianText.Attributes(_document.Catalog.Attributes.Count)}";

    /// <summary>True when an XSL stylesheet is open.</summary>
    public bool HasStylesheet => _transformer is not null;

    /// <summary>The name of the opened stylesheet.</summary>
    public string StylesheetTitle => _transformer?.Name ?? "Шаблон не вибрано";

    /// <summary>A short description of the stylesheet state.</summary>
    public string StylesheetDetails => _transformer is null
        ? "Потрібен для HTML-перегляду та збереження в HTML"
        : "Шаблон XSLT скомпільовано й готово до роботи";

    /// <summary>True when the program is signed in to Google Drive.</summary>
    public bool IsSignedIn => _cloud.IsSignedIn;

    /// <summary>The Google Drive state for the left panel.</summary>
    public string DriveStatusText => _cloud.IsSignedIn
        ? "Акаунт підключено"
        : "Не підключено: вхід відкриється в браузері";

    // ───────────── Results ─────────────

    /// <summary>The cards of the found books (at most <see cref="MaxShownResults"/>).</summary>
    public IReadOnlyList<BookItem> Results
    {
        get => _results;
        private set => SetProperty(ref _results, value);
    }

    /// <summary>True when there are cards to show.</summary>
    public bool HasResults => _results.Count > 0;

    /// <summary>The text of the badge next to "Результати".</summary>
    public string ResultsCountText => _found.Count > MaxShownResults
        ? $"{UkrainianText.Books(_found.Count)} · показано {MaxShownResults}"
        : UkrainianText.Books(_found.Count);

    /// <summary>True when the hint in place of the results is shown.</summary>
    public bool IsEmptyStateVisible => _results.Count == 0;

    /// <summary>The title of the hint in place of the results.</summary>
    public string EmptyStateTitle => _document is null ? "Почніть з XML-файлу"
        : !_hasSearched ? "Усе готово до пошуку"
        : "Нічого не знайдено";

    /// <summary>The text of the hint in place of the results.</summary>
    public string EmptyStateText => _document is null
        ? "Відкрийте бібліотеку з диска або з Google Drive — списки атрибутів для пошуку з'являться автоматично."
        : !_hasSearched
            ? "Виберіть значення атрибутів, введіть ключові слова й натисніть «Шукати». Без параметрів буде показано всі книги."
            : "Спробуйте інші значення атрибутів, інші ключові слова або натисніть «Очистити».";

    // ───────────── Status line ─────────────

    /// <summary>The text of the status line at the bottom.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>True while an operation runs: the spinner turns and the buttons are disabled.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    // ───────────── HTML preview ─────────────

    /// <summary>The page shown in the preview.</summary>
    public string PreviewHtml
    {
        get => _previewHtml;
        private set
        {
            if (SetProperty(ref _previewHtml, value))
            {
                OpenPreviewInBrowserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>"library.xml → library.xsl".</summary>
    public string PreviewTitle
    {
        get => _previewTitle;
        private set => SetProperty(ref _previewTitle, value);
    }

    /// <summary>True while the preview window is open.</summary>
    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        private set => SetProperty(ref _isPreviewVisible, value);
    }

    // ───────────── Event log window ─────────────

    /// <summary>The newest lines of the log, the newest first.</summary>
    public IReadOnlyList<LogLineItem> LogLines
    {
        get => _logLines;
        private set
        {
            if (SetProperty(ref _logLines, value))
            {
                OnPropertiesChanged(nameof(HasLogLines), nameof(HasNoLogLines));
            }
        }
    }

    /// <summary>True when the log has lines.</summary>
    public bool HasLogLines => _logLines.Count > 0;

    /// <summary>True when the log is still empty.</summary>
    public bool HasNoLogLines => _logLines.Count == 0;

    /// <summary>Where the log file is.</summary>
    public string LogPath => EventLogger.Instance.FilePath;

    /// <summary>True while the log window is open.</summary>
    public bool IsLogVisible
    {
        get => _isLogVisible;
        private set => SetProperty(ref _isLogVisible, value);
    }

    // ───────────── Opening files ─────────────

    /// <summary>Lets the user pick an XML file on the disk and opens it.</summary>
    private async Task OpenXmlAsync()
    {
        PickedFile? file = await _files.PickAsync("Виберіть XML-файл бібліотеки", FileKinds.XmlExtensions);
        if (file is null)
        {
            Status = "Відкриття файлу скасовано.";
            return;
        }

        Status = $"Читаю {file.Name}…";
        LibraryDocument document = await Task.Run(() => LibraryDocument.Load(file.Name, file.Content));
        SetDocument(document, "з диска");
    }

    /// <summary>Lets the user choose an XML file on Google Drive, downloads and opens it.</summary>
    private async Task OpenXmlFromDriveAsync()
    {
        CloudFile? file = await ChooseFromDriveAsync(FileKinds.XmlExtensions, "XML-файли");
        if (file is null)
        {
            return;
        }

        Status = $"Завантажую {file.Name} з Google Drive…";
        byte[] content = await _cloud.DownloadAsync(file.Id);
        LibraryDocument document = await Task.Run(() => LibraryDocument.Load(file.Name, content));
        SetDocument(document, "з Google Drive");
    }

    /// <summary>Lets the user pick an XSL stylesheet on the disk and compiles it.</summary>
    private async Task OpenXslAsync()
    {
        PickedFile? file = await _files.PickAsync("Виберіть XSL-шаблон", FileKinds.XslExtensions);
        if (file is null)
        {
            Status = "Відкриття шаблону скасовано.";
            return;
        }

        Status = $"Компілюю шаблон {file.Name}…";
        XslTransformer transformer = await Task.Run(() => new XslTransformer(file.Name, file.Content));
        SetTransformer(transformer, "з диска");
    }

    /// <summary>Lets the user choose an XSL stylesheet on Google Drive, downloads and compiles it.</summary>
    private async Task OpenXslFromDriveAsync()
    {
        CloudFile? file = await ChooseFromDriveAsync(FileKinds.XslExtensions, "XSL-шаблони");
        if (file is null)
        {
            return;
        }

        Status = $"Завантажую {file.Name} з Google Drive…";
        byte[] content = await _cloud.DownloadAsync(file.Id);
        XslTransformer transformer = await Task.Run(() => new XslTransformer(file.Name, content));
        SetTransformer(transformer, "з Google Drive");
    }

    /// <summary>Shows the files of one kind from Google Drive and returns the chosen one, or null.</summary>
    /// <param name="extensions">Which files to list.</param>
    /// <param name="caption">"XML-файли" or "XSL-шаблони" for the title of the list.</param>
    private async Task<CloudFile?> ChooseFromDriveAsync(IReadOnlyList<string> extensions, string caption)
    {
        Status = "Отримую список файлів з Google Drive…";
        IReadOnlyList<CloudFile> files = await _cloud.ListAsync(extensions);
        NotifyDriveState();

        if (files.Count == 0)
        {
            Status = "На Google Drive немає потрібних файлів.";
            await _dialogs.ShowMessageAsync("Google Drive", $"На вашому Google Drive немає файлів {string.Join(" або ", extensions)}.");
            return null;
        }

        Status = "Виберіть файл у списку.";
        List<ChoiceItem> items = files.Select(file => new ChoiceItem(file.Name, DescribeCloudFile(file))).ToList();
        int? index = await _dialogs.ChooseAsync($"{caption} на Google Drive", items);

        if (index is not { } position || position < 0 || position >= files.Count)
        {
            Status = "Вибір файлу скасовано.";
            return null;
        }

        CloudFile chosen = files[position];
        if (chosen.Size > FileKinds.MaxFileBytes)
        {
            throw new InvalidDataException($"Файл «{chosen.Name}» завеликий: можна відкрити файл до {FileKinds.MaxFileBytes / 1024 / 1024} МБ.");
        }

        return chosen;
    }

    /// <summary>"змінено 07.11.2026 20:41 · 12.4 КБ" for the list of Drive files.</summary>
    private static string DescribeCloudFile(CloudFile file)
    {
        var parts = new List<string>();
        if (file.Modified is { } modified)
        {
            parts.Add("змінено " + modified.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));
        }

        if (file.Size is { } size)
        {
            parts.Add(UkrainianText.FileSize(size));
        }

        return parts.Count == 0 ? "Google Drive" : string.Join(" · ", parts);
    }

    /// <summary>
    /// "Data loading": remembers the new document and rebuilds the drop-down lists from the attribute values
    /// of this file. The old results and the old preview belong to the old file, so they are dropped.
    /// </summary>
    private void SetDocument(LibraryDocument document, string source)
    {
        _document = document;
        Filters = document.Catalog.Attributes.Select(attribute => new AttributeFilterViewModel(attribute)).ToList();
        ClosePreview();
        ClearResults();

        Status = $"Відкрито {document.Name} {source}: {UkrainianText.Books(document.BookCount)}, "
                 + $"{UkrainianText.Attributes(document.Catalog.Attributes.Count)} для пошуку.";
        OnPropertiesChanged(nameof(HasDocument), nameof(HasNoDocument), nameof(DocumentTitle), nameof(DocumentDetails));
        RefreshEmptyState();
        RefreshCommands();
    }

    /// <summary>Remembers the new stylesheet.</summary>
    private void SetTransformer(XslTransformer transformer, string source)
    {
        _transformer = transformer;
        ClosePreview();

        Status = $"Відкрито шаблон {transformer.Name} {source}.";
        OnPropertiesChanged(nameof(HasStylesheet), nameof(StylesheetTitle), nameof(StylesheetDetails));
        RefreshCommands();
    }

    // ───────────── Search and Clear ─────────────

    /// <summary>
    /// Selects a segment of the strategy switch. The next search uses it; the results already shown stay,
    /// because all three strategies give the same answer.
    /// </summary>
    private void SelectStrategy(StrategyOptionViewModel option)
    {
        if (ReferenceEquals(option, _selectedOption))
        {
            return;
        }

        _selectedOption.IsSelected = false;
        _selectedOption = option;
        option.IsSelected = true;

        OnPropertyChanged(nameof(SelectedStrategy));
        Status = $"Спосіб аналізу: {option.Name}.";
    }

    /// <summary>Collects the chosen values and the keywords into criteria.</summary>
    private SearchCriteria BuildCriteria()
    {
        Dictionary<string, string> attributes = Filters
            .Where(filter => filter.Value is not null)
            .ToDictionary(filter => filter.Name, filter => filter.Value!, StringComparer.Ordinal);

        return new SearchCriteria(attributes, Keyword);
    }

    /// <summary>Runs the search on a background thread, so the window stays responsive on a big file.</summary>
    private async Task SearchAsync()
    {
        LibraryDocument? document = _document;
        if (document is null)
        {
            return;
        }

        SearchCriteria criteria = BuildCriteria();
        IXmlSearchStrategy strategy = SelectedStrategy;

        Status = $"Шукаю ({strategy.Name})…";
        IReadOnlyList<Book> books = await Task.Run(() => _service.Search(document, strategy, criteria));

        ShowResults(books);
        Status = books.Count == 0
            ? $"{strategy.Name}: нічого не знайдено."
            : $"{strategy.Name}: знайдено {UkrainianText.Count(books.Count, "книгу", "книги", "книг")}.";
    }

    /// <summary>Puts the found books on the screen.</summary>
    private void ShowResults(IReadOnlyList<Book> books)
    {
        _found = books;
        _hasSearched = true;

        // One new list instead of hundreds of single additions: the page lays the cards out only once.
        Results = books.Take(MaxShownResults).Select(BookItem.From).ToList();

        OnPropertiesChanged(nameof(HasResults), nameof(ResultsCountText));
        RefreshEmptyState();
        RefreshCommands();
    }

    /// <summary>The Clear button: every drop-down goes back to "any value", the keywords and the results are emptied.</summary>
    private Task ClearAsync()
    {
        foreach (AttributeFilterViewModel filter in Filters)
        {
            filter.Reset();
        }

        Keyword = string.Empty;
        ClearResults();
        Status = "Параметри пошуку та результати очищено.";
        return Task.CompletedTask;
    }

    /// <summary>Forgets the found books.</summary>
    private void ClearResults()
    {
        _found = [];
        _hasSearched = false;
        Results = [];

        OnPropertiesChanged(nameof(HasResults), nameof(ResultsCountText));
        RefreshEmptyState();
        RefreshCommands();
    }

    // ───────────── HTML ─────────────

    /// <summary>Transforms the whole document with the stylesheet and opens the preview.</summary>
    private async Task ShowHtmlAsync()
    {
        LibraryDocument? document = _document;
        XslTransformer? transformer = _transformer;
        if (document is null || transformer is null)
        {
            return;
        }

        Status = "Перетворюю документ за XSL-шаблоном…";
        string html = await Task.Run(() => _service.TransformToHtml(document, transformer));
        _previewFileName = FileNames.Build(Path.GetFileNameWithoutExtension(document.Name), ".html");

        if (html.Length > MaxPreviewLength)
        {
            await _files.OpenHtmlInBrowserAsync(html, _previewFileName);
            Status = "Сторінка завелика для вбудованого перегляду, тому її відкрито в браузері.";
            return;
        }

        PreviewTitle = $"{document.Name}  →  {transformer.Name}";
        PreviewHtml = html;
        IsPreviewVisible = true;
        Status = "HTML-сторінку сформовано.";
    }

    /// <summary>Opens the page from the preview in the default browser.</summary>
    private async Task OpenPreviewInBrowserAsync()
    {
        if (_previewHtml.Length == 0)
        {
            return;
        }

        await _files.OpenHtmlInBrowserAsync(_previewHtml, _previewFileName);
        Status = "Сторінку відкрито в браузері.";
    }

    /// <summary>Closes the preview and forgets the page (it may belong to another file).</summary>
    private void ClosePreview()
    {
        IsPreviewVisible = false;
        PreviewHtml = string.Empty;
        PreviewTitle = string.Empty;
    }

    // ───────────── Google Drive ─────────────

    /// <summary>Creates the HTML saver for the current stylesheet and saves.</summary>
    private Task SaveHtmlAsync()
    {
        XslTransformer? transformer = _transformer;
        return transformer is null ? Task.CompletedTask : SaveAsync(new HtmlFragmentSaver(transformer));
    }

    /// <summary>
    /// Asks for a file name and saves the found books with the given creator of the Factory Method.
    /// The view model does not know how the format is written — only which creator to use.
    /// </summary>
    private async Task SaveAsync(FragmentSaver saver)
    {
        IReadOnlyList<Book> books = _found;
        if (books.Count == 0)
        {
            return;
        }

        string? name = await _dialogs.PromptAsync(
            "Збереження на Google Drive",
            "Назва файлу (розширення буде додано автоматично):",
            "fragment_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture));

        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "Збереження скасовано.";
            return;
        }

        Status = "Зберігаю на Google Drive…";
        CloudFile file = await saver.SaveAsync(books, name, _cloud);
        Status = $"Збережено на Google Drive: {file.Name}";
    }

    /// <summary>Forgets the Google account.</summary>
    private async Task SignOutAsync()
    {
        await _cloud.SignOutAsync();
        Status = "Ви вийшли з Google акаунта. Наступна дія з Drive відкриє вхід у браузері.";
    }

    /// <summary>Tells the left panel and the sign-out button that the Drive state may have changed.</summary>
    private void NotifyDriveState()
    {
        OnPropertiesChanged(nameof(IsSignedIn), nameof(DriveStatusText));
        SignOutCommand.RaiseCanExecuteChanged();
    }

    // ───────────── Event log ─────────────

    /// <summary>Reads the newest lines of the log and opens the log window.</summary>
    private async Task ShowLogAsync()
    {
        IReadOnlyList<string> lines = await Task.Run(() => EventLogger.Instance.ReadLatest(LogLinesShown));
        LogLines = lines.Select(LogLineItem.Parse).ToList();
        OnPropertyChanged(nameof(LogPath));
        IsLogVisible = true;
    }

    // ───────────── Exit ─────────────

    /// <summary>Asks the required question and closes the program on "Так".</summary>
    private async Task ExitAsync()
    {
        bool confirmed;
        try
        {
            confirmed = await _dialogs.ConfirmAsync("Завершення роботи", ExitQuestion);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"The exit question failed: {ex}");
            return;
        }

        if (confirmed)
        {
            _lifetime.Quit();
        }
    }

    // ───────────── Plumbing ─────────────

    /// <summary>Creates a command that runs through <see cref="RunAsync"/> and is disabled while anything runs.</summary>
    private AsyncCommand Guarded(Func<Task> work, Func<bool>? condition = null)
    {
        var command = new AsyncCommand(() => RunAsync(work), () => !_isBusy && (condition?.Invoke() ?? true));
        _guardedCommands.Add(command);
        return command;
    }

    /// <summary>
    /// Runs one operation: disables the buttons, shows the spinner, and turns every failure into a message.
    /// Nothing an operation throws can close the program.
    /// </summary>
    private async Task RunAsync(Func<Task> work)
    {
        if (_isBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
            Status = "Дію скасовано.";
        }
        catch (Exception ex)
        {
            Status = "Не вдалося виконати дію.";
            await ShowErrorAsync(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyDriveState();
        }
    }

    /// <summary>Shows an error. The messages of our own exceptions are already written for the user.</summary>
    private async Task ShowErrorAsync(Exception error)
    {
        string message = error switch
        {
            InvalidDataException or CloudStorageException => error.Message,
            UnauthorizedAccessException => "Немає доступу до файлу: " + error.Message,
            IOException => "Помилка читання або запису файлу: " + error.Message,
            _ => "Неочікувана помилка: " + error.Message,
        };

        Debug.WriteLine(error);
        try
        {
            await _dialogs.ShowMessageAsync("Помилка", message);
        }
        catch (Exception dialogError)
        {
            Debug.WriteLine($"The error message could not be shown: {dialogError}");
        }
    }

    /// <summary>Asks every guarded button to check again whether it is enabled.</summary>
    private void RefreshCommands()
    {
        foreach (AsyncCommand command in _guardedCommands)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Updates the hint shown in place of the results.</summary>
    private void RefreshEmptyState()
        => OnPropertiesChanged(nameof(IsEmptyStateVisible), nameof(EmptyStateTitle), nameof(EmptyStateText));
}
