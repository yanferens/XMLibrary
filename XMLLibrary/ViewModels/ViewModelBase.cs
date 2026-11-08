using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XMLLibrary.ViewModels;

/// <summary>The base of every view model: raises <see cref="PropertyChanged"/> so bindings in XAML update themselves.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stores a new value and notifies the bindings, but only if the value really changed.</summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="field">The field behind the property.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">Filled in by the compiler with the name of the calling property.</param>
    /// <returns>True if the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Tells the bindings that a property has a new value.</summary>
    /// <param name="propertyName">The property name; filled in by the compiler when called from a property.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Notifies the bindings about several properties at once (for values computed from others).</summary>
    /// <param name="propertyNames">The property names.</param>
    protected void OnPropertiesChanged(params string[] propertyNames)
    {
        foreach (string name in propertyNames)
        {
            OnPropertyChanged(name);
        }
    }
}
