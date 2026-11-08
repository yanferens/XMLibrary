namespace XMLLibrary.ViewModels;

/// <summary>
/// The dialogs the view model needs. MainPage implements them with MAUI dialogs and its own overlay;
/// the tests use a fake, so the view model is tested without any window.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a message with one "Гаразд" button.</summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The text.</param>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Asks a question with the buttons "Так" and "Ні".</summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">The question.</param>
    /// <returns>True for "Так".</returns>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Asks for a line of text.</summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">What to type.</param>
    /// <param name="initialValue">The text already in the box.</param>
    /// <returns>The typed text, or null if the user cancelled.</returns>
    Task<string?> PromptAsync(string title, string message, string initialValue);

    /// <summary>Lets the user choose one item of a list.</summary>
    /// <param name="title">The title above the list.</param>
    /// <param name="items">The items.</param>
    /// <returns>The index of the chosen item, or null if the user cancelled.</returns>
    Task<int?> ChooseAsync(string title, IReadOnlyList<ChoiceItem> items);
}

/// <summary>Work with local files. Implemented in the app with the MAUI file picker and the default browser.</summary>
public interface IFileService
{
    /// <summary>Lets the user pick a file with one of the extensions.</summary>
    /// <param name="title">The title of the picker.</param>
    /// <param name="extensions">Allowed extensions with the dot: ".xml".</param>
    /// <returns>The picked file, or null if the user cancelled.</returns>
    Task<PickedFile?> PickAsync(string title, IReadOnlyList<string> extensions);

    /// <summary>Saves the HTML into a temporary file and opens it in the default browser.</summary>
    /// <param name="html">The page.</param>
    /// <param name="fileName">The name for the temporary file, e.g. "library.html".</param>
    Task OpenHtmlInBrowserAsync(string html, string fileName);
}

/// <summary>Closing the program. The page implements it; the tests record the call.</summary>
public interface IAppLifetime
{
    /// <summary>Closes the program without asking anything (the question was already asked).</summary>
    void Quit();
}

/// <summary>A file the user picked on the disk.</summary>
/// <param name="Name">The file name with the extension.</param>
/// <param name="Content">The whole content.</param>
public sealed record PickedFile(string Name, byte[] Content);
