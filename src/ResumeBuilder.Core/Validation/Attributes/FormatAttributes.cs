using System.ComponentModel.DataAnnotations;
using ResumeBuilder.Core.Text;

namespace ResumeBuilder.Core.Validation.Attributes;

/// <summary>
/// Собственные атрибуты валидации (лаба «Валидация», задание 2.3 — создание своей аннотации).
/// Все они пропускают пустое значение: обязательность проверяет <see cref="RequiredAttribute"/>.
/// </summary>
public abstract class RegexBasedAttribute : ValidationAttribute
{
    /// <summary>Проверка непустой строки — реализуется наследником (шаблонный метод).</summary>
    protected abstract bool IsValidText(string value);

    /// <inheritdoc />
    public sealed override bool IsValid(object? value) =>
        value is not string s || string.IsNullOrWhiteSpace(s) || IsValidText(s.Trim());
}

/// <summary>Формат e-mail.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EmailFormatAttribute : RegexBasedAttribute
{
    protected override bool IsValidText(string value) => RegexPatterns.EmailFormat().IsMatch(value);
}

/// <summary>Формат телефона: допустимые символы (RegEx) + количество цифр 7–15 (код).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PhoneFormatAttribute : RegexBasedAttribute
{
    protected override bool IsValidText(string value)
    {
        if (!RegexPatterns.PhoneFormat().IsMatch(value)) return false;
        var digits = value.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }
}

/// <summary>Формат URL.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UrlFormatAttribute : RegexBasedAttribute
{
    protected override bool IsValidText(string value) => RegexPatterns.UrlFormat().IsMatch(value);
}

/// <summary>ФИО: только буквы, пробелы, дефис, апостроф.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonNameAttribute : RegexBasedAttribute
{
    protected override bool IsValidText(string value) =>
        RegexPatterns.PersonName().IsMatch(TextFormatter.CollapseSpaces(value));
}

/// <summary>Дата не может быть в будущем.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotInFutureAttribute : ValidationAttribute
{
    /// <inheritdoc />
    public override bool IsValid(object? value) => value is not DateTime d || d.Date <= DateTime.Today;
}

/// <summary>Минимальный возраст по дате рождения.</summary>
/// <param name="years">Минимальное количество полных лет.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MinAgeAttribute(int years) : ValidationAttribute
{
    /// <summary>Минимальный возраст.</summary>
    public int Years { get; } = years;

    /// <inheritdoc />
    public override bool IsValid(object? value) =>
        value is not DateTime birth || AgeCalculator.FullYears(birth, DateTime.Today) >= Years;
}

/// <summary>Вспомогательный расчёт возраста.</summary>
public static class AgeCalculator
{
    /// <summary>Количество полных лет на дату <paramref name="today"/>.</summary>
    public static int FullYears(DateTime birth, DateTime today)
    {
        var age = today.Year - birth.Year;
        if (birth.Date > today.AddYears(-age)) age--;
        return age;
    }
}
