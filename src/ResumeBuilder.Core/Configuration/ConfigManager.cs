using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace ResumeBuilder.Core.Configuration;

/// <summary>
/// Загрузка настроек программы из нескольких источников (лаба 9).
/// Чем ниже источник в списке, тем выше его приоритет — он перекрывает предыдущие:
/// <list type="number">
/// <item>значения по умолчанию в коде (<see cref="AppConfig"/>);</item>
/// <item>appsettings.json рядом с exe — общие настройки, поставляются с программой;</item>
/// <item>appsettings.{Environment}.json — профиль Development или Production;</item>
/// <item>%AppData%\ResumeBuilder\usersettings.json — личные настройки пользователя;</item>
/// <item>переменные окружения с префиксом RESUMEBUILDER_;</item>
/// <item>аргументы командной строки.</item>
/// </list>
/// </summary>
public static class ConfigManager
{
    /// <summary>Префикс переменных окружения: RESUMEBUILDER_App__QrPixelsPerModule=10.</summary>
    public const string EnvironmentPrefix = "RESUMEBUILDER_";

    /// <summary>Короткие аргументы командной строки: --env Development, --lang en-US.</summary>
    public static readonly Dictionary<string, string> SwitchMappings = new()
    {
        ["--env"] = "App:Environment",
        ["--lang"] = "App:DefaultLanguage",
        ["--export-folder"] = "App:ExportFolder"
    };

    private static ILogger Logger => Log.ForContext(typeof(ConfigManager));

    /// <summary>Текущие настройки (одни на всю программу).</summary>
    public static AppConfig Current { get; private set; } = new();

    /// <summary>Объединённая конфигурация из всех источников (из неё же читает Serilog).</summary>
    public static IConfigurationRoot Root { get; private set; } = new ConfigurationBuilder().Build();

    /// <summary>Аргументы, с которыми запущена программа.</summary>
    public static string[] Arguments { get; private set; } = Array.Empty<string>();

    /// <summary>Срабатывает, когда файл настроек изменили во время работы программы («горячая» перезагрузка).</summary>
    public static event Action<AppConfig>? Changed;

    /// <summary>Файл личных настроек пользователя.</summary>
    public static string UserFilePath => Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "ResumeBuilder", "usersettings.json");

    /// <summary>Загружает настройки при запуске программы и включает слежение за файлами.</summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Итоговые настройки. Их нужно проверить методом <see cref="AppConfig.Validate"/>.</returns>
    public static AppConfig Load(string[] args)
    {
        Arguments = args;
        Directory.CreateDirectory(Path.GetDirectoryName(UserFilePath)!);
        Root = Build(AppContext.BaseDirectory, UserFilePath, args);
        Current = Bind(Root);

        // Когда любой json-файл меняется на диске, Root перечитывается сам (reloadOnChange),
        // а мы получаем сигнал и обновляем Current
        ChangeToken.OnChange(Root.GetReloadToken, OnReloaded);
        return Current;
    }

    /// <summary>Собирает конфигурацию из всех источников. Отдельный метод — чтобы проверить порядок в тестах.</summary>
    /// <param name="baseDirectory">Папка с appsettings.json.</param>
    /// <param name="userFile">Полный путь к файлу личных настроек.</param>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="environmentPrefix">Префикс переменных окружения.</param>
    public static IConfigurationRoot Build(string baseDirectory, string userFile, string[] args,
        string environmentPrefix = EnvironmentPrefix)
    {
        // Шаг 1. Узнаём профиль. Он может прийти из любого источника, кроме самого файла профиля
        IConfigurationRoot boot = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(baseDirectory, "appsettings.json"), optional: true)
            .AddJsonFile(userFile, optional: true)
            .AddEnvironmentVariables(environmentPrefix)
            .AddCommandLine(args, SwitchMappings)
            .Build();
        string environment = boot["App:Environment"] ?? new AppConfig().Environment;

        // Шаг 2. Все источники по возрастанию приоритета
        return new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddJsonFile(userFile, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(environmentPrefix)
            .AddCommandLine(args, SwitchMappings)
            .Build();
    }

    /// <summary>Переносит секцию "App" в объект. Ключей нет — остаются значения по умолчанию из кода.</summary>
    public static AppConfig Bind(IConfiguration configuration)
    {
        var config = new AppConfig();
        configuration.GetSection("App").Bind(config);
        return config;
    }

    /// <summary>
    /// Сохраняет настройки в файл в виде { "App": { ... } }.
    /// appsettings.json при этом не меняется — настройки из дистрибутива не теряются.
    /// </summary>
    public static void SaveToFile(AppConfig config, string path)
    {
        ArgumentNullException.ThrowIfNull(config);
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var json = JsonSerializer.Serialize(new { App = config }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        Logger.Information("Настройки сохранены в {Path}", path);
    }

    /// <summary>
    /// Сохраняет ЛИЧНЫЕ настройки пользователя. В файл попадают только значения, которые отличаются
    /// от настроек дистрибутива (appsettings.json + профиль). Поэтому, если в новой версии программы
    /// поменяют значение по умолчанию, пользователь его получит, а свои изменения не потеряет.
    /// </summary>
    /// <param name="config">Настройки, которые выбрал пользователь.</param>
    /// <param name="baseDirectory">Папка с appsettings.json (по умолчанию — папка программы).</param>
    /// <param name="path">Куда сохранить (по умолчанию — <see cref="UserFilePath"/>).</param>
    /// <returns>Сколько настроек отличается от стандартных и записано в файл.</returns>
    public static int SaveUserSettings(AppConfig config, string? baseDirectory = null, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        baseDirectory ??= AppContext.BaseDirectory;
        path ??= UserFilePath;

        AppConfig standard = Bind(new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{config.Environment}.json", optional: true)
            .Build());

        var changed = new Dictionary<string, object?>();
        foreach (PropertyInfo property in typeof(AppConfig).GetProperties())
        {
            object? value = property.GetValue(config);
            if (!Equals(value, property.GetValue(standard)))
                changed[property.Name] = value;
        }

        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(path, JsonSerializer.Serialize(new { App = changed }, new JsonSerializerOptions { WriteIndented = true }));
        Logger.Information("Личные настройки сохранены: {Keys}", string.Join(", ", changed.Keys));
        return changed.Count;
    }

    /// <summary>Читает настройки из файла (импорт). Чего нет в файле — берётся из <paramref name="baseConfig"/>.</summary>
    /// <exception cref="FileNotFoundException">Файла нет.</exception>
    /// <exception cref="InvalidDataException">Файл не является JSON.</exception>
    public static AppConfig LoadFromFile(string path, AppConfig baseConfig)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл настроек не найден", path);
        IConfigurationRoot file;
        try
        {
            file = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(path), optional: false).Build();
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            throw new InvalidDataException("Файл настроек повреждён: " + ex.Message, ex);
        }

        // Сначала копия базовых настроек, сверху — значения из файла
        var config = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(baseConfig))!;
        file.GetSection("App").Bind(config);
        return config;
    }

    /// <summary>Удаляет личные настройки пользователя (возврат к настройкам из дистрибутива).</summary>
    public static void ResetUserSettings()
    {
        if (File.Exists(UserFilePath))
            File.Delete(UserFilePath);
        Root.Reload();
    }

    /// <summary>Перечитать все источники вручную (например, сразу после сохранения).</summary>
    public static void Reload() => Root.Reload();

    /// <summary>Список источников по возрастанию приоритета — для окна настроек и журнала.</summary>
    public static string DescribeSources()
    {
        var lines = new List<string> { "1. Значения по умолчанию в коде (AppConfig)" };
        int n = 2;
        foreach (IConfigurationProvider provider in Root.Providers)
        {
            string text = provider switch
            {
                Microsoft.Extensions.Configuration.Json.JsonConfigurationProvider json =>
                    FullPath(json) + (File.Exists(FullPath(json)) ? "" : "  (файла нет)"),
                Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationProvider =>
                    "Переменные окружения " + EnvironmentPrefix + "*",
                Microsoft.Extensions.Configuration.CommandLine.CommandLineConfigurationProvider =>
                    "Аргументы командной строки: " + (Arguments.Length == 0 ? "(нет)" : string.Join(" ", Arguments)),
                _ => provider.ToString() ?? ""
            };
            lines.Add($"{n++}. {text}");
        }
        lines.Add("");
        lines.Add($"Профиль: {Current.Environment}");
        return string.Join(System.Environment.NewLine, lines);
    }

    private static string FullPath(Microsoft.Extensions.Configuration.Json.JsonConfigurationProvider json) =>
        json.Source.FileProvider?.GetFileInfo(json.Source.Path ?? "").PhysicalPath ?? json.Source.Path ?? "";

    private static void OnReloaded()
    {
        AppConfig config = Bind(Root);
        List<string> errors = config.Validate();
        if (errors.Count > 0)
        {
            // Ошибка в изменённом файле — оставляем прежние настройки, чтобы программа не сломалась
            Logger.Warning("Изменённые настройки не применены: {Errors}", string.Join("; ", errors));
            return;
        }
        Current = config;
        Logger.Information("Настройки перечитаны без перезапуска: цвет {Accent}, QR {Qr}, автосохранение {Autosave} мин",
            config.AccentColor, config.QrPixelsPerModule, config.AutosaveMinutes);
        Changed?.Invoke(config);
    }
}
