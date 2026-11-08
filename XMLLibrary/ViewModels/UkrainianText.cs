using System.Globalization;

namespace XMLLibrary.ViewModels;

/// <summary>Small helpers for correct Ukrainian text in the interface.</summary>
public static class UkrainianText
{
    /// <summary>
    /// Chooses the right form of a noun after a number: 1 книга, 3 книги, 5 книг, 11 книг, 21 книга.
    /// </summary>
    /// <param name="count">The number.</param>
    /// <param name="one">The form for 1, 21, 31…: "книга".</param>
    /// <param name="few">The form for 2–4, 22–24…: "книги".</param>
    /// <param name="many">The form for 0, 5–20, 25–30…: "книг".</param>
    /// <returns>For example "3 книги".</returns>
    public static string Count(int count, string one, string few, string many)
    {
        int lastTwo = Math.Abs(count) % 100;
        int last = lastTwo % 10;

        string form = lastTwo is >= 11 and <= 14 ? many
            : last == 1 ? one
            : last is >= 2 and <= 4 ? few
            : many;

        return $"{count.ToString(CultureInfo.InvariantCulture)} {form}";
    }

    /// <summary>"1 книга", "3 книги", "5 книг".</summary>
    /// <param name="count">The number of books.</param>
    public static string Books(int count) => Count(count, "книга", "книги", "книг");

    /// <summary>"1 атрибут", "3 атрибути", "5 атрибутів".</summary>
    /// <param name="count">The number of attributes.</param>
    public static string Attributes(int count) => Count(count, "атрибут", "атрибути", "атрибутів");

    /// <summary>A file size for people: "512 Б", "12.4 КБ", "3.1 МБ".</summary>
    /// <param name="bytes">The size in bytes.</param>
    public static string FileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Б",
        < 1024 * 1024 => (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " КБ",
        _ => (bytes / 1024.0 / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " МБ",
    };
}
