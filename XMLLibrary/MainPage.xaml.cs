using System.ComponentModel;
using System.Diagnostics;
using XMLLibrary.Core.Cloud;
using XMLLibrary.Core.Services;
using XMLLibrary.ViewModels;

namespace XMLLibrary;

/// <summary>
/// The main window. It shows <see cref="MainViewModel"/> and implements the things only a page can do:
/// dialogs, the Google Drive file list overlay, the WebView of the preview and closing the program.
/// There is no search, XML or Drive logic here.
/// </summary>
public partial class MainPage : ContentPage, IDialogService, IAppLifetime
{
    /// <summary>
    /// Windows can show only one system dialog at a time; a second one would throw.
    /// Every dialog waits here for the previous one to close.
    /// </summary>
    private readonly SemaphoreSlim _dialogGate = new(1, 1);

    private TaskCompletionSource<int?>? _choice;
    private IReadOnlyList<ChoiceItem> _choiceItems = [];

    /// <summary>Creates the page; the services come from dependency injection (see MauiProgram).</summary>
    /// <param name="service">Search and transformation.</param>
    /// <param name="cloud">Google Drive.</param>
    /// <param name="files">The file picker and the browser.</param>
    public MainPage(LibraryService service, ICloudStorage cloud, IFileService files)
    {
        InitializeComponent();

        ViewModel = new MainViewModel(service, cloud, this, files, this);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        BindingContext = ViewModel;
    }

    /// <summary>The view model of the window.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>True once the user answered "Так", so the window may really close.</summary>
    public bool IsExitConfirmed { get; private set; }

    /// <summary>
    /// Called by the window when the user presses × or Alt+F4. Starts the "Так / Ні" question and
    /// returns false, so the window stays open until the user answers.
    /// </summary>
    /// <returns>True when the window may close right now.</returns>
    public bool HandleCloseRequest()
    {
        if (IsExitConfirmed)
        {
            return true;
        }

        ViewModel.ExitCommand.Execute(null);
        return false;
    }

    /// <summary>Closes the program; the question has already been answered with "Так".</summary>
    void IAppLifetime.Quit()
    {
        IsExitConfirmed = true;
        Application.Current?.Quit();
    }

    // ───────────── IDialogService ─────────────

    /// <inheritdoc />
    async Task IDialogService.ShowMessageAsync(string title, string message)
    {
        await _dialogGate.WaitAsync();
        try
        {
            await DisplayAlert(title, message, "Гаразд");
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    /// <inheritdoc />
    async Task<bool> IDialogService.ConfirmAsync(string title, string message)
    {
        await _dialogGate.WaitAsync();
        try
        {
            return await DisplayAlert(title, message, "Так", "Ні");
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    /// <inheritdoc />
    async Task<string?> IDialogService.PromptAsync(string title, string message, string initialValue)
    {
        await _dialogGate.WaitAsync();
        try
        {
            return await DisplayPromptAsync(title, message, "Зберегти", "Скасувати",
                maxLength: 120, initialValue: initialValue);
        }
        finally
        {
            _dialogGate.Release();
        }
    }

    /// <summary>
    /// Shows the Drive file list over the page and waits until the user clicks a file or "Скасувати".
    /// The overlay is part of the page, not a system dialog, so it does not use the dialog gate.
    /// </summary>
    /// <param name="title">The title above the list.</param>
    /// <param name="items">The files.</param>
    /// <returns>The index of the chosen file, or null.</returns>
    Task<int?> IDialogService.ChooseAsync(string title, IReadOnlyList<ChoiceItem> items)
    {
        // Never two lists at once: an unfinished one counts as cancelled.
        CompleteChoice(null);

        _choice = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _choiceItems = items;

        DriveOverlayTitle.Text = title;
        DriveFilesView.SelectedItem = null;
        DriveFilesView.ItemsSource = items;
        DriveOverlay.IsVisible = true;

        return _choice.Task;
    }

    /// <summary>A file in the Drive list was clicked: the list closes and reports which one.</summary>
    private void OnDriveFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ChoiceItem item)
        {
            return;     // the selection was cleared by the code, not by the user
        }

        // Compare references, not values: two files may have the same name and date.
        int index = -1;
        for (int i = 0; i < _choiceItems.Count; i++)
        {
            if (ReferenceEquals(_choiceItems[i], item))
            {
                index = i;
                break;
            }
        }

        CompleteChoice(index >= 0 ? index : null);
    }

    /// <summary>The "Скасувати" button of the Drive list.</summary>
    private void OnDriveCancelClicked(object? sender, EventArgs e) => CompleteChoice(null);

    /// <summary>Hides the Drive list and gives the answer to the waiting view model.</summary>
    private void CompleteChoice(int? index)
    {
        DriveOverlay.IsVisible = false;

        TaskCompletionSource<int?>? choice = _choice;
        _choice = null;
        choice?.TrySetResult(index);
    }

    // ───────────── Preview ─────────────

    /// <summary>Puts a new page into the WebView when the view model prepares one.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.PreviewHtml))
        {
            return;
        }

        try
        {
            PreviewWebView.Source = new HtmlWebViewSource { Html = ViewModel.PreviewHtml };
        }
        catch (Exception ex)
        {
            // Without the WebView2 runtime the preview stays empty; "Відкрити в браузері" still works.
            Debug.WriteLine($"The preview could not be shown: {ex}");
        }
    }
}
