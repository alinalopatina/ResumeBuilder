using System.Collections.ObjectModel;

namespace ResumeBuilder.Wpf.ViewModels;

/// <summary>
/// Общая ViewModel для вкладки со списком (опыт, образование, навыки):
/// коллекция элементов, выбранный элемент, кнопки «Добавить» и «Удалить».
/// </summary>
public class ListViewModel<T> : ViewModelBase where T : new()
{
    private T? _selected;

    public ObservableCollection<T> Items { get; } = new();

    public T? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }

    public ListViewModel()
    {
        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected != null);
    }

    /// <summary>Заполнить список из модели.</summary>
    public void Load(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (T item in items)
            Items.Add(item);
    }

    private void Add()
    {
        var item = new T();
        Items.Add(item);
        Selected = item;
    }

    private void Remove()
    {
        if (Selected != null)
            Items.Remove(Selected);
    }
}
