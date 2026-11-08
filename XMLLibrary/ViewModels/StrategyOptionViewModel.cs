using System.Windows.Input;
using XMLLibrary.Core.Search;

namespace XMLLibrary.ViewModels;

/// <summary>One segment of the "SAX | DOM | LINQ to XML" switch.</summary>
public sealed class StrategyOptionViewModel : ViewModelBase
{
    private bool _isSelected;

    /// <summary>Creates a segment.</summary>
    /// <param name="strategy">The strategy this segment chooses.</param>
    /// <param name="onSelected">Called when the user clicks the segment.</param>
    public StrategyOptionViewModel(IXmlSearchStrategy strategy, Action<StrategyOptionViewModel> onSelected)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(onSelected);

        Strategy = strategy;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    /// <summary>The strategy behind the segment.</summary>
    public IXmlSearchStrategy Strategy { get; }

    /// <summary>The big text of the segment: "SAX".</summary>
    public string Name => Strategy.Name;

    /// <summary>The small text under the name: how the strategy reads the file.</summary>
    public string Hint => Strategy.Description;

    /// <summary>True for the chosen segment (it is highlighted).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>The click on the segment.</summary>
    public ICommand SelectCommand { get; }
}
