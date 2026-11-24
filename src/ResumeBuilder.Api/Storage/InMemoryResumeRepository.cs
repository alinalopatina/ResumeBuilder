using System.Collections.Concurrent;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Api.Storage;

/// <summary>Хранилище в памяти: данные пропадают при перезапуске контейнера.</summary>
public class InMemoryResumeRepository : IResumeRepository
{
    private readonly ConcurrentDictionary<int, (Resume Resume, DateTime Created)> _items = new();
    private int _lastId;

    public string Name => "memory";

    public Task InitAsync() => Task.CompletedTask;

    public Task<int> AddAsync(Resume resume)
    {
        int id = Interlocked.Increment(ref _lastId);
        _items[id] = (resume, DateTime.UtcNow);
        return Task.FromResult(id);
    }

    public Task<List<ResumeSummary>> ListAsync() =>
        Task.FromResult(_items.OrderBy(x => x.Key)
            .Select(x => new ResumeSummary(x.Key, x.Value.Resume.Personal.FullName, x.Value.Resume.Personal.Position, x.Value.Created))
            .ToList());

    public Task<Resume?> GetAsync(int id) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? item.Resume : null);

    public Task<bool> DeleteAsync(int id) => Task.FromResult(_items.TryRemove(id, out _));
}
