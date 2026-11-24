using System.Text.Json;
using Npgsql;
using ResumeBuilder.Core.Models;

namespace ResumeBuilder.Api.Storage;

/// <summary>
/// Хранилище в PostgreSQL. Резюме хранится целиком в столбце jsonb, имя и должность — отдельно для списка.
/// Данные лежат в томе Docker (volume) и не пропадают при перезапуске контейнеров.
/// </summary>
public class PostgresResumeRepository : IResumeRepository
{
    private readonly NpgsqlDataSource _db;
    private readonly ILogger<PostgresResumeRepository> _logger;

    public PostgresResumeRepository(string connectionString, ILogger<PostgresResumeRepository> logger)
    {
        _db = NpgsqlDataSource.Create(connectionString);
        _logger = logger;
    }

    public string Name => "postgres";

    /// <summary>Создаёт таблицу при первом запуске. Повторяет попытки, пока база не поднимется.</summary>
    public async Task InitAsync()
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS resumes (
                id         SERIAL PRIMARY KEY,
                full_name  TEXT NOT NULL,
                position   TEXT NOT NULL,
                data       JSONB NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            )
            """;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using NpgsqlCommand cmd = _db.CreateCommand(sql);
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Таблица resumes готова");
                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                _logger.LogWarning("База недоступна (попытка {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    public async Task<int> AddAsync(Resume resume)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "INSERT INTO resumes (full_name, position, data) VALUES ($1, $2, $3::jsonb) RETURNING id");
        cmd.Parameters.AddWithValue(resume.Personal.FullName ?? "");
        cmd.Parameters.AddWithValue(resume.Personal.Position ?? "");
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(resume));
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<ResumeSummary>> ListAsync()
    {
        var list = new List<ResumeSummary>();
        await using NpgsqlCommand cmd = _db.CreateCommand(
            "SELECT id, full_name, position, created_at FROM resumes ORDER BY id");
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new ResumeSummary(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
        return list;
    }

    public async Task<Resume?> GetAsync(int id)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand("SELECT data::text FROM resumes WHERE id = $1");
        cmd.Parameters.AddWithValue(id);
        object? json = await cmd.ExecuteScalarAsync();
        return json is string text ? JsonSerializer.Deserialize<Resume>(text) : null;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using NpgsqlCommand cmd = _db.CreateCommand("DELETE FROM resumes WHERE id = $1");
        cmd.Parameters.AddWithValue(id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
}
