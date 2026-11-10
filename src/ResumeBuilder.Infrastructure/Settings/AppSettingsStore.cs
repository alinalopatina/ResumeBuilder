using System.Text.Json;
using Microsoft.Extensions.Logging;
using ResumeBuilder.Infrastructure.Serialization;

namespace ResumeBuilder.Infrastructure.Settings;

/// <summary>
/// Пользовательские настройки, которые сохраняются при закрытии и восстанавливаются при запуске
/// (лаба «Сериализация», сценарий 1 «Сохранение состояния»).
/// </summary>
public sealed class UserSettings
{
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 780;
    /// <summary><c>null</c> — окно ещё ни разу не закрывалось, центрировать по экрану.
    /// (Не NaN: System.Text.Json по умолчанию не умеет записывать NaN.)</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool IsMaximized { get; set; }
    public string UiCulture { get; set; } = "ru-RU";
    public string? LastOpenedFile { get; set; }
    public List<string> RecentFiles { get; set; } = [];
    public string? LastExportFolder { get; set; }
    public bool MaskContactsByDefault { get; set; }

    /// <summary>Добавляет файл в список недавних (без повторов, максимум 10).</summary>
    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 10) RecentFiles.RemoveRange(10, RecentFiles.Count - 10);
        LastOpenedFile = path;
    }
}

/// <summary>Чтение/запись <see cref="UserSettings"/> и путей к служебным файлам в %AppData%\ResumeBuilder.</summary>
public sealed class AppSettingsStore(ILogger<AppSettingsStore> logger, string? baseFolder = null)
{
    /// <summary>Папка данных приложения.</summary>
    public string DataFolder { get; } = baseFolder ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResumeBuilder");

    /// <summary>Файл настроек.</summary>
    public string SettingsPath => Path.Combine(DataFolder, "settings.json");

    /// <summary>Файл автосохранения (двоичный кэш текущего черновика).</summary>
    public string AutosavePath => Path.Combine(DataFolder, "autosave.rbin");

    /// <summary>Загружает настройки; при ошибке — настройки по умолчанию (программа не должна падать из-за настроек).</summary>
    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new UserSettings();
            using var fs = File.OpenRead(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(fs, JsonResumeSerializer.Options) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось прочитать настройки, используются значения по умолчанию");
            return new UserSettings();
        }
    }

    /// <summary>Сохраняет настройки.</summary>
    public void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            using var fs = File.Create(SettingsPath);
            JsonSerializer.Serialize(fs, settings, JsonResumeSerializer.Options);
            logger.LogDebug("Настройки сохранены в {Path}", SettingsPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось сохранить настройки");
        }
    }
}
