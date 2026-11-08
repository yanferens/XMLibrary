using System.Diagnostics;
using System.Windows.Input;

namespace XMLLibrary.ViewModels;

/// <summary>
/// An <see cref="ICommand"/> for a button that runs an async method. While the method runs the command
/// is disabled, so a second click cannot start the same operation twice.
/// </summary>
/// <param name="execute">The work to do.</param>
/// <param name="canExecute">An extra condition for the button to be enabled; null means "always".</param>
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private readonly Func<Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private bool _isRunning;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isRunning && (canExecute?.Invoke() ?? true);

    /// <summary>
    /// Called by the button. ICommand.Execute cannot return a Task, so this is the one place with "async void";
    /// every exception is caught here, because an exception leaving an async void method closes the program.
    /// </summary>
    /// <param name="parameter">Not used.</param>
    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"A command failed: {ex}");
        }
    }

    /// <summary>The same as a click, but awaitable (used by the tests and by other commands).</summary>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Asks the bound button to check <see cref="CanExecute"/> again.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>An <see cref="ICommand"/> for a quick synchronous action, such as choosing an option.</summary>
/// <param name="execute">The action.</param>
public sealed class RelayCommand(Action execute) : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute();
}
