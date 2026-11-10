using System.Globalization;
using System.Resources;

namespace ResumeBuilder.Core.Localization;

/// <summary>
/// Источник локализованных строк (интернационализация: в коде — только ключи).
/// </summary>
public interface ILocalizer
{
    /// <summary>Текущая культура интерфейса.</summary>
    CultureInfo Culture { get; set; }

    /// <summary>Строка по ключу для текущей культуры.</summary>
    string this[string key] { get; }

    /// <summary>Строка по ключу с подстановкой аргументов (<c>string.Format</c> по правилам культуры).</summary>
    string Format(string key, params object?[] args);

    /// <summary>Строка по ключу для явно указанной культуры (например, язык резюме ≠ язык интерфейса).</summary>
    string Get(string key, CultureInfo culture);

    /// <summary>Событие смены языка (интерфейс подписывается и перерисовывает надписи).</summary>
    event EventHandler? CultureChanged;
}

/// <summary>
/// Реализация <see cref="ILocalizer"/> на стандартном механизме .NET: <see cref="ResourceManager"/> + файлы .resx.
/// </summary>
/// <remarks>
/// <c>Resources/Strings.resx</c> — нейтральная (русская) культура, <c>Resources/Strings.en.resx</c> — английская.
/// При сборке английские строки попадают в сателлитную сборку <c>en/ResumeBuilder.Core.resources.dll</c>.
/// Если ключа нет в нужной культуре, ResourceManager автоматически берёт строку из нейтральной (fallback).
/// </remarks>
public sealed class ResxLocalizer : ILocalizer
{
    /// <summary>Поддерживаемые языки интерфейса.</summary>
    public static readonly IReadOnlyList<CultureInfo> SupportedCultures =
        [CultureInfo.GetCultureInfo("ru-RU"), CultureInfo.GetCultureInfo("en-US")];

    private static readonly ResourceManager Resources =
        new("ResumeBuilder.Core.Resources.Strings", typeof(ResxLocalizer).Assembly);

    private CultureInfo _culture;

    /// <summary>Создаёт локализатор для указанной культуры (по умолчанию — культура ОС).</summary>
    public ResxLocalizer(CultureInfo? culture = null)
    {
        _culture = culture ?? CultureInfo.CurrentUICulture;
    }

    /// <inheritdoc />
    public event EventHandler? CultureChanged;

    /// <inheritdoc />
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (Equals(_culture, value)) return;
            _culture = value;
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public string this[string key] => Get(key, _culture);

    /// <inheritdoc />
    public string Get(string key, CultureInfo culture) =>
        Resources.GetString(key, culture) ?? $"[{key}]";

    /// <inheritdoc />
    public string Format(string key, params object?[] args) =>
        string.Format(_culture, this[key], args);
}
