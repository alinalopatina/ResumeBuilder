using System.Text.Json;

namespace ResumeBuilder.Core.Services;

/// <summary>
/// Состояние программы, которое запоминается между запусками: размер окна и последний открытый файл.
/// Хранится в %AppData%\ResumeBuilder\settings.json.
/// Настройки поведения программы (язык, цвет, папка экспорта…) — отдельно, в <see cref="Configuration.AppConfig"/>.
/// </summary>
public class AppSettings
{
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public string? LastFile { get; set; }

    private static string FolderPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResumeBuilder");

    private static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Не удалось прочитать настройки");
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
