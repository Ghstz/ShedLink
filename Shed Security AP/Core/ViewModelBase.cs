using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Shed_Security_AP.Core;

/// <summary>
/// Standard MVVM base class. Every ViewModel inherits from this to get
/// <see cref="INotifyPropertyChanged"/> for free. <c>SetProperty</c> handles
/// the equality check + notification in one call so property setters stay clean.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
