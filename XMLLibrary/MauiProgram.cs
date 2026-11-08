using Microsoft.Maui.LifecycleEvents;
using XMLLibrary.Cloud;
using XMLLibrary.Core.Cloud;
using XMLLibrary.Core.Services;
using XMLLibrary.Services;
using XMLLibrary.ViewModels;

namespace XMLLibrary;

/// <summary>The start of the program: services, the window events and the look of the native controls.</summary>
public static class MauiProgram
{
    /// <summary>Builds the MAUI application.</summary>
    public static MauiApp CreateMauiApp()
    {
        CrashLog.Attach();

        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                // The × button and Alt+F4 must ask "Чи дійсно ви хочете завершити роботу з програмою?" first.
                events.AddWindows(windows => windows.OnWindowCreated(window =>
                    window.AppWindow.Closing += OnWindowClosing));
#endif
            });

#if WINDOWS
        FlattenNativeInputs();
#endif

        // Dependency injection: one object of each service for the whole program.
        builder.Services.AddSingleton<LibraryService>();
        builder.Services.AddSingleton<ICloudStorage, GoogleDriveStorage>();
        builder.Services.AddSingleton<IFileService, MauiFileService>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }

#if WINDOWS
    /// <summary>
    /// The window is about to close. Cancel must be set before the first await, so the window stays open,
    /// and the page asks the question and closes the program itself on "Так".
    /// </summary>
    private static void OnWindowClosing(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is MainPage page && !page.HandleCloseRequest())
        {
            args.Cancel = true;
        }
    }

    /// <summary>
    /// The search box and the drop-down lists sit inside our own rounded tiles, so the native frames of the
    /// Windows text box and combo box are removed; otherwise there would be a box inside a box.
    /// This is purely cosmetic, so every step is guarded: WinUI may throw a COMException while
    /// a control is being created, and that must never stop the program.
    /// </summary>
    private static void FlattenNativeInputs()
    {
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("XMLLibraryFlatEntry", (handler, view) =>
        {
            Microsoft.UI.Xaml.Controls.TextBox? textBox = handler.PlatformView;
            if (textBox is null)
            {
                return;
            }

            TryStyle(() => textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0));
            TryStyle(() => textBox.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent));
        });

        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("XMLLibraryFlatPicker", (handler, view) =>
        {
            Microsoft.UI.Xaml.Controls.ComboBox? comboBox = handler.PlatformView;
            if (comboBox is null)
            {
                return;
            }

            TryStyle(() => comboBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0));
        });
    }

    /// <summary>Runs one cosmetic tweak and ignores a platform failure, so a styling problem cannot crash the app.</summary>
    /// <param name="tweak">The change to apply to a native control.</param>
    private static void TryStyle(Action tweak)
    {
        try
        {
            tweak();
        }
        catch (Exception)
        {
            // Cosmetic only: the control simply keeps its default look.
        }
    }
#endif
}