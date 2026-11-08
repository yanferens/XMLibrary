using XMLLibrary.Core.Model;

namespace XMLLibrary.ViewModels;

/// <summary>
/// One drop-down list of the search form: an attribute of the books and the values that occur in the file.
/// The first item always means "any value".
/// </summary>
public sealed class AttributeFilterViewModel : ViewModelBase
{
    /// <summary>The text of the first item of every list.</summary>
    public const string AnyValueCaption = "Будь-яке значення";

    private readonly IReadOnlyList<string> _values;
    private int _selectedIndex;

    /// <summary>Creates the list for one attribute.</summary>
    /// <param name="attribute">The attribute and its values from the catalog of the file.</param>
    public AttributeFilterViewModel(AttributeValues attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        Name = attribute.Name;
        Caption = LibraryXml.Caption(attribute.Name);
        _values = attribute.Values;
        Options = [AnyValueCaption, .. attribute.Values];
    }

    /// <summary>The attribute name as written in the file, e.g. "faculty".</summary>
    public string Name { get; }

    /// <summary>The caption above the list, e.g. "Факультет".</summary>
    public string Caption { get; }

    /// <summary>The items of the list: "any value" and then the values from the file.</summary>
    public List<string> Options { get; }

    /// <summary>
    /// The position of the chosen item. The index is used instead of the text, so a value that happens to be
    /// spelled like "Будь-яке значення" in the file can still be chosen. A Picker briefly reports -1 while it
    /// is being created; such values are turned into 0 ("any value") and the Picker is told the real value.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int index = value >= 0 && value < Options.Count ? value : 0;
            if (SetProperty(ref _selectedIndex, index))
            {
                OnPropertiesChanged(nameof(Value), nameof(IsActive));
            }
            else if (index != value)
            {
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The chosen value, or <c>null</c> for "any value".</summary>
    public string? Value => _selectedIndex > 0 ? _values[_selectedIndex - 1] : null;

    /// <summary>True when a concrete value is chosen (the tile is highlighted).</summary>
    public bool IsActive => _selectedIndex > 0;

    /// <summary>Chooses a value by its text; used by the tests. Unknown text means "any value".</summary>
    /// <param name="value">One of the values from the file.</param>
    public void Select(string? value)
    {
        int position = value is null ? -1 : IndexOfValue(value);
        SelectedIndex = position < 0 ? 0 : position + 1;
    }

    /// <summary>Goes back to "any value" (the Clear button).</summary>
    public void Reset() => SelectedIndex = 0;

    /// <summary>Finds a value among the values of the file (exact, case-sensitive match).</summary>
    private int IndexOfValue(string value)
    {
        for (int i = 0; i < _values.Count; i++)
        {
            if (_values[i] == value)
            {
                return i;
            }
        }

        return -1;
    }
}
