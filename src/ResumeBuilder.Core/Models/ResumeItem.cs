namespace ResumeBuilder.Core.Models;

/// <summary>
/// Базовый класс для элементов резюме (опыт, образование, навык).
/// Абстрактный метод GetDescription() каждый наследник реализует по-своему (полиморфизм).
/// </summary>
[Serializable]
public abstract class ResumeItem
{
    /// <summary>Краткое описание элемента одной строкой.</summary>
    public abstract string GetDescription();

    /// <summary>Проверка элемента. Ошибки добавляются в список.</summary>
    public virtual void Validate(List<string> errors) { }

    public override string ToString() => GetDescription();
}
