using System.Globalization;

namespace XMLLibrary.Core.Model;

/// <summary>
/// Orders attribute values the way a person expects in a drop-down list:
/// numbers by their value (2, 10, 156 instead of 10, 156, 2), numbers before words,
/// and words in Ukrainian alphabetical order (є, і, ї, ґ in their places, not after "я").
/// </summary>
public sealed class NaturalValueComparer : IComparer<string>
{
    private static readonly StringComparer TextComparer = CreateTextComparer();

    private NaturalValueComparer()
    {
    }

    /// <summary>The single shared instance; the comparer has no state.</summary>
    public static NaturalValueComparer Instance { get; } = new();

    /// <summary>Compares two attribute values.</summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>A negative number if <paramref name="x"/> goes first, zero if they are equal, a positive number otherwise.</returns>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        bool xIsNumber = TryParseNumber(x, out decimal xNumber);
        bool yIsNumber = TryParseNumber(y, out decimal yNumber);

        int result = (xIsNumber, yIsNumber) switch
        {
            (true, true) => xNumber.CompareTo(yNumber),
            (true, false) => -1,
            (false, true) => 1,
            _ => TextComparer.Compare(x, y),
        };

        // Different strings must never be "equal", otherwise the order of equal-looking values
        // ("2021" and "2021.0") would depend on the sorting algorithm.
        return result != 0 ? result : string.CompareOrdinal(x, y);
    }

    /// <summary>Parses "2021", "-5" or "3.5" with the invariant culture; thousands separators are not accepted.</summary>
    private static bool TryParseNumber(string text, out decimal number)
        => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                                  | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture, out number);

    /// <summary>
    /// Creates a comparer with Ukrainian alphabetical rules. Falls back to the invariant culture
    /// when the system has no culture data (for example in "invariant globalization" mode).
    /// </summary>
    private static StringComparer CreateTextComparer()
    {
        try
        {
            return StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: false);
        }
        catch (CultureNotFoundException)
        {
            return StringComparer.InvariantCulture;
        }
    }
}
