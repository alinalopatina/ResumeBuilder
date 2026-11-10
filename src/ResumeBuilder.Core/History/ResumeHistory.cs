using System.Text.Json;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Core.History;

/// <summary>
/// «Снимок» состояния резюме — Хранитель (Memento). Внутри — сериализованный JSON,
/// поэтому снимок неизменяем и не зависит от дальнейших правок исходного объекта.
/// </summary>
public sealed class ResumeMemento
{
    internal ResumeMemento(string json, string description)
    {
        Json = json;
        Description = description;
        CreatedAt = DateTime.Now;
    }

    internal string Json { get; }

    /// <summary>Описание действия, перед которым сделан снимок (для меню «Отменить ...»).</summary>
    public string Description { get; }

    /// <summary>Время снимка.</summary>
    public DateTime CreatedAt { get; }
}

/// <summary>
/// Паттерн <b>«Хранитель» (Memento)</b> — история изменений для кнопки «Отменить».
/// Связь с сериализацией: состояние сохраняется сериализацией и восстанавливается десериализацией
/// (контрольный вопрос №12 лабы «Сериализация»).
/// </summary>
public sealed class ResumeHistory(int capacity = 30)
{
    private readonly LinkedList<ResumeMemento> _stack = new();

    /// <summary>Можно ли отменить.</summary>
    public bool CanUndo => _stack.Count > 0;

    /// <summary>Описание последнего действия.</summary>
    public string? LastDescription => _stack.Last?.Value.Description;

    /// <summary>Количество снимков.</summary>
    public int Count => _stack.Count;

    /// <summary>Сохраняет снимок перед изменением.</summary>
    public void Push(Resume resume, string description)
    {
        _stack.AddLast(CreateMemento(resume, description));
        if (_stack.Count > capacity) _stack.RemoveFirst();
    }

    /// <summary>Возвращает предыдущее состояние или <c>null</c>, если история пуста.</summary>
    public Resume? Undo()
    {
        if (_stack.Last is null) return null;
        var memento = _stack.Last.Value;
        _stack.RemoveLast();
        return Restore(memento);
    }

    /// <summary>Очищает историю (например, при открытии другого файла).</summary>
    public void Clear() => _stack.Clear();

    /// <summary>Создаёт снимок.</summary>
    public static ResumeMemento CreateMemento(Resume resume, string description) =>
        new(JsonSerializer.Serialize(resume), description);

    /// <summary>Восстанавливает резюме из снимка (глубокая копия).</summary>
    public static Resume Restore(ResumeMemento memento) =>
        JsonSerializer.Deserialize<Resume>(memento.Json) ?? new Resume();

    /// <summary>Глубокое клонирование через сериализацию.</summary>
    public static Resume DeepClone(Resume resume)
    {
        var copy = Restore(CreateMemento(resume, "clone"));
        copy.FilePath = resume.FilePath;
        return copy;
    }
}
