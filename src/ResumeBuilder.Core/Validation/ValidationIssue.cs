namespace ResumeBuilder.Core.Validation;

/// <summary>Серьёзность нарушения валидации.</summary>
public enum ValidationSeverity
{
    /// <summary>Ошибка: экспорт резюме с такой ошибкой не имеет смысла.</summary>
    Error,

    /// <summary>Предупреждение: резюме можно экспортировать, но стоит исправить.</summary>
    Warning
}

/// <summary>
/// Одно нарушение правила валидации.
/// </summary>
/// <param name="Field">Имя проверяемого поля (например, <c>Personal.Email</c>).</param>
/// <param name="Message">Понятное пользователю (уже локализованное) сообщение.</param>
/// <param name="Severity">Серьёзность нарушения.</param>
public sealed record ValidationIssue(string Field, string Message, ValidationSeverity Severity = ValidationSeverity.Error)
{
    /// <inheritdoc />
    public override string ToString() => $"[{Severity}] {Field}: {Message}";
}
