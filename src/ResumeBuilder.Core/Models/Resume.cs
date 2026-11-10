using System.Text.Json.Serialization;

namespace ResumeBuilder.Core.Models;

/// <summary>
/// Резюме — корневой объект (агрегат) предметной области.
/// </summary>
public class Resume
{
    /// <summary>Текущая версия схемы данных. Увеличивается при изменении структуры класса.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Версия схемы, в которой резюме было сохранено (решение проблемы версионности сериализации).</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Идентификатор резюме.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Личные данные.</summary>
    public PersonalInfo Personal { get; set; } = new();

    /// <summary>Раздел «О себе».</summary>
    public string? Summary { get; set; }

    /// <summary>Опыт работы.</summary>
    public List<WorkExperience> Experience { get; set; } = [];

    /// <summary>Образование.</summary>
    public List<Education> Education { get; set; } = [];

    /// <summary>Навыки.</summary>
    public List<Skill> Skills { get; set; } = [];

    /// <summary>Иностранные языки.</summary>
    public List<LanguageSkill> Languages { get; set; } = [];

    /// <summary>Шаблон оформления PDF.</summary>
    public ResumeTemplate Template { get; set; } = ResumeTemplate.Modern;

    /// <summary>Что кодировать в QR-код.</summary>
    public QrContentMode QrMode { get; set; } = QrContentMode.VCard;

    /// <summary>Язык, на котором заполнено резюме (влияет на заголовки разделов в PDF/Word).</summary>
    public string CultureName { get; set; } = "ru-RU";

    /// <summary>Дата создания.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Дата последнего изменения.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Путь к файлу, из которого резюме загружено. Нужен только во время работы программы,
    /// поэтому <b>исключён из сериализации</b> (аналог <c>[NonSerialized]</c>).
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>
    /// Все элементы резюме через базовый тип — пример полиморфизма:
    /// у каждого элемента вызывается своя реализация <see cref="ResumeItem.GetSummary"/>.
    /// </summary>
    public IEnumerable<ResumeItem> AllItems() =>
        Experience.Cast<ResumeItem>().Concat(Education).Concat(Skills).Concat(Languages);
}
