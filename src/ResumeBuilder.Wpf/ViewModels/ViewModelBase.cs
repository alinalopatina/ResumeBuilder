using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ResumeBuilder.Wpf.ViewModels;

/// <summary>Базовый класс для ViewModel: сообщает окну, что свойство изменилось.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
