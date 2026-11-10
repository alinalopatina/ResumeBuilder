using System.Globalization;
using System.Text.Json.Serialization;
using ResumeBuilder.Core.Validation;

namespace ResumeBuilder.Core.Models;

/// <summary>
/// Абстрактный элемент раздела резюме (опыт, образование, навык, язык).
/// </summary>
/// <remarks>
/// Демонстрирует принципы ООП:
/// <list type="bullet">
/// <item><b>Абстракция</b> — общий контракт для всех элементов резюме;</item>
/// <item><b>Наследование</b> — <see cref="WorkExperience"/>, <see cref="Education"/>,
/// <see cref="Skill"/>, <see cref="LanguageSkill"/> наследуют этот класс;</item>
/// <item><b>Полиморфизм</b> — <see cref="GetSummary"/> и <see cref="Validate"/>
/// переопределяются в наследниках и вызываются через базовый тип.</item>
/// </list>
/// </remarks>
[JsonDerivedType(typeof(WorkExperience), "experience")]
[JsonDerivedType(typeof(Education), "education")]
[JsonDerivedType(typeof(Skill), "skill")]
[JsonDerivedType(typeof(LanguageSkill), "language")]
public abstract class ResumeItem
{
    /// <summary>Уникальный идентификатор элемента.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ключ ресурса с названием раздела, к которому относится элемент.</summary>
    [JsonIgnore]
    public abstract string SectionKey { get; }

    /// <summary>Краткое однострочное описание элемента для списков и отчётов.</summary>
    /// <param name="culture">Культура для форматирования дат и чисел.</param>
    public abstract string GetSummary(CultureInfo culture);

    /// <summary>
    /// Собственные (семантические) проверки элемента.
    /// Базовая реализация ничего не проверяет — наследники дополняют её.
    /// </summary>
    /// <param name="path">Путь к элементу для сообщения об ошибке, например <c>Experience[0]</c>.</param>
    /// <param name="today">Текущая дата (передаётся явно, чтобы код был тестируемым).</param>
    public virtual IEnumerable<ValidationIssue> Validate(string path, DateTime today) => [];

    /// <inheritdoc />
    public override string ToString() => GetSummary(CultureInfo.CurrentCulture);
}
