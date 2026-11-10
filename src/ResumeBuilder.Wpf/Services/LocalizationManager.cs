using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using ResumeBuilder.Core.Localization;

namespace ResumeBuilder.Wpf.Services;

/// <summary>
/// Мост между <see cref="ILocalizer"/> и привязками WPF. Индексатор <c>this[key]</c> позволяет
/// писать в XAML <c>{loc:Tr Menu_File}</c>, а событие <c>Item[]</c> обновляет все надписи
/// при смене языка <b>без перезапуска</b> приложения.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    /// <summary>Единственный экземпляр (Singleton) — источник для всех привязок.</summary>
    public static LocalizationManager Instance { get; } = new();

    private ILocalizer _localizer = new ResxLocalizer(CultureInfo.GetCultureInfo("ru-RU"));

    private LocalizationManager() { }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Текущий локализатор.</summary>
    public ILocalizer Localizer => _localizer;

    /// <summary>Строка по ключу.</summary>
    public string this[string key] => _localizer[key];

    /// <summary>Текущая культура интерфейса.</summary>
    public CultureInfo Culture => _localizer.Culture;

    /// <summary>Подключает локализатор из DI-контейнера.</summary>
    public void Attach(ILocalizer localizer)
    {
        _localizer = localizer;
        _localizer.CultureChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Меняет язык интерфейса.</summary>
    public void SetCulture(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        _localizer.Culture = culture; // вызовет Refresh через событие
    }

    private void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}

/// <summary>
/// Расширение разметки <c>{loc:Tr Ключ}</c> — короткая запись привязки к локализованной строке.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    /// <summary>Ключ ресурса.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
