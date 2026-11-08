namespace XMLLibrary.Core.Logging;

/// <summary>The three levels of program events required by the task.</summary>
public enum LogEventLevel
{
    /// <summary>A search (filtering) was made.</summary>
    Filtering,

    /// <summary>A document was transformed with XSLT.</summary>
    Transformation,

    /// <summary>A file was saved.</summary>
    Saving,
}
