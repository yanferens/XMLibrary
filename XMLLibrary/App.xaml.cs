namespace XMLLibrary;

/// <summary>The application: the dark theme and the one window with <see cref="MainPage"/>.</summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;

    /// <summary>Creates the application and loads the colours and styles of App.xaml.</summary>
    /// <param name="services">The dependency injection container (registered in MauiProgram).</param>
    public App(IServiceProvider services)
    {
        InitializeComponent();

        // XMLLibrary has one dark design; the system dialogs and the title bar follow it too.
        UserAppTheme = AppTheme.Dark;
        _services = services;
    }

    /// <summary>
    /// Creates the main window. The page is taken from the container only here, after InitializeComponent:
    /// it uses the styles of App.xaml, which must already be loaded when the page is built.
    /// </summary>
    /// <param name="activationState">Provided by MAUI; not used.</param>
    protected override Window CreateWindow(IActivationState? activationState)
        => new(_services.GetRequiredService<MainPage>())
        {
            Title = "XMLLibrary — кафедральна бібліотека",
            Width = 1320,
            Height = 860,
            MinimumWidth = 1040,
            MinimumHeight = 640,
        };
}
