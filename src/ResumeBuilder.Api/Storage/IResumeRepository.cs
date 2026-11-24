using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Api.Storage;

/// <summary>Краткие сведения о сохранённом резюме (для списка).</summary>
public record ResumeSummary(int Id, string FullName, string Position, DateTime CreatedAt);

/// <summary>
/// Хранилище резюме. Две реализации (паттерн «Стратегия»): PostgreSQL в docker-compose
/// и в памяти — если база не настроена (например, при одиночном docker run).
/// </summary>
public interface IResumeRepository
{
    string Name { get; }
    Task InitAsync();
    Task<int> AddAsync(Resume resume);
    Task<List<ResumeSummary>> ListAsync();
    Task<Resume?> GetAsync(int id);
    Task<bool> DeleteAsync(int id);
}
